using System.Runtime.InteropServices;

namespace FullLengthPlayer;

// The streamed mouse and a desktop mouse use the same release-based gesture path.
internal sealed class FullscreenGestures
{
    private int lastZone = -1;
    private Point last;
    private long lastTime;
    internal void Reset() => lastZone = -1;
    internal int Tap(Point point, Size size, long now, int interval, int tolerance)
    {
        int zone = Math.Clamp(point.X * 3 / Math.Max(1, size.Width), 0, 2);
        bool pair = zone == lastZone && now - lastTime >= 0 && now - lastTime <= interval
            && Math.Abs(point.X-last.X) <= tolerance && Math.Abs(point.Y-last.Y) <= tolerance;
        if (pair) { Reset(); return zone == 0 ? -5 : zone == 2 ? 5 : 0; }
        lastZone = zone; last = point; lastTime = now;
        return zone == 1 ? 1 : 0; // Centre responds immediately; its second tap is ignored.
    }
}

internal sealed class SpeedNotice : Form
{
    private readonly Label caption = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(30,30,30) };
    private long shownAt;
    internal string Caption => caption.Text;
    internal SpeedNotice()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
        caption.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        Controls.Add(caption);
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x20 | 0x80; return cp; } // No activate, click-through, tool window.
    }
    internal void Display(Form owner, double speed, long now)
    {
        caption.Text = speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "×";
        shownAt = now; Opacity = .92;
        double scale = owner.DeviceDpi / 96.0;
        Size = new Size((int)(130*scale), (int)(60*scale));
        var bounds = owner.RectangleToScreen(owner.ClientRectangle);
        Location = new Point(bounds.Right-Width-(int)(24*scale), bounds.Top+(int)(24*scale));
        if (!Visible) Show(owner);
    }
    internal void Advance(long now, bool allowed)
    {
        if (!Visible) return;
        long age = now-shownAt;
        if (!allowed || age >= 1200) { Hide(); return; }
        Opacity = .92 * Math.Clamp((1200-age)/350.0, 0, 1);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) caption.Font.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed partial class MainForm
{
    private readonly FullscreenGestures gestures = new();
    internal SpeedNotice SpeedToast { get; } = new();
    private Point? pressPoint;
    private int TapTolerance => Math.Max(SystemInformation.DoubleClickSize.Width, (int)(24*DeviceDpi/96.0));
    private bool IsGesturePoint(Point screen)
    {
        return Fullscreen && !restoringSession && Composition.RectangleToScreen(Composition.ClientRectangle).Contains(screen)
            && !(Hud.Visible && Hud.RectangleToScreen(Hud.ClientRectangle).Contains(screen));
    }
    internal bool FullscreenPointer(int message, Point screen, long now)
    {
        if (!IsGesturePoint(screen)) { pressPoint = null; gestures.Reset(); return false; }
        if (message is 0x0201 or 0x0203) { pressPoint = screen; return true; }
        if (message != 0x0202) return false;
        var start = pressPoint; pressPoint = null;
        if (start == null || Math.Abs(start.Value.X-screen.X)>TapTolerance || Math.Abs(start.Value.Y-screen.Y)>TapTolerance) { gestures.Reset(); return true; }
        int action = gestures.Tap(PointToClient(screen), ClientSize, now, SystemInformation.DoubleClickTime, TapTolerance);
        ActiveControl = null;
        if (action == 1) RunMaster(Master.TogglePause);
        else if (action != 0) RunMaster(() => Master.Jump(action));
        else RevealFullscreen();
        return true;
    }
    private void ShowSpeedNotice()
    {
        if (Fullscreen && Master.Snapshot() != null)
            SpeedToast.Display(this, Reaction.Player?.Number("speed") ?? 1, Environment.TickCount64);
    }
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
}
