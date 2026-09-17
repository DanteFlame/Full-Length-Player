using System.Reflection;
namespace FullLengthPlayer;
internal static class AppIcons
{
    internal const string DefaultId = "teal-orange-solid";
    internal static readonly string[] Ids = { "slate-red-solid", "teal-gold-solid", "teal-orange-solid", "slate-red-glass", "teal-gold-glass", "teal-orange-glass" };
    internal static readonly string[] Labels = { "Slate / Red · Solid", "Teal / Gold · Solid", "Teal / Orange · Solid", "Slate / Red · Glass", "Teal / Gold · Glass", "Teal / Orange · Glass" };
    internal static string PreferencePath => Path.Combine(SessionStore.DirectoryPath, "appearance.txt");
    internal static string Read(string? path = null)
    {
        try { string value = File.ReadAllText(path ?? PreferencePath).Trim(); return Ids.Contains(value) ? value : DefaultId; }
        catch { return DefaultId; }
    }
    internal static void Save(string id, string? path = null)
    {
        if (!Ids.Contains(id)) throw new ArgumentException("Unknown icon.");
        SessionStore.AtomicWrite(path ?? PreferencePath, System.Text.Encoding.UTF8.GetBytes(id));
    }
    internal static Icon Load(string id, int size = 32)
    {
        if (!Ids.Contains(id)) throw new ArgumentException("Unknown icon.");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FullLengthPlayer.Icons." + id + ".ico") ?? throw new InvalidOperationException("Bundled icon missing.");
        // ICO stores 256 as zero in its directory. Select the frame explicitly so
        // System.Drawing cannot mistake that entry for a zero-pixel candidate.
        using var memory = new MemoryStream(); stream.CopyTo(memory); byte[] bytes = memory.ToArray();
        int count = BitConverter.ToUInt16(bytes,4);
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + 16 * i;
            int width = bytes[entry] == 0 ? 256 : bytes[entry];
            int height = bytes[entry+1] == 0 ? 256 : bytes[entry+1];
            if (width != size || height != size) continue;
            int length = checked((int)BitConverter.ToUInt32(bytes,entry+8));
            int offset = checked((int)BitConverter.ToUInt32(bytes,entry+12));
            using var selected = new MemoryStream();
            using (var writer = new BinaryWriter(selected, System.Text.Encoding.UTF8, leaveOpen:true))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
                writer.Write(bytes,entry,12); writer.Write(22); writer.Write(bytes,offset,length);
            }
            selected.Position = 0;
            using var icon = new Icon(selected, new Size(size,size));
            return (Icon)icon.Clone();
        }
        throw new InvalidOperationException("Bundled icon size missing.");
    }
}
internal sealed partial class MainForm
{
    private Icon? chosenIcon;
    internal string IconId { get; private set; } = AppIcons.DefaultId;
    internal void ApplyIcon(string id, bool save = true)
    {
        var next = AppIcons.Load(id);
        try { if (save) AppIcons.Save(id); } catch { next.Dispose(); throw; }
        Icon = next; var old = chosenIcon; chosenIcon = next; IconId = id; old?.Dispose();
    }
    private void ChooseIcon()
    {
        using var dialog = new Form { Text = "Appearance — App icon", StartPosition = FormStartPosition.CenterParent, AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(660,480), MinimumSize = new Size(450,400) };
        var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        var note = new Label { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(12,5,12,5), Text = "Choose the window and running taskbar icon. Your choice is remembered. The EXE and existing shortcuts keep the default Teal / Orange Solid icon." };
        var close = new Button { Dock = DockStyle.Bottom, Height = 32, Text = "Done", DialogResult = DialogResult.OK };
        var pictures = new List<Bitmap>();
        for (int i = 0; i < AppIcons.Ids.Length; i++)
        {
            string id = AppIcons.Ids[i];
            using var icon = AppIcons.Load(id,128); var bitmap = icon.ToBitmap(); pictures.Add(bitmap);
            var button = new Button { Width = 195, Height = 175, Image = bitmap, Text = AppIcons.Labels[i], TextImageRelation = TextImageRelation.ImageAboveText, Tag = id, FlatStyle = FlatStyle.Flat, BackColor = id == IconId ? PlayerTheme.Raised : PlayerTheme.Surface };
            button.Click += (_, _) =>
            {
                try { ApplyIcon(id); foreach (Button other in grid.Controls) { other.BackColor = Equals(other.Tag, id) ? PlayerTheme.Raised : PlayerTheme.Surface; other.FlatAppearance.BorderColor = Equals(other.Tag, id) ? PlayerTheme.Accent : PlayerTheme.Raised; } }
                catch { MessageBox.Show(dialog, "The icon preference could not be saved.", "Appearance"); }
            };
            grid.Controls.Add(button);
        }
        dialog.Controls.Add(grid); dialog.Controls.Add(note); dialog.Controls.Add(close); dialog.AcceptButton = close; dialog.CancelButton = close;
        PlayerTheme.Dialog(dialog);
        foreach (Button button in grid.Controls) { button.BackColor = Equals(button.Tag, IconId) ? PlayerTheme.Raised : PlayerTheme.Surface; button.FlatAppearance.BorderSize = 1; button.FlatAppearance.BorderColor = Equals(button.Tag, IconId) ? PlayerTheme.Accent : PlayerTheme.Raised; }
        try { dialog.ShowDialog(this); } finally { foreach (var picture in pictures) picture.Dispose(); }
    }
    internal void NewSession()
    {
        if (restoringSession) return;
        Replay.Cancel();
        // Keep the old complete pairing available through Resume after clearing the canvas.
        if (!verificationMode && !failedRestore && Master.Snapshot() != null && !Master.SeekingTogether)
            SessionStore.Save(SessionStore.LastPath, CaptureSession());
        Reaction.ClearMedia(); Source.ClearMedia(); Master.Reset(); SharedSpeed.Reset();
        Reaction.StartPaused = Source.StartPaused = true;
        ApplySettings(new(new() { ["Source %"] = 70, ["Crop top %"] = 0, ["Crop bottom %"] = 0, ["Reaction zoom %"] = 100, ["Pan X %"] = 0, ["Pan Y %"] = 0 },
            0, ClosestCanvas(Screen.FromControl(this).Bounds.Size), 100, 100, "no", "no", 1), true);
        masterDragging = false; masterTimeline.Value = 0; wheelRemainder = 0; wheelPane = null; SelectPane(Reaction); UpdateMaster();
        if (Fullscreen) ToggleFullscreen();
    }
}
