using System.Globalization;

namespace FullLengthPlayer;

// Applies shared commands to the current pair. No persistent offset or drift loop yet.
internal sealed class MasterTransport(Func<MpvPlayer?> reaction, Func<MpvPlayer?> source)
{
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
    }
    internal void Jump(double seconds)
    {
        var p = Snapshot(); if (p != null) Move(p, seconds);
    }
    internal void SeekReaction(double seconds)
    {
        var p = Snapshot(); if (p != null) Move(p, seconds - p.ATime);
    }
    private static void Move(Position p, double delta)
    {
        if (!double.IsFinite(delta)) return;
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
