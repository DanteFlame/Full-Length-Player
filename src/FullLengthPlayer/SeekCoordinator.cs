using System.Globalization;

namespace FullLengthPlayer;

// The UI keeps pumping while both decoders seek. Neither resumes ahead of the other.
internal sealed class SeekCoordinator
{
    private sealed record Pending(MpvPlayer A, MpvPlayer B, double ATarget, double BTarget,
        bool PlayA, bool PlayB, bool FollowReaction, bool HoldSource, long Started);
    private Pending? pending;
    private int readyTicks;
    internal bool Waiting => pending != null;
    internal double? ATarget => pending?.ATarget;
    internal double? BTarget => pending?.BTarget;
    internal void Cancel() { pending = null; readyTicks = 0; }
    internal void TogglePause()
    {
        if (pending is not { } p) return;
        bool play = p.FollowReaction ? !p.PlayA : !p.PlayA && !p.PlayB;
        pending = p with { PlayA = play, PlayB = play && !p.HoldSource };
    }
    internal void Begin(MasterTransport.Position p, double aTarget, double bTarget, bool seekA = true, bool followReaction = false, bool holdSource = false)
    {
        bool playA = pending?.PlayA ?? p.A.Get("pause") != "yes";
        bool playB = pending?.PlayB ?? p.B.Get("pause") != "yes";
        if (followReaction) playB = playA && !holdSource;
        Cancel();
        p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
        if (!seekA)
        {
            double delta = bTarget - aTarget;
            aTarget = p.A.Number("time-pos"); bTarget = aTarget + delta;
        }
        // If a command fails, leave both safely paused instead of resuming half a seek.
        if (seekA) p.A.Command("seek", aTarget.ToString(CultureInfo.InvariantCulture), "absolute+exact");
        p.B.Command("seek", bTarget.ToString(CultureInfo.InvariantCulture), "absolute+exact");
        pending = new Pending(p.A, p.B, aTarget, bTarget, playA, playB, followReaction, holdSource, Environment.TickCount64);
    }
    internal string? Tick()
    {
        if (pending is not { } p) return null;
        if (Environment.TickCount64 - p.Started > 15000)
        {
            Cancel(); return "Seek timed out — both paused; retry the shared seek";
        }
        bool Ready(MpvPlayer player, double target) => player.Get("seeking") == "no"
            && player.Get("paused-for-cache") != "yes" && player.Get("idle-active") != "yes"
            && player.Get("time-pos") != null && Math.Abs(player.Number("time-pos") - target) <= 0.12;
        if (Environment.TickCount64 - p.Started < 100 || !Ready(p.A, p.ATarget) || !Ready(p.B, p.BTarget))
        {
            readyTicks = 0; return "Waiting for both videos to finish seeking";
        }
        if (++readyTicks < 2) return "Both videos settling";
        Cancel();
        // At an endpoint keep both paused, rather than immediately restarting an EOF player.
        bool ended = p.A.Get("eof-reached") == "yes" || (!p.FollowReaction && p.B.Get("eof-reached") == "yes");
        p.A.Set("pause", !ended && p.PlayA ? "no" : "yes");
        p.B.Set("pause", !ended && !p.HoldSource && p.B.Get("eof-reached") != "yes" && p.PlayB ? "no" : "yes");
        return ended ? "Reaction ended — seek back to continue" : "Seek complete";
    }
}
