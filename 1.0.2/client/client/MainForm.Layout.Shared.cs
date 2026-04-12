using System.Drawing;

namespace client;

partial class MainForm
{
    private static TableLayoutPanel CreateStackLayout()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 0,
            Tag = "page",
        };
    }

    private static void AddCardToStack(TableLayoutPanel stack, Control card, int rowIndex)
    {
        stack.RowCount = rowIndex + 1;
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(card, 0, rowIndex);
    }

    private Panel CreateCardPanel(string title)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 10,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(14, 50, 14, 14),
            Tag = "surface",
            BorderStyle = BorderStyle.FixedSingle,
        };

        var titleLabel = new Label
        {
            Location = new Point(14, 14),
            Width = 600,
            Height = 26,
            Text = title,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Tag = "title",
        };

        card.Controls.Add(titleLabel);
        return card;
    }

    private static FlowLayoutPanel CreateButtonRow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
            Tag = "surface",
        };
    }

    private static TableLayoutPanel CreateFormTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Tag = "surface",
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static Control AddField(TableLayoutPanel table, int rowIndex, string label, Control control)
    {
        table.RowCount = rowIndex + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var fieldPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, rowIndex == 0 ? 0 : 6, 0, 0),
            Padding = new Padding(0),
            Tag = "surface",
        };

        var labelControl = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = label,
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 0, 6),
            Tag = "caption",
        };

        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 6, 0, 0);

        fieldPanel.Controls.Add(control);
        fieldPanel.Controls.Add(labelControl);
        table.Controls.Add(fieldPanel, 0, rowIndex);
        return fieldPanel;
    }
}
