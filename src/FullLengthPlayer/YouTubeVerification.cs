using System.Text.Json;

namespace FullLengthPlayer;

internal static class YouTubeVerification
{
    internal static async Task Run(MainForm form, string media)
    {
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Until(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition() || form.Master.SeekingTogether) { if (DateTime.UtcNow > deadline) throw new Exception(message); await Task.Delay(100); }
        }
        foreach (var pair in new[] { (18.174, 18.15), (18.175, 18.20), (18.225, 18.25), (-18.175, -18.20), (-18.225, -18.25), (0.024, 0.0), (-0.025, -0.05) })
            Assert(MasterTransport.RoundOffset(pair.Item1) == pair.Item2, "Offset rounding failed.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        a.Command("seek", "6", "absolute+exact"); b.Command("seek", "6.1666667", "absolute+exact");
        await Until(() => a.Get("seeking") == "no" && b.Get("seeking") == "no" && Math.Abs(a.Number("time-pos") - 6) < 0.02 && Math.Abs(b.Number("time-pos") - 6.1666667) < 0.02, "Cannot prepare fractional lock capture.");
        form.Master.CaptureAlignment();
        await Until(() => form.Master.Locked, "Capture failed.");
        Assert(form.Master.Offset == 0.15, "Captured delta was not rounded to the 0.05 grid.");
        form.Master.SetOffset(0.226);
        await Until(() => form.Master.Offset == 0.25, "Typed offset not rounded.");
        form.HandleShortcut(Keys.Oemcomma, Point.Empty);
        await Until(() => form.Master.Offset == 0.20, "Comma must nudge -0.05.");
        form.HandleShortcut(Keys.OemPeriod, Point.Empty);
        await Until(() => form.Master.Offset == 0.25, "Period must nudge +0.05.");
        form.Master.SetOffset(-0.226);
        await Until(() => form.Master.Offset == -0.25, "Negative typed offset not rounded.");
        for (int i = 0; i < 10; i++) form.Master.Nudge(0.05);
        for (int i = 0; i < 10; i++) form.Master.Nudge(-0.05);
        await Until(() => form.Master.Offset == -0.25, "Nudging accumulated decimal error.");
        const string id = "BaW_jenozKc", canonical = "https://www.youtube.com/watch?v=BaW_jenozKc";
        Assert(YouTubeResolver.FormatSelection(0) == "bestvideo+bestaudio/best", "Default quality changed.");
        foreach (int height in new[] { 480, 720, 1080 })
        {
            var arguments = YouTubeResolver.StartInfo(canonical, height).ArgumentList.ToArray();
            Assert(arguments[Array.IndexOf(arguments, "--format") + 1] == $"bestvideo[height<={height}]+bestaudio/best[height<={height}]", "Quality cap could exceed requested resolution.");
        }
        try { YouTubeResolver.FormatSelection(123); throw new Exception("Invalid quality accepted."); } catch (ArgumentOutOfRangeException) { }
        foreach (var url in new[] { "https://youtu.be/" + id + "?si=tracking", canonical + "&list=PL123&t=7", "https://m.youtube.com/shorts/" + id, "https://www.youtube-nocookie.com/embed/" + id })
            Assert(YouTubeResolver.CanonicalUrl(url) == canonical, "YouTube URL normalization failed.");
        Assert(!YouTubeResolver.IsYouTube("https://youtube.com.example.org/watch?v=" + id), "Lookalike host accepted.");
        bool invalid = false;
        try { YouTubeResolver.CanonicalUrl("https://www.youtube.com/playlist?list=PL123"); } catch (ArgumentException) { invalid = true; }
        Assert(invalid, "Playlist-only URL accepted.");
        using var server = new NetworkVerification(Path.GetDirectoryName(media)!);
        // Run the real packaged executable against a local media endpoint. This also
        // checks arguments, bundled runtime paths and extraction from a path with spaces.
        var resolved = YouTubeResolver.Parse(await YouTubeResolver.RunProcess(YouTubeResolver.StartInfo(server.BaseUrl + "/plain/video.mkv"), CancellationToken.None));
        Assert(resolved.Video.Url.StartsWith(server.BaseUrl), "Packaged extractor did not return media.");
        var json = JsonSerializer.Serialize(new { requested_formats = new object[] {
            new { url = server.BaseUrl + "/plain/video-only.mkv", vcodec = "mpeg4", acodec = "none" },
            new { url = server.BaseUrl + "/plain/audio-only.mka", vcodec = "none", acodec = "pcm_s16le" }
        }});
        var split = YouTubeResolver.Parse(json);
        Assert(split.AudioUrl != null, "Separate audio was discarded.");
        await form.Reaction.LoadYouTube(canonical, (_, _) => Task.FromResult(split), 720);
        await Until(() => a.Number("time-pos") > 0.5 && a.Number("video-params/w") > 0 && a.Number("audio-params/samplerate") > 0, "Split video/audio playback failed.");
        Assert(form.Reaction.CaptureMedia().Kind == "youtube" && form.Reaction.CaptureMedia().Location == canonical, "Session did not preserve the original YouTube link.");
        var savedMedia = JsonSerializer.Deserialize<SavedMedia>(JsonSerializer.Serialize(form.Reaction.CaptureMedia()))!;
        Assert(savedMedia.YouTubeHeight == 720, "Session lost YouTube quality cap.");
        var diagnostic = PlaybackDiagnostics.Report(form.Reaction, form.Source);
        Assert(diagnostic.Contains("\"youtubeMaximumHeight\": 720") && !diagnostic.Contains(canonical) && !diagnostic.Contains(server.BaseUrl) && !diagnostic.Contains(media), "Diagnostics missing cap or leaking media identity.");
        using (var snapshot = JsonDocument.Parse(diagnostic))
            Assert(snapshot.RootElement.GetProperty("reaction").GetProperty("openToFileLoadedMilliseconds").ValueKind == JsonValueKind.Number, "File-loaded timing unavailable after load.");
        var splitSample = await AudioAlignment.Decode(form.Reaction.CaptureAudio(), 2, 20, CancellationToken.None);
        Assert(splitSample.Length >= 19 * AudioAlignment.Rate, "Audio analysis lost external YouTube audio.");
        Assert(server.Accepted.Contains("/plain/audio-only.mka"), "MPV did not request external audio.");
        Assert(form.Reaction.RefreshTrackMenu(false).DropDownItems.OfType<ToolStripMenuItem>().Any(x => x.Text?.Contains("external") == true), "External audio track missing.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        a.Command("seek", "5", "absolute+exact"); b.Command("seek", "8", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 5) < 0.1 && Math.Abs(b.Number("time-pos") - 8) < 0.1 && a.Get("seeking") == "no" && b.Get("seeking") == "no", "Cannot align split playback.");
        form.Master.CaptureAlignment(); form.Master.SeekReaction(15);
        await Until(() => Math.Abs(a.Number("time-pos") - 15) < 0.1 && Math.Abs(b.Number("time-pos") - 18) < 0.1, "Split playback shared seeking failed.");
        form.SharedSpeed.Set(2); form.Master.TogglePause();
        await Until(() => a.Number("time-pos") > 15.5 && b.Number("time-pos") > 18.5, "Split playback shared speed/resume failed.");
        // Replacement must cancel pending resolution and cannot resurrect the old URL.
        var pending = form.Reaction.LoadYouTube(canonical, async (_, token) => { await Task.Delay(30000, token); return split; });
        form.Reaction.LoadVideo(media);
        bool cancelled = false;
        try { await pending; } catch (OperationCanceledException) { cancelled = true; }
        Assert(cancelled, "Replacement did not cancel pending YouTube lookup.");
        await Until(() => a.Number("time-pos") > 0.3 && a.Get("path") == media, "Local replacement failed.");
        Assert(!form.Reaction.RefreshTrackMenu(false).DropDownItems.OfType<ToolStripMenuItem>().Any(x => x.Text?.Contains("external") == true), "External YouTube audio leaked into local playback.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { await YouTubeResolver.RunProcess(YouTubeResolver.StartInfo(canonical), cancellation.Token); throw new Exception("Cancelled resolver started."); } catch (OperationCanceledException) { }
        invalid = false;
        try { YouTubeResolver.Parse("{\"url\":\"file:///secret\"}"); } catch (InvalidOperationException e) { invalid = !e.Message.Contains("secret"); }
        Assert(invalid, "Invalid extraction exposed metadata.");
    }
}
