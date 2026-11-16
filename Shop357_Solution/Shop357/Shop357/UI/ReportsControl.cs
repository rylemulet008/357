using System.Data;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>Reports tab: pick a report, view it, export to CSV (opens in Excel) or print as PDF from Excel.</summary>
public class ReportsControl : UserControl, IRefreshable
{
    readonly ComboBox _report = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, Margin = new Padding(0, 5, 14, 0) };
    readonly DataGridView _grid = Ui.Grid();

    static readonly (string Name, string Sql)[] Reports =
    {
        ("Current stock",
            @"SELECT p.Code, p.Name, c.Name AS Category, p.Brand, p.Quantity, p.MinStock AS [Min Stock],
                     p.UnitCost AS [Unit Cost], p.Quantity * p.UnitCost AS [Stock Value]
              FROM Products p JOIN Categories c ON c.CategoryId = p.CategoryId
              WHERE p.IsActive = 1 ORDER BY p.Name"),

        ("Low-stock list",
            @"SELECT p.Code, p.Name, c.Name AS Category, p.Quantity, p.MinStock AS [Min Stock],
                     CASE WHEN p.Quantity = 0 THEN 'OUT' ELSE 'LOW' END AS Stock
              FROM Products p JOIN Categories c ON c.CategoryId = p.CategoryId
              WHERE p.IsActive = 1 AND p.Quantity <= p.MinStock ORDER BY p.Quantity, p.Name"),

        ("Purchase requests by status",
            @"SELECT r.Status, COUNT(DISTINCT r.PrId) AS [Number of PRs], COUNT(i.PrItemId) AS [Total Items]
              FROM PurchaseRequests r
              LEFT JOIN PurchaseRequestItems i ON i.PrId = r.PrId
              GROUP BY r.Status ORDER BY r.Status"),

        ("Purchase orders by supplier",
            @"SELECT s.Name AS Supplier, COUNT(*) AS [Number of POs], SUM(o.TotalAmount) AS [Total Amount],
                     SUM(CASE WHEN o.IsPaid = 1 THEN o.TotalAmount ELSE 0 END) AS [Paid],
                     SUM(CASE WHEN o.IsPaid = 0 THEN o.TotalAmount ELSE 0 END) AS [Unpaid]
              FROM PurchaseOrders o JOIN Suppliers s ON s.SupplierId = o.SupplierId
              GROUP BY s.Name ORDER BY SUM(o.TotalAmount) DESC"),

        ("Delivery discrepancies",
            @"SELECT d.ReceivedAt AS [Received On], o.PoNumber AS [PO No.], s.Name AS Supplier, p.Name AS Product,
                     oi.Quantity AS Ordered, di.ReceivedQty AS Arrived, di.DamagedQty AS Damaged, di.Notes
              FROM Deliveries d
              JOIN PurchaseOrders o ON o.PoId = d.PoId
              JOIN Suppliers s ON s.SupplierId = o.SupplierId
              JOIN DeliveryItems di ON di.DeliveryId = d.DeliveryId
              JOIN PurchaseOrderItems oi ON oi.PoItemId = di.PoItemId
              JOIN Products p ON p.ProductId = oi.ProductId
              WHERE d.Status = 'Discrepancy' AND (di.DamagedQty > 0 OR di.ReceivedQty < oi.Quantity)
              ORDER BY d.ReceivedAt DESC"),

        ("Purchase history",
            @"SELECT o.CreatedAt AS [Ordered On], o.PoNumber AS [PO No.], r.PrNumber AS [PR No.], s.Name AS Supplier,
                     p.Name AS Product, oi.Quantity, oi.UnitPrice AS [Unit Price], oi.Quantity * oi.UnitPrice AS [Line Total],
                     oi.ReceivedQty AS [Accepted], o.Status
              FROM PurchaseOrderItems oi
              JOIN PurchaseOrders o ON o.PoId = oi.PoId
              JOIN PurchaseRequests r ON r.PrId = o.PrId
              JOIN Suppliers s ON s.SupplierId = o.SupplierId
              JOIN Products p ON p.ProductId = oi.ProductId
              ORDER BY o.PoId DESC, oi.PoItemId"),

        ("Stock movements (latest 500)",
            @"SELECT TOP 500 m.CreatedAt AS [Date], p.Code, p.Name AS Product, m.QtyChange AS [Change],
                     m.QtyAfter AS [Balance], m.Reason, u.FullName AS [By]
              FROM StockMovements m
              JOIN Products p ON p.ProductId = m.ProductId
              JOIN Users u ON u.UserId = m.CreatedBy
              ORDER BY m.MovementId DESC"),
    };

    public ReportsControl()
    {
        Dock = DockStyle.Fill;

        foreach (var r in Reports) _report.Items.Add(r.Name);
        _report.SelectedIndex = 0;
        _report.SelectedIndexChanged += (s, e) => RefreshData();

        var bar = Ui.Bar();
        bar.Controls.Add(Ui.Lbl("Report:"));
        bar.Controls.Add(_report);
        bar.Controls.Add(Ui.Btn("Refresh", (s, e) => RefreshData()));
        bar.Controls.Add(Ui.Btn("Export to CSV / Excel", (s, e) => Ui.ExportCsv(_grid, _report.Text.Replace(' ', '_')), 170));

        Controls.Add(_grid);
        Controls.Add(bar);
    }

    public void RefreshData()
    {
        int i = Math.Max(_report.SelectedIndex, 0);
        _grid.DataSource = Db.Query(Reports[i].Sql);

        foreach (DataGridViewColumn c in _grid.Columns)
        {
            if (c.ValueType == typeof(decimal))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            else if (c.ValueType == typeof(DateTime))
                c.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
        }
    }
}
