using System.Globalization;

namespace FullLengthPlayer;

// Owns shared transport and the fixed B = A + offset alignment for the loaded pair.
internal sealed class MasterTransport(Func<MpvPlayer?> reaction, Func<MpvPlayer?> source)
{
    internal const double OffsetStep = 0.05;
    internal static double RoundOffset(double seconds)
    {
        if (!double.IsFinite(seconds) || Math.Abs(seconds) > 604800) throw new ArgumentOutOfRangeException(nameof(seconds));
        return (double)(Math.Round((decimal)seconds / 0.05m, 0, MidpointRounding.AwayFromZero) * 0.05m);
    }
    internal bool Locked { get; private set; }
    internal double Offset { get; private set; }
    internal double? Drift { get; private set; }
    internal string SyncStatus { get; private set; } = "Unlocked — align videos, then lock";
    internal int CorrectionCount { get; private set; }
    private readonly SeekCoordinator seeks = new();
    internal bool SeekingTogether => seeks.Waiting;
    private long nextCorrection;
    private bool sourceHeld, reactionHeld;
    internal double TimelineStart => Locked ? Math.Min(0, -Offset) : 0;
    internal double TimelineEnd => Snapshot() is { } p ? (Locked ? Math.Max(p.ADuration, p.BDuration - Offset) : p.ADuration) : 0;
    internal double TimelineTime => Snapshot() is { } p ? Clock(p) : 0;
    private double Clock(Position p) => Locked && reactionHeld ? p.BTime - Offset : p.ATime;
    internal void SetSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed < SpeedControl.Minimum || speed > SpeedControl.Maximum) throw new ArgumentOutOfRangeException(nameof(speed));
        var p = Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        // Holding the pair through a locked change also preserves the stored offset.
        if (Locked) SeekLocked(p, seeks.ATarget is { } pending ? (reactionHeld ? seeks.BTarget!.Value - Offset : pending) : Clock(p));
        string value = speed.ToString(CultureInfo.InvariantCulture);
        p.A.Set("speed", value); p.B.Set("speed", value);
        Settle();
    }
    private const double Tolerance = 0.08;
    private void Settle() => nextCorrection = Environment.TickCount64 + 2000;
    internal void Unlock(string reason = "Unlocked — align videos, then lock")
    {
        seeks.Cancel(); sourceHeld = reactionHeld = false; Locked = false; Drift = null; SyncStatus = reason;
    }
    internal void Reset() { Unlock(); Offset = 0; CorrectionCount = 0; nextCorrection = 0; }
    internal void CaptureAlignment()
    {
        var p = Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        if (seeks.Waiting || Busy(p)) throw new InvalidOperationException("Wait for both videos to finish seeking before locking.");
        if (Math.Abs(p.A.Number("speed") - p.B.Number("speed")) > 0.001)
            throw new InvalidOperationException("Set a shared speed before locking differently paced videos.");
        double captured = p.BTime - p.ATime;
        double rounded = RoundOffset(captured);
        if (Math.Abs(captured - rounded) > 0.000001) { SetOffset(rounded); return; }
        Offset = rounded;
        Locked = true; Drift = 0; SyncStatus = "Locked"; Settle();
    }
    internal void SetOffset(double seconds)
    {
        seconds = RoundOffset(seconds);
        var p = Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        if (Math.Abs(p.A.Number("speed") - p.B.Number("speed")) > 0.001)
            throw new InvalidOperationException("Set a shared speed before locking differently paced videos.");
        if (Math.Max(0, -seconds) >= Math.Min(p.ADuration, p.BDuration - seconds))
            throw new InvalidOperationException("That offset leaves no shared playable range.");
        double clock = Clock(p);
        Offset = seconds; Locked = true;
        SeekLocked(p, clock);
    }
    internal void Nudge(double seconds) => SetOffset((Locked ? Offset : (Snapshot() is { } p ? p.BTime - p.ATime : 0)) + seconds);
    private static bool Busy(Position p) => p.A.Get("seeking") == "yes" || p.B.Get("seeking") == "yes"
        || p.A.Get("paused-for-cache") == "yes" || p.B.Get("paused-for-cache") == "yes";
    internal void Tick()
    {
        if (seeks.Waiting)
        {
            SyncStatus = seeks.Tick() ?? SyncStatus;
            Settle(); return;
        }
        if (!Locked) return;
        var p = Snapshot();
        if (p == null) { SyncStatus = "Waiting for media"; return; }
        // Once A ends, B becomes the clock while its remaining content plays.
        if (!reactionHeld && p.A.Get("eof-reached") == "yes" && p.BTime < p.BDuration - 0.12 && p.BTime - Offset >= p.ADuration - 0.12)
            reactionHeld = true;
        double clock = Clock(p);
        bool outsideA = clock < 0 || clock >= p.ADuration - (p.A.Get("eof-reached") == "yes" ? 0.12 : 0);
        if (reactionHeld)
        {
            if (!outsideA) { reactionHeld = false; SeekLocked(p, clock); return; }
            p.A.Set("pause", "yes"); Drift = null;
            SyncStatus = clock < 0 ? "Locked — reaction waiting at start" : "Locked — reaction ended; source continues";
            return;
        }
        double target = clock + Offset;
        bool before = target < 0;
        bool after = target >= p.BDuration || (p.B.Get("eof-reached") == "yes" && target >= p.BDuration - 0.12);
        if (before || after)
        {
            sourceHeld = true; Drift = null;
            p.B.Set("pause", "yes");
            double edge = before ? 0 : p.BDuration;
            if (p.B.Get("seeking") != "yes" && Math.Abs(p.BTime - edge) > 0.12)
                p.B.Command("seek", edge.ToString(CultureInfo.InvariantCulture), "absolute+exact");
            SyncStatus = before ? "Locked — source waiting at start" : "Locked — source ended; reaction continues";
            return;
        }
        if (sourceHeld)
        {
            sourceHeld = false;
            SeekLocked(p, p.ATime); // Rejoin at the stored offset and A's playback state.
            return;
        }
        Drift = p.BTime - target;
        if (Busy(p)) { SyncStatus = "Waiting for playback to settle"; Settle(); return; }
        if (p.A.Get("eof-reached") == "yes")
        {
            p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
            SyncStatus = "Reaction ended — seek back to continue"; return;
        }
        if (Environment.TickCount64 < nextCorrection) return;
        if (p.A.Get("pause") != p.B.Get("pause"))
        {
            // Manual controls unlock first. A mismatch while locked is a native
            // restart/EOF condition; stop both instead of silently skipping correction forever.
            p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
            SyncStatus = "Play/pause mismatch — both paused; press play to continue";
        }
        SyncStatus = "Locked";
        if (Math.Abs(Drift.Value) > Tolerance)
        {
            try
            {
                seeks.Begin(p, p.ATime, target, seekA: false, followReaction: true);
                CorrectionCount++; SyncStatus = "Corrected source drift";
            }
            catch (Exception e) { Unlock("Correction stopped: " + e.Message); }
        }
        Settle();
    }
    private void SeekLocked(Position p, double reactionTime)
    {
        double target = Math.Clamp(reactionTime, TimelineStart, Math.Max(p.ADuration, p.BDuration - Offset));
        double sourceTarget = target + Offset;
        sourceHeld = sourceTarget < 0 || sourceTarget >= p.BDuration;
        reactionHeld = target < 0 || target >= p.ADuration;
        seeks.Begin(p, Math.Clamp(target, 0, p.ADuration), Math.Clamp(sourceTarget, 0, p.BDuration), followReaction: true, holdSource: sourceHeld, holdReaction: reactionHeld);
        SyncStatus = "Locked — settling after seek"; Settle();
    }
    internal sealed record Position(MpvPlayer A, MpvPlayer B, double ATime, double BTime, double ADuration, double BDuration);
    internal Position? Snapshot()
    {
        var a = reaction(); var b = source();
        if (a == null || b == null) return null;
        static double? Read(MpvPlayer p, string name) => double.TryParse(p.Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        var at = Read(a, "time-pos"); var bt = Read(b, "time-pos");
        var ad = Read(a, "duration"); var bd = Read(b, "duration");
        if (at == null || bt == null || ad is not > 0 || bd is not > 0 || a.Get("idle-active") == "yes" || b.Get("idle-active") == "yes") return null;
        return new Position(a, b, at.Value, bt.Value, ad.Value, bd.Value);
    }
    internal void TogglePause()
    {
        if (seeks.Waiting) { seeks.TogglePause(); return; }
        var p = Snapshot(); if (p == null) return;
        // Mixed states converge to paused; pressing again starts both.
        string state = (p.A.Get("pause") != "yes" || p.B.Get("pause") != "yes") ? "yes" : "no";
        double clock = Clock(p);
        p.A.Set("pause", Locked && (clock < 0 || clock >= p.ADuration) ? "yes" : state);
        bool hold = Locked && (clock + Offset < 0 || clock + Offset >= p.BDuration);
        p.B.Set("pause", hold ? "yes" : state);
        Settle();
    }
    internal void Jump(double seconds)
    {
        var p = Snapshot(); if (p != null) Move(p, seconds);
    }
    internal void SeekReaction(double seconds)
    {
        var p = Snapshot(); if (p != null) Move(p, seconds - PendingClock(p));
    }
    private double PendingClock(Position p) => seeks.ATarget is { } at ? (reactionHeld ? seeks.BTarget!.Value - Offset : at) : Clock(p);
    private void Move(Position p, double delta)
    {
        if (!double.IsFinite(delta)) return;
        if (Locked) { SeekLocked(p, PendingClock(p) + delta); return; }
        if (seeks.ATarget is { } pendingA && seeks.BTarget is { } pendingB)
            p = p with { ATime = pendingA, BTime = pendingB };
        // Move by the same amount, limiting the pair at either file's start/end.
        // This preserves manual alignment at the instant of this command.
        double lower = Math.Max(-p.ATime, -p.BTime);
        double upper = Math.Min(p.ADuration - p.ATime, p.BDuration - p.BTime);
        if (lower > upper) return;
        delta = Math.Clamp(delta, lower, upper);
        seeks.Begin(p, p.ATime + delta, p.BTime + delta);
    }
}
