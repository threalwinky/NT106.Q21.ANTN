using System.Drawing;
using client.Core;
using MaterialSkin;
using MaterialSkin.Controls;

namespace client;

partial class MainForm : MaterialForm
{
    private const int FileTransferChunkSize = 64 * 1024;

    private readonly MaterialSkinManager _materialSkinManager = MaterialSkinManager.Instance;
    private readonly NetrixClient _netrixClient = new();
    private readonly AuthApiClient _authApiClient = new();
    private readonly LoadBalancerApiClient _loadBalancerApiClient = new();
    private readonly ScreenCaptureService _screenCaptureService = new();
    private readonly Dictionary<string, IncomingFileTransferState> _incomingTransfers = new(StringComparer.Ordinal);
    private readonly Dictionary<Button, bool> _buttonInteractivity = new();
    private readonly Dictionary<TextBox, bool> _textInputInteractivity = new();
    private readonly HashSet<Keys> _pressedRemoteKeys = new();
    private readonly object _incomingTransfersLock = new();
    private readonly SemaphoreSlim _sessionActionLock = new(1, 1);

    private readonly Panel _authShellPanel = new();
    private readonly Panel _authCardPanel = new();
    private readonly Panel _authPageHostPanel = new();
    private readonly Panel _loginPagePanel = new();
    private readonly Panel _registerPagePanel = new();
    private readonly Panel _rootShellPanel = new();
    private readonly Panel _sidebarPanel = new();
    private readonly Panel _sidebarContentHostPanel = new();
    private readonly Panel _controlViewPanel = new();
    private readonly Panel _settingsViewPanel = new();
    private readonly Panel _workspacePanel = new();
    private readonly Panel _remoteHostPanel = new();
    private readonly Panel _chatDrawerPanel = new();
    private readonly Splitter _sidebarSplitter = new();
    private readonly Splitter _chatSplitter = new();

    private readonly Button _controlViewButton = new();
    private readonly Button _settingsViewButton = new();
    private readonly Button _showLoginPageButton = new();
    private readonly Button _showRegisterPageButton = new();
    private readonly Button _toggleSidebarButton = new();
    private readonly Button _showSidebarButton = new();
    private readonly Button _toggleChatButton = new();
    private readonly Button _toggleFullScreenButton = new();
    private readonly Button _closeChatDrawerButton = new();
    private readonly Button _sendFileButton = new();
    private readonly Button _logoutButton = new();
    private readonly Button _roomPasswordToggleButton = new();
    private readonly Button _loginPasswordToggleButton = new();
    private readonly Button _registerPasswordToggleButton = new();
    private readonly Button _registerConfirmPasswordToggleButton = new();

    private readonly Label _authHeroTitleLabel = new();
    private readonly Label _authHeroSubtitleLabel = new();
    private readonly Label _authMessageLabel = new();
    private readonly Label _toolbarSessionLabel = new();
    private readonly Label _remoteHintLabel = new();
    private readonly Label _chatTitleLabel = new();
    private readonly Label _participantsTitleLabel = new();
    private readonly Label _transferTitleLabel = new();

    private readonly ComboBox _modeComboBox = new();
    private readonly TextBox _displayNameTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly TextBox _registerUsernameTextBox = new();
    private readonly TextBox _registerPasswordTextBox = new();
    private readonly TextBox _registerConfirmPasswordTextBox = new();
    private readonly TextBox _lanMainServerTextBox = new();
    private readonly TextBox _roomIdTextBox = new();
    private readonly TextBox _roomPasswordTextBox = new();
    private readonly ComboBox _joinRoleComboBox = new();
    private readonly Button _registerButton = new();
    private readonly Button _loginButton = new();
    private readonly Button _createRoomButton = new();
    private readonly Button _joinRoomButton = new();
    private readonly Button _disconnectButton = new();
    private readonly CheckBox _darkThemeCheckBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _authStatusLabel = new();
    private readonly Label _roomStatusLabel = new();
    private readonly PictureBox _remoteScreenBox = new();
    private readonly PictureBox _authLogoPictureBox = new();
    private readonly ListBox _participantsListBox = new();
    private readonly ListBox _chatListBox = new();
    private readonly ListBox _transferListBox = new();
    private readonly TextBox _chatInputTextBox = new();
    private readonly Button _sendChatButton = new();

    private CancellationTokenSource? _hostCaptureCts;
    private CancellationTokenSource? _pingCts;
    private TaskCompletionSource<RoomSessionInfo>? _pendingRoomRequest;
    private string? _accessToken;
    private string? _authenticatedUsername;
    private string? _pendingRoomRequestName;
    private string? _selectedServerUrl;
    private string? _lastFrameSignature;
    private Control? _lanMainServerField;
    private string _downloadsFolderPath = string.Empty;
    private bool _remoteInputActive;
    private bool _isLoginPageActive = true;
    private bool _isSidebarVisible = true;
    private bool _isChatVisible;
    private bool _isFullScreen;
    private bool _isSessionActionInProgress;
    private bool _restoreSidebarVisible = true;
    private bool _restoreChatVisible;
    private DateTime _lastMouseMoveSentAtUtc = DateTime.MinValue;
    private FormBorderStyle _restoreBorderStyle = FormBorderStyle.Sizable;
    private FormWindowState _restoreWindowState = FormWindowState.Normal;
    private Rectangle _restoreBounds = Rectangle.Empty;

    public MainForm()
    {
        InitializeComponent();
        InitializeMaterialSkin();
        BuildLayout();
        LoadAuthLogo();
        InitializeTransferSettings();
        ApplyTheme();
        WireEvents();
        UpdateModeUi();
        UpdateAuthShellUi();
        UpdateShellStateUi();
        UpdateStatus("Sign in to use Netrix.");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopHostStreaming();
        StopPingLoop();

        try
        {
            _netrixClient.DisconnectAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        ClearIncomingTransfers();
        _remoteScreenBox.Image?.Dispose();
        _screenCaptureService.Dispose();
        _netrixClient.Dispose();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_isFullScreen && keyData == Keys.Escape)
        {
            ToggleFullScreen();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private AppMode CurrentMode => _modeComboBox.SelectedIndex == 1 ? AppMode.Internet : AppMode.Lan;

    private bool IsDarkTheme => _darkThemeCheckBox.Checked;
    private bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    private bool UseRoomEncryption => true;

    private ParticipantRole SelectedJoinRole =>
        _joinRoleComboBox.SelectedIndex switch
        {
            0 => ParticipantRole.Controller,
            _ => ParticipantRole.Viewer,
        };
}
