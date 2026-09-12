namespace FullLengthPlayer;

internal sealed class AudioSyncDialog : Form
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly Label status = new() { Left = 16, Top = 100, Width = 510, Height = 90 };
    private readonly Button apply = new() { Text = "Apply and lock", Left = 290, Top = 200, Width = 120, Enabled = false, DialogResult = DialogResult.OK };
    internal double Offset { get; private set; }
    internal AudioSyncDialog(AudioInput a, AudioInput b, double aTime, double bTime, double aDuration, double bDuration)
    {
        Text = "Find audio alignment (experimental)";
        ClientSize = new Size(550, 245); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        Controls.Add(new Label { Text = "First position both videos near the same scene (within 30 seconds).\nChoose shared music or dialogue, away from cuts and heavy commentary.\nCompares 20 seconds of A with nearby B audio; playback stays unchanged until Apply.", Left = 16, Top = 16, Width = 515, Height = 75 });
        var start = new Button { Text = "Analyze audio", Left = 16, Top = 200, Width = 125 };
        var close = new Button { Text = "Cancel", Left = 420, Top = 200, Width = 100, DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { status, start, apply, close }); CancelButton = close;
        start.Click += async (_, _) =>
        {
            start.Enabled = false; apply.Enabled = false;
            try
            {
                double aStart = Math.Max(0, Math.Min(aTime, aDuration - 20));
                double expectedB = bTime + aStart - aTime;
                double bStart = Math.Max(0, expectedB - 30);
                double bLength = Math.Min(bDuration - bStart, expectedB + 50 - bStart);
                if (aDuration < 20 || bLength < 20) throw new InvalidOperationException("Choose a shared scene with more audio remaining in both videos.");
                status.Text = "Reading reaction audio…";
                var samplesA = await AudioAlignment.Decode(a, aStart, 20, cancellation.Token);
                if (IsDisposed) return;
                status.Text = "Reading source audio…";
                var samplesB = await AudioAlignment.Decode(b, bStart, bLength, cancellation.Token);
                if (IsDisposed) return;
                status.Text = "Comparing audio patterns…";
                var match = await Task.Run(() => AudioAlignment.Match(samplesA, samplesB, aStart, bStart, cancellation.Token), cancellation.Token);
                if (IsDisposed) return;
                Offset = match.Offset;
                status.Text = match.Reliable
                    ? $"Suggested offset B−A: {Offset:+0.00;-0.00;0.00} s\nMatch score: {match.Score:0.00} (not a probability). Apply, then listen to check.\nIf it sounds wrong, undo by entering your previous offset."
                    : "No clear, unique match. Your alignment has not changed.\nTry another scene with clearer shared audio, or use the manual offset controls.";
                apply.Enabled = match.Reliable;
            }
            catch (OperationCanceledException) { }
            catch (Exception) when (IsDisposed) { }
            catch (Exception e) { status.Text = e is InvalidOperationException ? e.Message : "Audio analysis failed. Reopen the media or try local files."; }
            finally { if (!IsDisposed) start.Enabled = true; }
        };
        FormClosing += (_, _) => cancellation.Cancel();
        // CTS remains usable by an in-flight worker after the dialog is closed.
    }
}
