using System.Drawing;

namespace client;

partial class MainForm
{
    private void BuildLayout()
    {
        Text = "Netrix 1.0.3";
        MinimumSize = new Size(1360, 860);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _darkThemeCheckBox.Checked = false;

        _rootShellPanel.Dock = DockStyle.Fill;
        _rootShellPanel.Padding = new Padding(0);
        _rootShellPanel.Tag = "page";
        Controls.Add(_rootShellPanel);
        Controls.Add(_authShellPanel);

        BuildAuthShell();
        BuildSidebarShell();
        BuildWorkspaceShell();

        _rootShellPanel.Controls.Add(_workspacePanel);
        _rootShellPanel.Controls.Add(_sidebarSplitter);
        _rootShellPanel.Controls.Add(_sidebarPanel);
    }
}
