using System.Drawing;

namespace client;

partial class MainForm
{
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
        ConfigureToolbarButton(_toggleChatButton, "Chat");
        ConfigureToolbarButton(_toggleFullScreenButton, "Full Screen");
        _toggleChatButton.Size = new Size(118, 40);
        _toggleChatButton.MinimumSize = new Size(118, 40);
        _toggleChatButton.Margin = new Padding(8, 0, 0, 0);
        _toggleFullScreenButton.Size = new Size(132, 40);
        _toggleFullScreenButton.MinimumSize = new Size(132, 40);
        _toggleFullScreenButton.Margin = new Padding(8, 0, 0, 0);

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

        _chatInputTextBox.PlaceholderText = "Type a message";
        var chatInputHost = ConfigureTextBox(_chatInputTextBox, string.Empty);
        chatInputHost.Margin = new Padding(0);
        ConfigureActionButton(_sendChatButton, "Send", "toolbar");

        inputRow.Controls.Add(chatInputHost, 0, 0);
        inputRow.Controls.Add(_sendChatButton, 1, 0);

        chatCard.Controls.Add(header, 0, 0);
        chatCard.Controls.Add(_chatListBox, 0, 1);
        chatCard.Controls.Add(inputRow, 0, 2);
    }
}
