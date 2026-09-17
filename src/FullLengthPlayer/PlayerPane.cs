using System.Globalization;

namespace FullLengthPlayer;

// Each pane owns its surface, controls and native instance.
internal sealed class PlayerPane : UserControl
{
    private readonly Panel video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly Label heading = new() { Dock = DockStyle.Top, Height = 27, ForeColor = Color.White, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.White, AutoEllipsis = true };
    private readonly MediaSlider timeline = new() { Dock = DockStyle.Bottom, Height = 32, Maximum = 10000 };
    private readonly ToolStripDropDownButton audio = new("Audio");
    private readonly ToolStripDropDownButton subtitles = new("Subtitles");
    private readonly MediaSlider volume = new() { Minimum = 0, Maximum = 100, Value = 100, WheelAdjust = true, Width = 120, Height = 28 };
    private readonly Label volumeCaption = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Text = "Volume 100%" };
    private readonly ToolTip fileTip = new();
    private string? playbackError;
    private bool dragging;
    private bool updatingVolume;
    internal event Action? VolumeEdited;
    internal double Volume => Player?.Number("volume") ?? volume.Value;
    internal void SetVolume(double value)
    {
        Player?.Set("volume", Math.Clamp(value, 0, 100).ToString(CultureInfo.InvariantCulture));
        updatingVolume = true;
        try { volume.Value = (int)Math.Round(Math.Clamp(value, 0, 100)); } finally { updatingVolume = false; }
    }
    internal void AdjustVolume(int delta) { VolumeEdited?.Invoke(); SetVolume(Volume + delta); }
    private bool network;
    private AudioInput? analysisInput;
    private string? originalYouTube;
    internal bool StartPaused { get; set; } = true;
    internal SavedMedia CaptureMedia()
    {
        var input = analysisInput ?? throw new InvalidOperationException("Load media first.");
        string kind = originalYouTube != null ? "youtube" : network ? "network" : "local";
        // External subtitle IDs do not survive loading; leave them off for this milestone.
        string sid = Player?.Get("sid") ?? "no";
        for (int i = 0; i < (Player?.Number("track-list/count") ?? 0); i++)
            if (Player?.Get($"track-list/{i}/id") == sid && Player.Get($"track-list/{i}/type") == "sub" && Player.Get($"track-list/{i}/external") == "yes") sid = "no";
        return new(kind, originalYouTube ?? input.Path, input.Referer, input.Headers.ToArray(), Player?.Number("time-pos") ?? 0, Player?.Get("aid") ?? "auto", sid);
    }
    internal AudioInput CaptureAudio() => analysisInput is { } input && Player?.Get("aid") is { } aid && aid != "no"
        ? input with { Track = aid } : throw new InvalidOperationException("Select an audio track in both players first.");
    private CancellationTokenSource? resolving;
    private readonly ToolStripButton cancelResolve = new("Cancel YouTube") { Visible = false };
    internal string? PlaybackError => playbackError;
    private string fileName = "No video loaded";
    internal MpvPlayer? Player { get; private set; }
    internal string Role { get; }
    internal bool TrackMenuOpen => audio.DropDown.Visible || subtitles.DropDown.Visible;
    internal event Action<PlayerPane>? Activated;
    internal event Action? ManualTransport;
    internal event Action? MediaReplaced;
    internal Panel Surface => video;

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
        Button("Open URL", OpenUrl);
        cancelResolve.Click += (_, _) => CancelResolution();
        transport.Items.Add(cancelResolve);
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
        mixer.Controls.Add(volumeCaption);
        volume.ValueChanged += (_, _) => { if (!updatingVolume) Execute(() => { int desired = volume.Value; VolumeEdited?.Invoke(); SetVolume(desired); }); };
        mixer.Controls.Add(volume);
        Controls.Add(video);
        Controls.Add(timeline);
        Controls.Add(mixer);
        Controls.Add(status);
        Controls.Add(tracks);
        Controls.Add(transport);
        Controls.Add(heading);
        timeline.MouseDown += (_, _) => { ActivatePane(); dragging = true; };
        timeline.MouseCaptureChanged += (_, _) => { if (!timeline.Capture) dragging = false; };
        timeline.MouseUp += (_, _) => { dragging = false; Execute(SeekTimeline); };
        timeline.KeyUp += (_, e) => { if (e.KeyCode is Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Left or Keys.Right) Execute(SeekTimeline); };
        Enter += (_, _) => ActivatePane();
        heading.Click += (_, _) => ActivatePane();
        video.Click += (_, _) => ActivatePane();
        AllowDrop = true;
        DragEnter += (_, e) => { ActivatePane(); if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) Execute(() => LoadVideo(files[0])); };
        SetActive(false);
        status.Text = "Open a local video";
    }
    internal void ApplyModernStyle()
    {
        PlayerTheme.Apply(this);
        heading.Font = new Font("Segoe UI Semibold", 9f);
        heading.Height = 48;
        var accent = Role.StartsWith("Reaction") ? PlayerTheme.Accent : PlayerTheme.Orange;
        heading.ForeColor = accent; timeline.Accent = volume.Accent = accent;
        volume.AccessibleName = Role + " volume"; timeline.AccessibleName = Role + " timeline";
        volume.HoverText = f => $"{Role} · {Math.Round(f * 100):0}%";
        timeline.HoverText = f => TimeSpan.FromSeconds(f * (Player?.Number("duration") ?? 0)).ToString(@"hh\:mm\:ss");
        status.ForeColor = PlayerTheme.Muted;
        timeline.AutoSize = false; timeline.Height = 28;
        volume.AutoSize = false; volume.Height = 26;
        // Split opening from transport so every action fits the narrow inspector.
        var strip = Controls.OfType<ToolStrip>().First(s => s.Items.Cast<ToolStripItem>().Any(i => i.Text == "Open video"));
        var opening = new ToolStrip { Dock = DockStyle.Top };
        foreach (var item in strip.Items.Cast<ToolStripItem>().Take(3).ToArray()) opening.Items.Add(item);
        Controls.Add(opening);
        Controls.SetChildIndex(heading, Controls.Count - 1);
        Controls.SetChildIndex(opening, Controls.Count - 2);
        PlayerTheme.Apply(opening);
    }
    internal void Initialize(bool verification) { Player = new MpvPlayer(video.Handle, verification); StartPaused = !verification; }
    internal void ClearMedia()
    {
        PrepareLoad(false, "No video loaded"); Player!.Set("pause", "yes");
        Player.Set("aid", "auto"); Player.Set("sid", "auto");
        dragging = false; timeline.Value = 0; RefreshTrackMenu(false); RefreshTrackMenu(true);
        ActivatePane(); UpdatePlayback();
    }
    private void ActivatePane() => Activated?.Invoke(this);
    internal void SetActive(bool active)
    {
        heading.BackColor = active ? PlayerTheme.Raised : PlayerTheme.Surface;
        fileTip.SetToolTip(heading, fileName);
        heading.Text = $"{Role}{(active ? " • selected" : "")}\n{fileName}";
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
        PrepareLoad(false, "No video loaded");
        fileName = Path.GetFileName(path);
        analysisInput = new(Path.GetFullPath(path), "", Array.Empty<string>(), null, "auto");
        Player.Set("pause", StartPaused ? "yes" : "no");
        Player.Command("loadfile", Path.GetFullPath(path), "replace");
        ActivatePane();
    }
    internal async void OpenUrl()
    {
        using var dialog = new NetworkSourceDialog(Role == "Reaction A");
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Source == null) return;
        try
        {
            if (YouTubeResolver.IsYouTube(dialog.Source.Url)) await LoadYouTube(dialog.Source.Url);
            else LoadNetwork(dialog.Source);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Open URL"); }
    }
    private void PrepareLoad(bool remote, string title)
    {
        if (Player == null) throw new InvalidOperationException("MPV is unavailable.");
        CancelResolution();
        analysisInput = null; originalYouTube = null;
        ManualTransport?.Invoke(); MediaReplaced?.Invoke();
        Player.Command("stop");
        Player.PollError(); // Discard failures from the previous load.
        Player.SetStringList("audio-files", Array.Empty<string>());
        Player.Set("referrer", "");
        Player.SetStringList("http-header-fields", Array.Empty<string>());
        network = remote; playbackError = null; fileName = title;
    }
    internal void CancelResolution() { resolving?.Cancel(); resolving = null; cancelResolve.Visible = false; }
    internal async Task LoadYouTube(string url, Func<string, CancellationToken, Task<ResolvedVideo>>? resolver = null)
    {
        string canonical = YouTubeResolver.CanonicalUrl(url);
        CancelResolution();
        using var request = new CancellationTokenSource(); resolving = request; cancelResolve.Visible = true;
        try
        {
            var result = await (resolver ?? YouTubeResolver.Resolve)(canonical, request.Token);
            request.Token.ThrowIfCancellationRequested();
            LoadResolved(result); originalYouTube = canonical;
        }
        finally { if (resolving == request) { resolving = null; cancelResolve.Visible = false; } }
    }
    internal void LoadResolved(ResolvedVideo result) => LoadNetwork(result.Video, result.AudioUrl, "YouTube video");
    internal void LoadNetwork(NetworkSource source, string? audioUrl = null, string title = "Network stream")
    {
        PrepareLoad(true, title);
        try
        {
            analysisInput = new(source.Url, source.Referer, source.Headers.ToArray(), audioUrl, "auto");
            Player!.Set("referrer", source.Referer);
            Player.SetStringList("http-header-fields", source.Headers);
            if (audioUrl != null) Player.SetStringList("audio-files", new[] { audioUrl });
            Player.Set("pause", StartPaused ? "yes" : "no");
            Player.Command("loadfile", source.Url, "replace");
        }
        catch { throw new InvalidOperationException("Could not open stream. Check the URL and HTTP settings."); }
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
        volumeCaption.Text = $"Volume {Volume:0}%";
        foreach (var strip in Controls.OfType<ToolStrip>())
            foreach (ToolStripItem item in strip.Items)
            {
                if (item.Text is "Play / Pause" or "▶ Play" or "Ⅱ Pause") { item.Text = Player.Get("pause") == "yes" ? "▶ Play" : "Ⅱ Pause"; item.ToolTipText = "Play / pause this player · Shift+K"; }
                if (item.Text == "−5 s") item.ToolTipText = "Back five seconds · Shift+J";
                if (item.Text == "+5 s") item.ToolTipText = "Forward five seconds · Shift+L";
            }
        var error = Player.PollError();
        if (error != null) playbackError = fileName == "YouTube video"
            ? "YouTube stream unavailable — open the original video link again to refresh it."
            : network ? "Stream unavailable. Check the direct URL, expiry/access and HTTP headers; open a fresh URL to retry."
            : error;
        double time = Player.Number("time-pos"), duration = Player.Number("duration");
        if (!dragging) timeline.Value = duration > 0 ? (int)Math.Clamp(time / duration * 10000, 0, 10000) : 0;
        status.Text = playbackError != null ? $"Could not play: {playbackError}" : duration > 0
            ? $"{(Player.Get("pause") == "yes" ? "Paused" : "Playing")}  {TimeSpan.FromSeconds(time):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(duration):hh\\:mm\\:ss}  • {Player.Number("speed"):0.##}×"
            : network ? (playbackError != null ? playbackError : Player.Get("idle-active") == "yes" ? "Stream ended or unavailable — open a fresh URL to retry" : "Opening stream…") : "Open a local video";
        if (network && playbackError == null && Player.Get("paused-for-cache") == "yes") status.Text = "Buffering stream…";
        if (resolving != null) status.Text = "Resolving YouTube… (current playback continues; Cancel YouTube to stop)";
    }
    internal void Shutdown() { CancelResolution(); Player?.Dispose(); Player = null; }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Shutdown();
        if (disposing) fileTip.Dispose();
        base.Dispose(disposing);
    }
}
