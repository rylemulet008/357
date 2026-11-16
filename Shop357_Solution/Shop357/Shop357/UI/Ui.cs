using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Shop357.UI;

/// <summary>Implemented by every page so MainForm can reload it when it is opened.</summary>
public interface IRefreshable
{
    void RefreshData();
}

/// <summary>One place for the colours and fonts of the whole app. Change them here to re-theme everything.</summary>
public static class Theme
{
    public static readonly Color Sidebar = Color.FromArgb(31, 41, 55);
    public static readonly Color SidebarHover = Color.FromArgb(55, 65, 81);
    public static readonly Color Accent = Color.FromArgb(234, 88, 12);
    public static readonly Color AccentDark = Color.FromArgb(194, 65, 12);
    public static readonly Color Surface = Color.FromArgb(243, 244, 246);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(209, 213, 219);
    public static readonly Color Text = Color.FromArgb(17, 24, 39);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color Danger = Color.FromArgb(185, 28, 28);
    public static readonly Color DangerSoft = Color.FromArgb(254, 226, 226);
    public static readonly Color GridHeader = Color.FromArgb(55, 65, 81);
    public static readonly Color GridLine = Color.FromArgb(229, 231, 235);
    public static readonly Color AltRow = Color.FromArgb(249, 250, 251);
    public static readonly Color SelectBack = Color.FromArgb(254, 215, 170);

    public static readonly Font Base = new Font("Segoe UI", 10f);
    public static readonly Font Bold = new Font("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font Heading = new Font("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font Kpi = new Font("Segoe UI", 22f, FontStyle.Bold);
    public static readonly Font Small = new Font("Segoe UI", 9f);
}

public enum BtnKind
{
    Normal,
    Primary,
    Danger,
}

/// <summary>Small helpers so the screens (built in code, no designer) stay short.</summary>
public static class Ui
{
    // ----- tables -----
    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Theme.GridLine,
            EnableHeadersVisualStyles = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 38,
        };

        g.RowTemplate.Height = 30;

        g.ColumnHeadersDefaultCellStyle.BackColor = Theme.GridHeader;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = Theme.Bold;
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

        g.DefaultCellStyle.BackColor = Color.White;
        g.DefaultCellStyle.ForeColor = Theme.Text;
        g.DefaultCellStyle.SelectionBackColor = Theme.SelectBack;
        g.DefaultCellStyle.SelectionForeColor = Theme.Text;
        g.DefaultCellStyle.Font = Theme.Base;
        g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

        g.AlternatingRowsDefaultCellStyle.BackColor = Theme.AltRow;
        g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Theme.SelectBack;
        g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Theme.Text;
        return g;
    }

    // ----- buttons -----
    /// <summary>
    /// A styled button. When kind is not given it is chosen from the label:
    /// Add/New/Create/Receive/Send/Submit/Approve/Record = orange, Reject/Delete/Cancel PR = red, others = white.
    /// </summary>
    public static Button Btn(string text, EventHandler onClick, int width = 130, BtnKind? kind = null)
    {
        var b = new Button { Text = text, Width = width, Height = 34, Margin = new Padding(0, 0, 8, 6) };
        Style(b, kind ?? Infer(text));
        b.Click += onClick;
        return b;
    }

    static BtnKind Infer(string text)
    {
        string[] danger = { "Reject", "Delete", "Cancel PR" };
        string[] primary = { "Add ", "New ", "Create", "Receive", "Send ", "Submit", "Approve", "Record" };
        foreach (var d in danger)
            if (text.StartsWith(d)) return BtnKind.Danger;
        foreach (var p in primary)
            if (text.StartsWith(p)) return BtnKind.Primary;
        return BtnKind.Normal;
    }

    public static void Style(Button b, BtnKind kind)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.Cursor = Cursors.Hand;
        b.Font = Theme.Base;
        b.FlatAppearance.BorderSize = 1;

        switch (kind)
        {
            case BtnKind.Primary:
                b.BackColor = Theme.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = Theme.Accent;
                b.FlatAppearance.MouseOverBackColor = Theme.AccentDark;
                b.FlatAppearance.MouseDownBackColor = Theme.AccentDark;
                break;
            case BtnKind.Danger:
                b.BackColor = Color.White;
                b.ForeColor = Theme.Danger;
                b.FlatAppearance.BorderColor = Theme.Danger;
                b.FlatAppearance.MouseOverBackColor = Theme.DangerSoft;
                b.FlatAppearance.MouseDownBackColor = Theme.DangerSoft;
                break;
            default:
                b.BackColor = Color.White;
                b.ForeColor = Theme.Text;
                b.FlatAppearance.BorderColor = Theme.Border;
                b.FlatAppearance.MouseOverBackColor = Theme.Surface;
                b.FlatAppearance.MouseDownBackColor = Theme.GridLine;
                break;
        }
    }

    public static FlowLayoutPanel Bar() => new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Padding = new Padding(10, 10, 10, 4),
    };

    public static Label Lbl(string text) => new Label
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 9, 6, 0),
    };

    public static NumericUpDown Num(decimal min, decimal max, decimal value = 0, int decimals = 0) =>
        new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Min(Math.Max(value, min), max),
            DecimalPlaces = decimals,
            ThousandsSeparator = true,
        };

    // ----- message boxes -----
    const string Title = "357 Motorcycle Parts";

    public static void Info(string msg) =>
        MessageBox.Show(msg, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(string msg) =>
        MessageBox.Show(msg, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(string msg) =>
        MessageBox.Show(msg, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(string msg) =>
        MessageBox.Show(msg, "Please confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    // ----- grid helpers -----
    public static int? SelectedId(DataGridView g, string col)
    {
        if (g.CurrentRow == null || !g.Columns.Contains(col)) return null;
        var v = g.CurrentRow.Cells[col].Value;
        return v == null || v == DBNull.Value ? null : Convert.ToInt32(v);
    }

    public static string SelectedText(DataGridView g, string col)
    {
        if (g.CurrentRow == null || !g.Columns.Contains(col)) return null;
        return Convert.ToString(g.CurrentRow.Cells[col].Value);
    }

    public static void Hide(DataGridView g, params string[] cols)
    {
        foreach (var c in cols)
            if (g.Columns.Contains(c)) g.Columns[c].Visible = false;
    }

    public static void Fmt(DataGridView g, string format, params string[] cols)
    {
        foreach (var c in cols)
        {
            if (!g.Columns.Contains(c)) continue;
            g.Columns[c].DefaultCellStyle.Format = format;
            g.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    // ----- dialogs -----
    public static void Setup(Form f, string title, int width, int height)
    {
        f.Text = title;
        f.ClientSize = new Size(width, height);
        f.StartPosition = FormStartPosition.CenterParent;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.BackColor = Color.White;
        f.ForeColor = Theme.Text;
        if (Branding.AppIcon != null) f.Icon = Branding.AppIcon;
    }

    public static TableLayoutPanel FormTable()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(16, 14, 16, 6),
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    public static void AddRow(TableLayoutPanel t, string label, Control c)
    {
        int r = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 6, 0, 6),
        }, 0, r);
        c.Dock = DockStyle.Fill;
        c.Margin = new Padding(0, 3, 0, 3);
        t.Controls.Add(c, 1, r);
    }

    /// <summary>Adds the Save / Cancel bar. Add your content controls BEFORE calling this.</summary>
    public static void AddButtons(Form f, Func<bool> onOk, string okText = "Save")
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 58,
            Padding = new Padding(12, 11, 12, 8),
            BackColor = Theme.Surface,
        };
        var ok = new Button { Text = okText, Width = 150, Height = 34 };
        var cancel = new Button { Text = "Cancel", Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        Style(ok, BtnKind.Primary);
        Style(cancel, BtnKind.Normal);
        ok.Click += (s, e) =>
        {
            if (onOk()) f.DialogResult = DialogResult.OK;
        };
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);
        f.CancelButton = cancel;
        f.Controls.Add(bar);
    }

    /// <summary>Shows a read-only table in a pop-up window.</summary>
    public static void ShowGrid(IWin32Window owner, string title, DataTable dt, params string[] hide)
    {
        using var f = new Form
        {
            Text = title,
            Size = new Size(940, 520),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            BackColor = Color.White,
        };
        var g = Grid();
        g.DataSource = dt;
        Hide(g, hide);
        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.Cancel };
        Style(close, BtnKind.Normal);
        f.CancelButton = close;
        f.Controls.Add(g);
        f.Controls.Add(close);
        f.ShowDialog(owner);
    }

    // ----- misc -----
    public static string Hash(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password ?? "")));

    public static void ExportCsv(DataGridView g, string defaultName)
    {
        if (g.Rows.Count == 0)
        {
            Warn("There is nothing to export.");
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Filter = "CSV file - opens in Excel (*.csv)|*.csv",
            FileName = defaultName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".csv",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var cols = g.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(c => Csv(c.HeaderText))));
        foreach (DataGridViewRow r in g.Rows)
            sb.AppendLine(string.Join(",", cols.Select(c => Csv(Convert.ToString(r.Cells[c.Index].Value)))));

        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            Info("Saved: " + dlg.FileName);
        }
        catch (IOException ex)
        {
            Error("Could not save the file. Is it open in Excel?\n\n" + ex.Message);
        }
    }

    static string Csv(string s)
    {
        s ??= "";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
