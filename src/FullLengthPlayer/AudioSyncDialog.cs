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
        Controls.Add(new Label { Text = "First position both videos near the same scene (within 60 seconds).\nChoose shared music or dialogue, away from cuts and heavy commentary.\nPlayback stays unchanged until Apply. Uncheck below for a single sample.", Left = 16, Top = 16, Width = 515, Height = 75 });
        var consensus = new CheckBox { Text = "Multiple samples (~10-second budget)", Checked = true, Left = 16, Top = 76, Width = 470 };
        Controls.Add(consensus);
        var start = new Button { Text = "Analyze audio", Left = 16, Top = 200, Width = 125 };
        var close = new Button { Text = "Cancel", Left = 420, Top = 200, Width = 100, DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { status, start, apply, close }); CancelButton = close;
        start.Click += async (_, _) =>
        {
            start.Enabled = false; apply.Enabled = false; consensus.Enabled = false;
            try
            {
                if (consensus.Checked)
                {
                    var progress = new Progress<string>(message => { if (!IsDisposed) status.Text = message; });
                    var result = await AudioConsensus.Run(a, b, aTime, bTime, aDuration, bDuration, progress, cancellation.Token);
                    if (IsDisposed) return;
                    if (result.Match is { } consensusMatch)
                    {
                        Offset = consensusMatch.Offset; apply.Enabled = true;
                        status.Text = $"{result.Agreed} samples agree: B−A {Offset:+0.00;-0.00;0.00} s.\nApply and listen to confirm. {result.Checked} samples checked.";
                    }
                    else status.Text = result.Conflict ? "Strong samples disagree. The reaction may contain edits or pauses.\nAlignment unchanged; try a nearby section or the single-sample check."
                        : $"No consensus yet ({result.Agreed} clear matches / {result.Checked} samples).\nAlignment unchanged. Try another section or uncheck multiple samples.";
                    return;
                }
                double aStart = Math.Max(0, Math.Min(aTime, aDuration - 20));
                double expectedB = bTime + aStart - aTime;
                double bStart = Math.Max(0, expectedB - 60);
                double bLength = Math.Min(bDuration - bStart, expectedB + 80 - bStart);
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
            finally { if (!IsDisposed) { start.Enabled = true; consensus.Enabled = true; } }
        };
        FormClosing += (_, _) => cancellation.Cancel();
        // CTS remains usable by an in-flight worker after the dialog is closed.
    }
}
