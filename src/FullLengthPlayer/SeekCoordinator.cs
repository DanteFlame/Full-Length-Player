using System.Globalization;

namespace FullLengthPlayer;

// The UI keeps pumping while both decoders seek. Neither resumes ahead of the other.
internal sealed class SeekCoordinator(Func<long>? clock = null)
{
    private long Now => clock?.Invoke() ?? Environment.TickCount64;
    internal string State { get; private set; } = "Idle";
    internal string WaitReason { get; private set; } = "";
    internal int TimeoutMilliseconds { get; private set; }
    internal long ElapsedMilliseconds { get; private set; }
    internal bool Suspended => State is "Timed out" or "Cancelled";
    internal bool WillResume => pending is { } p && (p.PlayA || p.PlayB);
    private sealed record Pending(MpvPlayer A, MpvPlayer B, double ATarget, double BTarget,
        bool PlayA, bool PlayB, bool FollowReaction, bool HoldSource, bool HoldReaction, long Started, int TimeoutMilliseconds);
    private Pending? pending;
    private int readyTicks;
    internal bool Waiting => pending != null;
    internal double? ATarget => pending?.ATarget;
    internal double? BTarget => pending?.BTarget;
    internal void Cancel() { pending = null; readyTicks = 0; State = "Idle"; WaitReason = ""; ElapsedMilliseconds = 0; }
    internal void StopWaiting()
    {
        if (pending is not { } p) return;
        p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
        pending = null; readyTicks = 0; State = "Cancelled";
    }
    internal void TogglePause()
    {
        if (pending is not { } p) return;
        bool play = !p.PlayA && !p.PlayB;
        pending = p with { PlayA = play && !p.HoldReaction, PlayB = play && !p.HoldSource };
    }
    internal void Begin(MasterTransport.Position p, double aTarget, double bTarget, bool seekA = true, bool followReaction = false, bool holdSource = false, bool holdReaction = false, int? timeoutMilliseconds = null)
    {
        bool playA = pending?.PlayA ?? p.A.Get("pause") != "yes";
        bool playB = pending?.PlayB ?? p.B.Get("pause") != "yes";
        if (followReaction) { bool play = playA || playB; playA = play && !holdReaction; playB = play && !holdSource; }
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
        TimeoutMilliseconds = timeoutMilliseconds ?? (p.A.IsNetworkMedia || p.B.IsNetworkMedia ? 120000 : 15000);
        State = "Waiting";
        pending = new Pending(p.A, p.B, aTarget, bTarget, playA, playB, followReaction, holdSource, holdReaction, Now, TimeoutMilliseconds);
    }
    internal string? Tick()
    {
        if (pending is not { } p) return null;
        ElapsedMilliseconds = Now - p.Started;
        if (ElapsedMilliseconds > p.TimeoutMilliseconds)
        {
            p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
            pending = null; readyTicks = 0; State = "Timed out";
            return "Seek timed out — both paused; retry seek or press play";
        }
        string Reason(MpvPlayer player, double target) => player.Get("idle-active") == "yes" ? "loading"
            : player.Get("seeking") != "no" ? "seeking"
            : player.Get("paused-for-cache") == "yes" ? "buffering"
            : player.Get("time-pos") == null || Math.Abs(player.Number("time-pos") - target) > 0.12 ? "aligning" : "ready";
        string a = Reason(p.A, p.ATarget), b = Reason(p.B, p.BTarget);
        WaitReason = $"A {a}, B {b}";
        if (ElapsedMilliseconds < 100 || a != "ready" || b != "ready")
        {
            readyTicks = 0;
            return $"{WaitReason} • {ElapsedMilliseconds / 1000}s / {p.TimeoutMilliseconds / 1000}s • {(WillResume ? "will resume" : "will stay paused")}";
        }
        if (++readyTicks < 2) return "Both videos settling";
        pending = null; readyTicks = 0; State = "Completed";
        // At an endpoint keep both paused, rather than immediately restarting an EOF player.
        bool ended = !p.FollowReaction && (p.A.Get("eof-reached") == "yes" || p.B.Get("eof-reached") == "yes");
        p.A.Set("pause", !ended && !p.HoldReaction && p.A.Get("eof-reached") != "yes" && p.PlayA ? "no" : "yes");
        p.B.Set("pause", !ended && !p.HoldSource && p.B.Get("eof-reached") != "yes" && p.PlayB ? "no" : "yes");
        return ended ? "Reaction ended — seek back to continue" : "Seek complete";
    }
}
