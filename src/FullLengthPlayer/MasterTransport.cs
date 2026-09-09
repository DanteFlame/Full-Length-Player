using System.Globalization;

namespace FullLengthPlayer;

// Owns shared transport and the fixed B = A + offset alignment for the loaded pair.
internal sealed class MasterTransport(Func<MpvPlayer?> reaction, Func<MpvPlayer?> source)
{
    internal bool Locked { get; private set; }
    internal double Offset { get; private set; }
    internal double? Drift { get; private set; }
    internal string SyncStatus { get; private set; } = "Unlocked — align videos, then lock";
    internal int CorrectionCount { get; private set; }
    private long nextCorrection;
    private const double Tolerance = 0.08;
    private void Settle() => nextCorrection = Environment.TickCount64 + 2000;
    internal void Unlock(string reason = "Unlocked — align videos, then lock")
    {
        Locked = false; Drift = null; SyncStatus = reason;
    }
    internal void CaptureAlignment()
    {
        var p = Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        if (Busy(p)) throw new InvalidOperationException("Wait for both videos to finish seeking before locking.");
        Offset = p.BTime - p.ATime;
        Locked = true; Drift = 0; SyncStatus = "Locked"; Settle();
    }
    internal void SetOffset(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        var p = Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        if (Math.Max(0, -seconds) >= Math.Min(p.ADuration, p.BDuration - seconds))
            throw new InvalidOperationException("That offset leaves no shared playable range.");
        Offset = seconds; Locked = true;
        if (p.ATime + Offset >= 0 && p.ATime + Offset <= p.BDuration)
        {
            p.B.Command("seek", (p.ATime + Offset).ToString(CultureInfo.InvariantCulture), "absolute+exact");
            SyncStatus = "Locked — applying offset"; Settle();
        }
        else SeekLocked(p, p.ATime);
    }
    internal void Nudge(double seconds) => SetOffset((Locked ? Offset : (Snapshot() is { } p ? p.BTime - p.ATime : 0)) + seconds);
    private static bool Busy(Position p) => p.A.Get("seeking") == "yes" || p.B.Get("seeking") == "yes"
        || p.A.Get("paused-for-cache") == "yes" || p.B.Get("paused-for-cache") == "yes";
    internal void Tick()
    {
        if (!Locked) return;
        var p = Snapshot();
        if (p == null) { SyncStatus = "Waiting for media"; return; }
        Drift = p.BTime - (p.ATime + Offset);
        if (Busy(p)) { SyncStatus = "Waiting for playback to settle"; Settle(); return; }
        if (Environment.TickCount64 < nextCorrection) return;
        double target = p.ATime + Offset;
        if (p.A.Get("eof-reached") == "yes" || p.B.Get("eof-reached") == "yes" || target < 0 || target > p.BDuration)
        {
            p.A.Set("pause", "yes"); p.B.Set("pause", "yes");
            // Restore the pair to its shared boundary if A ran past it between checks.
            // Do not re-seek an already settled final frame every timer interval.
            double boundaryA = Math.Clamp(p.ATime, Math.Max(0, -Offset), Math.Min(p.ADuration, p.BDuration - Offset));
            if (Math.Abs(p.ATime - boundaryA) > Tolerance || Math.Abs(p.BTime - boundaryA - Offset) > Tolerance)
                SeekLocked(p, boundaryA);
            SyncStatus = "Shared range ended — seek back to continue"; Settle(); return;
        }
        if (p.A.Get("pause") != p.B.Get("pause")) { SyncStatus = "Waiting for matching play/pause states"; return; }
        SyncStatus = "Locked";
        if (Math.Abs(Drift.Value) > Tolerance)
        {
            try
            {
                p.B.Command("seek", target.ToString(CultureInfo.InvariantCulture), "absolute+exact");
                CorrectionCount++; SyncStatus = "Corrected source drift";
            }
            catch (Exception e) { Unlock("Correction stopped: " + e.Message); }
        }
        Settle();
    }
    private void SeekLocked(Position p, double reactionTime)
    {
        double low = Math.Max(0, -Offset), high = Math.Min(p.ADuration, p.BDuration - Offset);
        if (high < low) { Unlock("No shared range — realign the videos"); return; }
        double target = Math.Clamp(reactionTime, low, high);
        p.A.Command("seek", target.ToString(CultureInfo.InvariantCulture), "absolute+exact");
        p.B.Command("seek", (target + Offset).ToString(CultureInfo.InvariantCulture), "absolute+exact");
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
        var p = Snapshot(); if (p == null) return;
        // Mixed states converge to paused; pressing again starts both.
        string state = p.A.Get("pause") != "yes" || p.B.Get("pause") != "yes" ? "yes" : "no";
        p.A.Set("pause", state); p.B.Set("pause", state);
        Settle();
    }
    internal void Jump(double seconds)
    {
        var p = Snapshot(); if (p != null) Move(p, seconds);
    }
    internal void SeekReaction(double seconds)
    {
        var p = Snapshot(); if (p != null) Move(p, seconds - p.ATime);
    }
    private void Move(Position p, double delta)
    {
        if (!double.IsFinite(delta)) return;
        if (Locked) { SeekLocked(p, p.ATime + delta); return; }
        // Move by the same amount, limiting the pair at either file's start/end.
        // This preserves manual alignment at the instant of this command.
        double lower = Math.Max(-p.ATime, -p.BTime);
        double upper = Math.Min(p.ADuration - p.ATime, p.BDuration - p.BTime);
        if (lower > upper) return;
        delta = Math.Clamp(delta, lower, upper);
        p.A.Command("seek", (p.ATime + delta).ToString(CultureInfo.InvariantCulture), "absolute+exact");
        p.B.Command("seek", (p.BTime + delta).ToString(CultureInfo.InvariantCulture), "absolute+exact");
    }
}
