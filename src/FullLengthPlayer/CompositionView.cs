namespace FullLengthPlayer;

// Native surfaces retain their handles; clipping and positioning do not touch playback.
internal sealed class CompositionView : Panel
{
    private readonly Panel canvas = new() { BackColor = Color.Black };
    private readonly Panel mask = new() { BackColor = Color.Black };
    private readonly PlayerPane reaction, source;
    private readonly Panel[] grips = new Panel[4];
    internal Rectangle CanvasBounds => canvas.Bounds;
    internal Rectangle SourceBounds => source.Surface.Bounds;
    internal Rectangle MaskBounds => mask.Bounds;
    internal Rectangle ReactionBounds => reaction.Surface.Bounds;
    internal double CanvasAspect { get; set; } = 16.0 / 9;
    internal double SourceFraction { get; set; } = 0.7;
    private SourceAnchor sourceAnchor;
    internal SourceAnchor AnchorPosition
    {
        get => sourceAnchor;
        set
        {
            sourceAnchor = value;
            if (AnchorRow != 1) ReactionBottom = AnchorRow == 0;
        }
    }
    internal bool ReactionBottom { get; set; }
    internal bool TopAnchor { get => AnchorPosition == SourceAnchor.Top; set => AnchorPosition = value ? SourceAnchor.Top : SourceAnchor.Bottom; }
    internal int AnchorColumn => AnchorPosition switch { SourceAnchor.TopLeft or SourceAnchor.Left or SourceAnchor.BottomLeft => 0, SourceAnchor.TopRight or SourceAnchor.Right or SourceAnchor.BottomRight => 2, _ => 1 };
    internal int AnchorRow => AnchorPosition switch { SourceAnchor.TopLeft or SourceAnchor.Top or SourceAnchor.TopRight => 0, SourceAnchor.Left or SourceAnchor.Right => 1, _ => 2 };
    internal double CropTop { get; set; }
    internal double CropBottom { get; set; }
    internal double Zoom { get; set; } = 1;
    internal double PanX { get; set; }
    internal double PanY { get; set; }
    internal bool ShowHandles { get; set; } = true;
    internal event Action? ResizedSource;
    private Point dragStart;
    private int dragWidth;
    private bool resizing;
    private int dragCorner;
    internal CompositionView(PlayerPane a, PlayerPane b)
    {
        reaction = a; source = b;
        Dock = DockStyle.Fill; BackColor = Color.FromArgb(18, 18, 18);
        Controls.Add(canvas); canvas.Controls.Add(mask);
        a.Surface.Dock = b.Surface.Dock = DockStyle.None;
        mask.Controls.Add(a.Surface); canvas.Controls.Add(b.Surface);
        b.Surface.BringToFront();
        for (int i = 0; i < grips.Length; i++)
        {
            int corner = i;
            var grip = grips[i] = new Panel { BackColor = Color.DodgerBlue, Cursor = i is 0 or 3 ? Cursors.SizeNWSE : Cursors.SizeNESW };
            canvas.Controls.Add(grip); grip.BringToFront();
            grip.MouseDown += (_, e) => { if (e.Button != MouseButtons.Left) return; resizing = true; dragCorner = corner; dragStart = Cursor.Position; dragWidth = SourceBounds.Width; grip.Capture = true; };
            grip.MouseMove += (_, _) =>
            {
                if (!resizing || dragCorner != corner) return;
                Point delta = new(Cursor.Position.X - dragStart.X, Cursor.Position.Y - dragStart.Y);
                double change = ResizeChange(corner, delta, Aspect(source));
                SourceFraction = Math.Clamp((dragWidth + change) / Math.Max(1, canvas.Width), 0.15, 1);
                Arrange(); ResizedSource?.Invoke();
            };
            grip.MouseUp += (_, _) => { resizing = false; grip.Capture = false; };
            grip.MouseCaptureChanged += (_, _) => { if (!grip.Capture) resizing = false; };
        }
        Resize += (_, _) => Arrange();
    }
    internal double ResizeChange(int corner, Point delta, double aspect)
    {
        int x = AnchorColumn == 1 ? (corner % 2 == 0 ? -2 : 2) : AnchorColumn == 0 ? (corner % 2 == 0 ? 0 : 1) : (corner % 2 == 0 ? -1 : 0);
        int y = AnchorRow == 1 ? (corner < 2 ? -2 : 2) : AnchorRow == 0 ? (corner < 2 ? 0 : 1) : (corner < 2 ? -1 : 0);
        double dx = delta.X * x, dy = delta.Y * y * aspect;
        return Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
    }
    private static double Aspect(PlayerPane pane)
    {
        double w = pane.Player?.Number("video-out-params/dw") ?? 0;
        double h = pane.Player?.Number("video-out-params/dh") ?? 0;
        return w > 0 && h > 0 ? w / h : 16.0 / 9;
    }
    internal void Arrange()
    {
        int width = Math.Max(1, Math.Min(ClientSize.Width, (int)(ClientSize.Height * CanvasAspect)));
        int height = Math.Max(1, (int)Math.Round(width / CanvasAspect));
        canvas.Bounds = new Rectangle((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
        int rw = Math.Max(1, (int)Math.Round(width * Zoom));
        int rh = Math.Max(1, (int)Math.Round(rw / Aspect(reaction)));
        int top = (int)Math.Round(rh * CropTop);
        int visible = Math.Max(1, (int)Math.Round(rh * (1 - CropTop - CropBottom)));
        int maskHeight = Math.Min(height, visible);
        mask.Bounds = new Rectangle(0, ReactionBottom ? height - maskHeight : 0, width, maskHeight);
        reaction.Surface.Bounds = new Rectangle((width - rw) / 2 + (int)(PanX * width), (ReactionBottom ? maskHeight - visible - top : -top) + (int)(PanY * height), rw, rh);
        int sw = Math.Max(1, (int)Math.Round(Math.Min(width * SourceFraction, height * Aspect(source))));
        int sh = Math.Max(1, (int)Math.Round(sw / Aspect(source)));
        source.Surface.Bounds = new Rectangle(AnchorColumn * (width - sw) / 2, AnchorRow * (height - sh) / 2, sw, sh);
        int size = Math.Max(6, (int)(10 * DeviceDpi / 96.0));
        var rect = SourceBounds;
        for (int i = 0; i < grips.Length; i++)
        {
            grips[i].Bounds = new Rectangle(i % 2 == 0 ? rect.Left : rect.Right - size, i < 2 ? rect.Top : rect.Bottom - size, size, size);
            bool fixedCorner = AnchorColumn == (i % 2 == 0 ? 0 : 2) && AnchorRow == (i < 2 ? 0 : 2);
            grips[i].Visible = ShowHandles && !fixedCorner;
        }
    }
    internal PlayerPane? HitPlayer(Point screen)
    {
        Point p = canvas.PointToClient(screen);
        if (!canvas.ClientRectangle.Contains(p)) return null;
        if (SourceBounds.Contains(p)) return source;
        if (MaskBounds.Contains(p)) return reaction;
        return null;
    }
}
