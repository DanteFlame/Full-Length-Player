namespace FullLengthPlayer;

internal sealed partial class MainForm : Form, IMessageFilter
{
    internal PlayerPane Reaction { get; } = new("Reaction A");
    internal PlayerPane Source { get; } = new("Source B");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    private PlayerPane active;
    internal MasterTransport Master { get; }
    internal CommentaryReplay Replay { get; }
    private PlayerPane? wheelPane;
    private int wheelRemainder;
    private readonly ToolStripButton replayButton = new("What Did They Say? (H)");
    internal SpeedControl SharedSpeed { get; }
    internal SpeedPreferences Preferences { get; } = new();
    private readonly ToolStripDropDownButton speedMenu = new("Speed: 1×");
    internal CompositionView Composition { get; }
    private readonly FlowLayoutPanel compositionBar = new() { Dock = DockStyle.Top, Height = 66, AutoScroll = true, BackColor = SystemColors.Control };
    private readonly TableLayoutPanel playerControls = new() { Dock = DockStyle.Bottom, Height = 200, ColumnCount = 2, RowCount = 1 };
    private readonly Label info = new() { Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.White, Text = "A/S/D/G: both speeds • Shift+J/K/L: hovered player • F/F11: fullscreen • Esc: exit fullscreen", AutoEllipsis = true };
    private Rectangle windowBounds;
    private FormWindowState previousState;
    internal bool Fullscreen { get; private set; }
    private long nextUiUpdate;
    private readonly ToolStrip masterBar = new() { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
    private readonly TrackBar masterTimeline = new() { Dock = DockStyle.Top, Height = 32, Maximum = 10000, TickStyle = TickStyle.None };
    private readonly Label masterStatus = new() { Dock = DockStyle.Top, Height = 24, ForeColor = Color.White, AutoEllipsis = true };
    private bool masterDragging;
    internal FullscreenOverlay Hud { get; } = new();
    private long fullscreenActivity;
    private Point lastPointer;
    private bool cursorHidden;
    private ComboBox? canvasChoice;
    internal static int ClosestCanvas(Size display)
    {
        double aspect = display.Width / (double)Math.Max(1, display.Height);
        double[] options = { 16.0 / 9, 4.0 / 3, 16.0 / 10 };
        return Enumerable.Range(0, 3).MinBy(i => Math.Abs(Math.Log(options[i] / aspect)));
    }
    internal void RevealFullscreen()
    {
        fullscreenActivity = Environment.TickCount64;
        if (Fullscreen) { Hud.Visible = true; Hud.BringToFront(); }
        if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
    }
    internal void UpdateFullscreen(long now, Point pointer, bool focused)
    {
        AdvanceSpeedHold(now, pointer, focused);
        SpeedToast.Advance(now, Fullscreen && focused);
        if (!Fullscreen || !focused) { pressPoint = null; gestures.Reset(); Hud.Visible = false; if (cursorHidden) { Cursor.Show(); cursorHidden = false; } return; }
        if (pointer != lastPointer) { lastPointer = pointer; RevealFullscreen(); }
        if (Hud.Dragging) RevealFullscreen();
        bool visible = now - fullscreenActivity < 2500;
        Hud.Visible = visible;
        if (!visible && !cursorHidden && Bounds.Contains(pointer)) { Cursor.Hide(); cursorHidden = true; }
    }
    private readonly FlowLayoutPanel syncBar = new() { Dock = DockStyle.Top, Height = 36, AutoScroll = true, WrapContents = false, BackColor = SystemColors.Control };
    private readonly NumericUpDown offsetInput = new() { DecimalPlaces = 2, Increment = 0.05m, Minimum = -604800, Maximum = 604800, Width = 110 };
    private readonly Label syncStatus = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private double displayedOffset = double.NaN;
    public MainForm()
    {
        Text = "Full-Length Player";
        ApplyIcon(AppIcons.Read(), save: false);
        BackColor = Color.Black;
        ClientSize = new Size(1280, 650);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        active = Reaction;
        Master = new MasterTransport(() => Reaction.Player, () => Source.Player);
        Replay = new CommentaryReplay(Reaction, Source, Master);
        Reaction.ManualTransport += Replay.Cancel; Source.ManualTransport += Replay.Cancel;
        Reaction.VolumeEdited += Replay.Cancel; Source.VolumeEdited += Replay.Cancel;
        Application.AddMessageFilter(this);
        SharedSpeed = new SpeedControl(() => Reaction.Player?.Number("speed") ?? 1, value =>
        {
            Master.SetSpeed(value);
        });
        Reaction.MediaReplaced += SharedSpeed.Reset;
        Source.MediaReplaced += SharedSpeed.Reset;
        Reaction.MediaReplaced += () => CancelSpeedHold(false);
        Source.MediaReplaced += () => CancelSpeedHold(false);
        Deactivate += (_, _) => { CancelSpeedHold(false); SpeedToast.Hide(); };
        MouseCaptureChanged += (_, _) => { if (!Capture) CancelSpeedHold(false); };
        Reaction.ManualTransport += () => Master.Unlock("Unlocked by independent control — relock after aligning");
        Source.ManualTransport += () => Master.Unlock("Unlocked by independent control — relock after aligning");
        void SyncButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, _) => RunMaster(action);
            syncBar.Controls.Add(button);
        }
        SyncButton("Lock current alignment", Master.CaptureAlignment);
        SyncButton("Unlock", () => Master.Unlock());
        syncBar.Controls.Add(new Label { Text = "Offset B−A (s)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        syncBar.Controls.Add(offsetInput);
        // Apply explicit edits only; timer updates must never change the stored offset.
        SyncButton("Apply offset", () => Master.SetOffset((double)offsetInput.Value));
        SyncButton("−0.05 s", () => Master.Nudge(-MasterTransport.OffsetStep));
        SyncButton("+0.05 s", () => Master.Nudge(MasterTransport.OffsetStep));
        SyncButton("Find audio sync…", FindAudioSync);
        syncBar.Controls.Add(syncStatus);
        masterBar.Items.Add(new ToolStripLabel("BOTH PLAYERS"));
        void MasterButton(string text, Action action)
        {
            var button = new ToolStripButton(text);
            button.Click += (_, _) => RunMaster(action);
            masterBar.Items.Add(button);
        }
        MasterButton("Play / Pause both", Master.TogglePause);
        MasterButton("−5 s both", () => Master.Jump(-5));
        MasterButton("+5 s both", () => Master.Jump(5));
        masterBar.Items.Add(new ToolStripSeparator());
        MasterButton("−0.25× (S)", () => SharedSpeed.Step(-0.25));
        MasterButton("+0.25× (D)", () => SharedSpeed.Step(0.25));
        masterBar.Items.Add(speedMenu);
        for (double speed = 0.25; speed <= 4; speed += 0.25)
        {
            double value = speed;
            var item = new ToolStripMenuItem($"{value:0.##}×");
            item.Click += (_, _) => RunMaster(() => SharedSpeed.Set(value));
            speedMenu.DropDownItems.Add(item);
        }
        MasterButton("1× ↔ (A)", () => SharedSpeed.Toggle("normal", 1));
        MasterButton("Favorite ↔ (G)", () => SharedSpeed.Toggle("favorite", Preferences.Favorite));
        MasterButton("Favorite settings", EditFavorite);
        replayButton.Click += (_, _) => { try { TriggerCommentaryReplay(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Commentary replay"); } };
        masterBar.Items.Add(replayButton);
        masterTimeline.MouseDown += (_, _) => masterDragging = true;
        masterTimeline.MouseUp += (_, _) => { masterDragging = false; SeekMasterTimeline(); };
        masterTimeline.KeyUp += (_, e) => { if (e.KeyCode is Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown) SeekMasterTimeline(); };
        Composition = new CompositionView(Reaction, Source);
        playerControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        playerControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        playerControls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        playerControls.Controls.Add(Reaction, 0, 0); playerControls.Controls.Add(Source, 1, 0);
        BuildCompositionControls();
        Controls.Add(Composition);
        Controls.Add(playerControls);
        Controls.Add(compositionBar);
        Controls.Add(info);
        Controls.Add(masterTimeline);
        Controls.Add(masterStatus);
        Controls.Add(syncBar);
        Controls.Add(masterBar);
        BuildSessionControls();
        BuildModernUi();
        Controls.Add(Hud);
        Hud.Seek += fraction => RunMaster(() => Master.SeekReaction(Master.TimelineStart + (Master.TimelineEnd - Master.TimelineStart) * fraction));
        Hud.Activity += RevealFullscreen;
        Hud.ExitRequested += ToggleFullscreen;
        Resize += (_, _) => Hud.Bounds = new Rectangle(0, Math.Max(0, ClientSize.Height - Hud.Height), ClientSize.Width, Hud.Height);
        UpdateMaster();
        Reaction.Surface.MouseDown += (_, _) => ActiveControl = null;
        Source.Surface.MouseDown += (_, _) => ActiveControl = null;
        Reaction.Activated += SelectPane;
        Source.Activated += SelectPane;
        SelectPane(Reaction);
        timer.Tick += (_, _) =>
        {
            UpdateFullscreen(Environment.TickCount64, Cursor.Position, Form.ActiveForm == this);
            try { Master.Tick(); Replay.Tick(); } catch (Exception e) { Master.Unlock("Sync stopped: " + e.Message); }
            if (Environment.TickCount64 < nextUiUpdate) return;
            nextUiUpdate = Environment.TickCount64 + 200;
            Reaction.UpdatePlayback(); Source.UpdatePlayback(); Composition.Arrange(); UpdateMaster();
        };
        Shown += async (_, _) =>
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool verify = args.Length == 3 && args[0] == "--verify-playback";
            verificationMode = verify;
            try
            {
                canvasChoice!.SelectedIndex = ClosestCanvas(Screen.FromControl(this).Bounds.Size);
                Reaction.Initialize(verify);
                Source.Initialize(verify);
                timer.Start();
                if (verify) await PlaybackVerification.Run(this, args[1], args[2]);
                else
                {
                    if (SessionStore.ReadSettings() is { } saved) ApplySettings(saved, restoreCanvas: false);
                    if (args.Length >= 1) Reaction.LoadVideo(args[0]);
                    if (args.Length >= 2) Source.LoadVideo(args[1]);
                }
            }
            catch (Exception error)
            {
                if (verify) { File.WriteAllText(args[2] + ".error.txt", error.ToString()); Environment.ExitCode = 1; Close(); }
                else
                {
                    var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FullLengthPlayer", "logs");
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "mpv-error.log"), error.ToString());
                    MessageBox.Show(this, error.ToString(), "MPV initialization error");
                }
            }
        };
    }
    private void FindAudioSync()
    {
        var p = Master.Snapshot() ?? throw new InvalidOperationException("Load both videos first.");
        if (Master.SeekingTogether) throw new InvalidOperationException("Wait for seeking to finish first.");
        using var dialog = new AudioSyncDialog(Reaction.CaptureAudio(), Source.CaptureAudio(), p.ATime, p.BTime, p.ADuration, p.BDuration);
        if (dialog.ShowDialog(this) == DialogResult.OK) Master.SetOffset(dialog.Offset);
    }
    private void BuildCompositionControls()
    {
        ComboBox Choice(string label, string[] choices, Action<int> change)
        {
            compositionBar.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            box.Items.AddRange(choices); box.SelectedIndex = 0;
            box.SelectedIndexChanged += (_, _) => { change(box.SelectedIndex); Composition.Arrange(); };
            compositionBar.Controls.Add(box); layoutInputs[label] = box; return box;
        }
        canvasChoice = Choice("Canvas", new[] { "16:9", "4:3", "16:10" }, i => Composition.CanvasAspect = i switch { 0 => 16.0 / 9, 1 => 4.0 / 3, _ => 16.0 / 10 });
        Choice("Source edge", new[] { "Bottom", "Top" }, i => Composition.TopAnchor = i == 1);
        NumericUpDown Number(string label, decimal value, decimal min, decimal max, Action<double> change)
        {
            compositionBar.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            var input = new NumericUpDown { Width = 55, Minimum = min, Maximum = max, Value = value };
            input.ValueChanged += (_, _) => { change((double)input.Value / 100); Composition.Arrange(); };
            compositionBar.Controls.Add(input); layoutInputs[label] = input; return input;
        }
        var size = Number("Source %", 70, 15, 100, v => Composition.SourceFraction = v);
        Composition.ResizedSource += () => size.Value = Math.Clamp((decimal)Math.Round(Composition.SourceFraction * 100), 15, 100);
        Number("Crop top %", 0, 0, 45, v => Composition.CropTop = v);
        Number("Crop bottom %", 0, 0, 45, v => Composition.CropBottom = v);
        Number("Reaction zoom %", 100, 50, 200, v => Composition.Zoom = v);
        Number("Pan X %", 0, -100, 100, v => Composition.PanX = v);
        Number("Pan Y %", 0, -100, 100, v => Composition.PanY = v);
        var full = new Button { Text = "Fullscreen", AutoSize = true };
        full.Click += (_, _) => ToggleFullscreen(); compositionBar.Controls.Add(full);
    }
    internal void ToggleFullscreen()
    {
        CancelSpeedHold(false); gestures.Reset(); SpeedToast.Hide();
        SuspendLayout();
        if (!Fullscreen)
        {
            windowBounds = Bounds; previousState = WindowState;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = Screen.FromControl(this).Bounds;
            Fullscreen = true;
        }
        else
        {
            Fullscreen = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = windowBounds; WindowState = previousState;
        }
        LayoutModernChrome();
        Composition.ShowHandles = !Fullscreen;
        ActiveControl = null;
        ResumeLayout(true); Composition.Arrange();
        if (Fullscreen) { lastPointer = Cursor.Position; RevealFullscreen(); } else { Hud.Visible = false; if (cursorHidden) { Cursor.Show(); cursorHidden = false; } }
    }
    private void RunMaster(Action action)
    {
        try { Replay.Cancel(); action(); RevealFullscreen(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Shared playback error"); }
        UpdateMaster();
    }
    private void SeekMasterTimeline()
    {
        var position = Master.Snapshot();
        if (position != null) RunMaster(() => Master.SeekReaction(Master.TimelineStart + (Master.TimelineEnd - Master.TimelineStart) * masterTimeline.Value / 10000.0));
    }
    private void UpdateMaster()
    {
        replayButton.Text = Replay.Active ? "End replay (H)" : "What Did They Say? (H)";
        var position = Master.Snapshot();
        double aSpeed = Reaction.Player?.Number("speed") ?? 1, bSpeed = Source.Player?.Number("speed") ?? 1;
        speedMenu.Text = Math.Abs(aSpeed - bSpeed) < 0.001 ? $"Speed: {aSpeed:0.##}×" : $"Speed A/B: {aSpeed:0.##}× / {bSpeed:0.##}×";
        masterBar.Enabled = masterTimeline.Enabled = syncBar.Enabled = position != null;
        syncStatus.Text = $"{Master.SyncStatus}" + (Master.Locked ? $" • Fixed offset {Master.Offset:+0.00;-0.00;0.00} s • Drift {Master.Drift.GetValueOrDefault():+0.000;-0.000;0.000} s" : "");
        if (displayedOffset != Master.Offset)
        {
            displayedOffset = Master.Offset;
            offsetInput.Value = Math.Clamp((decimal)Master.Offset, offsetInput.Minimum, offsetInput.Maximum);

        }
        if (position == null) { masterStatus.Text = "Load both videos to use shared controls"; masterTimeline.Value = 0; return; }
        double elapsed = Master.TimelineTime - Master.TimelineStart, duration = Master.TimelineEnd - Master.TimelineStart;
        Hud.UpdatePosition(elapsed, duration);
        if (!masterDragging) masterTimeline.Value = duration > 0 ? (int)Math.Clamp(elapsed / duration * 10000, 0, 10000) : 0;
        masterStatus.Text = $"Shared timeline: {TimeSpan.FromSeconds(Math.Max(0, elapsed)):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(Math.Max(0, duration)):hh\\:mm\\:ss} • { (Master.Locked ? "Alignment locked" : "Alignment unlocked") }";
    }
    private void SelectPane(PlayerPane pane)
    {
        active = pane;
        Reaction.SetActive(pane == Reaction);
        Source.SetActive(pane == Source);
    }
    private void EditFavorite()
    {
        using var dialog = new Form { Text = "Favorite playback speed", ClientSize = new Size(300, 105), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var field = new NumericUpDown { Left = 20, Top = 15, Width = 130, Minimum = 0.25m, Maximum = 4, Increment = 0.25m, DecimalPlaces = 2, Value = (decimal)Preferences.Favorite };
        var save = new Button { Text = "Save", Left = 190, Top = 60, DialogResult = DialogResult.OK };
        dialog.Controls.Add(field); dialog.Controls.Add(save); dialog.AcceptButton = save;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Preferences.Save((double)field.Value);
        SharedSpeed.Reset();
    }
    internal PlayerPane HoverTarget(Point cursor)
    {
        var hit = Composition.HitPlayer(cursor);
        if (hit != null) return hit;
        if (Reaction.RectangleToScreen(Reaction.ClientRectangle).Contains(cursor)) return Reaction;
        if (Source.RectangleToScreen(Source.ClientRectangle).Contains(cursor)) return Source;
        return active;
    }
    internal bool HandleShortcut(Keys keyData, Point cursor)
    {
        if (restoringSession) return true;
        if ((keyData & Keys.KeyCode) is Keys.A or Keys.S or Keys.D or Keys.G or Keys.H or Keys.J or Keys.K or Keys.L or Keys.Space or Keys.Left or Keys.Right)
            CancelSpeedHold(false);
        bool shift = (keyData & Keys.Modifiers) == Keys.Shift;
        Keys key = keyData & Keys.KeyCode;
        if (keyData == Keys.F1) { SelectPane(Reaction); return true; }
        if (keyData == Keys.F2) { SelectPane(Source); return true; }
        if (keyData == (Keys.Control | Keys.O)) { active.Open(); return true; }
        if ((keyData & Keys.Modifiers) != Keys.None && !shift) return false;
        if (key == Keys.H && !shift) { TriggerCommentaryReplay(); return true; }
        if (key is Keys.A or Keys.S or Keys.D or Keys.G or Keys.J or Keys.L or Keys.K or Keys.Space or Keys.Left or Keys.Right or Keys.Oemcomma or Keys.OemPeriod)
            Replay.Cancel();
        if (key is Keys.J or Keys.L or Keys.K or Keys.Space or Keys.Left or Keys.Right) RevealFullscreen();
        var target = HoverTarget(cursor);
        var speeds = SharedSpeed;
        switch (key)
        {
            case Keys.F when !shift:
            case Keys.F11 when !shift: ToggleFullscreen(); break;
            case Keys.Escape when Fullscreen: ToggleFullscreen(); break;
            case Keys.A: speeds.Toggle("normal", 1); break;
            case Keys.S: speeds.Step(-0.25); break;
            case Keys.D: speeds.Step(0.25); break;
            case Keys.G: speeds.Toggle("favorite", Preferences.Favorite); break;
            case Keys.J:
            case Keys.Left: if (shift) target.Seek(-5); else Master.Jump(-5); break;
            case Keys.L:
            case Keys.Right: if (shift) target.Seek(5); else Master.Jump(5); break;
            case Keys.K:
            case Keys.Space: if (shift) target.TogglePause(); else Master.TogglePause(); break;
            case Keys.Oemcomma when !shift: Master.Nudge(-MasterTransport.OffsetStep); break;
            case Keys.OemPeriod when !shift: Master.Nudge(MasterTransport.OffsetStep); break;
            default: return false;
        }
        if (key is Keys.A or Keys.S or Keys.D or Keys.G) ShowSpeedNotice();
        return true;
    }
    internal bool HandleVolumeWheel(Point screen, int delta)
    {
        var pane = Composition.HitPlayer(screen);
        if (pane == null) { wheelPane = null; wheelRemainder = 0; return false; }
        if (wheelPane != pane) { wheelPane = pane; wheelRemainder = 0; }
        wheelRemainder += delta;
        int steps = wheelRemainder / 120;
        wheelRemainder %= 120;
        if (steps != 0) { pane.AdjustVolume(steps * 5); ShowVolumeNotice(pane); }
        return true;
    }
    public bool PreFilterMessage(ref Message message)
    {
        if (Enabled && Form.ActiveForm == this && Fullscreen && Control.ModifierKeys == Keys.None
            && message.Msg is 0x0201 or 0x0202 or 0x0203)
        {
            long coordinates = message.LParam.ToInt64();
            Point location = new(unchecked((short)(coordinates & 0xffff)), unchecked((short)((coordinates >> 16) & 0xffff)));
            if (ClientToScreen(message.HWnd, ref location) && FullscreenPointer(message.Msg, location, Environment.TickCount64)) return true;
        }
        if (message.Msg != 0x020A || !Enabled || Form.ActiveForm != this || Control.ModifierKeys != Keys.Shift) return false;
        long point = message.LParam.ToInt64();
        Point screen = new(unchecked((short)(point & 0xffff)), unchecked((short)((point >> 16) & 0xffff)));
        int delta = unchecked((short)((message.WParam.ToInt64() >> 16) & 0xffff));
        try { return HandleVolumeWheel(screen, delta); }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Volume"); return true; }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Fullscreen remains reachable after editing crop/size fields.
        if (keyData is Keys.F or Keys.F11 || (keyData == Keys.Escape && Fullscreen))
        {
            ToggleFullscreen(); return true;
        }
        if (compositionBar.ContainsFocus || offsetInput.ContainsFocus || Reaction.TrackMenuOpen || Source.TrackMenuOpen || speedMenu.DropDown.Visible)
            return base.ProcessCmdKey(ref msg, keyData);
        try { if (HandleShortcut(keyData, Cursor.Position)) { UpdateMaster(); return true; } }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Playback error"); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CancelSpeedHold(false);
        SaveOnExit();
        if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
        Application.RemoveMessageFilter(this);
        Replay.Cancel();
        SpeedToast.Dispose();
        timer.Stop(); timer.Dispose();
        Reaction.Shutdown(); Source.Shutdown();
        Icon = null; chosenIcon?.Dispose(); chosenIcon = null;
        base.OnFormClosed(e);
    }
}
