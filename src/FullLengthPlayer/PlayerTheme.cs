namespace FullLengthPlayer;

internal static class PlayerTheme
{
    internal static readonly Color Background = Color.FromArgb(17, 21, 27);
    internal static readonly Color Surface = Color.FromArgb(26, 32, 40);
    internal static readonly Color Raised = Color.FromArgb(38, 47, 58);
    internal static readonly Color Ink = Color.FromArgb(233, 239, 245);
    internal static readonly Color Muted = Color.FromArgb(157, 173, 189);
    internal static readonly Color Orange = Color.FromArgb(255, 166, 64);
    internal static readonly Color Accent = Color.FromArgb(66, 210, 195);
    internal static void Apply(Control control)
    {
        control.BackColor = Surface; control.ForeColor = Ink;
        if (control is Button b)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.BackColor = Raised; b.UseVisualStyleBackColor = false;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(49, 66, 77);
            b.Cursor = Cursors.Hand;
        }
        if (control is TextBox text) { text.BorderStyle = BorderStyle.FixedSingle; text.BackColor = Raised; }
        if (control is ComboBox box) { box.FlatStyle = FlatStyle.Flat; box.BackColor = Raised; }
        if (control is NumericUpDown number) { number.BorderStyle = BorderStyle.FixedSingle; number.BackColor = Raised; }
        if (control is ToolStrip strip)
        {
            strip.Renderer = new Renderer(); strip.GripStyle = ToolStripGripStyle.Hidden;
            strip.Padding = new Padding(4, 3, 4, 3);
            foreach (ToolStripItem item in strip.Items) item.Padding = new Padding(3, 3, 3, 3);
        }
        foreach (Control child in control.Controls) Apply(child);
    }
    internal static void Dialog(Form form)
    {
        form.Font = new Font("Segoe UI", 9f); Apply(form);
        foreach (Button button in Descendants(form).OfType<Button>())
            if (button.DialogResult == DialogResult.OK || button.Text is "Analyze audio" or "Open stream") { button.BackColor = Accent; button.ForeColor = Background; }
    }
    private static IEnumerable<Control> Descendants(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (Control nested in Descendants(child)) yield return nested; } }
    private sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuItemSelected => Raised;
        public override Color MenuItemBorder => Accent;
        public override Color MenuBorder => Raised;
        public override Color ButtonSelectedHighlight => Raised;
        public override Color ButtonSelectedGradientBegin => Raised;
        public override Color ButtonSelectedGradientMiddle => Raised;
        public override Color ButtonSelectedGradientEnd => Raised;
        public override Color ButtonPressedGradientBegin => Raised;
        public override Color ButtonPressedGradientMiddle => Raised;
        public override Color ButtonPressedGradientEnd => Raised;
        public override Color MenuItemSelectedGradientBegin => Raised;
        public override Color MenuItemSelectedGradientEnd => Raised;
        public override Color MenuItemPressedGradientBegin => Raised;
        public override Color MenuItemPressedGradientMiddle => Raised;
        public override Color MenuItemPressedGradientEnd => Raised;
        public override Color CheckBackground => Raised;
        public override Color CheckSelectedBackground => Raised;
        public override Color SeparatorDark => Raised;
        public override Color SeparatorLight => Raised;
    }
    private sealed class Renderer : ToolStripProfessionalRenderer
    {
        internal Renderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Surface);
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        { e.TextColor = e.Item.Enabled ? Ink : Muted; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        { e.ArrowColor = Ink; base.OnRenderArrow(e); }
    }
}
