namespace FullLengthPlayer;

// Preserve the original saved values: bottom=0, top=1.
internal enum SourceAnchor { Bottom, Top, TopLeft, TopRight, Left, Right, BottomLeft, BottomRight }

internal sealed class AnchorPicker : TableLayoutPanel
{
    private readonly Dictionary<SourceAnchor, Button> buttons = new();
    private readonly ToolTip hints = new();
    private SourceAnchor selected;
    internal event Action? SelectionChanged;
    internal SourceAnchor Selected
    {
        get => selected;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            bool changed = selected != value; selected = value; Invalidate(true);
            if (changed) SelectionChanged?.Invoke();
        }
    }
    internal void Choose(SourceAnchor position) => buttons[position].PerformClick();
    internal AnchorPicker()
    {
        Size = new Size(112, 100); ColumnCount = RowCount = 3;
        AccessibleName = "Source position";
        for (int i = 0; i < 3; i++) { ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3)); RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3)); }
        var cells = new[] {
            (SourceAnchor.TopLeft, 0, 0, "↖", "Top left"), (SourceAnchor.Top, 1, 0, "↑", "Top centre"), (SourceAnchor.TopRight, 2, 0, "↗", "Top right"),
            (SourceAnchor.Left, 0, 1, "←", "Left centre"), (SourceAnchor.Right, 2, 1, "→", "Right centre"),
            (SourceAnchor.BottomLeft, 0, 2, "↙", "Bottom left"), (SourceAnchor.Bottom, 1, 2, "↓", "Bottom centre"), (SourceAnchor.BottomRight, 2, 2, "↘", "Bottom right") };
        foreach (var (position, column, row, arrow, name) in cells)
        {
            var button = new Button { Dock = DockStyle.Fill, Margin = new Padding(1), AccessibleName = "Source: " + name, AccessibleDescription = "Snap source video to " + name, Cursor = Cursors.Hand };
            buttons.Add(position, button); Controls.Add(button, column, row);
            hints.SetToolTip(button, name);
            button.Click += (_, _) => Selected = position;
            button.Paint += (_, e) =>
            {
                bool active = Selected == position;
                e.Graphics.Clear(active ? PlayerTheme.Orange : PlayerTheme.Raised);
                TextRenderer.DrawText(e.Graphics, arrow, Font, button.ClientRectangle, active ? PlayerTheme.Background : PlayerTheme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (button.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(button.ClientRectangle, -3, -3));
            };
        }
        Controls.Add(new Label { Text = "B", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AccessibleName = "Source video" }, 1, 1);
    }
    protected override void Dispose(bool disposing) { if (disposing) hints.Dispose(); base.Dispose(disposing); }
}
