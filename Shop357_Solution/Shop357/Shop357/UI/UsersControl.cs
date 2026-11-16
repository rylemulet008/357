using System.Data;
using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>Owner only: add users, reset passwords, activate / deactivate.</summary>
public class UsersControl : UserControl, IRefreshable
{
    readonly DataGridView _grid = Ui.Grid();

    public UsersControl()
    {
        Dock = DockStyle.Fill;

        var bar = Ui.Bar();
        bar.Controls.Add(Ui.Btn("Add User", (s, e) => AddUser()));
        bar.Controls.Add(Ui.Btn("Reset Password", (s, e) => ResetPassword()));
        bar.Controls.Add(Ui.Btn("Activate / Deactivate", (s, e) => Toggle(), 150));

        Controls.Add(_grid);
        Controls.Add(bar);
    }

    public void RefreshData()
    {
        _grid.DataSource = Db.Query(@"
            SELECT UserId, Username, FullName AS [Full Name], Role,
                   CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status
            FROM Users ORDER BY Role, Username");
        Ui.Hide(_grid, "UserId");
    }

    void AddUser()
    {
        using var f = new UserForm();
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void ResetPassword()
    {
        var id = Ui.SelectedId(_grid, "UserId");
        if (id == null)
        {
            Ui.Warn("Select a user first.");
            return;
        }
        var pw = PromptForm.Ask(FindForm(), "Reset password", "New password for " + Ui.SelectedText(_grid, "Username"), true);
        if (pw == null) return;
        if (pw.Length < 6)
        {
            Ui.Warn("Use at least 6 characters.");
            return;
        }
        Db.Exec("UPDATE Users SET PasswordHash = @h WHERE UserId = @id", Db.P("@h", Ui.Hash(pw)), Db.P("@id", id));
        Ui.Info("Password changed.");
    }

    void Toggle()
    {
        var id = Ui.SelectedId(_grid, "UserId");
        if (id == null)
        {
            Ui.Warn("Select a user first.");
            return;
        }
        if (id == Session.UserId)
        {
            Ui.Warn("You cannot deactivate your own account.");
            return;
        }
        Db.Exec("UPDATE Users SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE UserId = @id", Db.P("@id", id));
        RefreshData();
    }
}

public class UserForm : Form
{
    readonly TextBox _user = new TextBox();
    readonly TextBox _name = new TextBox();
    readonly TextBox _pass = new TextBox { UseSystemPasswordChar = true };
    readonly ComboBox _role = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

    public UserForm()
    {
        Ui.Setup(this, "Add User", 460, 290);
        _role.Items.AddRange(new object[] { "Staff", "Owner" });
        _role.SelectedIndex = 0;

        var t = Ui.FormTable();
        Ui.AddRow(t, "Username *", _user);
        Ui.AddRow(t, "Full name *", _name);
        Ui.AddRow(t, "Password * (6+ characters)", _pass);
        Ui.AddRow(t, "Role *", _role);
        Controls.Add(t);
        Ui.AddButtons(this, Save);
    }

    bool Save()
    {
        if (_user.Text.Trim() == "" || _name.Text.Trim() == "")
        {
            Ui.Warn("Username and full name are required.");
            return false;
        }
        if (_pass.Text.Length < 6)
        {
            Ui.Warn("Use a password of at least 6 characters.");
            return false;
        }

        try
        {
            Db.Exec("INSERT INTO Users(Username, PasswordHash, FullName, Role) VALUES(@u, @h, @n, @r)",
                Db.P("@u", _user.Text.Trim()), Db.P("@h", Ui.Hash(_pass.Text)),
                Db.P("@n", _name.Text.Trim()), Db.P("@r", Convert.ToString(_role.SelectedItem)));
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            Ui.Warn("That username is already taken.");
            return false;
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }
    }
}
