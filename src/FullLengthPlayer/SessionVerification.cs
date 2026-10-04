namespace FullLengthPlayer;
internal static class SessionVerification
{
    internal static async Task Run(MainForm form, string media)
    {
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Session: " + message); }
        async Task Until(Func<bool> ready)
        {
            long end = Environment.TickCount64 + 15000;
            while (!ready() || form.Master.SeekingTogether)
            { if (Environment.TickCount64 > end) throw new InvalidOperationException("Session test timed out."); await Task.Delay(50); }
        }
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        form.Replay.Cancel(); form.Master.Unlock();
        form.Reaction.LoadVideo(media); form.Source.LoadVideo(Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv"));
        await Until(() => a.Number("time-pos") > .1 && a.Number("time-pos") < 3 && b.Number("time-pos") > .1 && b.Number("time-pos") < 3);
        a.Set("pause", "yes"); b.Set("pause", "yes");
        form.SharedSpeed.Set(1.75); form.Reaction.SetVolume(45); form.Source.SetVolume(85);
        form.Master.SetOffset(-3.25); form.Master.SeekReaction(15);
        await Until(() => Math.Abs(a.Number("time-pos") - 15) < .1);
        var settings = form.CaptureSettings() with { Anchor = 1, Canvas = 2 };
        settings.Numbers["Crop top %"] = 12; settings.Numbers["Pan X %"] = -8;
        form.ApplySettings(settings, true);
        var saved = form.CaptureSession();
        string directory = Path.Combine(Path.GetTempPath(), "Full Length Session Test " + Guid.NewGuid());
        string path = Path.Combine(directory, "pair.flpsession");
        try
        {
            SessionStore.Save(path, saved);
            var read = SessionStore.Read(path);
            Check(read.A.Location == saved.A.Location && read.Offset == -3.25, "Protected round trip lost media or offset.");
            var sensitive = saved with { A = saved.A with { Kind = "network", Location = "https://example.com/video?token=PRIVATE_SESSION_TEST", Headers = new[] { "Authorization: PRIVATE_HEADER_TEST" } } };
            SessionStore.Save(path, sensitive);
            var bytes = File.ReadAllBytes(path);
            Check(!System.Text.Encoding.UTF8.GetString(bytes).Contains("PRIVATE_"), "Session contains plaintext credentials.");
            Check(SessionStore.Read(path).A.Headers[0] == sensitive.A.Headers[0], "Protected headers were lost.");
            bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
            try { SessionStore.Read(path); throw new Exception("Tampered session was accepted."); } catch (InvalidOperationException) { }
            SessionStore.Save(path, saved);
            form.Master.SeekReaction(25); await Until(() => Math.Abs(a.Number("time-pos") - 25) < .1);
            form.SharedSpeed.Set(1); form.Reaction.SetVolume(10);
            await form.RestoreSession(SessionStore.Read(path));
            Check(a.Get("pause") == "yes" && b.Get("pause") == "yes", "Restore started playback.");
            Check(Math.Abs(a.Number("time-pos") - 15) < .1 && Math.Abs(b.Number("time-pos") - 11.75) < .1, "Restored positions are wrong.");
            Check(form.Master.Locked && form.Master.Offset == -3.25, "Lock was lost.");
            Check(a.Number("speed") == 1.75 && b.Number("speed") == 1.75 && form.Reaction.Volume == 45 && form.Source.Volume == 85, "Speed/volumes not restored.");
            Check(form.Composition.TopAnchor && Math.Abs(form.Composition.CropTop - .12) < .001 && Math.Abs(form.Composition.CanvasAspect - 1.6) < .001, "Layout not restored.");
            Check(a.Get("aid") == saved.A.Audio && b.Get("sid") == saved.B.Subtitles, "Selected tracks not restored.");
            var missing = saved with { A = saved.A with { Location = Path.Combine(directory, "missing.mkv") } };
            try { await form.RestoreSession(missing); throw new Exception("Missing file was accepted."); } catch (FileNotFoundException) { }
            Check(form.Master.Locked && Math.Abs(a.Number("time-pos") - 15) < .1, "Missing file changed current playback.");
            // Both ends of the combined timeline need to survive a saved session.
            await form.RestoreSession(saved with { Clock = 41 });
            Check(Math.Abs(form.Master.TimelineTime - 41) < .15 && b.Get("pause") == "yes", "Source-only tail not restored.");
            await form.RestoreSession(saved with { Offset = 3, Clock = -2 });
            Check(Math.Abs(form.Master.TimelineTime + 2) < .15 && a.Get("pause") == "yes", "Source-only preamble not restored.");
            await form.RestoreSession(saved with { Locked = false });
            Check(!form.Master.Locked && Math.Abs(a.Number("time-pos") - saved.A.Position) < .1 && Math.Abs(b.Number("time-pos") - saved.B.Position) < .1, "Independent positions not restored.");
            await form.RestoreSession(saved);
            using (var streams = new NetworkVerification(Path.GetDirectoryName(media)!))
            {
                var remote = saved with {
                    A = saved.A with { Kind = "network", Location = streams.BaseUrl + "/plain/video.mkv", Referer = "", Headers = Array.Empty<string>() },
                    B = saved.B with { Kind = "network", Location = streams.BaseUrl + "/plain/video.mkv", Referer = "", Headers = Array.Empty<string>() }
                };
                await form.RestoreSession(remote);
                Check(form.Master.Locked && Math.Abs(a.Number("time-pos") - 15) < .2 && Math.Abs(b.Number("time-pos") - 11.75) < .2, "Dual remote locked restore lost alignment");
                Check(a.Get("pause") == "yes" && b.Get("pause") == "yes" && form.RestoreStatus == "Completed (paused)", "Dual remote restore did not finish paused");
                await form.RestoreSession(remote with { Locked = false });
                Check(Math.Abs(a.Number("time-pos") - remote.A.Position) < .2 && Math.Abs(b.Number("time-pos") - remote.B.Position) < .2, "Dual remote independent positions incorrect");
            }
            await form.RestoreSession(saved);
            form.Replay.Trigger(); await Until(() => form.Replay.Active);
            var duringReplay = form.CaptureSession();
            Check(!form.Replay.Active && duringReplay.Settings.Speed == 1.75 && duringReplay.Settings.VolumeA == 45 && duringReplay.Settings.VolumeB == 85, "Temporary replay settings leaked into save.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
