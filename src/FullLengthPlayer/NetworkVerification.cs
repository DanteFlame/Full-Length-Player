using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FullLengthPlayer;

// Loopback-only integration fixture: no credentials or external services needed in CI.
internal sealed class NetworkVerification : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new();
    private readonly string directory;
    internal string BaseUrl { get; }
    internal ConcurrentBag<string> Accepted { get; } = new();
    internal int Rejected;
    internal NetworkVerification(string directory)
    {
        this.directory = directory; listener.Start();
        BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Serve();
    }
    private async Task Serve()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                _ = Respond(client);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (cancellation.IsCancellationRequested) { }
    }
    private async Task Respond(TcpClient client)
    {
        using (client)
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
            var request = await reader.ReadLineAsync(cancellation.Token);
            if (request == null) return;
            string route = request.Split(' ')[1].Split('?')[0];
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 100; i++)
            {
                var line = await reader.ReadLineAsync(cancellation.Token);
                if (string.IsNullOrEmpty(line)) break;
                int colon = line.IndexOf(':');
                if (colon > 0) headers[line[..colon]] = line[(colon + 1)..].Trim();
            }
            bool guarded = route.StartsWith("/guard/", StringComparison.Ordinal);
            bool allowed = guarded
                ? headers.GetValueOrDefault("Referer") == NetworkSource.PatreonReferer && headers.GetValueOrDefault("X-Test") == "a,b\\c"
                : !headers.ContainsKey("Referer") && !headers.ContainsKey("X-Test");
            async Task Reply(string status, byte[] body, string extra = "")
            {
                var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nConnection: close\r\nContent-Length: {body.Length}\r\n{extra}\r\n");
                await stream.WriteAsync(head, cancellation.Token);
                await stream.WriteAsync(body, cancellation.Token);
            }
            if (!allowed || route == "/deny")
            {
                Interlocked.Increment(ref Rejected); await Reply("403 Forbidden", Array.Empty<byte>()); return;
            }
            Accepted.Add(route);
            if (route == "/guard/redirect.m3u8")
            {
                await Reply("302 Found", Array.Empty<byte>(), "Location: /guard/master.m3u8?token=fixture\r\n"); return;
            }
            if (route == "/guard/master.m3u8")
            {
                await Reply("200 OK", Encoding.ASCII.GetBytes("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=500000\nindex.m3u8?token=fixture\n"), "Content-Type: application/vnd.apple.mpegurl\r\n"); return;
            }
            string name = Path.GetFileName(route);
            string file = guarded ? Path.Combine(directory, "hls", name) : Path.Combine(directory, name is "video-only.mkv" or "audio-only.mka" ? name : "source video.mkv");
            if (!File.Exists(file)) { await Reply("404 Not Found", Array.Empty<byte>()); return; }
            var bytes = await File.ReadAllBytesAsync(file, cancellation.Token);
            var type = name.EndsWith(".m3u8") ? "application/vnd.apple.mpegurl" : guarded ? "video/mp2t" : "video/x-matroska";
            int start = 0;
            if (headers.TryGetValue("Range", out var range) && range.StartsWith("bytes=")) int.TryParse(range[6..].Split('-')[0], out start);
            start = Math.Clamp(start, 0, bytes.Length - 1);
            if (headers.ContainsKey("Range")) await Reply("206 Partial Content", bytes[start..], $"Content-Type: {type}\r\nAccept-Ranges: bytes\r\nContent-Range: bytes {start}-{bytes.Length - 1}/{bytes.Length}\r\n");
            else await Reply("200 OK", bytes, $"Content-Type: {type}\r\nAccept-Ranges: bytes\r\n");
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    public void Dispose() { cancellation.Cancel(); listener.Stop(); cancellation.Dispose(); }

    internal static async Task Run(MainForm form, string media)
    {
        using var server = new NetworkVerification(Path.GetDirectoryName(media)!);
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        void Assert(bool value, string error) { if (!value) throw new Exception(error); }
        async Task Until(Func<bool> condition, string error)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!condition()) { if (DateTime.UtcNow > deadline) throw new Exception(error); await Task.Delay(100); }
        }
        bool Rejected(string url, string referer, string headers)
        {
            try { NetworkSource.Parse(url, referer, headers); return false; } catch (ArgumentException) { return true; }
        }
        Assert(Rejected("file:///secret", "", "") && Rejected(server.BaseUrl, "bad", "")
            && Rejected(server.BaseUrl, "", "X-Test: one\rInjected: bad")
            && Rejected(server.BaseUrl, "", "X-Test: a\nx-test: b"), "Invalid network settings accepted.");
        form.Reaction.LoadNetwork(NetworkSource.Parse(server.BaseUrl + "/guard/index.m3u8", "", ""));
        await Until(() => form.Reaction.PlaybackError != null && server.Rejected > 0, "Header-protected stream should reject missing headers.");
        form.Reaction.LoadNetwork(NetworkSource.Parse(server.BaseUrl + "/guard/redirect.m3u8?token=fixture", NetworkSource.PatreonReferer, "X-Test: a,b\\c"));
        form.Source.LoadNetwork(NetworkSource.Parse(server.BaseUrl + "/plain/video.mkv", "", ""));
        await Until(() => a.Number("time-pos") > 0.5 && b.Number("time-pos") > 0.5 && a.Number("duration") > 30 && a.Number("audio-params/samplerate") > 0, "HLS and direct HTTP did not decode concurrently.");
        Assert(server.Accepted.Contains("/guard/master.m3u8") && server.Accepted.Contains("/guard/index.m3u8") && server.Accepted.Any(x => x.EndsWith(".ts")), "Headers must reach redirects, variant playlists and segments.");
        Assert(form.Reaction.PlaybackError == null && b.Get("referrer") == "", "Network settings leaked between panes.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        a.Command("seek", "5", "absolute+exact"); b.Command("seek", "8", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 5) < 0.1 && Math.Abs(b.Number("time-pos") - 8) < 0.1 && a.Get("seeking") == "no" && b.Get("seeking") == "no", "Cannot align network sources.");
        form.Master.CaptureAlignment();
        var delta = form.Master.Offset;
        form.Master.SeekReaction(22);
        await Until(() => !form.Master.SeekingTogether && Math.Abs(a.Number("time-pos") - 22) < 0.1 && Math.Abs(b.Number("time-pos") - a.Number("time-pos") - delta) < 0.12, "Shared HLS seek lost offset.");
        form.SharedSpeed.Set(1.5); form.Master.TogglePause();
        await Until(() => !form.Master.SeekingTogether && a.Number("time-pos") > 22.5 && b.Number("time-pos") > 25.5, "HLS shared speed/resume failed.");
        form.Master.TogglePause();
        form.Reaction.LoadVideo(media);
        await Until(() => a.Number("time-pos") > 0.3 && a.Get("referrer") == "", "Local replacement did not clear network settings.");
        int rejected = server.Rejected;
        form.Reaction.LoadNetwork(NetworkSource.Parse(server.BaseUrl + "/plain/video.mkv", "", ""));
        await Until(() => a.Number("time-pos") > 0.5 && a.Get("path") == server.BaseUrl + "/plain/video.mkv", "Plain network replacement failed.");
        Assert(server.Rejected == rejected, "Old headers leaked into the next source.");
        form.Reaction.LoadNetwork(NetworkSource.Parse(server.BaseUrl + "/deny?secret=do-not-display", "", ""));
        await Until(() => form.Reaction.PlaybackError != null, "Forbidden response must surface a recoverable error.");
        Assert(!form.Reaction.PlaybackError!.Contains("secret"), "Error exposed a URL query.");
        form.Reaction.LoadVideo(media); form.Source.LoadVideo(Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv"));
        await Until(() => a.Number("time-pos") > 0.5 && b.Number("time-pos") > 0.5 && form.Reaction.PlaybackError == null, "Local recovery after HTTP failure failed.");
    }
}
