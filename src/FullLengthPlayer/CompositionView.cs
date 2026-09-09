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
    internal bool TopAnchor { get; set; }
    internal double CropTop { get; set; } = 0.2;
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
                double dx = delta.X * (corner % 2 == 0 ? -2 : 2);
                double dy = delta.Y * (corner < 2 ? -1 : 1) * Aspect(source);
                double change = Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
                SourceFraction = Math.Clamp((dragWidth + change) / Math.Max(1, canvas.Width), 0.15, 1);
                Arrange(); ResizedSource?.Invoke();
            };
            grip.MouseUp += (_, _) => { resizing = false; grip.Capture = false; };
            grip.MouseCaptureChanged += (_, _) => { if (!grip.Capture) resizing = false; };
        }
        Resize += (_, _) => Arrange();
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
        mask.Bounds = new Rectangle(0, 0, width, Math.Min(height, visible));
        reaction.Surface.Bounds = new Rectangle((width - rw) / 2 + (int)(PanX * width), -top + (int)(PanY * height), rw, rh);
        int sw = Math.Max(1, (int)Math.Round(Math.Min(width * SourceFraction, height * Aspect(source))));
        int sh = Math.Max(1, (int)Math.Round(sw / Aspect(source)));
        source.Surface.Bounds = new Rectangle((width - sw) / 2, TopAnchor ? 0 : height - sh, sw, sh);
        source.Surface.BringToFront();
        int size = Math.Max(6, (int)(10 * DeviceDpi / 96.0));
        var rect = SourceBounds;
        for (int i = 0; i < grips.Length; i++)
        {
            grips[i].Bounds = new Rectangle(i % 2 == 0 ? rect.Left : rect.Right - size, i < 2 ? rect.Top : rect.Bottom - size, size, size);
            grips[i].Visible = ShowHandles; grips[i].BringToFront();
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
