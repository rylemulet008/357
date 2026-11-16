using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

public class LoginForm : Form
{
    readonly TextBox _user = new TextBox { Font = new Font("Segoe UI", 11f) };
    readonly TextBox _pass = new TextBox { Font = new Font("Segoe UI", 11f), UseSystemPasswordChar = true };

    public LoginForm()
    {
        Ui.Setup(this, "357 Motorcycle Parts - Log in", 720, 420);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        if (Branding.AppIcon != null) Icon = Branding.AppIcon;

        // ----- left: brand panel -----
        var left = new Panel { Dock = DockStyle.Left, Width = 280, BackColor = Theme.Sidebar };
        var logo = Branding.Picture(240);
        if (logo != null)
        {
            // The shop logo, centred in the panel, with the system name under it.
            logo.Location = new Point(20, 56);
            left.Controls.Add(logo);
            left.Controls.Add(new Label
            {
                Text = "Procurement and Inventory System",
                ForeColor = Color.FromArgb(156, 163, 175),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(280, 28),
                Location = new Point(0, 312),
            });
        }
        else
        {
            // Fallback when the logo cannot be loaded: text only.
            left.Controls.Add(new Label
            {
                Text = "357",
                ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 40f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(26, 134),
            });
            left.Controls.Add(new Label
            {
                Text = "MOTORCYCLE PARTS\nAND ACCESSORIES SHOP",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(32, 214),
            });
        }

        // ----- right: form -----
        var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        right.Controls.Add(new Label
        {
            Text = "Welcome back",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(40, 40),
        });
        right.Controls.Add(new Label
        {
            Text = "Log in to continue",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(42, 86),
        });
        right.Controls.Add(new Label { Text = "Username", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(42, 130) });
        _user.SetBounds(42, 154, 330, 32);
        right.Controls.Add(_user);
        right.Controls.Add(new Label { Text = "Password", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(42, 200) });
        _pass.SetBounds(42, 224, 330, 32);
        right.Controls.Add(_pass);

        var login = new Button { Text = "Log in", Location = new Point(42, 282), Size = new Size(330, 44) };
        Ui.Style(login, BtnKind.Primary);
        login.Font = Theme.Bold;
        login.Click += (s, e) => DoLogin();
        AcceptButton = login;
        right.Controls.Add(login);

        right.Controls.Add(new Label
        {
            Text = "First time? Run Database\\Shop357DB.sql in SSMS,\nthen log in with owner / owner123.",
            ForeColor = Theme.Muted,
            Font = Theme.Small,
            AutoSize = true,
            Location = new Point(42, 346),
        });

        Controls.Add(right);
        Controls.Add(left);
    }

    void DoLogin()
    {
        var username = _user.Text.Trim();
        if (username == "" || _pass.Text == "")
        {
            Ui.Warn("Enter your username and password.");
            return;
        }

        try
        {
            var dt = Db.Query(
                "SELECT UserId, Username, FullName, Role FROM Users WHERE Username = @u AND PasswordHash = @p AND IsActive = 1",
                Db.P("@u", username), Db.P("@p", Ui.Hash(_pass.Text)));

            if (dt.Rows.Count == 0)
            {
                Ui.Warn("Wrong username or password.");
                _pass.Clear();
                _pass.Focus();
                return;
            }

            var r = dt.Rows[0];
            Session.UserId = Convert.ToInt32(r["UserId"]);
            Session.Username = Convert.ToString(r["Username"]);
            Session.FullName = Convert.ToString(r["FullName"]);
            Session.Role = Convert.ToString(r["Role"]);
            DialogResult = DialogResult.OK;
        }
        catch (SqlException ex)
        {
            Ui.Error("Cannot connect to the database.\n\n" +
                     "1. Make sure SQL Server Express is running.\n" +
                     "2. Run Database\\Shop357DB.sql in SSMS.\n" +
                     "3. If your instance is not called SQLEXPRESS, change ConnectionString in Data\\Db.cs.\n\n" +
                     "Details: " + ex.Message);
        }
    }
}
