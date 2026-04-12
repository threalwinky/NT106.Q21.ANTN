using client.Core;
using System.Drawing;
using System.Reflection;

namespace client;

partial class MainForm
{
    private void ShowLoginPage()
    {
        _isLoginPageActive = true;
        UpdateAuthShellUi();
    }

    private void ShowRegisterPage()
    {
        _isLoginPageActive = false;
        UpdateAuthShellUi();
    }

    private void UpdateAuthShellUi()
    {
        _authShellPanel.Visible = !IsAuthenticated;
        _rootShellPanel.Visible = IsAuthenticated;
        _loginPagePanel.Visible = _isLoginPageActive;
        _registerPagePanel.Visible = !_isLoginPageActive;
        RefreshInteractiveStyles();
    }

    private async Task RegisterAsync()
    {
        try
        {
            ValidateRegisterCredentials();
            using var cts = CreateShortTimeout();
            var response = await _authApiClient.RegisterAsync(
                NetrixEndpoints.AuthServer,
                _registerUsernameTextBox.Text.Trim(),
                _registerPasswordTextBox.Text,
                cts.Token);
            ApplyAuthenticatedSession(response, "Registration completed.");
            _registerPasswordTextBox.Clear();
            _registerConfirmPasswordTextBox.Clear();
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }

    private async Task LoginAsync()
    {
        try
        {
            ValidateLoginCredentials();
            using var cts = CreateShortTimeout();
            var response = await _authApiClient.LoginAsync(
                NetrixEndpoints.AuthServer,
                _usernameTextBox.Text.Trim(),
                _passwordTextBox.Text,
                cts.Token);
            ApplyAuthenticatedSession(response, "Login completed.");
            _passwordTextBox.Clear();
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }

    private async Task LogoutAsync()
    {
        await DisconnectAsync("Signed out.");
        _accessToken = null;
        _authenticatedUsername = null;
        _passwordTextBox.Clear();
        _registerPasswordTextBox.Clear();
        _registerConfirmPasswordTextBox.Clear();
        _authStatusLabel.Text = "Sign in required";
        UpdateAuthShellUi();
    }

    private void ApplyAuthenticatedSession(AuthTokenResponse response, string message)
    {
        _accessToken = response.AccessToken;
        _authenticatedUsername = response.Username;
        _isLoginPageActive = true;
        _authStatusLabel.Text = $"Signed in as {response.Username}";
        if (string.IsNullOrWhiteSpace(_displayNameTextBox.Text))
        {
            _displayNameTextBox.Text = response.Username;
        }

        UpdateAuthShellUi();
        UpdateStatus(message);
        RefreshSessionChrome();
    }

    private void LoadAuthLogo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("netrix-logo.png", StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                _authLogoPictureBox.Visible = false;
                return;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                _authLogoPictureBox.Visible = false;
                return;
            }

            using var image = Image.FromStream(stream);
            _authLogoPictureBox.Image = new Bitmap(image);
        }
        catch
        {
            _authLogoPictureBox.Visible = false;
        }
    }
}
