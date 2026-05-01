using System.Drawing;

namespace client;

partial class MainForm
{
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
            RowCount = 2,
            Tag = "surface",
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sidebarPanel.Controls.Add(sidebarLayout);

        sidebarLayout.Controls.Add(BuildSidebarHeader(), 0, 0);

        _sidebarContentHostPanel.Dock = DockStyle.Fill;
        _sidebarContentHostPanel.Tag = "surface";
        _sidebarContentHostPanel.Controls.Add(BuildControlView());
        sidebarLayout.Controls.Add(_sidebarContentHostPanel, 0, 1);
    }

    private Control BuildSidebarHeader()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(10, 10, 14, 10),
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

        ConfigureIconButton(_toggleSidebarButton, "<");
        _toggleSidebarButton.Margin = new Padding(12, 0, 0, 0);

        headerLayout.Controls.Add(
            new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Tag = "raised",
            },
            0,
            0);
        headerLayout.Controls.Add(_toggleSidebarButton, 1, 0);
        headerPanel.Controls.Add(headerLayout);
        return headerPanel;
    }

    private Control BuildSidebarNavigation()
    {
        var navPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Tag = "page",
        };
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _controlViewButton.Dock = DockStyle.Fill;
        _controlViewButton.Text = "Settings";
        _controlViewButton.Tag = "nav";
        _controlViewButton.MinimumSize = new Size(0, 48);

        navPanel.Controls.Add(_controlViewButton, 0, 0);
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
        AddCardToStack(stack, BuildFileTransferCard(), 2);
        AddCardToStack(stack, BuildGeneralSettingsCard(), 3);

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
        AddField(table, 0, "Account", ConfigureStatusLabel(_authStatusLabel));
        AddField(table, 1, "Room", ConfigureStatusLabel(_roomStatusLabel));

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_logoutButton, "Logout", "action");
        buttonRow.Controls.Add(_logoutButton);
        table.Controls.Add(buttonRow, 0, 2);

        card.Controls.Add(table);
        return card;
    }

    private Control BuildRoomControlCard()
    {
        var card = CreateCardPanel("Room Control");
        var table = CreateFormTable();
        AddField(table, 0, "Room ID", ConfigureRoomIdTextBox());
        AddField(table, 1, "Password", ConfigurePasswordBox(_roomPasswordTextBox, _roomPasswordToggleButton));
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
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Tag = "surface",
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _transferTitleLabel.Dock = DockStyle.Top;
        _transferTitleLabel.Height = 22;
        _transferTitleLabel.Text = "Transfer activity";
        _transferTitleLabel.Tag = "subtitle";

        var transferHintLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 0, 0, 8),
            Text = "Send File transfers to the other connected machine only. Incoming files are saved automatically to Downloads/Netrix.",
            Tag = "caption",
        };

        _transferListBox.Dock = DockStyle.Top;
        _transferListBox.Height = 120;
        _transferListBox.Margin = new Padding(0);
        _transferListBox.Tag = "list";

        var buttonRow = CreateButtonRow();
        ConfigureActionButton(_sendFileButton, "Send File", "action");
        buttonRow.Controls.Add(_sendFileButton);

        layout.Controls.Add(_transferTitleLabel, 0, 0);
        layout.Controls.Add(transferHintLabel, 0, 1);
        layout.Controls.Add(_transferListBox, 0, 2);
        layout.Controls.Add(buttonRow, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildGeneralSettingsCard()
    {
        var card = CreateCardPanel("General");
        var table = CreateFormTable();

        AddField(table, 0, "Mode", ConfigureModeComboBox());
        AddField(table, 1, "Display Name", ConfigureTextBox(_displayNameTextBox, "Host-01"));
        _lanMainServerField = AddField(table, 2, "LAN Main Server", ConfigureLanMainServerTextBox());
        AddField(table, 3, "Theme", ConfigureThemeToggleCheckBox());

        card.Controls.Add(table);
        return card;
    }

    private Control BuildInternetSettingsCard()
    {
        var card = CreateCardPanel("Services");
        var table = CreateFormTable();

        AddField(table, 0, "Internet Main WebSocket", ConfigureStaticStatusLabel(client.Core.NetrixEndpoints.MainServerWs));
        AddField(table, 1, "Auth Server", ConfigureStaticStatusLabel(client.Core.NetrixEndpoints.AuthServer));
        AddField(table, 2, "Load Balancer", ConfigureStaticStatusLabel(client.Core.NetrixEndpoints.LoadBalancer));
        AddField(table, 3, "Policy", ConfigureStaticStatusLabel("Internet uses the hardcoded public services. LAN uses the custom main-server WebSocket above."));

        card.Controls.Add(table);
        return card;
    }
}
