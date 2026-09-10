using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FullLengthPlayer;

internal sealed record ResolvedVideo(NetworkSource Video, string? AudioUrl);

internal static class YouTubeResolver
{
    internal static bool IsYouTube(string text) => Uri.TryCreate(text.Trim(), UriKind.Absolute, out var u)
        && (u.Host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtu.be" or "www.youtu.be" or "youtube-nocookie.com" or "www.youtube-nocookie.com");
    internal static string CanonicalUrl(string text)
    {
        if (!IsYouTube(text) || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http") || u.UserInfo.Length != 0)
            throw new ArgumentException("Enter a YouTube video link.");
        string? id = null;
        var parts = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (u.Host is "youtu.be" or "www.youtu.be") id = parts.FirstOrDefault();
        else if (parts.Length == 2 && parts[0] is "shorts" or "embed" or "live") id = parts[1];
        else if (u.AbsolutePath == "/watch")
            id = u.Query.TrimStart('?').Split('&').Where(x => x.StartsWith("v=", StringComparison.Ordinal)).Select(x => Uri.UnescapeDataString(x[2..])).FirstOrDefault();
        if (id == null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$")) throw new ArgumentException("Use a single YouTube video link, not a channel or playlist.");
        return "https://www.youtube.com/watch?v=" + id;
    }
    internal static ProcessStartInfo StartInfo(string url)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "youtube");
        var info = new ProcessStartInfo(Path.Combine(directory, "yt-dlp.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = directory
        };
        foreach (string arg in new[] { "--ignore-config", "--no-plugin-dirs", "--no-cache-dir", "--no-playlist", "--skip-download", "--dump-single-json", "--no-warnings", "--no-progress", "--socket-timeout", "15", "--retries", "1", "--extractor-retries", "1", "--js-runtimes", "deno:" + Path.Combine(directory, "deno.exe"), "--format", "bestvideo+bestaudio/best", "--", url }) info.ArgumentList.Add(arg);
        info.Environment["DENO_NO_UPDATE_CHECK"] = "1";
        return info;
    }
    internal static async Task<ResolvedVideo> Resolve(string url, CancellationToken cancellation) =>
        Parse(await RunProcess(StartInfo(CanonicalUrl(url)), cancellation));

    internal static async Task<string> RunProcess(ProcessStartInfo start, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        using var process = new Process { StartInfo = start };
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (!process.Start()) throw new InvalidOperationException();
            using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
            // Drain both pipes concurrently. Never echo extractor stderr or its signed URLs.
            async Task<string> Read(StreamReader reader, int maximum)
            {
                var text = new StringBuilder(); char[] buffer = new char[4096]; int count;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token)) > 0)
                {
                    if (text.Length + count > maximum) { timeout.Cancel(); throw new InvalidOperationException(); }
                    text.Append(buffer, 0, count);
                }
                return text.ToString();
            }
            var stdout = Read(process.StandardOutput, 16 * 1024 * 1024);
            var stderr = Read(process.StandardError, 256 * 1024);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0) throw new InvalidOperationException();
            return await stdout;
        }
        catch (Exception) when (cancellation.IsCancellationRequested) { throw new OperationCanceledException(cancellation); }
        catch (Exception)
        {
            throw new InvalidOperationException("YouTube could not be opened. Check that the video is available without signing in, then retry. YouTube may temporarily restrict requests, or the bundled resolver may need an update.");
        }
    }
    internal static ResolvedVideo Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("entries", out _) || (root.TryGetProperty("is_live", out var live) && live.ValueKind == JsonValueKind.True))
                throw new InvalidOperationException();
            JsonElement video = root; JsonElement? audio = null;
            if (root.TryGetProperty("requested_formats", out var selected))
            {
                var formats = selected.EnumerateArray().ToArray();
                if (formats.Length != 2) throw new InvalidOperationException();
                video = formats.Single(x => x.TryGetProperty("vcodec", out var codec) && codec.GetString() != "none");
                audio = formats.Single(x => x.TryGetProperty("vcodec", out var codec) && codec.GetString() == "none");
            }
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Headers(JsonElement element, bool rejectConflicts = false)
            {
                if (!element.TryGetProperty("http_headers", out var fields)) return;
                foreach (var field in fields.EnumerateObject())
                {
                    string value = field.Value.GetString() ?? "";
                    if (rejectConflicts && headers.TryGetValue(field.Name, out var previous) && previous != value) throw new InvalidOperationException();
                    headers[field.Name] = value;
                }
            }
            Headers(root); Headers(video);
            if (audio.HasValue) Headers(audio.Value, true);
            string referer = headers.GetValueOrDefault("Referer", ""); headers.Remove("Referer");
            var network = NetworkSource.Parse(video.GetProperty("url").GetString() ?? "", referer, string.Join("\n", headers.Select(x => x.Key + ": " + x.Value)));
            string? audioUrl = audio.HasValue ? NetworkSource.Parse(audio.Value.GetProperty("url").GetString() ?? "", "", "").Url : null;
            return new ResolvedVideo(network, audioUrl);
        }
        catch { throw new InvalidOperationException("The resolver did not return a playable on-demand video with audio."); }
    }
}
