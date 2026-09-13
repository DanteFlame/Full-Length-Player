namespace FullLengthPlayer;

// Temporary listening state. Media replacement and manual edits restore it first.
internal sealed class CommentaryReplay(PlayerPane a, PlayerPane b, MasterTransport master)
{
    private sealed record Saved(double ReturnTime, double SpeedA, double SpeedB, double VolumeA, double VolumeB, string MuteA, string MuteB);
    private Saved? saved;
    internal bool Active => saved != null;
    internal void Trigger()
    {
        if (Active) { Cancel(); return; }
        var p = master.Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        if (!master.Locked) throw new InvalidOperationException("Lock the alignment before replaying commentary.");
        if (master.SeekingTogether || p.A.Get("seeking") == "yes" || p.B.Get("seeking") == "yes")
            throw new InvalidOperationException("Wait for seeking to finish before replaying commentary.");
        if (master.TimelineTime - master.TimelineStart <= 0.05) return;
        bool paused = p.A.Get("pause") == "yes";
        saved = new(master.TimelineTime, p.A.Number("speed"), p.B.Number("speed"), a.Volume, b.Volume,
            p.A.Get("mute") ?? "no", p.B.Get("mute") ?? "no");
        try
        {
            master.SetSpeed(1);
            a.SetVolume(100); p.A.Set("mute", "no"); p.B.Set("mute", "yes");
            master.SeekReaction(Math.Max(master.TimelineStart, saved.ReturnTime - 10));
            if (paused) master.TogglePause(); // Change the coordinated seek's resume intent.
        }
        catch { Cancel(); throw; }
    }
    internal void Tick()
    {
        if (saved is not { } state) return;
        if (!master.Locked) { Cancel(); return; }
        if (master.SeekingTogether) return;
        if (a.Player is { } player && (master.TimelineTime >= state.ReturnTime || master.TimelineTime >= master.TimelineEnd - 0.05)) Cancel();
    }
    internal void Cancel()
    {
        if (saved is not { } state) return;
        saved = null;
        // Restore without an additional seek at the end of the replay.
        a.Player?.Set("speed", state.SpeedA.ToString(System.Globalization.CultureInfo.InvariantCulture));
        b.Player?.Set("speed", state.SpeedB.ToString(System.Globalization.CultureInfo.InvariantCulture));
        a.SetVolume(state.VolumeA); b.SetVolume(state.VolumeB);
        a.Player?.Set("mute", state.MuteA); b.Player?.Set("mute", state.MuteB);
    }
}
