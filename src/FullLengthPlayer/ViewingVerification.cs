namespace FullLengthPlayer;
internal static class ViewingVerification
{
    internal static async Task Run(MainForm form, string media)
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Viewing: " + message); }
        async Task Until(Func<bool> condition, string message)
        {
            long end = Environment.TickCount64 + 15000;
            while (!condition() || form.Master.SeekingTogether) { if (Environment.TickCount64 > end) throw new InvalidOperationException(message); await Task.Delay(50); }
        }
        Check(MainForm.ClosestCanvas(new Size(1920,1080)) == 0 && MainForm.ClosestCanvas(new Size(2048,1536)) == 1 && MainForm.ClosestCanvas(new Size(2560,1600)) == 2, "Canvas choice failed.");
        form.Reaction.LoadVideo(media); form.Source.LoadVideo(Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv"));
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        await Until(() => a.Number("time-pos") > 0.2 && b.Number("time-pos") > 0.2 && a.Number("duration") > 30 && b.Number("duration") > 30, "Load failed.");
        a.Set("pause", "yes"); b.Set("pause", "yes"); form.SharedSpeed.Set(2);
        form.Master.SetOffset(3); form.Master.SeekReaction(-3);
        await Until(() => a.Number("time-pos") < 0.1 && b.Number("time-pos") < 0.1, "Source preamble seek failed.");
        Check(form.Master.TimelineStart == -3 && Math.Abs(form.Master.TimelineTime + 3) < 0.1, "Combined clock start incorrect.");
        form.Master.TogglePause();
        await Until(() => b.Number("time-pos") > 1 && b.Number("time-pos") < 3, "Source preamble did not play.");
        Check(a.Get("pause") == "yes" && a.Number("time-pos") < 0.1, "Reaction should wait at zero.");
        await Until(() => a.Number("time-pos") > 0.5 && a.Get("pause") == "no", "Reaction did not join source.");
        Check(Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 3) < 0.15, "Reaction rejoin offset wrong.");
        form.Master.SetOffset(-3); form.Master.SeekReaction(38);
        await Until(() => a.Get("eof-reached") == "yes" && b.Number("time-pos") > 37.5, "Source did not continue beyond reaction.");
        Check(a.Get("pause") == "yes" && b.Get("pause") == "no" && form.Master.TimelineTime > 40, "Reaction EOF stopped source or clock.");
        form.Master.TogglePause();
        Check(b.Get("pause") == "yes", "Pause in source tail failed.");
        form.SharedSpeed.Set(1.5);
        await Until(() => b.Get("pause") == "yes", "Speed resumed paused source tail.");
        form.Master.SeekReaction(41);
        await Until(() => Math.Abs(b.Number("time-pos") - 38) < 0.1 && a.Get("pause") == "yes", "Source tail timeline seek failed.");
        form.Master.SeekReaction(10);
        await Until(() => Math.Abs(a.Number("time-pos") - 10) < 0.1 && Math.Abs(b.Number("time-pos") - 7) < 0.1, "Seek back from source tail failed.");
        form.HandleShortcut(Keys.L, Point.Empty);
        await Until(() => Math.Abs(a.Number("time-pos") - 15) < 0.1, "L must jump five seconds.");
        form.HandleShortcut(Keys.J, Point.Empty);
        await Until(() => Math.Abs(a.Number("time-pos") - 10) < 0.1, "J must jump five seconds.");
        form.ToggleFullscreen(); form.RevealFullscreen();
        Check(form.Hud.Visible, "Fullscreen HUD did not show.");
        var pointer = Cursor.Position;
        form.UpdateFullscreen(Environment.TickCount64 + 3000, pointer, true);
        Check(!form.Hud.Visible, "Paused fullscreen HUD should disappear.");
        form.HandleShortcut(Keys.K, Point.Empty);
        Check(form.Hud.Visible, "Pause/play key did not reveal HUD.");
        form.UpdateFullscreen(Environment.TickCount64 + 3000, pointer, true);
        form.HandleShortcut(Keys.J, Point.Empty);
        Check(form.Hud.Visible, "Skip did not reveal HUD.");
        form.ToggleFullscreen(); Check(!form.Hud.Visible, "HUD remained after fullscreen.");
        var agree = new[] { new AudioMatch(5.0,0.6,0.2,true), new AudioMatch(5.05,0.7,0.3,true), new AudioMatch(5.0,0.6,0.2,true) };
        Check(AudioConsensus.Decide(agree,3).Match?.Offset == 5, "Consensus median wrong.");
        Check(AudioConsensus.Decide(agree.Take(2).ToArray(),2).Match == null, "Two samples cannot establish consensus.");
        Check(AudioConsensus.Decide(agree.Append(new AudioMatch(8,0.8,0.3,true)).ToArray(),4).Conflict, "Conflicting offsets were averaged.");
        using var token = new CancellationTokenSource(); token.Cancel();
        try { await AudioConsensus.Run(form.Reaction.CaptureAudio(),form.Source.CaptureAudio(),0,0,40,40,new Progress<string>(),token.Token); throw new InvalidOperationException("Consensus ignored cancellation."); }
        catch (OperationCanceledException) { }
    }
}
