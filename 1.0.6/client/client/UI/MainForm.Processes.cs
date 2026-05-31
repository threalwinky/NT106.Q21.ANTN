using client.Diagnostics;
using client.Models;

namespace client.UI;

partial class MainForm
{
    private async Task ShowProcessesAsync()
    {
        var session = _netrixClient.CurrentSession;
        if (session is null)
        {
            ShowErrorDialog("Create or join a room before viewing host processes.");
            return;
        }

        EnsureProcessListForm();

        if (session.Role == ParticipantRole.Host)
        {
            _processListForm?.ShowLoading("Collecting local host processes...");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var snapshot = await ProcessSnapshotCollector.CaptureAsync(Guid.NewGuid().ToString("N"), cts.Token);
                ShowProcessSnapshot(snapshot);
            }
            catch (Exception ex)
            {
                ShowErrorDialog(ex.Message);
                _processListForm?.ShowLoading($"Failed to collect processes: {ex.Message}");
            }

            return;
        }

        if (session.Role != ParticipantRole.Controller || !session.CanSendControl)
        {
            ShowErrorDialog("Host approval is required before viewing host processes.");
            return;
        }

        var requestId = Guid.NewGuid().ToString("N");
        _processListForm?.ShowLoading("Requesting host process list...");
        try
        {
            using var cts = CreateShortTimeout();
            await _netrixClient.SendProcessRequestAsync(requestId, cts.Token);
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
            _processListForm?.ShowLoading($"Request failed: {ex.Message}");
        }
    }

    private void HandleProcessRequest(ProcessRequestInfo request)
    {
        if (_netrixClient.CurrentSession?.Role != ParticipantRole.Host
            || string.IsNullOrWhiteSpace(request.RequesterClientId)
            || string.IsNullOrWhiteSpace(request.RequestId))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var snapshot = await ProcessSnapshotCollector.CaptureAsync(request.RequestId, cts.Token);
                await _netrixClient.SendProcessSnapshotAsync(request.RequesterClientId, snapshot, cts.Token);
            }
            catch (Exception ex)
            {
                OnUiThread(() => AppendTransferStatus($"Process snapshot failed: {ex.Message}"));
            }
        });
    }

    private void ShowProcessSnapshot(ProcessSnapshotInfo snapshot)
    {
        EnsureProcessListForm();
        _processListForm?.RenderSnapshot(snapshot);
        if (_processListForm is null)
        {
            return;
        }

        if (!_processListForm.Visible)
        {
            _processListForm.Show(this);
        }

        _processListForm.Activate();
    }

    private void EnsureProcessListForm()
    {
        if (_processListForm is { IsDisposed: false })
        {
            if (!_processListForm.Visible)
            {
                _processListForm.Show(this);
            }

            return;
        }

        _processListForm = new ProcessListForm(ShowProcessesAsync);
        _processListForm.FormClosed += (_, _) => _processListForm = null;
        _processListForm.Show(this);
    }
}
