using System.Diagnostics;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace FullLengthPlayer;

public sealed class MainForm : Form
{
    private static readonly string[] VideoExtensions =
    [
        ".mkv", ".mp4", ".m4v", ".webm", ".avi", ".mov", ".wmv", ".ts", ".m2ts"
    ];

    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _reactionPlayer;
    private readonly MediaPlayer _contentPlayer;
    private Media? _reactionMedia;
    private Media? _contentMedia;

    private readonly System.Windows.Forms.Timer _syncTimer = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 100 };

    private SplitContainer _split = null!;
    private Panel _playerPanel = null!;
    private VideoView _reactionView = null!;
    private VideoView _contentView = null!;
    private Panel _overlayPanel = null!;
    private Panel _overlayChrome = null!;
    private Label _driftLabel = null!;
    private Label _statusLabel = null!;
    private Label _reactionTimeLabel = null!;
    private Label _contentTimeLabel = null!;
    private Label _seekCurrentLabel = null!;
    private Label _seekDurationLabel = null!;
    private TrackBar _seekBar = null!;

    private TextBox _reactionSourceBox = null!;
    private TextBox _contentSourceBox = null!;
    private NumericUpDown _offsetBox = null!;
    private ComboBox _speedBox = null!;
    private CheckBox _lockStepBox = null!;
    private CheckBox _overlayVisibleBox = null!;
    private TrackBar _reactionVolume = null!;
    private TrackBar _contentVolume = null!;
    private TrackBar _overlaySize = null!;
    private ComboBox _reactionAudio = null!;
    private ComboBox _reactionSubs = null!;
    private ComboBox _contentAudio = null!;
    private ComboBox _contentSubs = null!;

    private bool _isSeeking;
    private bool _updatingTracks;
    private bool _fullscreen;
    private Rectangle _normalBounds;
    private FormBorderStyle _normalBorderStyle;
    private bool _normalTopMost;

    private bool _draggingOverlay;
    private Point _overlayDragStartMouse;
    private Point _overlayDragStartLocation;

    private readonly AppSettings _settings;

    private const int SidebarMinWidth = 330;
    private const int PlayerPanelMinWidth = 520;

    public MainForm()
    {
        Text = "Full-Length Player — Dual Sync";
        MinimumSize = new Size(1100, 700);
        BackColor = Color.FromArgb(11, 13, 16);
        ForeColor = Color.FromArgb(229, 231, 235);
        KeyPreview = true;

        _settings = AppSettings.Load();
        _libVlc = new LibVLC("--no-video-title-show", "--input-fast-seek", "--avcodec-hw=any");
        _reactionPlayer = new MediaPlayer(_libVlc);
        _contentPlayer = new MediaPlayer(_libVlc);

        BuildUi();
        WirePlayers();
        ApplySettings();

        _syncTimer.Tick += (_, _) => MaintainSync();
        _syncTimer.Start();
        _uiTimer.Tick += (_, _) => UpdateUiFromPlayers();
        _uiTimer.Start();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        Controls.Add(root);

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 7, 8, 7),
            BackColor = Color.FromArgb(15, 20, 26)
        };
        root.Controls.Add(topBar, 0, 0);

        var toggleSidebar = MakeButton("☰ Settings");
        toggleSidebar.Click += (_, _) => _split.Panel1Collapsed = !_split.Panel1Collapsed;
        topBar.Controls.Add(toggleSidebar);

        var title = new Label
        {
            Text = "Full-Length Player — Reaction Primary",
            AutoSize = false,
            Width = 360,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(203, 213, 225)
        };
        topBar.Controls.Add(title);

        topBar.Controls.Add(MakeHint("Space play/pause • ←/→ jump 5s • ,/. offset ±0.10s • F11 fullscreen"));

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            BackColor = Color.FromArgb(11, 13, 16)
        };
        _split.HandleCreated += (_, _) => ApplySafeSplitterDistance(430);
        _split.SizeChanged += (_, _) => ApplySafeSplitterDistance(_split.SplitterDistance);
        root.Controls.Add(_split, 0, 1);

        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(21, 26, 33),
            Padding = new Padding(10)
        };
        _split.Panel1.Controls.Add(sidebar);
        sidebar.Resize += (_, _) =>
        {
            foreach (Control child in sidebar.Controls) child.Width = Math.Max(320, sidebar.ClientSize.Width - 26);
        };

        _playerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            AllowDrop = true
        };
        _split.Panel2.Controls.Add(_playerPanel);
        _playerPanel.DragEnter += PlayerPanel_DragEnter;
        _playerPanel.DragDrop += PlayerPanel_DragDrop;
        _playerPanel.Resize += (_, _) => ApplyOverlaySize();

        _reactionView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            MediaPlayer = _reactionPlayer
        };
        _playerPanel.Controls.Add(_reactionView);

        BuildOverlay();
        BuildHoverControls();

        BuildGlobalSection(sidebar);
        BuildReactionSection(sidebar);
        BuildContentSection(sidebar);
        BuildTrackSection(sidebar);
        BuildNotesSection(sidebar);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 20, 26),
            ForeColor = Color.FromArgb(148, 163, 184),
            Padding = new Padding(8, 4, 8, 4),
            Text = "Ready. Load a reaction and a movie/show, then set offset and play."
        };
        root.Controls.Add(_statusLabel, 0, 2);
    }

    private void ApplySafeSplitterDistance(int desiredDistance)
    {
        if (_split.IsDisposed)
        {
            return;
        }

        var max = _split.ClientSize.Width - _split.Panel2MinSize;
        if (max < _split.Panel1MinSize)
        {
            return;
        }

        var safeDistance = Math.Clamp(desiredDistance, _split.Panel1MinSize, max);
        if (_split.SplitterDistance != safeDistance)
        {
            _split.SplitterDistance = safeDistance;
        }
    }

    private void BuildOverlay()
    {
        _overlayPanel = new Panel
        {
            Width = 480,
            Height = 270,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };

        _contentView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            MediaPlayer = _contentPlayer
        };
        _overlayPanel.Controls.Add(_contentView);

        _overlayChrome = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(170, 15, 20, 26),
            Cursor = Cursors.SizeAll
        };
        _overlayPanel.Controls.Add(_overlayChrome);
        _overlayChrome.BringToFront();

        var chromeText = new Label
        {
            Text = "Movie/Show (B) — drag here",
            Dock = DockStyle.Left,
            Width = 180,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(229, 231, 235),
            Padding = new Padding(8, 0, 0, 0),
            Cursor = Cursors.SizeAll
        };
        _overlayChrome.Controls.Add(chromeText);

        _driftLabel = new Label
        {
            Text = "Δ 0.00s",
            Dock = DockStyle.Right,
            Width = 82,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(229, 231, 235),
            Cursor = Cursors.SizeAll
        };
        _overlayChrome.Controls.Add(_driftLabel);

        _overlayChrome.MouseDown += OverlayChrome_MouseDown;
        _overlayChrome.MouseMove += OverlayChrome_MouseMove;
        _overlayChrome.MouseUp += OverlayChrome_MouseUp;
        chromeText.MouseDown += OverlayChrome_MouseDown;
        chromeText.MouseMove += OverlayChrome_MouseMove;
        chromeText.MouseUp += OverlayChrome_MouseUp;
        _driftLabel.MouseDown += OverlayChrome_MouseDown;
        _driftLabel.MouseMove += OverlayChrome_MouseMove;
        _driftLabel.MouseUp += OverlayChrome_MouseUp;

        _playerPanel.Controls.Add(_overlayPanel);
        _overlayPanel.BringToFront();
        ApplyOverlaySize();
    }

    private void BuildHoverControls()
    {
        var bottomControls = new Panel
        {
            Height = 48,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(190, 0, 0, 0)
        };
        _playerPanel.Controls.Add(bottomControls);
        bottomControls.BringToFront();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        bottomControls.Controls.Add(table);

        var playToggle = MakeButton("⏯");
        playToggle.Click += (_, _) => TogglePlayPause();
        table.Controls.Add(playToggle, 0, 0);

        _seekCurrentLabel = MakeTimeLabel("0:00");
        table.Controls.Add(_seekCurrentLabel, 1, 0);

        _seekBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 10000,
            TickStyle = TickStyle.None,
            BackColor = Color.Black
        };
        _seekBar.MouseDown += (_, _) => _isSeeking = true;
        _seekBar.MouseUp += (_, _) => CommitSeekFromBar();
        _seekBar.Scroll += (_, _) =>
        {
            if (_isSeeking && _reactionPlayer.Length > 0)
                _seekCurrentLabel.Text = FormatMilliseconds((long)(_reactionPlayer.Length * (_seekBar.Value / 10000.0)));
        };
        table.Controls.Add(_seekBar, 2, 0);

        _seekDurationLabel = MakeTimeLabel("0:00");
        table.Controls.Add(_seekDurationLabel, 3, 0);

        var full = MakeButton("Fullscreen");
        full.Click += (_, _) => ToggleFullscreen();
        table.Controls.Add(full, 4, 0);
    }

    private void BuildGlobalSection(FlowLayoutPanel sidebar)
    {
        var play = MakeButton("▶ Play");
        play.Click += (_, _) => PlayBoth();
        var pause = MakeButton("⏸ Pause");
        pause.Click += (_, _) => PauseBoth();
        var stop = MakeButton("⏹ Stop");
        stop.Click += (_, _) => StopBoth();
        var snap = MakeButton("Snap B to A now");
        snap.Click += (_, _) => SnapContentToReaction(true);
        var back = MakeButton("−0.10s");
        back.Click += (_, _) => NudgeOffset(-0.10m);
        var fwd = MakeButton("+0.10s");
        fwd.Click += (_, _) => NudgeOffset(0.10m);

        _speedBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 92 };
        foreach (var item in new[] { "0.25", "0.5", "0.75", "1", "1.25", "1.5", "1.75", "2" }) _speedBox.Items.Add(item);
        _speedBox.SelectedItem = "1";
        _speedBox.SelectedIndexChanged += (_, _) => ApplyPlaybackRate();

        _offsetBox = new NumericUpDown
        {
            DecimalPlaces = 2,
            Increment = 0.05m,
            Minimum = -7200,
            Maximum = 7200,
            Width = 100
        };
        _offsetBox.ValueChanged += (_, _) => SnapContentToReaction(false);

        _lockStepBox = new CheckBox { Text = "Lock-step follow", Checked = true, AutoSize = true };

        sidebar.Controls.Add(MakeSection("Global Transport",
            MakeRow(play, pause, stop),
            MakeLabeledRow("Speed", _speedBox, MakeHint("applies to both")),
            MakeLabeledRow("Sync offset (B vs A)", _offsetBox, back, fwd),
            MakeRow(snap, _lockStepBox)));
    }

    private void BuildReactionSection(FlowLayoutPanel sidebar)
    {
        _reactionSourceBox = MakeTextBox("Paste or type a reaction video URL, file path, or folder path...");
        var paste = MakeButton("Paste");
        paste.Click += (_, _) => PasteInto(_reactionSourceBox);
        var browse = MakeButton("Browse");
        browse.Click += (_, _) => BrowseInto(_reactionSourceBox, true);
        var load = MakeButton("Load A");
        load.Click += (_, _) => LoadReaction();

        _reactionVolume = MakeVolumeBar();
        _reactionVolume.Scroll += (_, _) => _reactionPlayer.Volume = _reactionVolume.Value;
        _reactionTimeLabel = MakeBadge("0:00");

        sidebar.Controls.Add(MakeSection("Player A — Reaction (Primary)",
            MakeRow(_reactionSourceBox),
            MakeRow(paste, browse, load),
            MakeLabeledRow("Volume", _reactionVolume, _reactionTimeLabel)));
    }

    private void BuildContentSection(FlowLayoutPanel sidebar)
    {
        _contentSourceBox = MakeTextBox("Paste or type a movie/show URL, file path, or folder path...");
        var paste = MakeButton("Paste");
        paste.Click += (_, _) => PasteInto(_contentSourceBox);
        var browse = MakeButton("Browse");
        browse.Click += (_, _) => BrowseInto(_contentSourceBox, false);
        var load = MakeButton("Load B");
        load.Click += (_, _) => LoadContent();

        _contentVolume = MakeVolumeBar();
        _contentVolume.Scroll += (_, _) => _contentPlayer.Volume = _contentVolume.Value;
        _contentTimeLabel = MakeBadge("0:00");

        _overlayVisibleBox = new CheckBox { Text = "Overlay visible", Checked = true, AutoSize = true };
        _overlayVisibleBox.CheckedChanged += (_, _) => _overlayPanel.Visible = _overlayVisibleBox.Checked;
        _overlaySize = new TrackBar
        {
            Minimum = 20,
            Maximum = 80,
            Value = 33,
            TickFrequency = 10,
            SmallChange = 1,
            LargeChange = 5,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(21, 26, 33)
        };
        _overlaySize.Scroll += (_, _) => ApplyOverlaySize();

        sidebar.Controls.Add(MakeSection("Player B — Movie/Show (Overlay)",
            MakeRow(_contentSourceBox),
            MakeRow(paste, browse, load),
            MakeLabeledRow("Volume", _contentVolume, _contentTimeLabel),
            MakeLabeledRow("Overlay size", _overlaySize),
            MakeRow(_overlayVisibleBox)));
    }

    private void BuildTrackSection(FlowLayoutPanel sidebar)
    {
        _reactionAudio = MakeCombo();
        _reactionSubs = MakeCombo();
        _contentAudio = MakeCombo();
        _contentSubs = MakeCombo();

        _reactionAudio.SelectedIndexChanged += (_, _) => ApplySelectedTrack(_reactionPlayer, _reactionAudio, true);
        _reactionSubs.SelectedIndexChanged += (_, _) => ApplySelectedTrack(_reactionPlayer, _reactionSubs, false);
        _contentAudio.SelectedIndexChanged += (_, _) => ApplySelectedTrack(_contentPlayer, _contentAudio, true);
        _contentSubs.SelectedIndexChanged += (_, _) => ApplySelectedTrack(_contentPlayer, _contentSubs, false);

        var refresh = MakeButton("Refresh tracks");
        refresh.Click += (_, _) => RefreshTracks();

        var subA = MakeButton("Add subtitles to A");
        subA.Click += (_, _) => AddExternalSubtitle(_reactionPlayer);
        var subB = MakeButton("Add subtitles to B");
        subB.Click += (_, _) => AddExternalSubtitle(_contentPlayer);

        sidebar.Controls.Add(MakeSection("Audio / Subtitles",
            MakeLabeledRow("A audio", _reactionAudio),
            MakeLabeledRow("A subtitles", _reactionSubs),
            MakeLabeledRow("B audio", _contentAudio),
            MakeLabeledRow("B subtitles", _contentSubs),
            MakeRow(refresh),
            MakeRow(subA, subB)));
    }

    private void BuildNotesSection(FlowLayoutPanel sidebar)
    {
        sidebar.Controls.Add(MakeSection("Notes",
            MakeHint("Drop files onto the player area: first drop fills A, second drop fills B."),
            MakeHint("MKV/x265/subtitle playback comes from LibVLC, not browser video."),
            MakeHint("A is master. B follows A + your offset.")));
    }

    private void WirePlayers()
    {
        _reactionPlayer.EndReached += (_, _) => SafeBeginInvoke(() => PauseBoth());
        _contentPlayer.EndReached += (_, _) => SafeBeginInvoke(() => PauseBoth());
        _reactionPlayer.Playing += (_, _) => SafeBeginInvoke(RefreshTracks);
        _contentPlayer.Playing += (_, _) => SafeBeginInvoke(RefreshTracks);
    }

    private void ApplySettings()
    {
        _reactionSourceBox.Text = _settings.ReactionSource ?? string.Empty;
        _contentSourceBox.Text = _settings.ContentSource ?? string.Empty;
        _offsetBox.Value = ClampDecimal((decimal)_settings.OffsetSeconds, _offsetBox.Minimum, _offsetBox.Maximum);
        _reactionVolume.Value = Math.Clamp(_settings.ReactionVolume, 0, 100);
        _contentVolume.Value = Math.Clamp(_settings.ContentVolume, 0, 100);
        _reactionPlayer.Volume = _reactionVolume.Value;
        _contentPlayer.Volume = _contentVolume.Value;
        _lockStepBox.Checked = _settings.LockStep;
        _overlayVisibleBox.Checked = _settings.OverlayVisible;
        _overlayPanel.Visible = _settings.OverlayVisible;
        _overlaySize.Value = Math.Clamp(_settings.OverlayPercent, _overlaySize.Minimum, _overlaySize.Maximum);
        ApplyOverlaySize();

        var rateText = _settings.PlaybackRate.ToString("0.##");
        _speedBox.SelectedItem = _speedBox.Items.Contains(rateText) ? rateText : "1";
        ApplyPlaybackRate();
    }

    private void SaveSettings()
    {
        _settings.ReactionSource = _reactionSourceBox.Text.Trim();
        _settings.ContentSource = _contentSourceBox.Text.Trim();
        _settings.OffsetSeconds = (double)_offsetBox.Value;
        _settings.ReactionVolume = _reactionVolume.Value;
        _settings.ContentVolume = _contentVolume.Value;
        _settings.PlaybackRate = CurrentRate();
        _settings.LockStep = _lockStepBox.Checked;
        _settings.OverlayPercent = _overlaySize.Value;
        _settings.OverlayVisible = _overlayVisibleBox.Checked;
        _settings.Save();
    }

    private void LoadReaction() => LoadInto(_reactionPlayer, ref _reactionMedia, _reactionSourceBox.Text, "Reaction A");

    private void LoadContent() => LoadInto(_contentPlayer, ref _contentMedia, _contentSourceBox.Text, "Movie/Show B");

    private void LoadInto(MediaPlayer player, ref Media? mediaSlot, string source, string label)
    {
        var resolved = ResolveMediaInput(source);
        if (resolved is null) return;

        try
        {
            mediaSlot?.Dispose();
            var fromType = IsNetworkLocation(resolved) ? FromType.FromLocation : FromType.FromPath;
            var media = new Media(_libVlc, resolved, fromType);
            media.AddOption(":input-fast-seek");
            media.AddOption(":no-video-title-show");
            mediaSlot = media;
            player.Media = media;
            player.Volume = player == _reactionPlayer ? _reactionVolume.Value : _contentVolume.Value;
            player.SetRate(CurrentRate());
            SetStatus($"{label} loaded: {DisplayName(resolved)}");
            RefreshTracksSoon();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, $"Failed to load {label}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string? ResolveMediaInput(string raw)
    {
        var input = (raw ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show(this, "Paste a URL/path or use Browse first.", "No media source", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        input = Environment.ExpandEnvironmentVariables(input);

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            input = uri.LocalPath;

        if (Directory.Exists(input))
        {
            var firstVideo = Directory.EnumerateFiles(input)
                .Where(path => VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (firstVideo is null)
            {
                MessageBox.Show(this, "That folder did not contain a supported video file.", "No video found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            return firstVideo;
        }

        if (File.Exists(input)) return Path.GetFullPath(input);
        if (IsNetworkLocation(input)) return input;

        MessageBox.Show(this, "That file/folder path does not exist, and it does not look like a supported URL.", "Source not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return null;
    }

    private static bool IsNetworkLocation(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme is "http" or "https" or "rtsp" or "rtmp" or "ftp" or "smb";
    }

    private void BrowseInto(TextBox target, bool reaction)
    {
        using var dialog = new OpenFileDialog
        {
            Title = reaction ? "Choose reaction video" : "Choose movie/show video",
            Filter = "Video files|*.mkv;*.mp4;*.m4v;*.webm;*.avi;*.mov;*.wmv;*.ts;*.m2ts|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
            if (reaction) LoadReaction(); else LoadContent();
        }
    }

    private static void PasteInto(TextBox target)
    {
        if (Clipboard.ContainsText()) target.Text = Clipboard.GetText();
    }

    private void PlayBoth()
    {
        if (_reactionPlayer.Media is null || _contentPlayer.Media is null)
        {
            SetStatus("Load both A and B before playing.");
            return;
        }

        SnapContentToReaction(false);
        ApplyPlaybackRate();
        _contentPlayer.Play();
        _reactionPlayer.Play();
        SetStatus("Playing both players in sync.");
    }

    private void PauseBoth()
    {
        if (_reactionPlayer.IsPlaying) _reactionPlayer.Pause();
        if (_contentPlayer.IsPlaying) _contentPlayer.Pause();
        SetStatus("Paused.");
    }

    private void StopBoth()
    {
        _reactionPlayer.Stop();
        _contentPlayer.Stop();
        SetStatus("Stopped. Press Play to start again from the beginning.");
    }

    private void TogglePlayPause()
    {
        if (_reactionPlayer.IsPlaying || _contentPlayer.IsPlaying) PauseBoth();
        else PlayBoth();
    }

    private void ApplyPlaybackRate()
    {
        var rate = CurrentRate();
        try
        {
            _reactionPlayer.SetRate(rate);
            _contentPlayer.SetRate(rate);
        }
        catch
        {
            // Some media cannot change speed until playback starts. The rate is applied again on Play.
        }
    }

    private float CurrentRate()
    {
        if (_speedBox.SelectedItem is null) return 1.0f;
        return float.TryParse(_speedBox.SelectedItem.ToString(), out var rate) ? rate : 1.0f;
    }

    private long OffsetMs() => (long)Math.Round((double)_offsetBox.Value * 1000.0);

    private void NudgeOffset(decimal delta)
    {
        _offsetBox.Value = ClampDecimal(_offsetBox.Value + delta, _offsetBox.Minimum, _offsetBox.Maximum);
        SnapContentToReaction(true);
    }

    private void SnapContentToReaction(bool userInitiated)
    {
        if (_contentPlayer.Media is null || _reactionPlayer.Media is null) return;
        var target = ClampToContentLength(_reactionPlayer.Time + OffsetMs());
        try
        {
            _contentPlayer.Time = target;
            UpdateDriftLabel(0);
            if (userInitiated) FlashDrift();
        }
        catch
        {
            // Ignore transient seek failures while media is still opening.
        }
    }

    private void MaintainSync()
    {
        if (!_lockStepBox.Checked) return;
        if (_reactionPlayer.Media is null || _contentPlayer.Media is null) return;

        var target = ClampToContentLength(_reactionPlayer.Time + OffsetMs());
        var drift = target - _contentPlayer.Time;
        UpdateDriftLabel(drift / 1000.0);

        if (Math.Abs(drift) <= 150) return;

        try
        {
            // If paused or badly out of sync, seek B directly. This matches the original app's B-follows-A design.
            if (!_reactionPlayer.IsPlaying || !_contentPlayer.IsPlaying || Math.Abs(drift) > 750)
            {
                _contentPlayer.Time = target;
                return;
            }

            // Small correction while playing: move part of the drift each tick to reduce visible jerkiness.
            var correction = Math.Sign(drift) * Math.Min(Math.Abs(drift), 250);
            _contentPlayer.Time = ClampToContentLength(_contentPlayer.Time + correction);
        }
        catch
        {
            // Media can briefly reject seeks while buffering/opening. Try again on the next timer tick.
        }
    }

    private long ClampToContentLength(long target)
    {
        target = Math.Max(0, target);
        var length = _contentPlayer.Length;
        if (length > 1000) target = Math.Min(target, length - 500);
        return target;
    }

    private void CommitSeekFromBar()
    {
        if (_reactionPlayer.Length > 0)
        {
            var newTime = (long)(_reactionPlayer.Length * (_seekBar.Value / 10000.0));
            _reactionPlayer.Time = Math.Max(0, newTime);
            SnapContentToReaction(false);
        }
        _isSeeking = false;
    }

    private void JumpBoth(long deltaMs)
    {
        if (_reactionPlayer.Media is null) return;
        var newTime = Math.Max(0, _reactionPlayer.Time + deltaMs);
        var length = _reactionPlayer.Length;
        if (length > 1000) newTime = Math.Min(newTime, length - 500);
        _reactionPlayer.Time = newTime;
        SnapContentToReaction(false);
    }

    private void RefreshTracksSoon()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 800 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            RefreshTracks();
        };
        timer.Start();
    }

    private void RefreshTracks()
    {
        if (_updatingTracks) return;
        _updatingTracks = true;
        try
        {
            FillAudioTracks(_reactionPlayer, _reactionAudio);
            FillSubtitleTracks(_reactionPlayer, _reactionSubs);
            FillAudioTracks(_contentPlayer, _contentAudio);
            FillSubtitleTracks(_contentPlayer, _contentSubs);
        }
        finally
        {
            _updatingTracks = false;
        }
    }

    private static void FillAudioTracks(MediaPlayer player, ComboBox combo)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        foreach (var desc in player.AudioTrackDescription ?? [])
        {
            combo.Items.Add(new TrackItem(desc.Id, desc.Name ?? $"Audio {desc.Id}"));
        }
        SelectTrack(combo, player.AudioTrack);
        combo.EndUpdate();
    }

    private static void FillSubtitleTracks(MediaPlayer player, ComboBox combo)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.Add(new TrackItem(-1, "Disabled"));
        foreach (var desc in player.SpuDescription ?? [])
        {
            combo.Items.Add(new TrackItem(desc.Id, desc.Name ?? $"Subtitle {desc.Id}"));
        }
        SelectTrack(combo, player.Spu);
        combo.EndUpdate();
    }

    private static void SelectTrack(ComboBox combo, int id)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is TrackItem item && item.Id == id)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void ApplySelectedTrack(MediaPlayer player, ComboBox combo, bool audio)
    {
        if (_updatingTracks) return;
        if (combo.SelectedItem is not TrackItem item) return;
        try
        {
            if (audio) player.SetAudioTrack(item.Id);
            else player.SetSpu(item.Id);
        }
        catch (Exception ex)
        {
            SetStatus($"Track change failed: {ex.Message}");
        }
    }

    private void AddExternalSubtitle(MediaPlayer player)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose subtitle file",
            Filter = "Subtitle files|*.srt;*.ass;*.ssa;*.vtt;*.sub|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var uri = new Uri(dialog.FileName).AbsoluteUri;
            var ok = player.AddSlave(MediaSlaveType.Subtitle, uri, true);
            SetStatus(ok ? $"Added subtitle: {Path.GetFileName(dialog.FileName)}" : "LibVLC rejected that subtitle file.");
            RefreshTracksSoon();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Failed to add subtitles", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyOverlaySize()
    {
        if (_playerPanel is null || _overlayPanel is null || _overlaySize is null) return;
        var percent = _overlaySize.Value / 100.0;
        var width = Math.Max(260, (int)(_playerPanel.ClientSize.Width * percent));
        var height = (int)(width * 9.0 / 16.0);
        _overlayPanel.Size = new Size(width, height);

        if (_overlayPanel.Left <= 0 && _overlayPanel.Top <= 0)
        {
            _overlayPanel.Left = Math.Max(0, _playerPanel.ClientSize.Width - _overlayPanel.Width - 14);
            _overlayPanel.Top = Math.Max(0, _playerPanel.ClientSize.Height - _overlayPanel.Height - 62);
        }
        else
        {
            KeepOverlayInside();
        }
    }

    private void KeepOverlayInside()
    {
        _overlayPanel.Left = Math.Clamp(_overlayPanel.Left, 0, Math.Max(0, _playerPanel.ClientSize.Width - _overlayPanel.Width));
        _overlayPanel.Top = Math.Clamp(_overlayPanel.Top, 0, Math.Max(0, _playerPanel.ClientSize.Height - _overlayPanel.Height));
    }

    private void OverlayChrome_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _draggingOverlay = true;
        _overlayDragStartMouse = _playerPanel.PointToClient(Cursor.Position);
        _overlayDragStartLocation = _overlayPanel.Location;
    }

    private void OverlayChrome_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_draggingOverlay) return;
        var now = _playerPanel.PointToClient(Cursor.Position);
        var dx = now.X - _overlayDragStartMouse.X;
        var dy = now.Y - _overlayDragStartMouse.Y;
        _overlayPanel.Location = new Point(_overlayDragStartLocation.X + dx, _overlayDragStartLocation.Y + dy);
        KeepOverlayInside();
    }

    private void OverlayChrome_MouseUp(object? sender, MouseEventArgs e)
    {
        _draggingOverlay = false;
    }

    private void PlayerPanel_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
    }

    private void PlayerPanel_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var path = files[0];
        if (string.IsNullOrWhiteSpace(_reactionSourceBox.Text) || _reactionPlayer.Media is null)
        {
            _reactionSourceBox.Text = path;
            LoadReaction();
        }
        else
        {
            _contentSourceBox.Text = path;
            LoadContent();
        }
    }

    private void UpdateUiFromPlayers()
    {
        _reactionTimeLabel.Text = FormatMilliseconds(_reactionPlayer.Time);
        _contentTimeLabel.Text = FormatMilliseconds(_contentPlayer.Time);
        _seekCurrentLabel.Text = FormatMilliseconds(_reactionPlayer.Time);
        _seekDurationLabel.Text = FormatMilliseconds(_reactionPlayer.Length);

        if (!_isSeeking && _reactionPlayer.Length > 0)
        {
            var value = (int)Math.Clamp((_reactionPlayer.Time / (double)_reactionPlayer.Length) * 10000.0, 0, 10000);
            if (_seekBar.Value != value) _seekBar.Value = value;
        }
    }

    private void UpdateDriftLabel(double driftSeconds)
    {
        if (_driftLabel.IsDisposed) return;
        _driftLabel.Text = $"Δ {driftSeconds:0.00}s";
    }

    private void FlashDrift()
    {
        var oldColor = _driftLabel.BackColor;
        _driftLabel.BackColor = Color.FromArgb(96, 165, 250);
        var timer = new System.Windows.Forms.Timer { Interval = 220 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            if (!_driftLabel.IsDisposed) _driftLabel.BackColor = oldColor;
        };
        timer.Start();
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _normalBounds = Bounds;
            _normalBorderStyle = FormBorderStyle;
            _normalTopMost = TopMost;
            _split.Panel1Collapsed = true;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = true;
            _fullscreen = true;
        }
        else
        {
            TopMost = _normalTopMost;
            FormBorderStyle = _normalBorderStyle;
            Bounds = _normalBounds;
            _split.Panel1Collapsed = false;
            _fullscreen = false;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Space:
                TogglePlayPause();
                return true;
            case Keys.Left:
                JumpBoth(-5000);
                return true;
            case Keys.Right:
                JumpBoth(5000);
                return true;
            case Keys.Oemcomma:
                NudgeOffset(-0.10m);
                return true;
            case Keys.OemPeriod:
                NudgeOffset(0.10m);
                return true;
            case Keys.F11:
                ToggleFullscreen();
                return true;
            case Keys.Escape when _fullscreen:
                ToggleFullscreen();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private static string FormatMilliseconds(long ms)
    {
        if (ms <= 0) return "0:00";
        var time = TimeSpan.FromMilliseconds(ms);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static string DisplayName(string source)
    {
        if (IsNetworkLocation(source)) return source;
        try { return Path.GetFileName(source); }
        catch { return source; }
    }

    private void SetStatus(string message)
    {
        if (_statusLabel is not null && !_statusLabel.IsDisposed) _statusLabel.Text = message;
    }

    private void SafeBeginInvoke(Action action)
    {
        if (IsDisposed) return;
        try
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch (InvalidOperationException)
        {
            // Closing.
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        _syncTimer.Stop();
        _uiTimer.Stop();
        _reactionPlayer.Stop();
        _contentPlayer.Stop();
        _reactionView.MediaPlayer = null;
        _contentView.MediaPlayer = null;
        _reactionPlayer.Dispose();
        _contentPlayer.Dispose();
        _reactionMedia?.Dispose();
        _contentMedia?.Dispose();
        _libVlc.Dispose();
        base.OnFormClosing(e);
    }

    private static decimal ClampDecimal(decimal value, decimal min, decimal max) => Math.Min(Math.Max(value, min), max);

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        Height = 32,
        AutoSize = true,
        BackColor = Color.FromArgb(31, 41, 55),
        ForeColor = Color.FromArgb(229, 231, 235),
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(3)
    };

    private static TextBox MakeTextBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        Height = 30,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(15, 19, 25),
        ForeColor = Color.FromArgb(229, 231, 235),
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(3)
    };

    private static ComboBox MakeCombo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(15, 19, 25),
        ForeColor = Color.FromArgb(229, 231, 235),
        Margin = new Padding(3)
    };

    private static TrackBar MakeVolumeBar() => new()
    {
        Minimum = 0,
        Maximum = 100,
        Value = 100,
        TickFrequency = 25,
        SmallChange = 5,
        LargeChange = 10,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(21, 26, 33)
    };

    private static Label MakeBadge(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 58,
        Height = 28,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.FromArgb(203, 213, 225),
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(3)
    };

    private static Label MakeTimeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.FromArgb(229, 231, 235),
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(3)
    };

    private static Label MakeHint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(380, 0),
        ForeColor = Color.FromArgb(148, 163, 184),
        Margin = new Padding(3, 6, 3, 6)
    };

    private static Control MakeSection(string title, params Control[] rows)
    {
        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ForeColor = Color.FromArgb(203, 213, 225),
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.FromArgb(21, 26, 33)
        };

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(21, 26, 33),
            Padding = new Padding(0, 4, 0, 0)
        };

        foreach (var row in rows)
        {
            row.Width = 380;
            stack.Controls.Add(row);
        }

        group.Controls.Add(stack);
        return group;
    }

    private static Control MakeRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 0, 2),
            BackColor = Color.FromArgb(21, 26, 33)
        };

        foreach (var control in controls)
        {
            if (control is TextBox or TrackBar or ComboBox) control.Width = Math.Max(control.Width, 300);
            row.Controls.Add(control);
        }

        return row;
    }

    private static Control MakeLabeledRow(string label, params Control[] controls)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = controls.Length + 1,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 0, 2),
            BackColor = Color.FromArgb(21, 26, 33)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        foreach (var _ in controls) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / controls.Length));

        row.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(203, 213, 225),
            Margin = new Padding(3)
        }, 0, 0);

        for (var i = 0; i < controls.Length; i++)
        {
            controls[i].Dock = DockStyle.Fill;
            row.Controls.Add(controls[i], i + 1, 0);
        }
        return row;
    }

    private sealed class TrackItem(int id, string name)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public override string ToString() => Name;
    }
}
