using System.Drawing;

namespace client;

partial class MainForm
{
    private void BuildAuthShell()
    {
        _authShellPanel.Dock = DockStyle.Fill;
        _authShellPanel.Padding = new Padding(28);
        _authShellPanel.Tag = "page";

        var shellLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Tag = "page",
        };
        shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 620));
        shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        shellLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 660));
        shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _authShellPanel.Controls.Add(shellLayout);

        _authCardPanel.Dock = DockStyle.Fill;
        _authCardPanel.Padding = new Padding(24);
        _authCardPanel.BorderStyle = BorderStyle.FixedSingle;
        _authCardPanel.Tag = "surface";
        _authCardPanel.Margin = new Padding(0);
        _authCardPanel.MinimumSize = new Size(620, 660);

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Tag = "surface",
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 232));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _authCardPanel.Controls.Add(cardLayout);

        var heroHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0),
            Tag = "surface",
        };

        _authLogoPictureBox.Dock = DockStyle.Top;
        _authLogoPictureBox.Height = 220;
        _authLogoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _authLogoPictureBox.Margin = new Padding(0);
        _authLogoPictureBox.BackColor = Color.Transparent;
        _authLogoPictureBox.Tag = "surface";
        heroHost.Controls.Add(_authLogoPictureBox);

        var navPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            Tag = "page",
        };
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _showLoginPageButton.Dock = DockStyle.Fill;
        _showLoginPageButton.Text = "Login";
        _showLoginPageButton.Tag = "nav";
        _showLoginPageButton.MinimumSize = new Size(0, 48);
        _showLoginPageButton.Margin = new Padding(0, 0, 6, 0);

        _showRegisterPageButton.Dock = DockStyle.Fill;
        _showRegisterPageButton.Text = "Register";
        _showRegisterPageButton.Tag = "nav";
        _showRegisterPageButton.MinimumSize = new Size(0, 48);
        _showRegisterPageButton.Margin = new Padding(6, 0, 0, 0);

        navPanel.Controls.Add(_showLoginPageButton, 0, 0);
        navPanel.Controls.Add(_showRegisterPageButton, 1, 0);

        _authPageHostPanel.Dock = DockStyle.Fill;
        _authPageHostPanel.Tag = "surface";
        _authPageHostPanel.Padding = new Padding(6, 4, 6, 8);
        _authPageHostPanel.Controls.Add(BuildRegisterPage());
        _authPageHostPanel.Controls.Add(BuildLoginPage());

        cardLayout.Controls.Add(heroHost, 0, 0);
        cardLayout.Controls.Add(navPanel, 0, 1);
        cardLayout.Controls.Add(_authPageHostPanel, 0, 2);

        shellLayout.Controls.Add(_authCardPanel, 1, 1);
    }

    private Control BuildLoginPage()
    {
        _loginPagePanel.Dock = DockStyle.Fill;
        _loginPagePanel.Tag = "surface";
        _loginPagePanel.AutoScroll = false;

        var table = CreateFormTable();
        table.Dock = DockStyle.Fill;
        _loginPagePanel.Controls.Add(table);

        _usernameTextBox.PlaceholderText = "Username";
        _passwordTextBox.PlaceholderText = "Password";

        AddField(table, 0, "Username", ConfigureTextBox(_usernameTextBox, string.Empty));
        AddField(table, 1, "Password", ConfigurePasswordBox(_passwordTextBox, _loginPasswordToggleButton));

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_loginButton, "Login", "action");
        buttonRow.Controls.Add(_loginButton);
        table.Controls.Add(buttonRow, 0, 2);

        return _loginPagePanel;
    }

    private Control BuildRegisterPage()
    {
        _registerPagePanel.Dock = DockStyle.Fill;
        _registerPagePanel.Tag = "surface";
        _registerPagePanel.AutoScroll = false;

        var table = CreateFormTable();
        table.Dock = DockStyle.Fill;
        _registerPagePanel.Controls.Add(table);

        _registerUsernameTextBox.PlaceholderText = "Username";
        _registerPasswordTextBox.PlaceholderText = "Password";
        _registerConfirmPasswordTextBox.PlaceholderText = "Confirm password";

        AddField(table, 0, "Username", ConfigureTextBox(_registerUsernameTextBox, string.Empty));
        AddField(table, 1, "Password", ConfigurePasswordBox(_registerPasswordTextBox, _registerPasswordToggleButton));
        AddField(table, 2, "Confirm", ConfigurePasswordBox(_registerConfirmPasswordTextBox, _registerConfirmPasswordToggleButton));

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_registerButton, "Register", "action");
        buttonRow.Controls.Add(_registerButton);
        table.Controls.Add(buttonRow, 0, 3);

        return _registerPagePanel;
    }
}
