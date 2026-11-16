using Shop357.Data;

namespace Shop357.UI;

/// <summary>A tiny "type a value" pop-up (used for reject reasons and new passwords).</summary>
public class PromptForm : Form
{
    readonly TextBox _text = new TextBox();

    PromptForm(string title, string prompt, bool password)
    {
        Ui.Setup(this, title, 440, 150);
        _text.UseSystemPasswordChar = password;
        var t = Ui.FormTable();
        Ui.AddRow(t, prompt, _text);
        Controls.Add(t);
        Ui.AddButtons(this, () =>
        {
            if (_text.Text.Trim() == "")
            {
                Ui.Warn("Please type something first.");
                return false;
            }
            return true;
        }, "OK");
    }

    /// <summary>Returns the text, or null when the user cancels.</summary>
    public static string Ask(IWin32Window owner, string title, string prompt, bool password = false)
    {
        using var f = new PromptForm(title, prompt, password);
        return f.ShowDialog(owner) == DialogResult.OK ? f._text.Text.Trim() : null;
    }
}

/// <summary>The bell button: alerts for the current role (new PRs to approve, discrepancies, ...).</summary>
public class NotificationsForm : Form
{
    readonly DataGridView _grid = Ui.Grid();

    public NotificationsForm()
    {
        Text = "Notifications";
        Size = new Size(800, 460);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        BackColor = Color.White;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(12, 11, 12, 8), FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface };
        var close = new Button { Text = "Close", Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        Ui.Style(close, BtnKind.Normal);
        var markRead = Ui.Btn("Mark all as read", (s, e) =>
        {
            Db.Exec("UPDATE Notifications SET IsRead = 1 WHERE IsRead = 0 AND (ForRole = @r OR ForRole = 'All')",
                Db.P("@r", Session.Role));
            LoadData();
        }, 150);
        bar.Controls.Add(close);
        bar.Controls.Add(markRead);
        CancelButton = close;

        Controls.Add(_grid);
        Controls.Add(bar);
        LoadData();
    }

    void LoadData()
    {
        _grid.DataSource = Db.Query(@"
            SELECT TOP 100 NotificationId, CreatedAt AS [When], Message,
                   CASE WHEN IsRead = 1 THEN 'Read' ELSE 'NEW' END AS [Status]
            FROM Notifications
            WHERE ForRole = @r OR ForRole = 'All'
            ORDER BY NotificationId DESC", Db.P("@r", Session.Role));
        Ui.Hide(_grid, "NotificationId");
        if (_grid.Columns.Contains("Message")) _grid.Columns["Message"].FillWeight = 300;
    }

    public static int UnreadCount() =>
        Db.ScalarInt("SELECT COUNT(*) FROM Notifications WHERE IsRead = 0 AND (ForRole = @r OR ForRole = 'All')",
            Db.P("@r", Session.Role));
}
