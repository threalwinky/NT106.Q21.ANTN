using System.Drawing;
using MaterialSkin;

namespace client;

partial class MainForm
{
    private readonly record struct ThemePalette(
        Color Page,
        Color Surface,
        Color Raised,
        Color Hero,
        Color HeroBorder,
        Color Input,
        Color Border,
        Color Text,
        Color Muted,
        Color Contrast,
        Color ContrastText);

    private ThemePalette CurrentPalette =>
        IsDarkTheme
            ? new ThemePalette(
                Page: Color.FromArgb(26, 32, 39),
                Surface: Color.FromArgb(33, 42, 52),
                Raised: Color.FromArgb(40, 50, 61),
                Hero: Color.FromArgb(51, 92, 130),
                HeroBorder: Color.FromArgb(83, 135, 182),
                Input: Color.FromArgb(44, 54, 66),
                Border: Color.FromArgb(78, 91, 104),
                Text: Color.FromArgb(244, 247, 250),
                Muted: Color.FromArgb(184, 194, 204),
                Contrast: Color.FromArgb(129, 212, 250),
                ContrastText: Color.FromArgb(22, 28, 34))
            : new ThemePalette(
                Page: Color.FromArgb(246, 248, 250),
                Surface: Color.FromArgb(255, 255, 255),
                Raised: Color.FromArgb(248, 250, 252),
                Hero: Color.FromArgb(56, 106, 163),
                HeroBorder: Color.FromArgb(92, 136, 190),
                Input: Color.FromArgb(251, 253, 255),
                Border: Color.FromArgb(188, 199, 209),
                Text: Color.FromArgb(33, 43, 54),
                Muted: Color.FromArgb(95, 109, 121),
                Contrast: Color.FromArgb(55, 71, 79),
                ContrastText: Color.FromArgb(255, 255, 255));

    private void InitializeMaterialSkin()
    {
        _materialSkinManager.AddFormToManage(this);
    }

    private void ApplyTheme()
    {
        SuspendLayout();

        try
        {
            var palette = CurrentPalette;

            _materialSkinManager.Theme = IsDarkTheme
                ? MaterialSkinManager.Themes.DARK
                : MaterialSkinManager.Themes.LIGHT;
            _materialSkinManager.ColorScheme = IsDarkTheme
                ? new ColorScheme(Primary.BlueGrey800, Primary.BlueGrey900, Primary.BlueGrey500, Accent.LightBlue200, TextShade.WHITE)
                : new ColorScheme(Primary.BlueGrey800, Primary.BlueGrey900, Primary.BlueGrey500, Accent.LightBlue200, TextShade.WHITE);

            BackColor = palette.Page;
            ForeColor = palette.Text;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            ApplyThemeRecursive(this, palette);
            UpdateThemeToggleText();
            UpdateShellStateUi();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void ApplyThemeRecursive(Control control, ThemePalette palette)
    {
        if (ReferenceEquals(control, this))
        {
            control.BackColor = palette.Page;
            control.ForeColor = palette.Text;
        }
        else
        switch (control)
        {
            case Button button:
                StyleButton(button, palette, button.Tag as string);
                break;
            case CheckBox checkBox:
                StyleToggle(checkBox, palette);
                break;
            case TextBox textBox:
                StyleTextInput(textBox, palette);
                break;
            case ComboBox comboBox:
                StyleComboInput(comboBox, palette);
                break;
            case Panel panel when string.Equals(panel.Tag as string, "input_border", StringComparison.Ordinal):
                panel.BackColor = palette.Border;
                panel.ForeColor = palette.Text;
                break;
            case Panel panel when string.Equals(panel.Tag as string, "input_host", StringComparison.Ordinal):
                panel.BackColor = palette.Input;
                panel.ForeColor = palette.Text;
                break;
            case TableLayoutPanel tableLayoutPanel when string.Equals(tableLayoutPanel.Tag as string, "input_host_layout", StringComparison.Ordinal):
                tableLayoutPanel.BackColor = palette.Input;
                tableLayoutPanel.ForeColor = palette.Text;
                break;
            case Panel panel when string.Equals(panel.Tag as string, "hero", StringComparison.Ordinal):
                panel.BackColor = palette.Hero;
                panel.ForeColor = Color.White;
                break;
            case ListBox listBox:
                StyleListBox(listBox, palette);
                break;
            case Label label:
                StyleLabel(label, palette);
                break;
            case PictureBox pictureBox:
                pictureBox.BackColor = ResolveBackColor(pictureBox.Tag as string, palette);
                pictureBox.ForeColor = palette.Text;
                break;
            case Splitter splitter:
                splitter.BackColor = palette.Border;
                splitter.ForeColor = palette.Border;
                break;
            default:
                control.BackColor = ResolveBackColor(control.Tag as string, palette);
                control.ForeColor = palette.Text;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeRecursive(child, palette);
        }
    }

    private void StyleTextInput(TextBox textBox, ThemePalette palette)
    {
        var interactive = IsTextInputInteractive(textBox);
        textBox.BackColor = interactive ? palette.Input : palette.Surface;
        textBox.ForeColor = interactive ? palette.Text : palette.Muted;
        textBox.BorderStyle = BorderStyle.None;
        textBox.Cursor = interactive ? Cursors.IBeam : Cursors.Default;
    }

    private static void StyleComboInput(ComboBox comboBox, ThemePalette palette)
    {
        comboBox.BackColor = palette.Input;
        comboBox.ForeColor = palette.Text;
        comboBox.FlatStyle = FlatStyle.Flat;
    }

    private static void StyleLabel(Label label, ThemePalette palette)
    {
        label.ForeColor = ResolveForeColor(label.Tag as string, palette);

        if (string.Equals(label.Tag as string, "status", StringComparison.Ordinal))
        {
            label.BackColor = palette.Raised;
            label.Padding = new Padding(12, 0, 12, 0);
            label.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (string.Equals(label.Tag as string, "hero_badge", StringComparison.Ordinal))
        {
            label.BackColor = Color.FromArgb(225, 238, 250);
            label.ForeColor = palette.Hero;
            label.BorderStyle = BorderStyle.None;
        }
        else
        {
            label.BackColor = label.Parent?.BackColor ?? ResolveBackColor(label.Tag as string, palette);
            label.BorderStyle = BorderStyle.None;
        }
    }

    private static void StyleListBox(ListBox listBox, ThemePalette palette)
    {
        listBox.BackColor = palette.Raised;
        listBox.ForeColor = palette.Text;
        listBox.BorderStyle = BorderStyle.None;
        listBox.IntegralHeight = false;
    }

    private static void StyleToggle(CheckBox checkBox, ThemePalette palette)
    {
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.FlatAppearance.BorderSize = 1;
        checkBox.FlatAppearance.BorderColor = palette.Border;
        checkBox.FlatAppearance.CheckedBackColor = palette.Contrast;
        checkBox.BackColor = checkBox.Checked ? palette.Contrast : palette.Raised;
        checkBox.ForeColor = checkBox.Checked ? palette.ContrastText : palette.Text;
        checkBox.Padding = Padding.Empty;
        checkBox.TextAlign = ContentAlignment.MiddleCenter;
        checkBox.UseCompatibleTextRendering = false;
        checkBox.UseVisualStyleBackColor = false;
    }

    private void StyleButton(Button button, ThemePalette palette, string? kind, bool isActive = false)
    {
        var interactive = IsButtonInteractive(button);

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = palette.Border;
        button.Padding = Padding.Empty;
        button.UseVisualStyleBackColor = false;
        button.UseCompatibleTextRendering = false;
        button.TextAlign = ContentAlignment.MiddleCenter;

        var isToggled = isActive && (string.Equals(kind, "nav", StringComparison.Ordinal) || string.Equals(kind, "toolbar", StringComparison.Ordinal));
        var isAction = string.Equals(kind, "action", StringComparison.Ordinal);

        if (!interactive)
        {
            button.BackColor = isAction ? palette.Raised : palette.Surface;
            button.ForeColor = palette.Muted;
            button.FlatAppearance.BorderColor = palette.Border;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.FlatAppearance.MouseDownBackColor = button.BackColor;
            button.Cursor = Cursors.Default;
            return;
        }

        button.FlatAppearance.MouseOverBackColor = isToggled ? palette.Contrast : palette.Raised;
        button.FlatAppearance.MouseDownBackColor = palette.Raised;
        button.BackColor = isToggled ? palette.Contrast : (isAction ? palette.Raised : palette.Surface);
        button.ForeColor = isToggled ? palette.ContrastText : palette.Text;
        button.FlatAppearance.BorderColor = isToggled ? palette.Contrast : palette.Border;
        button.Cursor = Cursors.Hand;
    }

    private static Color ResolveBackColor(string? tag, ThemePalette palette)
    {
        return tag switch
        {
            "page" => palette.Page,
            "surface" => palette.Surface,
            "raised" => palette.Raised,
            "hero" => palette.Hero,
            "input" => palette.Input,
            "input_border" => palette.Border,
            "input_host" => palette.Input,
            "input_host_layout" => palette.Input,
            "list" => palette.Raised,
            "screen" => palette.Raised,
            "status" => palette.Raised,
            _ => palette.Surface,
        };
    }

    private static Color ResolveForeColor(string? tag, ThemePalette palette)
    {
        return tag switch
        {
            "subtitle" => palette.Muted,
            "caption" => palette.Muted,
            "hero_title" => Color.White,
            "hero_subtitle" => Color.FromArgb(236, 242, 248),
            "hero_badge" => palette.Hero,
            "accent_title" => palette.Hero,
            "status" => palette.Text,
            _ => palette.Text,
        };
    }
}
