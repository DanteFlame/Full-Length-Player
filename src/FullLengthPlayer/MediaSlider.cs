using System.Drawing.Drawing2D;
namespace FullLengthPlayer;

// Shared lightweight slider for transport and volume, with native-control focus semantics.
internal sealed class MediaSlider : Control
{
    private int minimum, maximum = 100, value;
    private bool dragging;
    private readonly ToolTip tip = new() { ShowAlways = true, InitialDelay = 0, ReshowDelay = 0 };
    internal event EventHandler? ValueChanged;
    public int Minimum { get => minimum; set { minimum = value; Value = this.value; Invalidate(); } }
    public int Maximum { get => maximum; set { maximum = Math.Max(minimum, value); Value = this.value; Invalidate(); } }
    public int Value { get => value; set { int next = Math.Clamp(value, minimum, maximum); if (this.value == next) return; this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    internal Color Accent { get; set; } = PlayerTheme.Accent;
    internal Func<double, string>? HoverText { get; set; }
    internal (double Start, double End)? SharedRange { get; set; }
    internal double FractionAt(int x) => Math.Clamp((x - 10.0) / Math.Max(1, Width - 20), 0, 1);
    internal MediaSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        TabStop = true; Height = 28; AccessibleRole = AccessibleRole.Slider; Cursor = Cursors.Hand;
    }
    private void SetFromMouse(int x) => Value = minimum + (int)Math.Round(FractionAt(x) * (maximum - minimum));
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { Focus(); dragging = true; Capture = true; SetFromMouse(e.X); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging) SetFromMouse(e.X);
        if (HoverText != null) tip.Show(HoverText(FractionAt(e.X)), this, Math.Clamp(e.X, 0, Math.Max(0, Width - 160)), -32, 1500);
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && dragging) { SetFromMouse(e.X); dragging = false; Capture = false; }
        base.OnMouseUp(e);
    }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) dragging = false; base.OnMouseCaptureChanged(e); }
    protected override void OnMouseLeave(EventArgs e) { tip.Hide(this); base.OnMouseLeave(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Value += Math.Sign(e.Delta) * 5; base.OnMouseWheel(e); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        Value = e.KeyCode switch { Keys.Home => Minimum, Keys.End => Maximum, Keys.Left => Value - 1, Keys.Right => Value + 1, Keys.PageDown => Value - Math.Max(1, (Maximum - Minimum) / 10), Keys.PageUp => Value + Math.Max(1, (Maximum - Minimum) / 10), _ => Value };
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float width = Math.Max(1, Width - 20), y = Height / 2f;
        float x = 10 + width * (Value - Minimum) / Math.Max(1, Maximum - Minimum);
        using var rail = new Pen(PlayerTheme.Raised, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fill = new Pen(Enabled ? Accent : PlayerTheme.Muted, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(rail, 10, y, Width - 10, y); e.Graphics.DrawLine(fill, 10, y, x, y);
        if (SharedRange is { } range && range.End > range.Start)
        {
            using var band = new Pen(PlayerTheme.Orange, 2);
            e.Graphics.DrawLine(band, 10 + width * (float)range.Start, y + 7, 10 + width * (float)range.End, y + 7);
        }
        using var knob = new SolidBrush(Enabled ? Accent : PlayerTheme.Muted);
        e.Graphics.FillEllipse(knob, x - 5, y - 5, 10, 10);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2), PlayerTheme.Muted, BackColor);
    }
    protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
}
