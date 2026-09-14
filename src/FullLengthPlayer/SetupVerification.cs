namespace FullLengthPlayer;
internal static class SetupVerification
{
    internal static async Task Run(MainForm form, string media)
    {
        static void Check(bool good, string message) { if (!good) throw new InvalidOperationException("Setup: " + message); }
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        async Task Until(Func<bool> good)
        {
            long end = Environment.TickCount64 + 20000;
            while (!good() || form.Master.SeekingTogether) { if (Environment.TickCount64 > end) throw new TimeoutException("Setup check timed out."); await Task.Delay(50); }
        }
        form.NewSession();
        await Until(() => a.Get("idle-active") == "yes" && b.Get("idle-active") == "yes");
        Check(form.Master.Snapshot() == null && !form.Master.Locked && form.Master.Offset == 0 && !form.Replay.Active, "New session retained transport state.");
        Check(form.Composition.CropTop == 0 && form.Composition.CropBottom == 0 && form.Composition.Zoom == 1 && form.Composition.PanX == 0 && !form.Composition.TopAnchor, "New session retained layout.");
        Check(a.Number("speed") == 1 && b.Number("speed") == 1 && form.Reaction.Volume == 100 && form.Source.Volume == 100, "New session retained playback settings.");
        form.Reaction.LoadVideo(media); form.Source.LoadVideo(Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv"));
        await Until(() => form.Master.Snapshot() != null && a.Number("video-params/w") > 0 && b.Number("video-params/w") > 0);
        await Task.Delay(500);
        Check(a.Get("pause") == "yes" && b.Get("pause") == "yes" && a.Number("time-pos") < .1 && b.Number("time-pos") < .1, "Local files did not load paused.");
        using var server = new NetworkVerification(Path.GetDirectoryName(media)!);
        form.Reaction.LoadNetwork(NetworkSource.Parse(server.BaseUrl + "/plain/video.mkv", "", ""));
        await Until(() => a.Get("path") == server.BaseUrl + "/plain/video.mkv" && a.Get("time-pos") != null && a.Number("video-params/w") > 0);
        await Task.Delay(500);
        Check(a.Get("pause") == "yes" && a.Number("time-pos") < .1, "Direct stream did not load paused.");
        var resolved = new ResolvedVideo(NetworkSource.Parse(server.BaseUrl + "/plain/video-only.mkv", "", ""), server.BaseUrl + "/plain/audio-only.mka");
        await form.Reaction.LoadYouTube("https://www.youtube.com/watch?v=BaW_jenozKc", (_, _) => Task.FromResult(resolved));
        await Until(() => a.Get("path") == resolved.Video.Url && a.Number("audio-params/samplerate") > 0 && a.Get("time-pos") != null);
        await Task.Delay(500);
        Check(a.Get("pause") == "yes" && a.Number("time-pos") < .1, "Resolved YouTube video did not load paused.");
        form.Master.CaptureAlignment(); form.Master.SeekReaction(15);
        await Until(() => Math.Abs(a.Number("time-pos") - 15) < .1);
        form.Replay.Trigger(); await Until(() => form.Replay.Active);
        form.NewSession();
        await Until(() => a.Get("idle-active") == "yes" && b.Get("idle-active") == "yes");
        Check(!form.Replay.Active && !form.Master.Locked && a.Get("mute") == "no" && b.Get("mute") == "no" && a.Number("speed") == 1, "New session left temporary replay settings.");
        var pending = form.Reaction.LoadYouTube("https://www.youtube.com/watch?v=BaW_jenozKc", async (_, token) => { await Task.Delay(30000,token); return resolved; });
        form.NewSession();
        try { await pending; throw new InvalidOperationException("New session failed to cancel YouTube lookup."); } catch (OperationCanceledException) { }
        Check(form.Master.Snapshot() == null && a.Get("http-header-fields") is "" or "[]" && a.Get("audio-files") is "" or "[]", "New session retained stream state.");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".appearance.txt");
        try
        {
            foreach (string id in AppIcons.Ids)
            {
                foreach (int size in new[] {16,24,32,48,64,128,256})
                { using var icon = AppIcons.Load(id,size); Check(icon.Width == size && icon.Height == size, "Icon size missing."); using var bitmap = icon.ToBitmap(); Check(bitmap.Width == size, "Icon cannot render."); }
                AppIcons.Save(id,path); Check(AppIcons.Read(path) == id, "Icon preference not restored.");
                form.ApplyIcon(id, save:false); Check(form.IconId == id && form.Icon != null, "Window icon did not change.");
            }
            string current = form.IconId; form.NewSession(); Check(form.IconId == current, "New session reset appearance.");
            File.WriteAllText(path,"unknown"); Check(AppIcons.Read(path) == AppIcons.Ids[0], "Bad preference should use default icon.");
        }
        finally { File.Delete(path); }
    }
}
