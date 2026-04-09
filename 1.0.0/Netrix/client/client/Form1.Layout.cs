using System.Drawing;

namespace client;

partial class Form1
{
    private void BuildLayout()
    {
        Text = "Netrix";
        MinimumSize = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(rootLayout);

        var topLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
        };
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.Controls.Add(topLayout, 0, 0);

        topLayout.Controls.Add(BuildConnectionGroup(), 0, 0);
        topLayout.Controls.Add(BuildInternetGroup(), 1, 0);
        topLayout.Controls.Add(BuildRoomGroup(), 2, 0);

        var contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 850,
            Orientation = Orientation.Vertical,
        };
        rootLayout.Controls.Add(contentSplit, 0, 1);

        contentSplit.Panel1.Controls.Add(BuildRemoteScreenPanel());
        contentSplit.Panel2.Controls.Add(BuildSidebarPanel());
    }

    private Control BuildConnectionGroup()
    {
        var group = new GroupBox
        {
            Text = "Connection",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 8, 8),
            MinimumSize = new Size(0, 190),
        };

        var table = CreateFormTable();
        AddField(table, 0, "Mode", ConfigureModeComboBox());
        AddField(table, 1, "Display Name", ConfigureTextBox(_displayNameTextBox, "Host-01"));
        AddField(table, 2, "LAN WebSocket", ConfigureTextBox(_lanServerUrlTextBox, "ws://127.0.0.1:8000/ws"));
        AddField(table, 3, "Status", ConfigureStatusLabel(_statusLabel));

        group.Controls.Add(table);
        return group;
    }

    private Control BuildInternetGroup()
    {
        var group = new GroupBox
        {
            Text = "Internet Flow",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 8, 8),
            MinimumSize = new Size(0, 190),
        };

        var table = CreateFormTable();
        AddField(table, 0, "Auth URL", ConfigureTextBox(_authUrlTextBox, "http://127.0.0.1:8001"));
        AddField(table, 1, "LB URL", ConfigureTextBox(_loadBalancerUrlTextBox, "http://127.0.0.1:8002"));
        AddField(table, 2, "Username", ConfigureTextBox(_usernameTextBox, "netrix_user"));
        AddField(table, 3, "Password", ConfigurePasswordBox(_passwordTextBox));
        AddField(table, 4, "Auth State", ConfigureStatusLabel(_authStatusLabel));

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        _registerButton.Text = "Register";
        _registerButton.AutoSize = true;
        _loginButton.Text = "Login";
        _loginButton.AutoSize = true;
        buttonRow.Controls.Add(_registerButton);
        buttonRow.Controls.Add(_loginButton);

        table.Controls.Add(buttonRow, 0, 5);
        table.SetColumnSpan(buttonRow, 2);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildRoomGroup()
    {
        var group = new GroupBox
        {
            Text = "Room Session",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 8),
            MinimumSize = new Size(0, 190),
        };

        var table = CreateFormTable();
        AddField(table, 0, "Room ID", ConfigureRoomIdTextBox());
        AddField(table, 1, "Room Password", ConfigurePasswordBox(_roomPasswordTextBox));
        AddField(table, 2, "Join As", ConfigureRoleComboBox());
        AddField(table, 3, "Room State", ConfigureStatusLabel(_roomStatusLabel));

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        _createRoomButton.Text = "Create Room";
        _createRoomButton.AutoSize = true;
        _joinRoomButton.Text = "Join Room";
        _joinRoomButton.AutoSize = true;
        _disconnectButton.Text = "Disconnect";
        _disconnectButton.AutoSize = true;

        buttonRow.Controls.Add(_createRoomButton);
        buttonRow.Controls.Add(_joinRoomButton);
        buttonRow.Controls.Add(_disconnectButton);

        table.Controls.Add(buttonRow, 0, 4);
        table.SetColumnSpan(buttonRow, 2);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildRemoteScreenPanel()
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 8, 0),
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "Remote Screen",
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _remoteScreenBox.Dock = DockStyle.Fill;
        _remoteScreenBox.BackColor = Color.FromArgb(24, 24, 28);
        _remoteScreenBox.BorderStyle = BorderStyle.FixedSingle;
        _remoteScreenBox.SizeMode = PictureBoxSizeMode.Zoom;
        _remoteScreenBox.TabStop = true;

        var hintLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Text = "Controller mode: click the screen first, then use mouse and keyboard.",
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        container.Controls.Add(_remoteScreenBox);
        container.Controls.Add(hintLabel);
        container.Controls.Add(titleLabel);
        return container;
    }

    private Control BuildSidebarPanel()
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 65));

        var participantsGroup = new GroupBox
        {
            Text = "Participants",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };
        _participantsListBox.Dock = DockStyle.Fill;
        participantsGroup.Controls.Add(_participantsListBox);

        var chatGroup = new GroupBox
        {
            Text = "Chat",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };

        var chatLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _chatListBox.Dock = DockStyle.Fill;

        var chatInputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
        };
        chatInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        chatInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _chatInputTextBox.Dock = DockStyle.Fill;
        _sendChatButton.Text = "Send";
        _sendChatButton.AutoSize = true;

        chatInputLayout.Controls.Add(_chatInputTextBox, 0, 0);
        chatInputLayout.Controls.Add(_sendChatButton, 1, 0);

        chatLayout.Controls.Add(_chatListBox, 0, 0);
        chatLayout.Controls.Add(chatInputLayout, 0, 1);
        chatGroup.Controls.Add(chatLayout);

        container.Controls.Add(participantsGroup, 0, 0);
        container.Controls.Add(chatGroup, 0, 1);
        return container;
    }

    private static TableLayoutPanel CreateFormTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddField(TableLayoutPanel table, int rowIndex, string label, Control control)
    {
        table.RowCount = rowIndex + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(
            new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 8, 8),
            },
            0,
            rowIndex);
        table.Controls.Add(control, 1, rowIndex);
    }

    private ComboBox ConfigureModeComboBox()
    {
        _modeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeComboBox.Items.AddRange(new object[] { "LAN", "Internet" });
        _modeComboBox.SelectedIndex = 0;
        _modeComboBox.Dock = DockStyle.Fill;
        return _modeComboBox;
    }

    private ComboBox ConfigureRoleComboBox()
    {
        _joinRoleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _joinRoleComboBox.Items.AddRange(new object[] { "Controller", "Viewer" });
        _joinRoleComboBox.SelectedIndex = 0;
        _joinRoleComboBox.Dock = DockStyle.Fill;
        return _joinRoleComboBox;
    }

    private static TextBox ConfigureTextBox(TextBox textBox, string value)
    {
        textBox.Text = value;
        textBox.Dock = DockStyle.Fill;
        return textBox;
    }

    private TextBox ConfigureRoomIdTextBox()
    {
        _roomIdTextBox.Dock = DockStyle.Fill;
        _roomIdTextBox.PlaceholderText = "Generated after create or enter shared room id";
        return _roomIdTextBox;
    }

    private static TextBox ConfigurePasswordBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.UseSystemPasswordChar = true;
        return textBox;
    }

    private static Label ConfigureStatusLabel(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.AutoEllipsis = true;
        label.Text = "Not set";
        return label;
    }
}
