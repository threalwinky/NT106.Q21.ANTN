using System.Collections.Concurrent;
using System.Drawing;
using MaterialSkin;
using MaterialSkin.Controls;

using client.Models;
using client.Networking;
using client.Streaming.Capture;
using client.Streaming.Codecs;

namespace client.UI;

partial class MainForm : MaterialForm
{
    private const int FileTransferChunkSize = 64 * 1024;
    private const int TargetStreamingFps = 30;
    private const long StreamingJpegQuality = 45;
    private const int StreamingMaxWidth = 1280;
    private const int StreamingH264Bitrate = 5_000_000;
    private static readonly TimeSpan TargetFrameInterval = TimeSpan.FromMilliseconds(1000.0 / TargetStreamingFps);

    private readonly MaterialSkinManager _materialSkinManager = MaterialSkinManager.Instance;
    private readonly NetrixClient _netrixClient = new();
    private readonly AuthApiClient _authApiClient = new();
    private readonly LoadBalancerApiClient _loadBalancerApiClient = new();
    private readonly ScreenCaptureService _screenCaptureService = new(StreamingJpegQuality, StreamingMaxWidth);
    private readonly H264VideoEncoderService _h264VideoEncoderService =
        new(TargetStreamingFps, StreamingH264Bitrate, StreamingMaxWidth);
    private readonly H264VideoDecoderService _h264VideoDecoderService = new();
    private readonly BlockingCollection<RemoteFrame> _remoteFrameQueue = new(new ConcurrentQueue<RemoteFrame>(), 8);
    private readonly CancellationTokenSource _remoteFrameRenderCts = new();
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
    private readonly Button _showProcessesButton = new();
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

    private readonly TextBox _displayNameTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly TextBox _registerUsernameTextBox = new();
    private readonly TextBox _registerPasswordTextBox = new();
    private readonly TextBox _registerConfirmPasswordTextBox = new();
    private readonly ComboBox _authNetworkModeComboBox = new();
    private readonly TextBox _lanServerIpTextBox = new();
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
    private Thread? _hostStreamingThread;
    private Thread? _remoteFrameRenderThread;
    private TaskCompletionSource<RoomSessionInfo>? _pendingRoomRequest;
    private string? _accessToken;
    private string? _authenticatedUsername;
    private string? _pendingRoomRequestName;
    private string? _selectedServerUrl;
    private string? _lastClipboardText;
    private string? _lastFrameSignature;
    private ProcessListForm? _processListForm;
    private Control? _lanServerIpField;
    private string _downloadsFolderPath = string.Empty;
    private bool _remoteInputActive;
    private bool _isLoginPageActive = true;
    private bool _isSidebarVisible = true;
    private bool _isChatVisible;
    private bool _isFullScreen;
    private bool _isSessionActionInProgress;
    private bool _restoreSidebarVisible = true;
    private bool _restoreChatVisible;
    private bool _clipboardListenerRegistered;
    private DateTime _lastMouseMoveSentAtUtc = DateTime.MinValue;
    private DateTime _clipboardSuppressUntilUtc = DateTime.MinValue;
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
        StartRemoteFrameRenderer();
        UpdateModeUi();
        UpdateAuthShellUi();
        UpdateShellStateUi();
        UpdateStatus("Sign in to use Netrix.");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopHostStreaming();
        StopPingLoop();
        StopRemoteFrameRenderer();

        try
        {
            _netrixClient.DisconnectAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        ClearIncomingTransfers();
        _processListForm?.Close();
        _remoteScreenBox.Image?.Dispose();
        _h264VideoEncoderService.Dispose();
        _h264VideoDecoderService.Dispose();
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

    private AppMode CurrentMode => _authNetworkModeComboBox.SelectedIndex == 1 ? AppMode.Internet : AppMode.Lan;

    private bool IsDarkTheme => _darkThemeCheckBox.Checked;
    private bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    private bool UseRoomEncryption => true;

    private ParticipantRole SelectedJoinRole => ParticipantRole.Controller;
}
