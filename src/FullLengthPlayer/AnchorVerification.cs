using System.Text.Json;
namespace FullLengthPlayer;

internal sealed partial class MainForm
{
    internal async Task VerifyAnchors(string report)
    {
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Source anchors: " + message); }
        var saved = CaptureSettings();
        var picker = (AnchorPicker)layoutInputs["Source edge"];
        IntPtr a = Reaction.Surface.Handle, b = Source.Surface.Handle;
        ShowSetupPage(1);
        try
        {
            foreach (double aspect in new[] { 4.0 / 3, 16.0 / 9, 1.6, 21.0 / 9, 32.0 / 9 })
            foreach (SourceAnchor position in Enum.GetValues<SourceAnchor>())
            foreach (double fraction in new[] { .25, .7, 1.0 })
            foreach (double zoom in new[] { .6, 1.0, 1.4 })
            {
                picker.Choose(position);
                Composition.CanvasAspect = aspect; Composition.SourceFraction = fraction;
                Composition.CropTop = .2; Composition.CropBottom = .1; Composition.PanX = Composition.PanY = 0; Composition.Zoom = zoom;
                Composition.Arrange();
                var c = Composition.CanvasBounds; var r = Composition.SourceBounds;
                Check(Composition.AnchorPosition == position, "Picker did not select " + position);
                Check(r.X == Composition.AnchorColumn * (c.Width - r.Width) / 2 && r.Y == Composition.AnchorRow * (c.Height - r.Height) / 2, "Incorrect placement: " + position);
                Check(new Rectangle(0, 0, c.Width, c.Height).Contains(r), "Source escaped canvas");
                var expected = position switch {
                    SourceAnchor.TopLeft => (2, 2), SourceAnchor.Top => (1, 2), SourceAnchor.TopRight => (0, 2),
                    SourceAnchor.Left => (2, 1), SourceAnchor.Right => (0, 1),
                    SourceAnchor.BottomLeft => (2, 0), SourceAnchor.Bottom => (1, 0), SourceAnchor.BottomRight => (0, 0),
                    _ => throw new InvalidOperationException()
                };
                Check(Composition.ReactionColumn == expected.Item1 && Composition.ReactionRow == expected.Item2, "Incorrect opposing anchor");
                Check(Composition.ReactionBounds.X == expected.Item1 * (c.Width - Composition.ReactionBounds.Width) / 2, "Reaction lost horizontal anchor");
                Check(Composition.MaskBounds.Top == expected.Item2 * (c.Height - Composition.MaskBounds.Height) / 2, "Reaction mask lost vertical anchor");
                int visible = Math.Max(1, (int)Math.Round(Composition.ReactionBounds.Height * .7));
                int crop = (int)Math.Round(Composition.ReactionBounds.Height * .2);
                Check(Composition.ReactionBounds.Y == expected.Item2 * (Composition.MaskBounds.Height - visible) / 2 - crop, "Cropped reaction lost vertical anchor");
                var roundTrip = JsonSerializer.Deserialize<SavedSettings>(JsonSerializer.Serialize(CaptureSettings()))!;
                ApplySettings(roundTrip, true);
                Check(Composition.AnchorPosition == position && Composition.ReactionBottom == roundTrip.ReactionBottom, "Session lost anchor");
            }
            foreach (bool bottom in new[] { false, true })
            {
                picker.Choose(bottom ? SourceAnchor.Top : SourceAnchor.Bottom);
                foreach (var side in new[] { SourceAnchor.Left, SourceAnchor.Right })
                {
                    picker.Choose(side);
                    Check(Composition.ReactionRow == 1, "Side midpoint must vertically center reaction");
                }
            }
            // Original settings JSON had no ReactionBottom property.
            var old = JsonSerializer.Serialize(saved with { Anchor = 1 }).Replace(",\"ReactionBottom\":" + (saved.ReactionBottom ? "true" : "false"), "");
            ApplySettings(JsonSerializer.Deserialize<SavedSettings>(old)!, true);
            Check(Composition.TopAnchor && Composition.ReactionBottom, "Legacy top session changed layout");
            picker.Choose(SourceAnchor.BottomLeft);
            Check(Composition.ResizeChange(1, new Point(10, -10), 16.0 / 9) > 0, "Free corner cannot enlarge bottom-left source");
            Check(Composition.ResizeChange(2, new Point(10, 10), 16.0 / 9) == 0, "Pinned corner was not fixed");
            Composition.CanvasAspect = 16.0 / 9; Composition.Zoom = .6;
            Composition.SourceFraction = .4; Composition.Arrange();
            var before = Composition.SourceBounds; var canvas = Composition.CanvasBounds;
            Cursor.Position = Composition.PointToScreen(new Point(canvas.Left + before.Right - 3, canvas.Top + before.Top + 3));
            mouse_event(2, 0, 0, 0, UIntPtr.Zero); await Task.Delay(40);
            Cursor.Position = new Point(Cursor.Position.X + 30, Cursor.Position.Y - 20); await Task.Delay(100);
            mouse_event(4, 0, 0, 0, UIntPtr.Zero); await Task.Delay(100);
            Check(Composition.SourceBounds.Width > before.Width && Composition.SourceBounds.Left == 0 && Composition.SourceBounds.Bottom == Composition.CanvasBounds.Height, "Mouse resize lost bottom-left anchor");
            ToggleFullscreen(); await Task.Delay(100);
            Check(Composition.SourceBounds.Left == 0 && Composition.SourceBounds.Bottom == Composition.CanvasBounds.Height, "Fullscreen lost corner");
            ToggleFullscreen();
            Check(Reaction.Surface.Handle == a && Source.Surface.Handle == b, "Anchor selection recreated native surfaces");
            ResetLayoutGroup(new[] { "Canvas", "Source edge", "Source %" });
            Check(picker.Selected == SourceAnchor.Bottom && !Composition.ReactionBottom, "Reset lost defaults");
            picker.Choose(SourceAnchor.TopRight); await Task.Delay(100);
            using var shot = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(PointToScreen(Point.Empty), Point.Empty, shot.Size);
            shot.Save(report + ".ui-anchors.png");
            Console.WriteLine("PASS: all eight opposing anchors, five canvas ratios including ultrawide, three reaction scales, cropping, resize, fullscreen, legacy settings and session round trips.");
        }
        finally { if (Fullscreen) ToggleFullscreen(); ApplySettings(saved, true); }
    }
}
