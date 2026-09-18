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
            foreach (double aspect in new[] { 4.0 / 3, 16.0 / 9, 1.6 })
            foreach (SourceAnchor position in Enum.GetValues<SourceAnchor>())
            foreach (double fraction in new[] { .25, .7, 1.0 })
            {
                picker.Choose(position);
                Composition.CanvasAspect = aspect; Composition.SourceFraction = fraction;
                Composition.CropTop = .2; Composition.CropBottom = .1; Composition.PanX = 0;
                Composition.Arrange();
                var c = Composition.CanvasBounds; var r = Composition.SourceBounds;
                Check(Composition.AnchorPosition == position, "Picker did not select " + position);
                Check(r.X == Composition.AnchorColumn * (c.Width - r.Width) / 2 && r.Y == Composition.AnchorRow * (c.Height - r.Height) / 2, "Incorrect placement: " + position);
                Check(new Rectangle(0, 0, c.Width, c.Height).Contains(r), "Source escaped canvas");
                Check(Composition.ReactionBounds.X == (c.Width - Composition.ReactionBounds.Width) / 2, "Reaction shifted horizontally");
                Check(Composition.ReactionBottom ? Composition.MaskBounds.Bottom == c.Height : Composition.MaskBounds.Top == 0, "Reaction lost vertical anchor");
                if (Composition.AnchorRow != 1) Check(Composition.ReactionBottom == (Composition.AnchorRow == 0), "Reaction did not oppose source");
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
                    Check(Composition.ReactionBottom == bottom, "Side midpoint changed reaction edge");
                }
            }
            // Original settings JSON had no ReactionBottom property.
            var old = JsonSerializer.Serialize(saved with { Anchor = 1 }).Replace(",\"ReactionBottom\":" + (saved.ReactionBottom ? "true" : "false"), "");
            ApplySettings(JsonSerializer.Deserialize<SavedSettings>(old)!, true);
            Check(Composition.TopAnchor && Composition.ReactionBottom, "Legacy top session changed layout");
            picker.Choose(SourceAnchor.BottomLeft);
            Check(Composition.ResizeChange(1, new Point(10, -10), 16.0 / 9) > 0, "Free corner cannot enlarge bottom-left source");
            Check(Composition.ResizeChange(2, new Point(10, 10), 16.0 / 9) == 0, "Pinned corner was not fixed");
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
            Console.WriteLine("PASS: all eight anchors, three canvas ratios, resize geometry, fullscreen, legacy settings and session round trips.");
        }
        finally { if (Fullscreen) ToggleFullscreen(); ApplySettings(saved, true); }
    }
}
