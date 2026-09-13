namespace FullLengthPlayer;

internal static class ConvenienceVerification
{
    internal static async Task Run(MainForm form, string media)
    {
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Conveniences: " + message); }
        async Task Until(Func<bool> condition, string message)
        {
            long until = Environment.TickCount64 + 15000;
            while (!condition()) { if (Environment.TickCount64 > until) throw new InvalidOperationException(message); await Task.Delay(50); }
        }
        form.Reaction.LoadVideo(media);
        form.Source.LoadVideo(Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv"));
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        await Until(() => a.Number("duration") > 30 && b.Number("duration") > 30 && a.Number("time-pos") > 0.2 && b.Number("time-pos") > 0.2, "Reload failed.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        form.Master.SetOffset(2); form.Master.SeekReaction(12); form.SharedSpeed.Set(1.5);
        await Until(() => !form.Master.SeekingTogether && Math.Abs(a.Number("time-pos") - 12) < 0.1, "Replay setup seek failed.");
        form.Reaction.SetVolume(37); form.Source.SetVolume(64); a.Set("mute", "yes"); b.Set("mute", "no");
        form.Replay.Trigger();
        Check(form.Replay.Active && a.Number("speed") == 1 && b.Number("speed") == 1 && a.Number("volume") == 100 && a.Get("mute") == "no" && b.Get("mute") == "yes", "Replay did not focus reaction audio.");
        await Until(() => !form.Master.SeekingTogether && a.Number("time-pos") > 2 && a.Number("time-pos") < 4 && a.Get("pause") == "no", "Replay did not rewind 10 seconds and play from paused state.");
        Check(form.Master.Offset == 2 && form.Master.Locked, "Replay lost offset.");
        form.Replay.Trigger();
        Check(!form.Replay.Active && a.Number("speed") == 1.5 && b.Number("speed") == 1.5 && a.Number("volume") == 37 && b.Number("volume") == 64 && a.Get("mute") == "yes" && b.Get("mute") == "no", "H cancellation did not restore exact state.");
        form.Master.SeekReaction(2.5);
        await Until(() => !form.Master.SeekingTogether, "Short replay setup failed.");
        form.Replay.Trigger();
        await Until(() => !form.Replay.Active, "Replay did not restore automatically at original playhead.");
        Check(a.Number("speed") == 1.5 && b.Number("speed") == 1.5 && a.Number("volume") == 37 && b.Number("volume") == 64 && a.Get("mute") == "yes" && b.Get("mute") == "no" && a.Get("pause") == "no", "Automatic replay restore incorrect.");
        var layout = form.Composition;
        layout.CanvasAspect = 4.0 / 3; layout.CropTop = 0.2; layout.CropBottom = 0.1; layout.Zoom = 1; layout.PanY = 0;
        layout.SourceFraction = 0.4;
        foreach (bool top in new[] { true, false })
        {
            layout.TopAnchor = top; layout.Arrange();
            Check(top ? layout.MaskBounds.Bottom == layout.CanvasBounds.Height : layout.MaskBounds.Top == 0, "Reaction did not anchor opposite source.");
            layout.CropTop = 0.35; layout.CropBottom = 0.15; layout.Arrange();
            Check(top ? layout.MaskBounds.Bottom == layout.CanvasBounds.Height : layout.MaskBounds.Top == 0, "Cropping moved reaction off its edge.");
        }
        layout.TopAnchor = true; layout.Arrange();
        Point bPoint = form.Source.Surface.PointToScreen(new Point(layout.SourceBounds.Width / 2, layout.SourceBounds.Height / 2));
        Point aPoint = form.Reaction.Surface.PointToScreen(new Point(layout.ReactionBounds.Width / 2, layout.ReactionBounds.Height - (int)(layout.ReactionBounds.Height * layout.CropBottom) - 10));
        Check(layout.HitPlayer(aPoint) == form.Reaction, "Reaction wheel target hidden.");
        form.HandleVolumeWheel(aPoint, 120); Check(a.Number("volume") == 42 && b.Number("volume") == 64, "Wheel A isolation failed.");
        form.HandleVolumeWheel(bPoint, -120); Check(b.Number("volume") == 59 && a.Number("volume") == 42, "Wheel B isolation failed.");
        form.HandleVolumeWheel(bPoint, 60); Check(b.Number("volume") == 59, "Partial wheel jumped too early.");
        form.HandleVolumeWheel(bPoint, 60); Check(b.Number("volume") == 64, "Partial wheel accumulation failed.");
        form.HandleVolumeWheel(bPoint, 12000); Check(b.Number("volume") == 100, "Volume upper bound failed.");
        Check(!form.HandleVolumeWheel(new Point(-30000, -30000), 120), "Wheel outside video should be ignored.");
        form.ToggleFullscreen();
        bPoint = form.Source.Surface.PointToScreen(new Point(layout.SourceBounds.Width / 2, layout.SourceBounds.Height / 2));
        form.HandleVolumeWheel(bPoint, -120); Check(b.Number("volume") == 95, "Fullscreen wheel failed.");
        form.ToggleFullscreen();
        await Until(() => !form.Master.SeekingTogether, "Waiting to test manual cancellation.");
        form.Replay.Trigger(); form.Source.AdjustVolume(-5);
        Check(!form.Replay.Active && b.Number("volume") == 90 && a.Number("speed") == 1.5, "Manual volume edit failed to restore replay first.");
        await Until(() => !form.Master.SeekingTogether, "Waiting for replacement test.");
        form.Replay.Trigger(); form.Reaction.LoadVideo(media);
        Check(!form.Replay.Active && b.Get("mute") == "no" && b.Number("speed") == 1.5, "Media replacement left temporary replay state.");
    }
}
