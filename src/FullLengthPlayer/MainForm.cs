namespace FullLengthPlayer;

internal sealed class MainForm : Form
{
    internal PlayerPane Reaction { get; } = new("Reaction A");
    internal PlayerPane Source { get; } = new("Source B");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    private PlayerPane active;
    internal MasterTransport Master { get; }
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
    private readonly FlowLayoutPanel syncBar = new() { Dock = DockStyle.Top, Height = 36, AutoScroll = true, WrapContents = false, BackColor = SystemColors.Control };
    private readonly NumericUpDown offsetInput = new() { DecimalPlaces = 2, Increment = 0.05m, Minimum = -604800, Maximum = 604800, Width = 110 };
    private readonly Label syncStatus = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private double displayedOffset = double.NaN;
    public MainForm()
    {
        Text = "Full-Length Player";
        BackColor = Color.Black;
        ClientSize = new Size(1280, 650);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        active = Reaction;
        Master = new MasterTransport(() => Reaction.Player, () => Source.Player);
        SharedSpeed = new SpeedControl(() => Reaction.Player?.Number("speed") ?? 1, value =>
        {
            Master.SetSpeed(value);
        });
        Reaction.MediaReplaced += SharedSpeed.Reset;
        Source.MediaReplaced += SharedSpeed.Reset;
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
        syncBar.Controls.Add(syncStatus);
        masterBar.Items.Add(new ToolStripLabel("BOTH PLAYERS"));
        void MasterButton(string text, Action action)
        {
            var button = new ToolStripButton(text);
            button.Click += (_, _) => RunMaster(action);
            masterBar.Items.Add(button);
        }
        MasterButton("Play / Pause both", Master.TogglePause);
        MasterButton("−10 s both", () => Master.Jump(-10));
        MasterButton("+10 s both", () => Master.Jump(10));
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
        UpdateMaster();
        Reaction.Surface.MouseDown += (_, _) => ActiveControl = null;
        Source.Surface.MouseDown += (_, _) => ActiveControl = null;
        Reaction.Activated += SelectPane;
        Source.Activated += SelectPane;
        SelectPane(Reaction);
        timer.Tick += (_, _) =>
        {
            try { Master.Tick(); } catch (Exception e) { Master.Unlock("Sync stopped: " + e.Message); }
            if (Environment.TickCount64 < nextUiUpdate) return;
            nextUiUpdate = Environment.TickCount64 + 200;
            Reaction.UpdatePlayback(); Source.UpdatePlayback(); Composition.Arrange(); UpdateMaster();
        };
        Shown += async (_, _) =>
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool verify = args.Length == 3 && args[0] == "--verify-playback";
            try
            {
                Reaction.Initialize(verify);
                Source.Initialize(verify);
                timer.Start();
                if (verify) await PlaybackVerification.Run(this, args[1], args[2]);
                else
                {
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
    private void BuildCompositionControls()
    {
        void Choice(string label, string[] choices, Action<int> change)
        {
            compositionBar.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            box.Items.AddRange(choices); box.SelectedIndex = 0;
            box.SelectedIndexChanged += (_, _) => { change(box.SelectedIndex); Composition.Arrange(); };
            compositionBar.Controls.Add(box);
        }
        Choice("Canvas", new[] { "16:9", "4:3", "16:10" }, i => Composition.CanvasAspect = i switch { 0 => 16.0 / 9, 1 => 4.0 / 3, _ => 16.0 / 10 });
        Choice("Source edge", new[] { "Bottom", "Top" }, i => Composition.TopAnchor = i == 1);
        NumericUpDown Number(string label, decimal value, decimal min, decimal max, Action<double> change)
        {
            compositionBar.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            var input = new NumericUpDown { Width = 55, Minimum = min, Maximum = max, Value = value };
            input.ValueChanged += (_, _) => { change((double)input.Value / 100); Composition.Arrange(); };
            compositionBar.Controls.Add(input); return input;
        }
        var size = Number("Source %", 70, 15, 100, v => Composition.SourceFraction = v);
        Composition.ResizedSource += () => size.Value = Math.Clamp((decimal)Math.Round(Composition.SourceFraction * 100), 15, 100);
        Number("Crop top %", 20, 0, 45, v => Composition.CropTop = v);
        Number("Crop bottom %", 0, 0, 45, v => Composition.CropBottom = v);
        Number("Reaction zoom %", 100, 50, 200, v => Composition.Zoom = v);
        Number("Pan X %", 0, -100, 100, v => Composition.PanX = v);
        Number("Pan Y %", 0, -100, 100, v => Composition.PanY = v);
        var full = new Button { Text = "Fullscreen", AutoSize = true };
        full.Click += (_, _) => ToggleFullscreen(); compositionBar.Controls.Add(full);
    }
    internal void ToggleFullscreen()
    {
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
        foreach (Control control in new Control[] { masterBar, syncBar, masterStatus, masterTimeline, compositionBar, playerControls, info }) control.Visible = !Fullscreen;
        Composition.ShowHandles = !Fullscreen;
        ActiveControl = null;
        ResumeLayout(true); Composition.Arrange();
    }
    private void RunMaster(Action action)
    {
        try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Shared playback error"); }
        UpdateMaster();
    }
    private void SeekMasterTimeline()
    {
        var position = Master.Snapshot();
        if (position != null) RunMaster(() => Master.SeekReaction(position.ADuration * masterTimeline.Value / 10000.0));
    }
    private void UpdateMaster()
    {
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
        if (!masterDragging) masterTimeline.Value = (int)Math.Clamp(position.ATime / position.ADuration * 10000, 0, 10000);
        masterStatus.Text = $"Shared timeline (Reaction A): {TimeSpan.FromSeconds(position.ATime):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(position.ADuration):hh\\:mm\\:ss} • Seeks move both equally, stopping at either file’s boundary";
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
        bool shift = (keyData & Keys.Modifiers) == Keys.Shift;
        Keys key = keyData & Keys.KeyCode;
        if (keyData == Keys.F1) { SelectPane(Reaction); return true; }
        if (keyData == Keys.F2) { SelectPane(Source); return true; }
        if (keyData == (Keys.Control | Keys.O)) { active.Open(); return true; }
        if ((keyData & Keys.Modifiers) != Keys.None && !shift) return false;
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
        return true;
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
        timer.Stop(); timer.Dispose();
        Reaction.Shutdown(); Source.Shutdown();
        base.OnFormClosed(e);
    }
}
