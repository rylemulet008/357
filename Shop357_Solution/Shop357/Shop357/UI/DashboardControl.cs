using System.Data;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>A clickable number tile for the dashboard.</summary>
public class KpiCard : Panel
{
    readonly Label _value;

    public KpiCard(string caption, Color accent, Action onClick)
    {
        Width = 218;
        Height = 96;
        Margin = new Padding(0, 0, 16, 16);
        BackColor = Theme.AltRow;
        Cursor = Cursors.Hand;

        var strip = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = accent };
        _value = new Label
        {
            Text = "-",
            Font = Theme.Kpi,
            ForeColor = Theme.Text,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(14, 8, 0, 0),
        };
        var cap = new Label
        {
            Text = caption,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 0, 0, 0),
        };

        Controls.Add(cap);
        Controls.Add(_value);
        Controls.Add(strip);

        EventHandler click = (s, e) => onClick();
        Click += click;
        strip.Click += click;
        _value.Click += click;
        cap.Click += click;
    }

    public void SetValue(string text, bool alert)
    {
        _value.Text = text;
        _value.ForeColor = alert ? Theme.Danger : Theme.Text;
    }
}

/// <summary>Home screen: key numbers, items to restock, and the latest notifications.</summary>
public class DashboardControl : UserControl, IRefreshable
{
    readonly KpiCard _products;
    readonly KpiCard _low;
    readonly KpiCard _out;
    readonly KpiCard _pending;
    readonly KpiCard _open;
    readonly KpiCard _late;
    readonly KpiCard _value;
    readonly DataGridView _restock = Ui.Grid();
    readonly DataGridView _notes = Ui.Grid();
    readonly Label _hello = new Label
    {
        Dock = DockStyle.Top,
        Height = 64,
        Padding = new Padding(16, 14, 0, 0),
        Font = new Font("Segoe UI", 13f, FontStyle.Bold),
    };

    public DashboardControl(Action<string> go)
    {
        Dock = DockStyle.Fill;

        _products = new KpiCard("Active products", Theme.Accent, () => go("Inventory"));
        _low = new KpiCard("Low stock", Theme.Danger, () => go("Inventory"));
        _out = new KpiCard("Out of stock", Theme.Danger, () => go("Inventory"));
        _pending = new KpiCard("Requests waiting for approval", Theme.Accent, () => go("Requisition"));
        _open = new KpiCard("Open purchase orders", Theme.Accent, () => go("Delivery"));
        _late = new KpiCard("Late deliveries", Theme.Danger, () => go("Delivery"));
        _value = new KpiCard("Stock value (at cost)", Theme.Accent, () => go("Reports"));

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(16, 0, 0, 0),
        };
        cards.Controls.AddRange(new Control[] { _products, _low, _out, _pending, _open, _late, _value });

        var left = new GroupBox { Text = "Items to restock (click a tile above to open the full list)", Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10) };
        left.Controls.Add(_restock);
        var right = new GroupBox { Text = "Latest notifications", Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10) };
        right.Controls.Add(_notes);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(16, 0, 16, 16) };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Margin = new Padding(0, 0, 8, 0);
        right.Margin = new Padding(8, 0, 0, 0);
        split.Controls.Add(left, 0, 0);
        split.Controls.Add(right, 1, 0);

        Controls.Add(split);
        Controls.Add(cards);
        Controls.Add(_hello);
    }

    public void RefreshData()
    {
        _hello.Text = $"Welcome, {Session.FullName}.  Today is {DateTime.Now:dddd, MMMM d, yyyy}.";

        var k = Db.Query(@"
            SELECT
              (SELECT COUNT(*) FROM Products WHERE IsActive = 1) AS Products,
              (SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND Quantity > 0 AND Quantity <= MinStock) AS Low,
              (SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND Quantity = 0) AS OutOfStock,
              (SELECT COUNT(*) FROM PurchaseRequests WHERE Status = 'Pending Approval') AS Pending,
              (SELECT COUNT(*) FROM PurchaseOrders WHERE Status IN ('Ordered', 'Partially Delivered')) AS OpenPos,
              (SELECT COUNT(*) FROM PurchaseOrders
                 WHERE Status IN ('Ordered', 'Partially Delivered') AND ExpectedDate < CAST(SYSDATETIME() AS DATE)) AS LatePos,
              (SELECT ISNULL(SUM(Quantity * UnitCost), 0) FROM Products WHERE IsActive = 1) AS StockValue").Rows[0];

        int low = Convert.ToInt32(k["Low"]);
        int outOf = Convert.ToInt32(k["OutOfStock"]);
        int late = Convert.ToInt32(k["LatePos"]);

        _products.SetValue(Convert.ToString(k["Products"]), false);
        _low.SetValue(low.ToString(), low > 0);
        _out.SetValue(outOf.ToString(), outOf > 0);
        _pending.SetValue(Convert.ToString(k["Pending"]), false);
        _open.SetValue(Convert.ToString(k["OpenPos"]), false);
        _late.SetValue(late.ToString(), late > 0);
        _value.SetValue(Convert.ToDecimal(k["StockValue"]).ToString("N0"), false);

        _restock.DataSource = Db.Query(@"
            SELECT TOP 12 p.Code, p.Name, p.Quantity, p.MinStock AS [Min Stock]
            FROM Products p
            WHERE p.IsActive = 1 AND p.Quantity <= p.MinStock
            ORDER BY p.Quantity, p.Name");

        _notes.DataSource = Db.Query(@"
            SELECT TOP 10 CreatedAt AS [When], Message
            FROM Notifications
            WHERE ForRole = @r OR ForRole = 'All'
            ORDER BY NotificationId DESC", Db.P("@r", Session.Role));
        if (_notes.Columns.Contains("When")) _notes.Columns["When"].DefaultCellStyle.Format = "MMM d, HH:mm";
        if (_notes.Columns.Contains("When")) _notes.Columns["When"].FillWeight = 40;
        if (_notes.Columns.Contains("Message")) _notes.Columns["Message"].FillWeight = 160;
    }
}
