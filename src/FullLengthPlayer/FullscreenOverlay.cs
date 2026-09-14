namespace FullLengthPlayer;

internal sealed class FullscreenOverlay : Panel
{
    internal TrackBar Timeline { get; } = new() { Dock = DockStyle.Bottom, Maximum = 10000, TickStyle = TickStyle.None, Height = 32 };
    private readonly Label time = new() { Dock = DockStyle.Top, Height = 23, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
    internal bool Dragging { get; private set; }
    internal event Action<double>? Seek;
    internal event Action? Activity;
    internal FullscreenOverlay()
    {
        BackColor = Color.FromArgb(35, 35, 35); Height = 60; Visible = false;
        Controls.Add(Timeline); Controls.Add(time);
        Timeline.MouseDown += (_, _) => { Dragging = true; Activity?.Invoke(); };
        Timeline.MouseUp += (_, _) => { Dragging = false; Activity?.Invoke(); Seek?.Invoke(Timeline.Value / 10000.0); };
        Timeline.MouseCaptureChanged += (_, _) => { if (!Timeline.Capture) Dragging = false; };
        Timeline.KeyUp += (_, e) => { if (e.KeyCode is Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown) { Activity?.Invoke(); Seek?.Invoke(Timeline.Value / 10000.0); } };
    }
    internal void UpdatePosition(double elapsed, double duration)
    {
        if (!Dragging) Timeline.Value = duration > 0 ? (int)Math.Clamp(elapsed / duration * 10000, 0, 10000) : 0;
        time.Text = $"{TimeSpan.FromSeconds(Math.Max(0, elapsed)):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(Math.Max(0, duration)):hh\\:mm\\:ss}";
    }
}
