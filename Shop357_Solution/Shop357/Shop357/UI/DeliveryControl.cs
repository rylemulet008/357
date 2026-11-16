using System.Data;
using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>
/// Delivery tab: open purchase orders with expected dates (late = red), receive deliveries,
/// flag missing/damaged items, and add only the good quantity to inventory.
/// </summary>
public class DeliveryControl : UserControl, IRefreshable
{
    readonly DataGridView _pos = Ui.Grid();
    readonly DataGridView _history = Ui.Grid();

    public DeliveryControl()
    {
        Dock = DockStyle.Fill;

        var bar = Ui.Bar();
        bar.Controls.Add(Ui.Btn("Receive Delivery", (s, e) => Receive(), 150));
        bar.Controls.Add(Ui.Btn("View PO Items", (s, e) => ViewItems()));

        var top = new GroupBox { Text = "Open purchase orders (red = past the expected date)", Dock = DockStyle.Fill };
        top.Controls.Add(_pos);
        var bottom = new GroupBox { Text = "Delivery history for the selected purchase order", Dock = DockStyle.Fill };
        bottom.Controls.Add(_history);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(bottom, 0, 1);

        Controls.Add(layout);
        Controls.Add(bar);

        _pos.SelectionChanged += (s, e) => LoadHistory();
        _pos.DataBindingComplete += (s, e) => Colour();
        _history.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewRow r in _history.Rows)
                if (Convert.ToString(r.Cells["Result"].Value) == "Discrepancy")
                    r.DefaultCellStyle.BackColor = Color.FromArgb(255, 228, 228);
        };
    }

    public void RefreshData()
    {
        _pos.DataSource = Db.Query(@"
            SELECT o.PoId, o.PoNumber AS [PO No.], s.Name AS Supplier, o.ExpectedDate AS [Expected],
                   CASE WHEN o.ExpectedDate < CAST(SYSDATETIME() AS DATE) THEN 'LATE' ELSE '' END AS [Late?],
                   o.Status, o.PaymentMethod AS [Payment],
                   CASE WHEN o.IsPaid = 1 THEN 'Paid' ELSE 'Unpaid' END AS [Paid?],
                   (SELECT SUM(Quantity) FROM PurchaseOrderItems WHERE PoId = o.PoId) AS [Qty Ordered],
                   (SELECT SUM(ReceivedQty) FROM PurchaseOrderItems WHERE PoId = o.PoId) AS [Qty Accepted]
            FROM PurchaseOrders o
            JOIN Suppliers s ON s.SupplierId = o.SupplierId
            WHERE o.Status IN ('Ordered', 'Partially Delivered')
            ORDER BY o.ExpectedDate, o.PoId");
        Ui.Hide(_pos, "PoId");
        if (_pos.Columns.Contains("Expected")) _pos.Columns["Expected"].DefaultCellStyle.Format = "yyyy-MM-dd";
        LoadHistory();
    }

    void Colour()
    {
        foreach (DataGridViewRow r in _pos.Rows)
            if (Convert.ToString(r.Cells["Late?"].Value) == "LATE")
                r.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
    }

    void LoadHistory()
    {
        var id = Ui.SelectedId(_pos, "PoId");
        _history.DataSource = id == null ? null : Db.Query(@"
            SELECT d.DeliveryId, d.ReceivedAt AS [Received On], u.FullName AS [Received By], d.Status AS Result,
                   (SELECT SUM(ReceivedQty) FROM DeliveryItems WHERE DeliveryId = d.DeliveryId) AS [Qty Arrived],
                   (SELECT SUM(DamagedQty) FROM DeliveryItems WHERE DeliveryId = d.DeliveryId) AS [Qty Damaged],
                   d.Notes
            FROM Deliveries d JOIN Users u ON u.UserId = d.ReceivedBy
            WHERE d.PoId = @p ORDER BY d.DeliveryId DESC", Db.P("@p", id));
        Ui.Hide(_history, "DeliveryId");
    }

    int? NeedPo()
    {
        var id = Ui.SelectedId(_pos, "PoId");
        if (id == null) Ui.Warn("Select a purchase order first.");
        return id;
    }

    void ViewItems()
    {
        var id = NeedPo();
        if (id == null) return;
        var dt = Db.Query(@"
            SELECT p.Code, p.Name AS Product, i.Quantity AS Ordered, i.ReceivedQty AS [Accepted so far],
                   i.Quantity - i.ReceivedQty AS [Still to come]
            FROM PurchaseOrderItems i JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PoId = @id ORDER BY i.PoItemId", Db.P("@id", id));
        Ui.ShowGrid(FindForm(), "Items of " + Ui.SelectedText(_pos, "PO No."), dt);
    }

    void Receive()
    {
        var id = NeedPo();
        if (id == null) return;
        using var f = new ReceiveForm(id.Value, Ui.SelectedText(_pos, "PO No."));
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }
}

/// <summary>
/// For each item enter how many arrived and how many of those are damaged.
/// Accepted (good) quantity = arrived - damaged. Only the good quantity is added to inventory.
/// </summary>
public class ReceiveForm : Form
{
    readonly int _poId;
    readonly string _poNo;
    readonly DataGridView _grid = Ui.Grid();
    readonly DataTable _table = new DataTable();
    readonly TextBox _notes = new TextBox();

    public ReceiveForm(int poId, string poNo)
    {
        _poId = poId;
        _poNo = poNo;
        Ui.Setup(this, "Receive Delivery - " + poNo, 900, 560);

        _table.Columns.Add("PoItemId", typeof(int));
        _table.Columns.Add("ProductId", typeof(int));
        _table.Columns.Add("Product", typeof(string));
        _table.Columns.Add("Ordered", typeof(int));
        _table.Columns.Add("Already accepted", typeof(int));
        _table.Columns.Add("Still to come", typeof(int));
        _table.Columns.Add("Arrived now", typeof(int));
        _table.Columns.Add("Damaged", typeof(int));
        _table.Columns.Add("Item note", typeof(string));

        foreach (DataRow r in Db.Query(@"
            SELECT i.PoItemId, i.ProductId, p.Name, i.Quantity, i.ReceivedQty
            FROM PurchaseOrderItems i JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PoId = @id ORDER BY i.PoItemId", Db.P("@id", poId)).Rows)
        {
            int ordered = Convert.ToInt32(r["Quantity"]);
            int got = Convert.ToInt32(r["ReceivedQty"]);
            int remaining = Math.Max(ordered - got, 0);
            _table.Rows.Add(r["PoItemId"], r["ProductId"], r["Name"], ordered, got, remaining, remaining, 0, "");
        }

        _grid.DataSource = _table;
        _grid.ReadOnly = false;
        foreach (DataGridViewColumn c in _grid.Columns)
            c.ReadOnly = c.Name != "Arrived now" && c.Name != "Damaged" && c.Name != "Item note";
        Ui.Hide(_grid, "PoItemId", "ProductId");
        _grid.DataError += (s, e) =>
        {
            Ui.Warn("Please type a whole number (0 or more).");
            e.ThrowException = false;
        };

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(12, 6, 12, 0),
            Text = "Check each item against the purchase order. \"Arrived now\" = everything that came in this delivery. " +
                   "\"Damaged\" = how many of those are damaged or defective. Only the good ones are added to inventory.",
        };

        var t = Ui.FormTable();
        Ui.AddRow(t, "Delivery notes", _notes);

        Controls.Add(_grid);
        Controls.Add(hint);
        Controls.Add(t);
        Ui.AddButtons(this, Save, "Record Delivery");
    }

    bool Save()
    {
        _grid.EndEdit();

        int totalArrived = 0, totalGood = 0, totalDamaged = 0;
        bool shortage = false, over = false;

        foreach (DataRow r in _table.Rows)
        {
            if (r["Arrived now"] == DBNull.Value || r["Damaged"] == DBNull.Value)
            {
                Ui.Warn("Fill in 'Arrived now' and 'Damaged' for every item (use 0 if none).");
                return false;
            }

            int arrived = Convert.ToInt32(r["Arrived now"]);
            int damaged = Convert.ToInt32(r["Damaged"]);
            int remaining = Convert.ToInt32(r["Still to come"]);
            string name = Convert.ToString(r["Product"]);

            if (arrived < 0 || damaged < 0)
            {
                Ui.Warn($"{name}: numbers cannot be negative.");
                return false;
            }
            if (damaged > arrived)
            {
                Ui.Warn($"{name}: damaged ({damaged}) cannot be more than arrived ({arrived}).");
                return false;
            }

            int good = arrived - damaged;
            if (arrived > remaining) over = true;
            if (good < remaining) shortage = true;

            totalArrived += arrived;
            totalGood += good;
            totalDamaged += damaged;
        }

        if (totalArrived == 0)
        {
            Ui.Warn("Nothing was entered as arrived.");
            return false;
        }

        // Rule: a delivery cannot be more than ordered without an Owner override.
        if (over)
        {
            if (!Session.IsOwner)
            {
                Ui.Warn("An item arrived in a larger quantity than ordered.\nOnly the Owner can approve extra quantity. Ask the Owner to record this delivery.");
                return false;
            }
            if (!Ui.Confirm("More items arrived than were ordered.\nAs Owner, do you approve receiving the extra quantity?"))
                return false;
        }

        string status = shortage || totalDamaged > 0 ? "Discrepancy" : "Accepted";
        string summary = status == "Discrepancy"
            ? $"{totalGood} good item(s) will be added to inventory.\n{totalDamaged} damaged and/or some items are still missing: this will be flagged as a DISCREPANCY.\n\nRecord this delivery?"
            : $"All {totalGood} item(s) are complete and in good condition.\nThey will be added to inventory.\n\nRecord this delivery?";
        if (!Ui.Confirm(summary)) return false;

        try
        {
            Db.Run(tx =>
            {
                int deliveryId = tx.ScalarInt(@"
                    INSERT INTO Deliveries(PoId, ReceivedBy, Status, Notes) VALUES(@po, @u, @s, @n);
                    SELECT SCOPE_IDENTITY();",
                    Db.P("@po", _poId), Db.P("@u", Session.UserId), Db.P("@s", status),
                    Db.P("@n", string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim()));

                foreach (DataRow r in _table.Rows)
                {
                    int arrived = Convert.ToInt32(r["Arrived now"]);
                    int damaged = Convert.ToInt32(r["Damaged"]);
                    int good = arrived - damaged;
                    string note = Convert.ToString(r["Item note"]);

                    if (arrived == 0 && damaged == 0) continue;

                    tx.Exec(@"INSERT INTO DeliveryItems(DeliveryId, PoItemId, ReceivedQty, DamagedQty, Notes)
                              VALUES(@d, @i, @r, @x, @n)",
                        Db.P("@d", deliveryId), Db.P("@i", Convert.ToInt32(r["PoItemId"])),
                        Db.P("@r", arrived), Db.P("@x", damaged),
                        Db.P("@n", string.IsNullOrWhiteSpace(note) ? null : note.Trim()));

                    if (good > 0)
                    {
                        tx.Exec("UPDATE PurchaseOrderItems SET ReceivedQty = ReceivedQty + @g WHERE PoItemId = @i",
                            Db.P("@g", good), Db.P("@i", Convert.ToInt32(r["PoItemId"])));

                        // Rule: inventory changes only here, and only by the accepted quantity.
                        StockService.Apply(tx, Convert.ToInt32(r["ProductId"]), good,
                            $"Delivery accepted ({_poNo})", "Delivery", deliveryId);
                    }
                }

                // Delivered only when every item has been fully accepted.
                int stillMissing = tx.ScalarInt(
                    "SELECT COUNT(*) FROM PurchaseOrderItems WHERE PoId = @po AND ReceivedQty < Quantity", Db.P("@po", _poId));
                tx.Exec("UPDATE PurchaseOrders SET Status = @s WHERE PoId = @po",
                    Db.P("@s", stillMissing == 0 ? "Delivered" : "Partially Delivered"), Db.P("@po", _poId));

                if (status == "Discrepancy")
                {
                    string msg = $"Delivery discrepancy on {_poNo}: {totalDamaged} damaged" +
                                 (shortage ? ", some items still missing" : "") +
                                 ". Contact the supplier and wait for the replacement.";
                    Notify.Send(tx, "Owner", msg, "PO", _poId);
                    Notify.Send(tx, "Staff", msg, "PO", _poId);
                }
            });
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }

        if (status == "Discrepancy")
            Ui.Info("Delivery recorded and flagged as a discrepancy.\nContact the supplier about the damaged or missing items. When the replacement arrives, use Receive Delivery again.");
        else
            Ui.Info("Delivery recorded. Inventory has been updated.");
        return true;
    }
}
