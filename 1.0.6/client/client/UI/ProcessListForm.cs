using client.Models;

namespace client.UI;

internal sealed class ProcessListForm : Form
{
    private readonly Func<Task> _refreshAsync;
    private readonly Label _summaryLabel = new();
    private readonly Button _refreshButton = new();
    private readonly Button _closeButton = new();
    private readonly DataGridView _processGrid = new();

    public ProcessListForm(Func<Task> refreshAsync)
    {
        _refreshAsync = refreshAsync;
        Text = "Host Processes";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 680);
        MinimumSize = new Size(860, 520);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
    }

    public void ShowLoading(string message)
    {
        _summaryLabel.Text = message;
        _refreshButton.Enabled = false;
    }

    public void RenderSnapshot(ProcessSnapshotInfo snapshot)
    {
        _refreshButton.Enabled = true;
        _summaryLabel.Text =
            $"{snapshot.MachineName} | {snapshot.Processes.Count} processes | {snapshot.ProcessorCount} CPUs | RAM {FormatBytes(snapshot.TotalMemoryBytes)} | {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

        _processGrid.SuspendLayout();
        try
        {
            _processGrid.Rows.Clear();
            foreach (var process in snapshot.Processes)
            {
                _processGrid.Rows.Add(
                    process.Name,
                    process.ProcessId,
                    process.CpuPercent,
                    Math.Round(process.WorkingSetBytes / 1024d / 1024d, 1),
                    process.MemoryPercent,
                    process.ThreadCount,
                    process.HandleCount,
                    process.StartTimeText,
                    process.WindowTitle);
            }
        }
        finally
        {
            _processGrid.ResumeLayout();
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.AutoEllipsis = true;

        ConfigureButton(_refreshButton, "Refresh");
        _refreshButton.Click += async (_, _) =>
        {
            try
            {
                await _refreshAsync();
            }
            finally
            {
                _refreshButton.Enabled = true;
            }
        };

        ConfigureButton(_closeButton, "Close");
        _closeButton.Click += (_, _) => Close();

        toolbar.Controls.Add(_summaryLabel, 0, 0);
        toolbar.Controls.Add(_refreshButton, 1, 0);
        toolbar.Controls.Add(_closeButton, 2, 0);

        ConfigureGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_processGrid, 0, 1);
    }

    private void ConfigureGrid()
    {
        _processGrid.Dock = DockStyle.Fill;
        _processGrid.AllowUserToAddRows = false;
        _processGrid.AllowUserToDeleteRows = false;
        _processGrid.AllowUserToResizeRows = false;
        _processGrid.ReadOnly = true;
        _processGrid.MultiSelect = false;
        _processGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _processGrid.RowHeadersVisible = false;
        _processGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _processGrid.BackgroundColor = Color.White;
        _processGrid.BorderStyle = BorderStyle.FixedSingle;
        _processGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        AddColumn("name", "Name", typeof(string), 220);
        AddColumn("pid", "PID", typeof(int), 70);
        AddColumn("cpu", "CPU %", typeof(double), 78, "0.0");
        AddColumn("memory", "RAM MB", typeof(double), 90, "0.0");
        AddColumn("memoryPercent", "RAM %", typeof(double), 78, "0.0");
        AddColumn("threads", "Threads", typeof(int), 78);
        AddColumn("handles", "Handles", typeof(int), 82);
        AddColumn("started", "Started", typeof(string), 150);
        AddColumn("window", "Window Title", typeof(string), 280);
    }

    private void AddColumn(string name, string header, Type valueType, int width, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            ValueType = valueType,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic,
        };

        if (!string.IsNullOrWhiteSpace(format))
        {
            column.DefaultCellStyle.Format = format;
        }

        _processGrid.Columns.Add(column);
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.Size = new Size(96, 32);
        button.Margin = new Padding(8, 4, 0, 4);
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = true;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown";
        }

        return bytes >= 1024L * 1024L * 1024L
            ? $"{bytes / 1024d / 1024d / 1024d:0.0} GB"
            : $"{bytes / 1024d / 1024d:0.0} MB";
    }
}
