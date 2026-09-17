namespace FullLengthPlayer;

internal sealed partial class MainForm
{
    internal async Task VerifyModernUi(string report)
    {
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Modern UI: " + message); }
        if (Fullscreen) ToggleFullscreen();
        var a = Reaction.Surface.Handle; var b = Source.Surface.Handle;
        ClientSize = new Size(1000, 700); Location = new Point(0, 0);
        setupButton.Checked = true;
        for (int page = 0; page < 3; page++)
        {
            ShowSetupPage(page); await Task.Delay(200);
            Check(setupPages[page].Visible, "Selected setup page is hidden");
            Check(setupTabs.All(t => t.Visible && t.Width > 60), "Setup tabs clipped");
            Check(!setupButton.IsOnOverflow && setupButton.Bounds.Width > 30, "Setup button hidden in overflow at normal size");
            Check(sessionBar.Items.Cast<ToolStripItem>().Where(i => i.Text.StartsWith("Fullscreen")).All(i => !i.IsOnOverflow), "Fullscreen button hidden at normal size");
            Check(Composition.Width > 400 && Composition.Height > 400, "Preview lost usable space");
            if (page == 1)
                foreach (var (key, input) in layoutInputs)
                {
                    var rect = input.RectangleToScreen(input.ClientRectangle);
                    Check(input.Visible && compositionBar.RectangleToScreen(compositionBar.ClientRectangle).Contains(rect), "Layout field clipped: " + key + " " + rect);
                    for (Control? parent = input.Parent; parent != null && parent != compositionBar; parent = parent.Parent)
                        Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(rect), "Layout group clips " + key);
                }
            using var shot = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(PointToScreen(Point.Empty), Point.Empty, shot.Size);
            shot.Save(report + $".ui-{page}.png");
        }
        await VerifyPolish(report);
        int width = Composition.Width;
        setupButton.Checked = false; await Task.Delay(100);
        Check(Composition.Width > width + 200, "Setup toggle did not expand preview");
        ToggleFullscreen(); await Task.Delay(100);
        Check(Composition.Size == ClientSize && !inspector.Visible, "Fullscreen chrome remained visible");
        ToggleFullscreen(); await Task.Delay(100);
        Check(!inspector.Visible, "Fullscreen forgot hidden setup panel");
        setupButton.Checked = true; ShowSetupPage(1);
        ToggleFullscreen(); ToggleFullscreen();
        Check(inspector.Visible && compositionBar.Visible, "Fullscreen forgot selected layout tab");
        Check(Reaction.Surface.Handle == a && Source.Surface.Handle == b, "UI recreated native surfaces");
        Check(layoutInputs.Count == 8 && layoutInputs.Values.All(c => !c.IsDisposed), "Layout controls lost");
        ClientSize = new Size(640, 480); await Task.Delay(100);
        Check(setupButton.Available && sessionBar.Visible && masterBar.Visible, "Small window lost access to controls");
        ShowSetupPage(0);
    }
}
