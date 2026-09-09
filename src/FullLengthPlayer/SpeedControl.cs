namespace FullLengthPlayer;

// A/G use one active temporary toggle per target (master, A or B).
internal sealed class SpeedControl(Func<double> read, Action<double> write)
{
    internal const double Minimum = 0.25, Maximum = 4;
    private string? toggle;
    private double destination, previous;
    internal void Reset() => toggle = null;
    internal void Set(double speed)
    {
        if (!double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        write(Math.Clamp(speed, Minimum, Maximum)); Reset();
    }
    internal void Step(double delta) => Set(read() + delta);
    internal void Toggle(string key, double target)
    {
        target = Math.Clamp(target, Minimum, Maximum);
        double current = read();
        if (toggle == key && Math.Abs(current - destination) < 0.001 && Math.Abs(target - destination) < 0.001)
        {
            write(previous); Reset(); return;
        }
        if (Math.Abs(current - target) < 0.001) { Reset(); return; }
        write(target);
        previous = current; destination = target; toggle = key;
    }
}
