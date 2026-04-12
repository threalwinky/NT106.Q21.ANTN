using System.Drawing;

namespace client;

partial class MainForm
{
    private Control ConfigureModeComboBox()
    {
        _modeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeComboBox.Items.AddRange(new object[] { "LAN", "Internet" });
        _modeComboBox.SelectedIndex = 1;
        return CreateComboInputHost(_modeComboBox);
    }

    private Control ConfigureRoleComboBox()
    {
        _joinRoleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _joinRoleComboBox.Items.AddRange(new object[] { "Controller", "Viewer" });
        _joinRoleComboBox.SelectedIndex = 0;
        return CreateComboInputHost(_joinRoleComboBox);
    }

    private Control ConfigureTextBox(TextBox textBox, string value)
    {
        textBox.Text = value;
        return CreateTextInputHost(textBox);
    }

    private Control ConfigureRoomIdTextBox()
    {
        _roomIdTextBox.PlaceholderText = "Enter the room hash shared by the host";
        return CreateTextInputHost(_roomIdTextBox);
    }

    private Control ConfigureLanMainServerTextBox()
    {
        _lanMainServerTextBox.PlaceholderText = "ws://192.168.131.1/ws";
        return CreateTextInputHost(_lanMainServerTextBox);
    }

    private Control ConfigurePasswordBox(TextBox textBox, Button toggleButton)
    {
        textBox.UseSystemPasswordChar = true;
        return CreatePasswordInputHost(textBox, toggleButton);
    }

    private CheckBox ConfigureThemeToggleCheckBox()
    {
        _darkThemeCheckBox.Dock = DockStyle.Fill;
        _darkThemeCheckBox.Appearance = Appearance.Button;
        _darkThemeCheckBox.TextAlign = ContentAlignment.MiddleCenter;
        _darkThemeCheckBox.Height = 40;
        _darkThemeCheckBox.MinimumSize = new Size(0, 40);
        _darkThemeCheckBox.Padding = Padding.Empty;
        _darkThemeCheckBox.UseCompatibleTextRendering = false;
        _darkThemeCheckBox.Tag = "toggle";
        UpdateThemeToggleText();
        return _darkThemeCheckBox;
    }

    private Label ConfigureStatusLabel(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.AutoEllipsis = true;
        label.Text = "Not set";
        label.Height = 40;
        label.MinimumSize = new Size(0, 40);
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Tag = "status";
        return label;
    }

    private static Label ConfigureStaticStatusLabel(string value)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Text = value,
            Height = 40,
            MinimumSize = new Size(0, 40),
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = "status",
        };
    }

    private static void ConfigureActionButton(Button button, string text, string kind)
    {
        button.Text = text;
        button.AutoSize = false;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Size = new Size(108, 40);
        button.MinimumSize = new Size(108, 40);
        button.Padding = Padding.Empty;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseCompatibleTextRendering = false;
        button.Tag = kind;
    }

    private static void ConfigureToolbarButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = false;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Size = new Size(104, 40);
        button.MinimumSize = new Size(104, 40);
        button.Padding = Padding.Empty;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseCompatibleTextRendering = false;
        button.Tag = "toolbar";
    }

    private static void ConfigureIconButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = false;
        button.Size = new Size(32, 32);
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        button.Padding = Padding.Empty;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseCompatibleTextRendering = false;
        button.Tag = "toolbar";
    }

    private static Control CreateTextInputHost(TextBox textBox)
    {
        var borderHost = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(1),
            Margin = new Padding(0),
            Tag = "input_border",
        };

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 8),
            Tag = "input_host",
        };

        textBox.Dock = DockStyle.Fill;
        textBox.AutoSize = false;
        textBox.Height = 20;
        textBox.MinimumSize = new Size(0, 20);
        textBox.Margin = new Padding(0);
        textBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        textBox.BorderStyle = BorderStyle.None;
        textBox.Tag = "input_inner";
        host.Controls.Add(textBox);
        borderHost.Controls.Add(host);
        return borderHost;
    }

    private static Control CreatePasswordInputHost(TextBox textBox, Button toggleButton)
    {
        var borderHost = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(1),
            Margin = new Padding(0),
            Tag = "input_border",
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "input_host_layout",
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        toggleButton.Dock = DockStyle.Fill;
        toggleButton.Margin = new Padding(8, 0, 0, 0);
        toggleButton.Text = "Show";
        toggleButton.Tag = "toolbar";
        toggleButton.Click -= HandlePasswordToggleClick;
        toggleButton.Click += HandlePasswordToggleClick;

        textBox.Dock = DockStyle.Fill;
        textBox.AutoSize = false;
        textBox.Height = 28;
        textBox.MinimumSize = new Size(0, 28);
        textBox.Margin = new Padding(12, 10, 0, 8);
        textBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        textBox.BorderStyle = BorderStyle.None;
        textBox.Tag = toggleButton;

        layout.Controls.Add(textBox, 0, 0);
        layout.Controls.Add(toggleButton, 1, 0);
        borderHost.Controls.Add(layout);
        return borderHost;
    }

    private static Control CreateComboInputHost(ComboBox comboBox)
    {
        var borderHost = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(1),
            Margin = new Padding(0),
            Tag = "input_border",
        };

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 7, 10, 5),
            Tag = "input_host",
        };

        comboBox.Dock = DockStyle.Fill;
        comboBox.Height = 24;
        comboBox.MinimumSize = new Size(0, 24);
        comboBox.Margin = new Padding(0);
        comboBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        comboBox.IntegralHeight = false;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.Tag = "input_inner";
        host.Controls.Add(comboBox);
        borderHost.Controls.Add(host);
        return borderHost;
    }

    private static void HandlePasswordToggleClick(object? sender, EventArgs e)
    {
        if (sender is not Button toggleButton || toggleButton.Parent is null)
        {
            return;
        }

        var textBox = toggleButton.Parent.Controls.OfType<TextBox>().FirstOrDefault();
        if (textBox is null)
        {
            return;
        }

        textBox.UseSystemPasswordChar = !textBox.UseSystemPasswordChar;
        toggleButton.Text = textBox.UseSystemPasswordChar ? "Show" : "Hide";
    }
}
