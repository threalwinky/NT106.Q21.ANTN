using System.Diagnostics;
using System.IO;

namespace client;

partial class MainForm
{
    private void InitializeTransferSettings()
    {
        _downloadsFolderPath = ResolveDefaultDownloadsFolder();
        EnsureDownloadsFolderExists();
    }

    private void SyncRoomSecurityState()
    {
        var session = _netrixClient.CurrentSession;
        if (session is null)
        {
            _netrixClient.ClearRoomSecurity();
            return;
        }

        var roomPassword = _roomPasswordTextBox.Text;
        if (UseRoomEncryption && !string.IsNullOrWhiteSpace(roomPassword))
        {
            _netrixClient.ConfigureRoomSecurity(session.RoomId, roomPassword, enabled: true);
            return;
        }

        _netrixClient.ClearRoomSecurity();
    }

    private void EnsureDownloadsFolderExists()
    {
        Directory.CreateDirectory(ResolveDownloadsFolder());
    }

    private void OpenDownloadsFolder()
    {
        try
        {
            EnsureDownloadsFolderExists();
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = ResolveDownloadsFolder(),
                    UseShellExecute = true,
                });
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }

    private string ResolveDownloadsFolder()
    {
        var folder = _downloadsFolderPath.Trim();
        return string.IsNullOrWhiteSpace(folder) ? ResolveDefaultDownloadsFolder() : folder;
    }

    private static string ResolveDefaultDownloadsFolder()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, "Downloads", "Netrix");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Netrix");
    }

    private string CreateUniqueDownloadPath(string originalFileName)
    {
        var safeFileName = SanitizeFileName(originalFileName);
        var directory = ResolveDownloadsFolder();
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        var candidate = Path.Combine(directory, safeFileName);
        var suffix = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({suffix}){extension}");
            suffix += 1;
        }

        return candidate;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "netrix-file.bin" : sanitized;
    }
}
