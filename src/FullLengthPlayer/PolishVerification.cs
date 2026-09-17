using System.Runtime.InteropServices;
namespace FullLengthPlayer;
internal sealed partial class MainForm
{
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    internal async Task VerifyPolish()
    {
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("UI polish: " + message); }
        async Task Click(Control control, double fraction)
        {
            Cursor.Position = control.PointToScreen(new Point(10 + (int)((control.Width - 20) * fraction), control.Height / 2));
            mouse_event(2, 0, 0, 0, UIntPtr.Zero); await Task.Delay(40);
            mouse_event(4, 0, 0, 0, UIntPtr.Zero); await Task.Delay(100);
        }
        ClientSize = new Size(1000, 700); ShowSetupPage(0); Activate();
        await Click(masterTimeline, .45);
        long deadline = Environment.TickCount64 + 10000;
        while (Master.SeekingTogether && Environment.TickCount64 < deadline) await Task.Delay(50);
        Check(!Master.SeekingTogether && Math.Abs(Master.TimelineTime - (Master.TimelineStart + .45 * (Master.TimelineEnd - Master.TimelineStart))) < .25, "Custom timeline mouse seeking failed");
        Check(Master.Locked && masterTimeline.SharedRange is { } overlap && overlap.Start >= 0 && overlap.End <= 1 && overlap.Start < overlap.End, "Overlap band missing");
        Check(SharedTimelineHint(.5).Contains("Both videos"), "Hover hint lost overlap meaning");
        var volume = Reaction.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<MediaSlider>()).Single();
        double sourceVolume = Source.Volume;
        await Click(volume, .35);
        Check(Math.Abs(Reaction.Volume - 35) <= 1 && Source.Volume == sourceVolume, "Volume slider did not isolate players");
        ((NumericUpDown)layoutInputs["Crop top %"]).Value = 20;
        ((NumericUpDown)layoutInputs["Pan X %"]).Value = 30;
        double oldOffset = Master.Offset;
        ResetLayoutGroup(new[] { "Crop top %", "Crop bottom %" });
        Check(Composition.CropTop == 0 && Composition.PanX == .3 && Master.Offset == oldOffset, "Crop reset changed unrelated settings");
        ResetLayoutGroup(new[] { "Pan X %", "Pan Y %", "Reaction zoom %" });
        Check(Composition.PanX == 0 && Composition.Zoom == 1, "Framing reset failed");
        using var url = new NetworkSourceDialog(true);
        url.Show(this); await Task.Delay(100);
        Check(url.BackColor == PlayerTheme.Surface && url.Controls.OfType<TextBox>().All(t => t.ForeColor == PlayerTheme.Ink), "URL dialog theme missing");
        url.Close();
        ActiveControl = null;
    }
}
