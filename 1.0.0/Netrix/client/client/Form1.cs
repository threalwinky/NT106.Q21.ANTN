using System.Drawing;
using client.Core;

namespace client;

partial class Form1 : Form
{
    private readonly NetrixClient _netrixClient = new();
    private readonly AuthApiClient _authApiClient = new();
    private readonly LoadBalancerApiClient _loadBalancerApiClient = new();
    private readonly ScreenCaptureService _screenCaptureService = new();

    private readonly ComboBox _modeComboBox = new();
    private readonly TextBox _displayNameTextBox = new();
    private readonly TextBox _lanServerUrlTextBox = new();
    private readonly TextBox _authUrlTextBox = new();
    private readonly TextBox _loadBalancerUrlTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly TextBox _roomIdTextBox = new();
    private readonly TextBox _roomPasswordTextBox = new();
    private readonly ComboBox _joinRoleComboBox = new();
    private readonly Button _registerButton = new();
    private readonly Button _loginButton = new();
    private readonly Button _createRoomButton = new();
    private readonly Button _joinRoomButton = new();
    private readonly Button _disconnectButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _authStatusLabel = new();
    private readonly Label _roomStatusLabel = new();
    private readonly PictureBox _remoteScreenBox = new();
    private readonly ListBox _participantsListBox = new();
    private readonly ListBox _chatListBox = new();
    private readonly TextBox _chatInputTextBox = new();
    private readonly Button _sendChatButton = new();

    private CancellationTokenSource? _hostCaptureCts;
    private CancellationTokenSource? _pingCts;
    private string? _accessToken;
    private string? _selectedServerUrl;
    private bool _remoteInputActive;
    private DateTime _lastMouseMoveSentAtUtc = DateTime.MinValue;

    public Form1()
    {
        InitializeComponent();
        BuildLayout();
        WireEvents();
        UpdateModeUi();
        UpdateStatus("Ready.");
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

        _remoteScreenBox.Image?.Dispose();
        _netrixClient.Dispose();
        base.OnFormClosing(e);
    }

    private AppMode CurrentMode => _modeComboBox.SelectedIndex == 1 ? AppMode.Internet : AppMode.Lan;

    private ParticipantRole SelectedJoinRole =>
        _joinRoleComboBox.SelectedIndex switch
        {
            0 => ParticipantRole.Controller,
            _ => ParticipantRole.Viewer,
        };
}

