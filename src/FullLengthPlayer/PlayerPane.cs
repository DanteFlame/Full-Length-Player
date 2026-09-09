using System.Globalization;

namespace FullLengthPlayer;

// Each pane owns its surface, controls and native instance.
internal sealed class PlayerPane : UserControl
{
    private readonly Panel video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly Label heading = new() { Dock = DockStyle.Top, Height = 27, ForeColor = Color.White, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.White, AutoEllipsis = true };
    private readonly TrackBar timeline = new() { Dock = DockStyle.Bottom, Height = 32, Maximum = 10000, TickStyle = TickStyle.None };
    private readonly ToolStripDropDownButton audio = new("Audio");
    private readonly ToolStripDropDownButton subtitles = new("Subtitles");
    private readonly TrackBar volume = new() { Minimum = 0, Maximum = 100, Value = 100, Width = 120, Height = 28, TickStyle = TickStyle.None };
    private string? playbackError;
    private bool dragging;
    private string fileName = "No video loaded";
    internal MpvPlayer? Player { get; private set; }
    internal string Role { get; }
    internal bool TrackMenuOpen => audio.DropDown.Visible || subtitles.DropDown.Visible;
    internal event Action<PlayerPane>? Activated;
    internal event Action? ManualTransport;

    public PlayerPane(string role)
    {
        Role = role;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 30);
        Padding = new Padding(3);
        var transport = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        void Button(string text, Action action)
        {
            var button = new ToolStripButton(text);
            button.Click += (_, _) => Execute(action);
            transport.Items.Add(button);
        }
        Button("Open video", Open);
        Button("Play / Pause", TogglePause);
        Button("−5 s", () => Seek(-5));
        Button("+5 s", () => Seek(5));
        var tracks = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        tracks.Items.AddRange(new ToolStripItem[] { audio, subtitles });
        var addSub = new ToolStripButton("Add subtitles");
        addSub.Click += (_, _) => Execute(AddSubtitles);
        tracks.Items.Add(addSub);
        audio.DropDownOpening += (_, _) => RefreshTrackMenu(false);
        subtitles.DropDownOpening += (_, _) => RefreshTrackMenu(true);
        var mixer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, BackColor = SystemColors.Control, WrapContents = false };
        mixer.Controls.Add(new Label { Text = "Volume", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        volume.ValueChanged += (_, _) => Execute(() => Player?.Set("volume", volume.Value.ToString(CultureInfo.InvariantCulture)));
        mixer.Controls.Add(volume);
        Controls.Add(video);
        Controls.Add(timeline);
        Controls.Add(mixer);
        Controls.Add(status);
        Controls.Add(tracks);
        Controls.Add(transport);
        Controls.Add(heading);
        timeline.MouseDown += (_, _) => { ActivatePane(); dragging = true; };
        timeline.MouseUp += (_, _) => { dragging = false; Execute(SeekTimeline); };
        timeline.KeyUp += (_, e) => { if (e.KeyCode is Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown) Execute(SeekTimeline); };
        Enter += (_, _) => ActivatePane();
        heading.Click += (_, _) => ActivatePane();
        video.Click += (_, _) => ActivatePane();
        AllowDrop = true;
        DragEnter += (_, e) => { ActivatePane(); if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) Execute(() => LoadVideo(files[0])); };
        SetActive(false);
        status.Text = "Open a local video";
    }
    internal void Initialize(bool verification) => Player = new MpvPlayer(video.Handle, verification);
    private void ActivatePane() => Activated?.Invoke(this);
    internal void SetActive(bool active)
    {
        heading.BackColor = active ? Color.FromArgb(40, 90, 140) : Color.FromArgb(55, 55, 55);
        heading.Text = $"{Role}{(active ? " • Independent shortcuts" : "")} — {fileName}";
    }
    private void Execute(Action action)
    {
        ActivatePane();
        try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, $"{Role} — Playback error"); }
    }
    internal void Open()
    {
        using var dialog = new OpenFileDialog { Title = $"{Role} — Open local video", Filter = "Video files|*.mkv;*.mp4;*.webm;*.avi;*.mov;*.m4v;*.ts|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadVideo(dialog.FileName);
    }
    internal void LoadVideo(string path)
    {
        if (Player == null) throw new InvalidOperationException("MPV is unavailable. Restart with all downloaded files together.");
        if (!File.Exists(path)) throw new FileNotFoundException("Local video not found.", path);
        ManualTransport?.Invoke();
        playbackError = null;
        fileName = Path.GetFileName(path);
        Player.Command("loadfile", Path.GetFullPath(path), "replace");
        Player.Set("pause", "no");
        ActivatePane();
    }
    internal void TogglePause() { ManualTransport?.Invoke(); Player?.Command("cycle", "pause"); }
    internal void Seek(int seconds) { ManualTransport?.Invoke(); Player?.Command("seek", seconds.ToString(CultureInfo.InvariantCulture), "relative+exact"); }
    private void SeekTimeline() { ManualTransport?.Invoke(); Player?.Command("seek", (timeline.Value / 100.0).ToString(CultureInfo.InvariantCulture), "absolute-percent+exact"); }
    private void AddSubtitles()
    {
        using var dialog = new OpenFileDialog { Title = $"{Role} — Add subtitles", Filter = "Subtitles|*.srt;*.ass;*.ssa;*.vtt|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) Player?.Command("sub-add", dialog.FileName, "select");
    }
    // Rebuild when opened so replacement media and external subtitles never leave stale IDs.
    internal ToolStripDropDownButton RefreshTrackMenu(bool sub)
    {
        var menu = sub ? subtitles : audio;
        while (menu.DropDownItems.Count > 0) menu.DropDownItems[0].Dispose();
        var property = sub ? "sid" : "aid";
        var selected = Player?.Get(property);
        void Item(string id, string label)
        {
            var item = new ToolStripMenuItem(label) { Checked = id == selected, Tag = id };
            item.Click += (_, _) => Execute(() => Player?.Set(property, id));
            menu.DropDownItems.Add(item);
        }
        Item("no", sub ? "Subtitles off" : "Audio off");
        if (Player == null) return menu;
        for (int i = 0; i < Player.Number("track-list/count"); i++)
        {
            string prefix = $"track-list/{i}/";
            if (Player.Get(prefix + "type") != (sub ? "sub" : "audio")) continue;
            string? id = Player.Get(prefix + "id");
            if (id == null) continue;
            string? title = Player.Get(prefix + "title");
            if (string.IsNullOrWhiteSpace(title)) title = sub ? "Subtitle track" : "Audio track";
            var details = new[] { Player.Get(prefix + "lang"), Player.Get(prefix + "codec"), Player.Get(prefix + "demux-channels") }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            string label = $"{id}: {title}";
            var detail = string.Join(" · ", details);
            if (detail.Length > 0) label += $" ({detail})";
            if (Player.Get(prefix + "external") == "yes") label += " [external]";
            if (Player.Get(prefix + "forced") == "yes") label += " [forced]";
            Item(id, label);
        }
        return menu;
    }
    internal void UpdatePlayback()
    {
        if (Player == null) return;
        playbackError = Player.PollError() ?? playbackError;
        double time = Player.Number("time-pos"), duration = Player.Number("duration");
        if (!dragging) timeline.Value = duration > 0 ? (int)Math.Clamp(time / duration * 10000, 0, 10000) : 0;
        status.Text = playbackError != null ? $"Could not play: {playbackError}" : duration > 0
            ? $"{(Player.Get("pause") == "yes" ? "Paused" : "Playing")}  {TimeSpan.FromSeconds(time):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(duration):hh\\:mm\\:ss}"
            : "Open a local video";
    }
    internal void Shutdown() { Player?.Dispose(); Player = null; }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Shutdown();
        base.Dispose(disposing);
    }
}
