using System.Globalization;

namespace FullLengthPlayer;
internal sealed partial class MainForm
{
    private readonly Dictionary<string, Control> layoutInputs = new();
    private readonly ToolStrip sessionBar = new() { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
    private bool verificationMode, restoringSession, failedRestore;
    private void BuildSessionControls()
    {
        var fresh = new ToolStripButton("New session");
        var appearance = new ToolStripButton("Appearance…");
        fresh.Click += (_, _) => { try { NewSession(); } catch { MessageBox.Show(this, "Could not preserve the current session. Save it to a writable location before starting a new session.", "New session"); } };
        appearance.Click += (_, _) => ChooseIcon();
        var save = new ToolStripButton("Save session…");
        var open = new ToolStripButton("Open session…");
        var resume = new ToolStripButton("Resume last session");
        sessionBar.Items.AddRange(new ToolStripItem[] { fresh, save, open, resume, appearance });
        Controls.Add(sessionBar);
        Reaction.MediaReplaced += () => { if (!restoringSession) failedRestore = false; };
        Source.MediaReplaced += () => { if (!restoringSession) failedRestore = false; };
        save.Click += (_, _) =>
        {
            try
            {
                var session = CaptureSession();
                using var dialog = new SaveFileDialog { Filter = "Full-Length Player session|*.flpsession", DefaultExt = "flpsession", AddExtension = true, FileName = "Viewing session.flpsession" };
                if (dialog.ShowDialog(this) == DialogResult.OK) SessionStore.Save(dialog.FileName, session);
            }
            catch { MessageBox.Show(this, "Load both videos and let any seek finish before saving. Check that the destination is writable.", "Save session"); }
        };
        open.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Full-Length Player session|*.flpsession" };
            if (dialog.ShowDialog(this) == DialogResult.OK) await OpenSessionFile(dialog.FileName);
        };
        resume.Click += async (_, _) => await OpenSessionFile(SessionStore.LastPath);
    }
    private async Task OpenSessionFile(string path)
    {
        try
        {
            if (!File.Exists(path)) { MessageBox.Show(this, "No saved session was found. Load a pair of videos first; the last session is saved when you close the app.", "Open session"); return; }
            await RestoreSession(SessionStore.Read(path));
        }
        catch
        {
            if (!IsDisposed) MessageBox.Show(this, "Could not restore this session. It must have been saved by this Windows account on this PC. Check that both local files still exist. For an expired or unavailable stream, open a fresh URL and save the session again.", "Open session");
        }
    }
    internal SavedSettings CaptureSettings() => new(new()
    {
        ["Source %"] = (decimal)(Composition.SourceFraction * 100),
        ["Crop top %"] = (decimal)(Composition.CropTop * 100),
        ["Crop bottom %"] = (decimal)(Composition.CropBottom * 100),
        ["Reaction zoom %"] = (decimal)(Composition.Zoom * 100),
        ["Pan X %"] = (decimal)(Composition.PanX * 100),
        ["Pan Y %"] = (decimal)(Composition.PanY * 100)
    }, Composition.TopAnchor ? 1 : 0, canvasChoice!.SelectedIndex,
        Reaction.Volume, Source.Volume, Reaction.Player!.Get("mute") ?? "no", Source.Player!.Get("mute") ?? "no", Reaction.Player!.Number("speed"));
    internal void ApplySettings(SavedSettings saved, bool restoreCanvas)
    {
        SessionStore.Validate(saved);
        foreach (var (key, value) in saved.Numbers)
            if (layoutInputs.TryGetValue(key, out var control) && control is NumericUpDown number)
                number.Value = Math.Clamp(value, number.Minimum, number.Maximum);
        ((ComboBox)layoutInputs["Source edge"]).SelectedIndex = saved.Anchor;
        if (restoreCanvas) canvasChoice!.SelectedIndex = saved.Canvas;
        Reaction.SetVolume(saved.VolumeA); Source.SetVolume(saved.VolumeB);
        Reaction.Player!.Set("mute", saved.MuteA); Source.Player!.Set("mute", saved.MuteB);
        string speed = saved.Speed.ToString(CultureInfo.InvariantCulture);
        Reaction.Player.Set("speed", speed); Source.Player.Set("speed", speed); SharedSpeed.Reset();
        Composition.Arrange();
    }
    internal SavedSession CaptureSession()
    {
        Replay.Cancel();
        if (restoringSession || Master.SeekingTogether || Master.Snapshot() == null) throw new InvalidOperationException("Load both videos and wait for seeking to finish.");
        return new(1, Reaction.CaptureMedia(), Source.CaptureMedia(), CaptureSettings(), Master.Locked, Master.Offset, Master.TimelineTime);
    }
    private void SaveOnExit()
    {
        if (verificationMode || restoringSession || Reaction.Player == null || Source.Player == null) return;
        try
        {
            Replay.Cancel(); SessionStore.SaveSettings(CaptureSettings());
            if (!failedRestore && !Master.SeekingTogether && Master.Snapshot() != null)
                SessionStore.Save(SessionStore.LastPath, CaptureSession());
        }
        catch { MessageBox.Show("The viewing settings or last session could not be saved. Check that your Windows profile is writable.", "Full-Length Player"); }
    }
    internal async Task RestoreSession(SavedSession session)
    {
        if (restoringSession) throw new InvalidOperationException("A session is already loading.");
        SessionStore.Validate(session.A); SessionStore.Validate(session.B); SessionStore.Validate(session.Settings);
        foreach (var media in new[] { session.A, session.B })
            if (media.Kind == "local" && !File.Exists(media.Location)) throw new FileNotFoundException("A saved media file is missing.");
        restoringSession = true; Enabled = false; UseWaitCursor = true;
        bool oldStartA = Reaction.StartPaused, oldStartB = Source.StartPaused;
        Replay.Cancel(); Master.Unlock(); Reaction.StartPaused = Source.StartPaused = true;
        var a = Reaction.Player!; var b = Source.Player!;
        a.Set("pause", "yes"); b.Set("pause", "yes");
        async Task Until(Func<bool> ready)
        {
            long end = Environment.TickCount64 + 45000;
            while (true)
            {
                if (IsDisposed || Disposing) throw new OperationCanceledException();
                Reaction.UpdatePlayback(); Source.UpdatePlayback();
                if (Reaction.PlaybackError != null || Source.PlaybackError != null) throw new InvalidOperationException("A saved stream could not be loaded.");
                if (ready()) return;
                if (Environment.TickCount64 > end) throw new TimeoutException("Session loading timed out.");
                await Task.Delay(50);
            }
        }
        async Task Load(PlayerPane pane, SavedMedia media)
        {
            if (media.Kind == "youtube") await pane.LoadYouTube(media.Location);
            else if (media.Kind == "local") pane.LoadVideo(media.Location);
            else pane.LoadNetwork(NetworkSource.Parse(media.Location, media.Referer, string.Join("\n", media.Headers ?? Array.Empty<string>())));
        }
        static void Track(MpvPlayer player, string property, string type, string value)
        {
            bool available = value is "auto" or "no";
            for (int i = 0; i < player.Number("track-list/count"); i++)
                available |= player.Get($"track-list/{i}/type") == type && player.Get($"track-list/{i}/id") == value;
            if (available) player.Set(property, value);
        }
        try
        {
            // Drain earlier file-loaded events before recording a generation for this replacement.
            Reaction.UpdatePlayback(); Source.UpdatePlayback();
            int generationA = a.FilesLoaded, generationB = b.FilesLoaded;
            await Load(Reaction, session.A); await Load(Source, session.B);
            await Until(() => a.FilesLoaded > generationA && b.FilesLoaded > generationB && Master.Snapshot() != null && a.Get("seeking") != "yes" && b.Get("seeking") != "yes");
            a.Set("pause", "yes"); b.Set("pause", "yes");
            ApplySettings(session.Settings, true);
            Track(a, "aid", "audio", session.A.Audio); Track(b, "aid", "audio", session.B.Audio);
            Track(a, "sid", "sub", session.A.Subtitles); Track(b, "sid", "sub", session.B.Subtitles);
            if (session.Locked)
            {
                Master.SetOffset(session.Offset); Master.SeekReaction(session.Clock);
                await Until(() => !Master.SeekingTogether);
                if (!Master.Locked || Math.Abs(Master.TimelineTime - Math.Clamp(session.Clock, Master.TimelineStart, Master.TimelineEnd)) > .2)
                    throw new InvalidOperationException("Saved alignment could not be restored.");
            }
            else
            {
                a.Command("seek", Math.Clamp(session.A.Position, 0, a.Number("duration")).ToString(CultureInfo.InvariantCulture), "absolute+exact");
                b.Command("seek", Math.Clamp(session.B.Position, 0, b.Number("duration")).ToString(CultureInfo.InvariantCulture), "absolute+exact");
                await Task.Delay(100);
                await Until(() => a.Get("seeking") != "yes" && b.Get("seeking") != "yes");
            }
            a.Set("pause", "yes"); b.Set("pause", "yes"); failedRestore = false;
        }
        catch
        {
            failedRestore = true;
            if (!IsDisposed) { Master.Unlock(); a.Set("pause", "yes"); b.Set("pause", "yes"); }
            throw;
        }
        finally
        {
            Reaction.StartPaused = oldStartA; Source.StartPaused = oldStartB; restoringSession = false;
            if (!IsDisposed) { Enabled = true; UseWaitCursor = false; }
        }
    }
}
