using System.Drawing;

namespace client;

partial class Form1
{
    private void BuildLayout()
    {
        Text = "Netrix 1.0.1";
        MinimumSize = new Size(1360, 860);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _darkThemeCheckBox.Checked = false;

        _rootShellPanel.Dock = DockStyle.Fill;
        _rootShellPanel.Padding = new Padding(0);
        _rootShellPanel.Tag = "page";
        Controls.Add(_rootShellPanel);

        BuildSidebarShell();
        BuildWorkspaceShell();

        _rootShellPanel.Controls.Add(_workspacePanel);
        _rootShellPanel.Controls.Add(_sidebarSplitter);
        _rootShellPanel.Controls.Add(_sidebarPanel);
    }

    private void BuildSidebarShell()
    {
        _sidebarPanel.Dock = DockStyle.Left;
        _sidebarPanel.Width = 500;
        _sidebarPanel.Padding = new Padding(12);
        _sidebarPanel.Tag = "surface";

        _sidebarSplitter.Dock = DockStyle.Left;
        _sidebarSplitter.Width = 6;
        _sidebarSplitter.MinSize = 380;
        _sidebarSplitter.MinExtra = 620;
        _sidebarSplitter.Tag = "page";

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Tag = "surface",
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sidebarPanel.Controls.Add(sidebarLayout);

        sidebarLayout.Controls.Add(BuildSidebarHeader(), 0, 0);
        sidebarLayout.Controls.Add(BuildSidebarNavigation(), 0, 1);

        _sidebarContentHostPanel.Dock = DockStyle.Fill;
        _sidebarContentHostPanel.Tag = "surface";
        _sidebarContentHostPanel.Controls.Add(BuildSettingsView());
        _sidebarContentHostPanel.Controls.Add(BuildControlView());
        sidebarLayout.Controls.Add(_sidebarContentHostPanel, 0, 2);
    }

    private Control BuildSidebarHeader()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(14),
            Tag = "raised",
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Tag = "raised",
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            Margin = new Padding(0),
            Tag = "raised",
        };

        _appTitleLabel.Dock = DockStyle.Top;
        _appTitleLabel.Height = 28;
        _appTitleLabel.Text = "Netrix 1.0.1";
        _appTitleLabel.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold);
        _appTitleLabel.Tag = "title";

        _appSubtitleLabel.Dock = DockStyle.Top;
        _appSubtitleLabel.Height = 22;
        _appSubtitleLabel.Text = "Remote Console";
        _appSubtitleLabel.Tag = "subtitle";

        ConfigureIconButton(_toggleSidebarButton, "<");
        _toggleSidebarButton.Margin = new Padding(12, 0, 0, 0);

        titleHost.Controls.Add(_appSubtitleLabel);
        titleHost.Controls.Add(_appTitleLabel);
        headerLayout.Controls.Add(titleHost, 0, 0);
        headerLayout.Controls.Add(_toggleSidebarButton, 1, 0);
        headerPanel.Controls.Add(headerLayout);
        return headerPanel;
    }

    private Control BuildSidebarNavigation()
    {
        var navPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Tag = "page",
        };
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _controlViewButton.Dock = DockStyle.Fill;
        _controlViewButton.Text = "Control";
        _controlViewButton.Tag = "nav";

        _settingsViewButton.Dock = DockStyle.Fill;
        _settingsViewButton.Text = "Settings";
        _settingsViewButton.Tag = "nav";

        navPanel.Controls.Add(_controlViewButton, 0, 0);
        navPanel.Controls.Add(_settingsViewButton, 1, 0);
        return navPanel;
    }

    private Control BuildControlView()
    {
        _controlViewPanel.Dock = DockStyle.Fill;
        _controlViewPanel.AutoScroll = true;
        _controlViewPanel.Tag = "page";

        var stack = CreateStackLayout();
        _controlViewPanel.Controls.Add(stack);

        AddCardToStack(stack, BuildSessionCard(), 0);
        AddCardToStack(stack, BuildRoomControlCard(), 1);
        AddCardToStack(stack, BuildParticipantsCard(), 2);
        AddCardToStack(stack, BuildFileTransferCard(), 3);

        return _controlViewPanel;
    }

    private Control BuildSettingsView()
    {
        _settingsViewPanel.Dock = DockStyle.Fill;
        _settingsViewPanel.AutoScroll = true;
        _settingsViewPanel.Tag = "page";

        var stack = CreateStackLayout();
        _settingsViewPanel.Controls.Add(stack);

        AddCardToStack(stack, BuildGeneralSettingsCard(), 0);
        AddCardToStack(stack, BuildInternetSettingsCard(), 1);

        return _settingsViewPanel;
    }

    private Control BuildSessionCard()
    {
        var card = CreateCardPanel("Session");
        var table = CreateFormTable();
        AddField(table, 0, "Status", ConfigureStatusLabel(_statusLabel));
        AddField(table, 1, "Room", ConfigureStatusLabel(_roomStatusLabel));
        card.Controls.Add(table);
        return card;
    }

    private Control BuildRoomControlCard()
    {
        var card = CreateCardPanel("Room Control");
        var table = CreateFormTable();
        AddField(table, 0, "Room ID", ConfigureRoomIdTextBox());
        AddField(table, 1, "Password", ConfigurePasswordBox(_roomPasswordTextBox));
        AddField(table, 2, "Join As", ConfigureRoleComboBox());

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_createRoomButton, "Create", "action");
        ConfigureActionButton(_joinRoomButton, "Join", "action");
        ConfigureActionButton(_disconnectButton, "Disconnect", "action");
        buttonRow.Controls.Add(_createRoomButton);
        buttonRow.Controls.Add(_joinRoomButton);
        buttonRow.Controls.Add(_disconnectButton);

        table.Controls.Add(buttonRow, 0, 3);

        card.Controls.Add(table);
        return card;
    }

    private Control BuildParticipantsCard()
    {
        var card = CreateCardPanel("Participants");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Tag = "surface",
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _participantsTitleLabel.Dock = DockStyle.Top;
        _participantsTitleLabel.Height = 22;
        _participantsTitleLabel.Text = "Connected peers";
        _participantsTitleLabel.Tag = "subtitle";

        _participantsListBox.Dock = DockStyle.Fill;
        _participantsListBox.Height = 220;
        _participantsListBox.Tag = "list";

        layout.Controls.Add(_participantsTitleLabel, 0, 0);
        layout.Controls.Add(_participantsListBox, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildFileTransferCard()
    {
        var card = CreateCardPanel("File Transfer");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Tag = "surface",
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _transferTitleLabel.Dock = DockStyle.Top;
        _transferTitleLabel.Height = 22;
        _transferTitleLabel.Text = "Transfer activity";
        _transferTitleLabel.Tag = "subtitle";

        _transferListBox.Dock = DockStyle.Fill;
        _transferListBox.Height = 180;
        _transferListBox.Tag = "list";

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_sendFileButton, "Send File", "action");
        buttonRow.Controls.Add(_sendFileButton);

        layout.Controls.Add(_transferTitleLabel, 0, 0);
        layout.Controls.Add(_transferListBox, 0, 1);
        layout.Controls.Add(buttonRow, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildGeneralSettingsCard()
    {
        var card = CreateCardPanel("General");
        var table = CreateFormTable();

        AddField(table, 0, "Mode", ConfigureModeComboBox());
        AddField(table, 1, "Display Name", ConfigureTextBox(_displayNameTextBox, "Host-01"));
        AddField(table, 2, "LAN WebSocket", ConfigureTextBox(_lanServerUrlTextBox, "ws://127.0.0.1:8000/ws"));
        AddField(table, 3, "Theme", ConfigureThemeToggleCheckBox());

        card.Controls.Add(table);
        return card;
    }

    private Control BuildInternetSettingsCard()
    {
        var card = CreateCardPanel("Internet");
        var table = CreateFormTable();

        AddField(table, 0, "Auth URL", ConfigureTextBox(_authUrlTextBox, "http://127.0.0.1:8001"));
        AddField(table, 1, "LB URL", ConfigureTextBox(_loadBalancerUrlTextBox, "http://127.0.0.1:8002"));
        AddField(table, 2, "Username", ConfigureTextBox(_usernameTextBox, "netrix_user"));
        AddField(table, 3, "Password", ConfigurePasswordBox(_passwordTextBox));
        AddField(table, 4, "Auth State", ConfigureStatusLabel(_authStatusLabel));

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_registerButton, "Register", "action");
        ConfigureActionButton(_loginButton, "Login", "action");
        buttonRow.Controls.Add(_registerButton);
        buttonRow.Controls.Add(_loginButton);

        table.Controls.Add(buttonRow, 0, 5);

        card.Controls.Add(table);
        return card;
    }

    private void BuildWorkspaceShell()
    {
        _workspacePanel.Dock = DockStyle.Fill;
        _workspacePanel.Padding = new Padding(12);
        _workspacePanel.Tag = "page";

        BuildChatDrawer();
        BuildRemoteHost();

        _workspacePanel.Controls.Add(_remoteHostPanel);
        _workspacePanel.Controls.Add(_chatSplitter);
        _workspacePanel.Controls.Add(_chatDrawerPanel);
    }

    private void BuildRemoteHost()
    {
        _remoteHostPanel.Dock = DockStyle.Fill;
        _remoteHostPanel.Padding = new Padding(0);
        _remoteHostPanel.Tag = "page";

        var remoteLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Tag = "page",
        };
        remoteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        remoteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        remoteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        _remoteHostPanel.Controls.Add(remoteLayout);

        remoteLayout.Controls.Add(BuildRemoteToolbar(), 0, 0);
        remoteLayout.Controls.Add(BuildRemoteCanvas(), 0, 1);
        remoteLayout.Controls.Add(BuildRemoteFooter(), 0, 2);
    }

    private Control BuildRemoteToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12, 12, 12, 12),
            Tag = "surface",
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        ConfigureIconButton(_showSidebarButton, ">");
        _showSidebarButton.Visible = false;
        ConfigureToolbarButton(_toggleChatButton, "Show Chat");
        ConfigureToolbarButton(_toggleFullScreenButton, "Full Screen");

        _toolbarSessionLabel.Dock = DockStyle.Fill;
        _toolbarSessionLabel.Text = "Remote Session";
        _toolbarSessionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _toolbarSessionLabel.AutoEllipsis = true;
        _toolbarSessionLabel.Tag = "title";

        toolbar.Controls.Add(_showSidebarButton, 0, 0);
        toolbar.Controls.Add(_toolbarSessionLabel, 1, 0);
        toolbar.Controls.Add(_toggleChatButton, 2, 0);
        toolbar.Controls.Add(_toggleFullScreenButton, 3, 0);

        return toolbar;
    }

    private Control BuildRemoteCanvas()
    {
        var canvasPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Tag = "surface",
        };

        _remoteScreenBox.Dock = DockStyle.Fill;
        _remoteScreenBox.BorderStyle = BorderStyle.FixedSingle;
        _remoteScreenBox.SizeMode = PictureBoxSizeMode.Zoom;
        _remoteScreenBox.TabStop = true;
        _remoteScreenBox.Tag = "screen";

        canvasPanel.Controls.Add(_remoteScreenBox);
        return canvasPanel;
    }

    private Control BuildRemoteFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 0, 0, 0),
            Tag = "page",
        };

        _remoteHintLabel.Dock = DockStyle.Fill;
        _remoteHintLabel.Text = "Click inside the remote screen before sending mouse or keyboard input.";
        _remoteHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        _remoteHintLabel.Tag = "subtitle";
        footer.Controls.Add(_remoteHintLabel);
        return footer;
    }

    private void BuildChatDrawer()
    {
        _chatSplitter.Dock = DockStyle.Right;
        _chatSplitter.Width = 6;
        _chatSplitter.MinSize = 260;
        _chatSplitter.MinExtra = 560;
        _chatSplitter.Tag = "page";
        _chatSplitter.Visible = false;

        _chatDrawerPanel.Dock = DockStyle.Right;
        _chatDrawerPanel.Width = 340;
        _chatDrawerPanel.Padding = new Padding(12, 0, 0, 0);
        _chatDrawerPanel.Tag = "page";
        _chatDrawerPanel.Visible = false;

        var chatCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            Tag = "surface",
        };
        chatCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        chatCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chatCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _chatDrawerPanel.Controls.Add(chatCard);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Tag = "surface",
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _chatTitleLabel.Dock = DockStyle.Fill;
        _chatTitleLabel.Text = "Chat";
        _chatTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _chatTitleLabel.Tag = "title";

        ConfigureToolbarButton(_closeChatDrawerButton, "Hide");

        header.Controls.Add(_chatTitleLabel, 0, 0);
        header.Controls.Add(_closeChatDrawerButton, 1, 0);

        _chatListBox.Dock = DockStyle.Fill;
        _chatListBox.Tag = "list";

        var inputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
            Tag = "surface",
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _chatInputTextBox.Dock = DockStyle.Fill;
        _chatInputTextBox.Tag = "input";
        ConfigureActionButton(_sendChatButton, "Send", "toolbar");

        inputRow.Controls.Add(_chatInputTextBox, 0, 0);
        inputRow.Controls.Add(_sendChatButton, 1, 0);

        chatCard.Controls.Add(header, 0, 0);
        chatCard.Controls.Add(_chatListBox, 0, 1);
        chatCard.Controls.Add(inputRow, 0, 2);
    }

    private static TableLayoutPanel CreateStackLayout()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 0,
            Tag = "page",
        };
    }

    private static void AddCardToStack(TableLayoutPanel stack, Control card, int rowIndex)
    {
        stack.RowCount = rowIndex + 1;
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(card, 0, rowIndex);
    }

    private Panel CreateCardPanel(string title)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 10,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(14, 50, 14, 14),
            Tag = "surface",
            BorderStyle = BorderStyle.FixedSingle,
        };

        var titleLabel = new Label
        {
            Location = new Point(14, 14),
            Width = 600,
            Height = 26,
            Text = title,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Tag = "title",
        };

        card.Controls.Add(titleLabel);
        return card;
    }

    private static FlowLayoutPanel CreateButtonRow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 0),
            Tag = "surface",
        };
    }

    private static TableLayoutPanel CreateFormTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Tag = "surface",
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddField(TableLayoutPanel table, int rowIndex, string label, Control control)
    {
        table.RowCount = rowIndex + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var fieldPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, rowIndex == 0 ? 0 : 6, 0, 0),
            Padding = new Padding(0),
            Tag = "surface",
        };

        var labelControl = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = label,
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 0, 6),
            Tag = "caption",
        };

        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 6, 0, 0);

        fieldPanel.Controls.Add(control);
        fieldPanel.Controls.Add(labelControl);
        table.Controls.Add(fieldPanel, 0, rowIndex);
    }

    private Control ConfigureModeComboBox()
    {
        _modeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeComboBox.Items.AddRange(new object[] { "LAN", "Internet" });
        _modeComboBox.SelectedIndex = 0;
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
        _roomIdTextBox.PlaceholderText = "Enter room id or use generated value";
        return CreateTextInputHost(_roomIdTextBox);
    }

    private Control ConfigurePasswordBox(TextBox textBox)
    {
        textBox.UseSystemPasswordChar = true;
        return CreateTextInputHost(textBox);
    }

    private CheckBox ConfigureThemeToggleCheckBox()
    {
        _darkThemeCheckBox.Dock = DockStyle.Fill;
        _darkThemeCheckBox.Appearance = Appearance.Button;
        _darkThemeCheckBox.TextAlign = ContentAlignment.MiddleCenter;
        _darkThemeCheckBox.Height = 40;
        _darkThemeCheckBox.MinimumSize = new Size(0, 40);
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

    private static void ConfigureActionButton(Button button, string text, string kind)
    {
        button.Text = text;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(92, 40);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Tag = kind;
    }

    private static void ConfigureToolbarButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(0, 38);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Tag = "toolbar";
    }

    private static void ConfigureIconButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = false;
        button.Size = new Size(32, 32);
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Tag = "toolbar";
    }

    private static Control CreateTextInputHost(TextBox textBox)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 40,
            MinimumSize = new Size(0, 40),
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle,
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
        return host;
    }

    private static Control CreateComboInputHost(ComboBox comboBox)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 40,
            MinimumSize = new Size(0, 40),
            Padding = new Padding(10, 7, 10, 5),
            Margin = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle,
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
        return host;
    }
}
