using System.Globalization;

namespace FullLengthPlayer;

// The UI keeps pumping while both decoders seek. Neither resumes ahead of the other.
internal sealed class SeekCoordinator(Func<long>? clock = null, Func<MpvPlayer, bool>? buffering = null)
{
    private bool Buffering(MpvPlayer player) => buffering?.Invoke(player) ?? player.Get("paused-for-cache") == "yes";
    internal string Operation { get; private set; } = "Seek";
    private long Now => clock?.Invoke() ?? Environment.TickCount64;
    internal string State { get; private set; } = "Idle";
    internal string WaitReason { get; private set; } = "";
    internal int TimeoutMilliseconds { get; private set; }
    internal long ElapsedMilliseconds { get; private set; }
    internal bool Suspended => State is "Timed out" or "Cancelled";
    internal bool WillResume => pending is { } p && (p.PlayA || p.PlayB);
    private sealed record Pending(MpvPlayer A, MpvPlayer B, double ATarget, double BTarget,
        bool PlayA, bool PlayB, bool FollowReaction, bool HoldSource, bool HoldReaction, long Started, int TimeoutMilliseconds, bool Refill = false);
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
    internal void HoldForBuffer(MasterTransport.Position p, double offset, bool reactionBuffering)
    {
        bool play = p.A.Get("pause") != "yes" || p.B.Get("pause") != "yes";
        Cancel();
        p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
        // Anchor recovery to the stalled side, so its unseen content is not skipped.
        double at = reactionBuffering ? p.A.Number("time-pos") : p.B.Number("time-pos") - offset;
        at = Math.Clamp(at, Math.Max(0, -offset), Math.Min(p.ADuration, p.BDuration - offset));
        TimeoutMilliseconds = 120000; State = "Waiting"; Operation = "Buffer recovery";
        pending = new Pending(p.A, p.B, at, at + offset, play, play, true, false, false, Now, TimeoutMilliseconds, true);
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
        Operation = "Seek";
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
            return $"{Operation} timed out — both paused; retry seek or press play";
        }
        string Reason(MpvPlayer player, double target) => player.Get("idle-active") == "yes" ? "loading"
            : player.Get("seeking") != "no" ? "seeking"
            : Buffering(player) ? "buffering"
            : player.Get("time-pos") == null || (!p.Refill && Math.Abs(player.Number("time-pos") - target) > 0.12) ? "aligning" : "ready";
        string a = Reason(p.A, p.ATarget), b = Reason(p.B, p.BTarget);
        WaitReason = $"A {a}, B {b}";
        if (ElapsedMilliseconds < 100 || a != "ready" || b != "ready")
        {
            readyTicks = 0;
            return $"{WaitReason} • {ElapsedMilliseconds / 1000}s / {p.TimeoutMilliseconds / 1000}s • {(WillResume ? "will resume" : "will stay paused")}";
        }
        if (++readyTicks < 2) return "Both videos settling";
        if (p.Refill && (Math.Abs(p.A.Number("time-pos") - p.ATarget) > 0.08 || Math.Abs(p.B.Number("time-pos") - p.BTarget) > 0.08))
        {
            // Keep pending resume intent while handing off to the normal paired
            // seek. Do not keep seeking into an empty network cache during refill.
            Begin(new(p.A, p.B, p.A.Number("time-pos"), p.B.Number("time-pos"), p.A.Number("duration"), p.B.Number("duration")), p.ATarget, p.BTarget, followReaction: true);
            Operation = "Buffer realignment";
            return "Buffer ready — realigning both videos";
        }
        pending = null; readyTicks = 0; State = "Completed";
        // At an endpoint keep both paused, rather than immediately restarting an EOF player.
        bool ended = !p.FollowReaction && (p.A.Get("eof-reached") == "yes" || p.B.Get("eof-reached") == "yes");
        p.A.Set("pause", !ended && !p.HoldReaction && p.A.Get("eof-reached") != "yes" && p.PlayA ? "no" : "yes");
        p.B.Set("pause", !ended && !p.HoldSource && p.B.Get("eof-reached") != "yes" && p.PlayB ? "no" : "yes");
        return ended ? "Reaction ended — seek back to continue" : "Seek complete";
    }
}
