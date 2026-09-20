using System.Text.Json;
namespace FullLengthPlayer;

internal sealed class PlaybackDiagnostics : Form
{
    internal static string Report(PlayerPane reaction, PlayerPane source, string? restoreStatus = null, MasterTransport? master = null) => JsonSerializer.Serialize(new {
        applicationVersion = typeof(PlaybackDiagnostics).Assembly.GetName().Version?.ToString(),
        buildVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(PlaybackDiagnostics).Assembly)?.InformationalVersion,
        sessionRestoreStage = restoreStatus,
        sync = master == null ? null : new { locked = master.Locked, offsetSeconds = master.Offset, driftSeconds = master.Drift, correctionCount = master.CorrectionCount, seekingTogether = master.SeekingTogether },
        reaction = reaction.DiagnosticSnapshot(), source = source.DiagnosticSnapshot()
    }, new JsonSerializerOptions { WriteIndented = true });

    internal PlaybackDiagnostics(PlayerPane reaction, PlayerPane source, Func<string>? restoreStatus = null, MasterTransport? master = null)
    {
        Text = "Playback diagnostics"; ClientSize = new Size(680, 520); MinimumSize = new Size(540, 400);
        StartPosition = FormStartPosition.CenterParent;
        var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, AccessibleName = "Playback diagnostics report" };
        var note = new Label { Dock = DockStyle.Top, Height = 74, Padding = new Padding(12), Text = "Snapshot only; nothing is uploaded. No filenames, URLs or HTTP headers.\nBuffer estimates may be unavailable/inaccurate; input rate covers the main stream, not separate YouTube audio. File-loaded time is not first-frame latency." };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6), FlowDirection = FlowDirection.RightToLeft };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
        var copy = new Button { Text = "Copy report", AutoSize = true };
        var refresh = new Button { Text = "Refresh" };
        refresh.Click += (_, _) => text.Text = Report(reaction, source, restoreStatus?.Invoke(), master);
        copy.Click += (_, _) => { try { Clipboard.SetText(text.Text); } catch { MessageBox.Show(this, "Clipboard unavailable. Select and copy the text manually.", "Copy report"); } };
        actions.Controls.AddRange(new Control[] { close, copy, refresh });
        Controls.Add(text); Controls.Add(note); Controls.Add(actions); CancelButton = close;
        PlayerTheme.Dialog(this); text.Text = Report(reaction, source, restoreStatus?.Invoke(), master);
    }
}
