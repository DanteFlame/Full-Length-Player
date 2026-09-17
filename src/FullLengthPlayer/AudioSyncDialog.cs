namespace FullLengthPlayer;

internal sealed class AudioSyncDialog : Form
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly Label status = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 8), AutoSize = false };
    private readonly Button apply = new() { Text = "Apply and lock", AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };
    internal CheckBox MultipleSamples { get; } = new() { Text = "Multiple samples (~10-second budget)", Checked = true, AutoSize = true, Margin = new Padding(0, 12, 0, 4) };
    internal Label Instructions { get; } = new() { AutoSize = true, Dock = DockStyle.Fill, Text = "Position both videos near the same scene (within 60 seconds).\nUse the audio language the reactor watched.\nPlayback stays unchanged until Apply. Uncheck below for one sample." };
    internal double Offset { get; private set; }
    internal AudioSyncDialog(AudioInput a, AudioInput b, double aTime, double bTime, double aDuration, double bDuration)
    {
        Text = "Find audio alignment (experimental)";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 330); MinimumSize = new Size(540, 340);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var consensus = MultipleSamples;
        var start = new Button { Text = "Analyze audio", AutoSize = true };
        var close = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        buttons.Controls.AddRange(new Control[] { start, apply, close });
        layout.Controls.Add(Instructions, 0, 0); layout.Controls.Add(consensus, 0, 1);
        layout.Controls.Add(status, 0, 2); layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout); CancelButton = close;
        PlayerTheme.Dialog(this);
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
                        status.Text = result.Agreed == 1
                            ? $"Single-sample candidate: B−A {Offset:+0.00;-0.00;0.00} s.\nOther samples did not confirm it ({result.Checked} checked).\nYou can apply and listen, as with the single-sample check."
                            : $"{result.Agreed} samples agree: B−A {Offset:+0.00;-0.00;0.00} s.\n{result.Checked} samples checked. Apply and listen to confirm.";
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
