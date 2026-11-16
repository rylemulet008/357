using Shop357.Data;

namespace Shop357.UI;

/// <summary>Main window: dark sidebar on the left, page title + current page on the right.</summary>
public class MainForm : Form
{
    public bool LoggedOut;

    static readonly Font NavFont = new Font("Segoe UI", 10.5f);
    static readonly Font NavFontBold = new Font("Segoe UI", 10.5f, FontStyle.Bold);

    readonly Panel _host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(20) };
    readonly Label _title = new Label
    {
        Dock = DockStyle.Fill,
        Font = Theme.Heading,
        ForeColor = Theme.Text,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(24, 0, 0, 0),
    };
    readonly Label _alert = new Label
    {
        Dock = DockStyle.Right,
        Width = 460,
        ForeColor = Theme.Danger,
        Font = Theme.Bold,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 24, 0),
    };
    readonly Button _bell = new Button { Width = 188, Height = 36, Margin = new Padding(0, 0, 0, 8) };
    readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 30000 };

    readonly Dictionary<string, Control> _pages = new Dictionary<string, Control>();
    readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
    string _current = "";

    public MainForm()
    {
        Text = "357 Motorcycle Parts and Accessories Shop - Procurement and Inventory System";
        Size = new Size(1280, 720);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        if (Branding.AppIcon != null) Icon = Branding.AppIcon;

        // ----- sidebar -----
        var side = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Theme.Sidebar };

        var logo = Branding.Picture(130);
        var brand = new Panel { Dock = DockStyle.Top, Height = logo != null ? 148 : 92, BackColor = Theme.Sidebar };
        if (logo != null)
        {
            logo.Location = new Point(45, 10);
            brand.Controls.Add(logo);
        }
        else
        {
            // Fallback when the logo cannot be loaded: text only.
            brand.Controls.Add(new Label
            {
                Text = "Parts & Accessories Shop",
                ForeColor = Color.FromArgb(156, 163, 175),
                Font = Theme.Small,
                AutoSize = true,
                Location = new Point(20, 56),
            });
            brand.Controls.Add(new Label
            {
                Text = "357 MOTORCYCLE",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 26),
            });
        }

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Sidebar,
            Padding = new Padding(0, 8, 0, 0),
        };

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 160,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Sidebar,
            Padding = new Padding(16, 12, 16, 12),
        };
        bottom.Controls.Add(new Label
        {
            Text = Session.FullName,
            ForeColor = Color.White,
            Font = Theme.Bold,
            AutoSize = false,
            Width = 188,
            Height = 24,
        });
        bottom.Controls.Add(new Label
        {
            Text = Session.Role,
            ForeColor = Color.FromArgb(156, 163, 175),
            AutoSize = false,
            Width = 188,
            Height = 26,
        });

        Ui.Style(_bell, BtnKind.Normal);
        _bell.Click += (s, e) =>
        {
            using var f = new NotificationsForm();
            f.ShowDialog(this);
            RefreshBadges();
        };
        var logout = new Button { Text = "Log out", Width = 188, Height = 36 };
        Ui.Style(logout, BtnKind.Normal);
        logout.Click += (s, e) =>
        {
            if (!Ui.Confirm("Log out now?")) return;
            LoggedOut = true;
            Session.Clear();
            Close();
        };
        bottom.Controls.Add(_bell);
        bottom.Controls.Add(logout);

        // Order matters for docking: Fill first, then Bottom, then Top (last added is laid out first).
        side.Controls.Add(nav);
        side.Controls.Add(bottom);
        side.Controls.Add(brand);

        // ----- pages -----
        AddPage(nav, "Dashboard", new DashboardControl(GoTo));
        AddPage(nav, "Inventory", new InventoryControl());
        AddPage(nav, "Requisition", new RequisitionControl());
        AddPage(nav, "Purchasing", new PurchasingControl());
        AddPage(nav, "Delivery", new DeliveryControl());
        AddPage(nav, "Suppliers", new SuppliersControl());
        AddPage(nav, "Reports", new ReportsControl());
        if (Session.IsOwner) AddPage(nav, "Users", new UsersControl());

        // ----- title bar + content -----
        var titleBar = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.White };
        titleBar.Controls.Add(_title);
        titleBar.Controls.Add(_alert);

        var main = new Panel { Dock = DockStyle.Fill };
        main.Controls.Add(_host);
        main.Controls.Add(titleBar);

        Controls.Add(main);
        Controls.Add(side);

        _timer.Tick += (s, e) => RefreshBadges();
        Load += (s, e) =>
        {
            GoTo("Dashboard");
            _timer.Start();
        };
        FormClosed += (s, e) => _timer.Stop();
    }

    void AddPage(FlowLayoutPanel nav, string name, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.BackColor = Theme.Card;
        page.ForeColor = Theme.Text;
        page.Visible = false;
        _host.Controls.Add(page);
        _pages[name] = page;

        var b = new Button
        {
            Text = "   " + name,
            Width = 220,
            Height = 42,
            Margin = new Padding(0),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            Font = NavFont,
            ForeColor = Color.FromArgb(209, 213, 219),
            BackColor = Theme.Sidebar,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
        b.FlatAppearance.MouseDownBackColor = Theme.SidebarHover;
        b.Click += (s, e) => GoTo(name);
        nav.Controls.Add(b);
        _navButtons[name] = b;
    }

    /// <summary>Opens a page by name (also used by the dashboard tiles).</summary>
    void GoTo(string name)
    {
        if (!_pages.ContainsKey(name)) return;
        _current = name;

        foreach (var kv in _pages) kv.Value.Visible = kv.Key == name;

        foreach (var kv in _navButtons)
        {
            bool on = kv.Key == name;
            kv.Value.BackColor = on ? Theme.Accent : Theme.Sidebar;
            kv.Value.ForeColor = on ? Color.White : Color.FromArgb(209, 213, 219);
            kv.Value.FlatAppearance.MouseOverBackColor = on ? Theme.Accent : Theme.SidebarHover;
            kv.Value.Font = on ? NavFontBold : NavFont;
        }

        _title.Text = name;
        RefreshCurrent();
    }

    void RefreshCurrent()
    {
        try
        {
            if (_pages.TryGetValue(_current, out var page) && page is IRefreshable r)
                r.RefreshData();
            RefreshBadges();
        }
        catch (Exception ex)
        {
            Ui.Error("Could not load this screen.\n\n" + ex.Message);
        }
    }

    void RefreshBadges()
    {
        try
        {
            int unread = NotificationsForm.UnreadCount();
            _bell.Text = unread > 0 ? $"Notifications ({unread})" : "Notifications";
            if (unread > 0) Ui.Style(_bell, BtnKind.Primary);
            else Ui.Style(_bell, BtnKind.Normal);

            int low = Db.ScalarInt("SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND Quantity <= MinStock");
            _alert.Text = low > 0 ? $"Low-stock alert: {low} item(s) at or below minimum" : "";
        }
        catch
        {
            // Ignore: the timer should never crash the app if the database is briefly unreachable.
        }
    }
}
