namespace FullLengthPlayer;

internal sealed partial class MainForm
{
    private readonly Panel appHeader = new() { Dock = DockStyle.Top, Height = 52 };
    private readonly Panel transportPanel = new() { Dock = DockStyle.Bottom, Height = 106 };
    private readonly Panel inspector = new() { Dock = DockStyle.Right, Width = 352, Padding = new Padding(12) };
    private readonly Panel inspectorPages = new() { Dock = DockStyle.Fill };
    private readonly ToolStripButton setupButton = new("Setup") { CheckOnClick = true, Checked = true };
    private readonly List<Button> setupTabs = new();
    private readonly List<Control> setupPages = new();
    private bool setupWanted = true;
    private int setupPage;
    private void BuildModernUi()
    {
        SuspendLayout();
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(1280, 800);
        BackColor = PlayerTheme.Background;
        // Reparent the controls only. Native MPV windows stay in the composition.
        foreach (Control c in new Control[] { playerControls, compositionBar, info, masterTimeline, masterStatus, syncBar, masterBar, sessionBar }) Controls.Remove(c);
        playerControls.Controls.Clear();
        var sessionMenu = new ToolStripDropDownButton("Session");
        foreach (ToolStripItem item in sessionBar.Items.Cast<ToolStripItem>().ToArray()) sessionMenu.DropDownItems.Add(item);
        sessionBar.Items.Add(sessionMenu);
        sessionBar.Items.Add(new ToolStripSeparator());
        setupButton.CheckedChanged += (_, _) => { setupWanted = setupButton.Checked; LayoutModernChrome(); };
        sessionBar.Items.Add(setupButton);
        var fullscreenButton = new ToolStripButton("Fullscreen  ↗");
        fullscreenButton.Click += (_, _) => ToggleFullscreen();
        sessionBar.Items.Add(fullscreenButton);
        sessionBar.Dock = DockStyle.Fill; sessionBar.AutoSize = false;
        sessionBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        var brand = new Label { Text = "FULL LENGTH PLAYER", Dock = DockStyle.Left, Width = 260, Padding = new Padding(54, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 11f), AutoEllipsis = true };
        brand.Paint += (_, e) => { if (Icon != null) { int size = (int)(32 * DeviceDpi / 96f); e.Graphics.DrawIcon(Icon, new Rectangle(12, (brand.Height - size) / 2, size, size)); } };
        appHeader.Controls.Add(sessionBar); appHeader.Controls.Add(brand);

        // Existing handlers and keyboard mappings remain the source of behavior.
        foreach (ToolStripItem item in masterBar.Items.Cast<ToolStripItem>().ToArray())
        {
            if (item is ToolStripButton b && (b.Text.Contains("0.25") || b.Text.Contains("↔") || b.Text == "Favorite settings"))
                speedMenu.DropDownItems.Add(item);
        }
        masterBar.Items[0].Text = "BOTH";
        masterBar.Items[1].Text = "Play / pause";
        masterBar.Items[2].Text = "−5 s"; masterBar.Items[3].Text = "+5 s";
        masterBar.AutoSize = false; masterBar.Height = 38;
        masterStatus.Height = 24; masterStatus.Padding = new Padding(12, 0, 0, 0);
        masterTimeline.AutoSize = false; masterTimeline.Height = 32;
        transportPanel.Padding = new Padding(8, 6, 8, 4);
        transportPanel.Controls.Add(masterStatus); transportPanel.Controls.Add(masterTimeline); transportPanel.Controls.Add(masterBar);

        var tabs = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        string[] names = { "Media", "Layout", "Sync" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i; tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            var tab = new Button { Text = names[i], Dock = DockStyle.Fill, Margin = new Padding(0, 0, 3, 6), AccessibleName = names[i] + " setup" };
            tab.Click += (_, _) => ShowSetupPage(index); tabs.Controls.Add(tab, i, 0); setupTabs.Add(tab);
        }
        var media = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        Reaction.Dock = Source.Dock = DockStyle.Top;
        Reaction.Height = Source.Height = 246; Reaction.Padding = Source.Padding = new Padding(4);
        media.Controls.Add(Source); media.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 }); media.Controls.Add(Reaction);
        setupPages.Add(media);
        compositionBar.Dock = DockStyle.Fill; compositionBar.WrapContents = false;
        compositionBar.FlowDirection = FlowDirection.TopDown;
        var oldLayout = compositionBar.Controls.Cast<Control>().ToArray();
        compositionBar.Controls.Clear();
        void Group(string title, string[] keys)
        {
            var group = new Panel { Width = 304, Height = 30 + ((keys.Length + 1) / 2) * 58, Margin = new Padding(0, 0, 0, 12) };
            var label = new Label { Text = title, Location = new Point(0, 5), Width = 230, ForeColor = PlayerTheme.Muted };
            var reset = new Button { Text = "Reset", Location = new Point(246, 0), Size = new Size(58, 25), AccessibleName = "Reset " + title };
            reset.Click += (_, _) => ResetLayoutGroup(keys);
            group.Controls.Add(label); group.Controls.Add(reset);
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i]; var input = layoutInputs[key]; int x = (i % 2) * 156, y = 32 + (i / 2) * 58;
                var caption = new Label { Text = key, Location = new Point(x, y), Size = new Size(148, 20) };
                input.Dock = DockStyle.None; input.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                input.Location = new Point(x, y + 21); input.Width = 148;
                group.Controls.Add(caption); group.Controls.Add(input);
            }
            compositionBar.Controls.Add(group);
        }
        Group("Canvas & source", new[] { "Canvas", "Source edge", "Source %" });
        Group("Reaction crop", new[] { "Crop top %", "Crop bottom %" });
        Group("Reaction framing", new[] { "Reaction zoom %", "Pan X %", "Pan Y %" });
        foreach (var unused in oldLayout.Where(c => c.Parent == null)) unused.Dispose();
        compositionBar.Controls.Add(new Label { Text = "Drag a source corner to resize.\nThe reaction anchors to the opposite edge.\nF / F11 to view fullscreen.", Width = 300, Height = 72, Margin = new Padding(0, 12, 0, 0) });
        setupPages.Add(compositionBar);
        syncBar.Dock = DockStyle.Fill; syncBar.FlowDirection = FlowDirection.TopDown; syncBar.WrapContents = false;
        foreach (Control c in syncBar.Controls)
        {
            c.Margin = new Padding(0, 0, 0, 10);
            if (c is Button) { c.AutoSize = false; c.Size = new Size(300, 34); }
        }
        void PairSync(string left, string right, int firstWidth)
        {
            var a = syncBar.Controls.OfType<Button>().Single(c => c.Text == left);
            var b = syncBar.Controls.OfType<Button>().Single(c => c.Text == right);
            int index = syncBar.Controls.GetChildIndex(a);
            var row = new FlowLayoutPanel { Width = 300, Height = 36, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
            a.Size = new Size(firstWidth, 34); b.Size = new Size(294 - firstWidth, 34);
            a.Margin = new Padding(0, 0, 6, 0); b.Margin = Padding.Empty;
            row.Controls.Add(a); row.Controls.Add(b); syncBar.Controls.Add(row); syncBar.Controls.SetChildIndex(row, index);
        }
        PairSync("Lock current alignment", "Unlock", 216);
        PairSync("−0.05 s", "+0.05 s", 147);
        syncStatus.AutoSize = false; syncStatus.Size = new Size(300, 130);
        offsetInput.Width = 180;
        setupPages.Add(syncBar);
        foreach (Control page in setupPages) { inspectorPages.Controls.Add(page); page.Visible = false; }
        inspector.Controls.Add(inspectorPages); inspector.Controls.Add(tabs);
        Controls.Add(inspector); Controls.Add(transportPanel); Controls.Add(appHeader);
        PlayerTheme.Apply(appHeader); PlayerTheme.Apply(transportPanel); PlayerTheme.Apply(inspector);
        masterStatus.ForeColor = PlayerTheme.Muted;
        Reaction.ApplyModernStyle(); Source.ApplyModernStyle();
        masterBar.Items[1].Font = new Font("Segoe UI Semibold", 11f);
        masterBar.Items[1].BackColor = PlayerTheme.Raised;
        masterBar.Items[1].Padding = new Padding(12, 2, 12, 2);
        masterBar.Items[1].ToolTipText = "Play / pause both · K or Space";
        masterBar.Items[2].Text = "↶ 5s"; masterBar.Items[2].ToolTipText = "Back five seconds · J";
        masterBar.Items[3].Text = "5s ↷"; masterBar.Items[3].ToolTipText = "Forward five seconds · L";
        speedMenu.ToolTipText = "Shared speed · A / S / D / G";
        replayButton.ToolTipText = "Commentary replay · H";
        masterTimeline.AccessibleName = "Shared timeline";
        masterTimeline.HoverText = SharedTimelineHint;
        Hud.Timeline.HoverText = SharedTimelineHint;
        ShowSetupPage(0);
        Resize += (_, _) => LayoutModernChrome();
        ResumeLayout(true); LayoutModernChrome();
    }
    internal void ResetLayoutGroup(IEnumerable<string> keys)
    {
        foreach (string key in keys)
        {
            if (layoutInputs[key] is NumericUpDown number) number.Value = key switch { "Source %" => 70, "Reaction zoom %" => 100, _ => 0 };
            else if (layoutInputs[key] is ComboBox choice) choice.SelectedIndex = key == "Canvas" ? ClosestCanvas(Screen.FromControl(this).Bounds.Size) : 0;
        }
        Composition.Arrange();
    }
    internal string SharedTimelineHint(double fraction)
    {
        double time = Math.Clamp(fraction, 0, 1) * Math.Max(0, Master.TimelineEnd - Master.TimelineStart);
        string label = TimeSpan.FromSeconds(time).ToString(@"hh\:mm\:ss");
        if (Master.Locked && Master.Snapshot() is { } p)
        {
            double aTime = time + Master.TimelineStart, bTime = aTime + Master.Offset;
            bool a = aTime >= 0 && aTime <= p.ADuration, b = bTime >= 0 && bTime <= p.BDuration;
            label += a && b ? " · Both videos (orange band)" : a ? " · Reaction only" : " · Source only";
        }
        return label;
    }
    internal void ShowSetupPage(int index)
    {
        setupPage = index;
        for (int i = 0; i < setupPages.Count; i++)
        {
            setupPages[i].Visible = i == index;
            setupTabs[i].BackColor = i == index ? PlayerTheme.Raised : PlayerTheme.Surface;
            setupTabs[i].ForeColor = i == index ? PlayerTheme.Accent : PlayerTheme.Muted;
        }
    }
    private void LayoutModernChrome()
    {
        if (setupPages.Count == 0) return;
        appHeader.Visible = transportPanel.Visible = !Fullscreen;
        // Keep a usable preview in narrow windows. Setup remains explicitly available.
        inspector.Visible = !Fullscreen && setupWanted;
        inspector.Width = Math.Min((int)(352 * DeviceDpi / 96f), Math.Max(260, ClientSize.Width - 180));
        Composition.Arrange();
    }
}
