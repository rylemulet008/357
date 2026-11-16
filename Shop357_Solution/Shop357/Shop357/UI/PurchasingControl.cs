using System.Data;
using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>
/// Purchasing tab. Two sub-tabs:
///  1) Quotations - approved PRs, record 2-3 supplier quotations, compare, create the PO
///  2) Purchase Orders - view items, mark as paid, close
/// </summary>
public class PurchasingControl : UserControl, IRefreshable
{
    readonly TabControl _tabs = new TabControl
    {
        Dock = DockStyle.Fill,
        ItemSize = new Size(170, 34),
        Padding = new Point(18, 6),
        SizeMode = TabSizeMode.Fixed,
    };
    readonly DataGridView _prs = Ui.Grid();
    readonly DataGridView _quotes = Ui.Grid();
    readonly DataGridView _pos = Ui.Grid();

    const string QuotesSql = @"
        SELECT q.QuotationId, s.Name AS Supplier, SUM(qi.UnitPrice * pi.Quantity) AS Total,
               q.DeliveryDays AS [Delivery (days)], q.Notes,
               CASE WHEN q.IsSelected = 1 THEN 'Yes' ELSE '' END AS [Chosen]
        FROM Quotations q
        JOIN Suppliers s ON s.SupplierId = q.SupplierId
        JOIN QuotationItems qi ON qi.QuotationId = q.QuotationId
        JOIN PurchaseRequestItems pi ON pi.PrItemId = qi.PrItemId
        WHERE q.PrId = @p
        GROUP BY q.QuotationId, s.Name, q.DeliveryDays, q.Notes, q.IsSelected
        ORDER BY SUM(qi.UnitPrice * pi.Quantity)";

    public PurchasingControl()
    {
        Dock = DockStyle.Fill;

        // ----- sub-tab 1: quotations -----
        var bar1 = Ui.Bar();
        bar1.Controls.Add(Ui.Btn("Add Quotation", (s, e) => AddQuote()));
        bar1.Controls.Add(Ui.Btn("Delete Quotation", (s, e) => DeleteQuote(), 140));
        bar1.Controls.Add(Ui.Btn("Compare Quotations", (s, e) => Compare(), 160));
        bar1.Controls.Add(Ui.Btn("Create PO from Selected", (s, e) => CreatePo(), 190));

        var topBox = new GroupBox { Text = "Approved purchase requests (waiting for a purchase order)", Dock = DockStyle.Fill };
        topBox.Controls.Add(_prs);
        var bottomBox = new GroupBox { Text = "Supplier quotations for the selected request (record at least 2)", Dock = DockStyle.Fill };
        bottomBox.Controls.Add(_quotes);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.Controls.Add(topBox, 0, 0);
        layout.Controls.Add(bottomBox, 0, 1);

        var page1 = new TabPage("Quotations");
        page1.Controls.Add(layout);
        page1.Controls.Add(bar1);

        // ----- sub-tab 2: purchase orders -----
        var bar2 = Ui.Bar();
        bar2.Controls.Add(Ui.Btn("View Items", (s, e) => ViewPoItems()));
        bar2.Controls.Add(Ui.Btn("Mark as Paid", (s, e) => MarkPaid()));
        bar2.Controls.Add(Ui.Btn("Close PO", (s, e) => ClosePo()));

        var page2 = new TabPage("Purchase Orders");
        page2.Controls.Add(_pos);
        page2.Controls.Add(bar2);

        _tabs.TabPages.Add(page1);
        _tabs.TabPages.Add(page2);
        Controls.Add(_tabs);

        _prs.SelectionChanged += (s, e) => LoadQuotes();
        _tabs.SelectedIndexChanged += (s, e) => RefreshData();
    }

    public void RefreshData()
    {
        _prs.DataSource = Db.Query(@"
            SELECT r.PrId, r.PrNumber AS [PR No.], u.FullName AS [Requested By], r.ApprovedAt AS [Approved On],
                   (SELECT COUNT(*) FROM PurchaseRequestItems i WHERE i.PrId = r.PrId) AS Items,
                   (SELECT COUNT(*) FROM Quotations q WHERE q.PrId = r.PrId) AS Quotations
            FROM PurchaseRequests r
            JOIN Users u ON u.UserId = r.CreatedBy
            WHERE r.Status = 'Approved'
            ORDER BY r.PrId");
        Ui.Hide(_prs, "PrId");
        LoadQuotes();

        _pos.DataSource = Db.Query(@"
            SELECT o.PoId, o.PoNumber AS [PO No.], r.PrNumber AS [PR No.], s.Name AS Supplier,
                   o.PaymentMethod AS [Payment], CASE WHEN o.IsPaid = 1 THEN 'Paid' ELSE 'Unpaid' END AS [Paid?],
                   o.Status, o.ExpectedDate AS [Expected], o.TotalAmount AS Total, o.CreatedAt AS [Created]
            FROM PurchaseOrders o
            JOIN PurchaseRequests r ON r.PrId = o.PrId
            JOIN Suppliers s ON s.SupplierId = o.SupplierId
            ORDER BY o.PoId DESC");
        Ui.Hide(_pos, "PoId");
        Ui.Fmt(_pos, "N2", "Total");
        if (_pos.Columns.Contains("Expected")) _pos.Columns["Expected"].DefaultCellStyle.Format = "yyyy-MM-dd";
    }

    void LoadQuotes()
    {
        var id = Ui.SelectedId(_prs, "PrId");
        _quotes.DataSource = id == null ? null : Db.Query(QuotesSql, Db.P("@p", id));
        Ui.Hide(_quotes, "QuotationId");
        Ui.Fmt(_quotes, "N2", "Total");
    }

    int? NeedPr()
    {
        var id = Ui.SelectedId(_prs, "PrId");
        if (id == null) Ui.Warn("Select an approved purchase request first.");
        return id;
    }

    void AddQuote()
    {
        var id = NeedPr();
        if (id == null) return;
        using var f = new QuotationForm(id.Value, Ui.SelectedText(_prs, "PR No."));
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void DeleteQuote()
    {
        var qid = Ui.SelectedId(_quotes, "QuotationId");
        if (qid == null)
        {
            Ui.Warn("Select a quotation first.");
            return;
        }
        if (!Ui.Confirm("Delete this quotation?")) return;
        Db.Run(tx =>
        {
            tx.Exec("DELETE FROM QuotationItems WHERE QuotationId = @q", Db.P("@q", qid));
            tx.Exec("DELETE FROM Quotations WHERE QuotationId = @q AND IsSelected = 0", Db.P("@q", qid));
        });
        RefreshData();
    }

    void Compare()
    {
        var id = NeedPr();
        if (id == null) return;
        if (_quotes.Rows.Count == 0)
        {
            Ui.Warn("There are no quotations to compare yet.");
            return;
        }
        using var f = new CompareForm(id.Value, Ui.SelectedText(_prs, "PR No."));
        f.ShowDialog(FindForm());
    }

    void CreatePo()
    {
        var prId = NeedPr();
        if (prId == null) return;
        var qid = Ui.SelectedId(_quotes, "QuotationId");
        if (qid == null)
        {
            Ui.Warn("Select the supplier's quotation you want to order from.");
            return;
        }

        // Rule: at least 2 quotations before a PO can be made.
        if (_quotes.Rows.Count < 2)
        {
            Ui.Warn("At least 2 supplier quotations are required before creating a purchase order.");
            return;
        }

        // The quotation must price every item of the request.
        int prItems = Db.ScalarInt("SELECT COUNT(*) FROM PurchaseRequestItems WHERE PrId = @p", Db.P("@p", prId));
        int quoted = Db.ScalarInt("SELECT COUNT(*) FROM QuotationItems WHERE QuotationId = @q", Db.P("@q", qid));
        if (quoted != prItems)
        {
            Ui.Warn("This quotation does not price every item of the request.");
            return;
        }

        decimal chosenTotal = Convert.ToDecimal(_quotes.CurrentRow.Cells["Total"].Value);
        decimal lowest = _quotes.Rows.Cast<DataGridViewRow>().Min(r => Convert.ToDecimal(r.Cells["Total"].Value));
        if (chosenTotal > lowest &&
            !Ui.Confirm($"This is not the lowest quotation (lowest is {lowest:N2}).\nContinue only if its quality or specifications are better.\n\nCreate the PO anyway?"))
            return;

        var supplier = Ui.SelectedText(_quotes, "Supplier");
        int days = Convert.ToInt32(_quotes.CurrentRow.Cells["Delivery (days)"].Value);
        using var form = new CreatePoForm(supplier, chosenTotal, days);
        if (form.ShowDialog(FindForm()) != DialogResult.OK) return;

        string poNo = null;
        try
        {
            Db.Run(tx =>
            {
                int poId = tx.ScalarInt(@"
                    INSERT INTO PurchaseOrders(PrId, QuotationId, SupplierId, PaymentMethod, ExpectedDate, TotalAmount, CreatedBy)
                    SELECT @pr, q.QuotationId, q.SupplierId, @pm, @d, 0, @u FROM Quotations q WHERE q.QuotationId = @q;
                    SELECT SCOPE_IDENTITY();",
                    Db.P("@pr", prId), Db.P("@pm", form.PaymentMethod), Db.P("@d", form.ExpectedDate.Date),
                    Db.P("@u", Session.UserId), Db.P("@q", qid));
                tx.Exec(Docs.NumberSql("PurchaseOrders", "PoId", "PoNumber", "PO"), Db.P("@id", poId));

                tx.Exec(@"
                    INSERT INTO PurchaseOrderItems(PoId, ProductId, Quantity, UnitPrice)
                    SELECT @po, pi.ProductId, pi.Quantity, qi.UnitPrice
                    FROM QuotationItems qi
                    JOIN PurchaseRequestItems pi ON pi.PrItemId = qi.PrItemId
                    WHERE qi.QuotationId = @q",
                    Db.P("@po", poId), Db.P("@q", qid));

                tx.Exec("UPDATE PurchaseOrders SET TotalAmount = (SELECT SUM(Quantity * UnitPrice) FROM PurchaseOrderItems WHERE PoId = @po) WHERE PoId = @po",
                    Db.P("@po", poId));
                tx.Exec("UPDATE Quotations SET IsSelected = 1 WHERE QuotationId = @q", Db.P("@q", qid));
                tx.Exec("UPDATE PurchaseRequests SET Status = 'Ordered' WHERE PrId = @pr", Db.P("@pr", prId));

                poNo = Convert.ToString(tx.Scalar("SELECT PoNumber FROM PurchaseOrders WHERE PoId = @po", Db.P("@po", poId)));
                Notify.Send(tx, "Owner", $"{poNo} was created for {supplier} ({form.PaymentMethod}).", "PO", poId);
            });
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return;
        }

        Ui.Info($"{poNo} created. Track it in the Delivery tab.");
        RefreshData();
        _tabs.SelectedIndex = 1;
    }

    // ----- purchase order actions -----
    int? NeedPo()
    {
        var id = Ui.SelectedId(_pos, "PoId");
        if (id == null) Ui.Warn("Select a purchase order first.");
        return id;
    }

    void ViewPoItems()
    {
        var id = NeedPo();
        if (id == null) return;
        var dt = Db.Query(@"
            SELECT p.Code, p.Name AS Product, i.Quantity AS Ordered, i.ReceivedQty AS [Accepted so far],
                   i.UnitPrice AS [Unit Price], i.Quantity * i.UnitPrice AS [Line Total]
            FROM PurchaseOrderItems i JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PoId = @id ORDER BY i.PoItemId", Db.P("@id", id));
        Ui.ShowGrid(FindForm(), "Items of " + Ui.SelectedText(_pos, "PO No."), dt);
    }

    void MarkPaid()
    {
        var id = NeedPo();
        if (id == null) return;
        if (Ui.SelectedText(_pos, "Paid?") == "Paid")
        {
            Ui.Info("This purchase order is already marked as paid.");
            return;
        }
        if (!Ui.Confirm($"Mark {Ui.SelectedText(_pos, "PO No.")} as paid ({Ui.SelectedText(_pos, "Payment")})?")) return;
        Db.Exec("UPDATE PurchaseOrders SET IsPaid = 1, PaidAt = SYSDATETIME() WHERE PoId = @id", Db.P("@id", id));
        RefreshData();
    }

    void ClosePo()
    {
        var id = NeedPo();
        if (id == null) return;
        if (Ui.SelectedText(_pos, "Status") != "Delivered" || Ui.SelectedText(_pos, "Paid?") != "Paid")
        {
            Ui.Warn("A purchase order can only be closed when it is fully delivered and paid.");
            return;
        }
        if (!Ui.Confirm("Close this purchase order?")) return;
        Db.Exec("UPDATE PurchaseOrders SET Status = 'Closed' WHERE PoId = @id", Db.P("@id", id));
        RefreshData();
    }
}

/// <summary>Record one supplier's quotation for an approved purchase request.</summary>
public class QuotationForm : Form
{
    readonly int _prId;
    readonly ComboBox _sup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown _days = Ui.Num(0, 365, 3);
    readonly TextBox _notes = new TextBox();
    readonly DataGridView _grid = Ui.Grid();
    readonly DataTable _table = new DataTable();

    public QuotationForm(int prId, string prNo)
    {
        _prId = prId;
        Ui.Setup(this, "Add Quotation - " + prNo, 860, 540);

        _sup.DataSource = Db.Query("SELECT SupplierId, Name FROM Suppliers WHERE IsActive = 1 ORDER BY Name");
        _sup.DisplayMember = "Name";
        _sup.ValueMember = "SupplierId";

        _table.Columns.Add("PrItemId", typeof(int));
        _table.Columns.Add("Product", typeof(string));
        _table.Columns.Add("Quantity", typeof(int));
        _table.Columns.Add("Unit Price", typeof(decimal));
        _table.Columns.Add("Specs", typeof(string));

        foreach (DataRow r in Db.Query(@"
            SELECT i.PrItemId, p.Name AS Product, i.Quantity, i.Specs
            FROM PurchaseRequestItems i JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PrId = @p ORDER BY i.PrItemId", Db.P("@p", prId)).Rows)
        {
            _table.Rows.Add(r["PrItemId"], r["Product"], r["Quantity"], DBNull.Value, r["Specs"]);
        }

        _grid.DataSource = _table;
        _grid.ReadOnly = false;
        foreach (DataGridViewColumn c in _grid.Columns)
            c.ReadOnly = c.Name != "Unit Price" && c.Name != "Specs";
        Ui.Hide(_grid, "PrItemId");
        _grid.Columns["Unit Price"].DefaultCellStyle.Format = "N2";
        _grid.DataError += (s, e) =>
        {
            Ui.Warn("Please type a valid number for the price.");
            e.ThrowException = false;
        };

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(12, 6, 0, 0),
            Text = "Type the supplier's Unit Price for every item. Edit Specs if the supplier offers something different.",
        };

        var t = Ui.FormTable();
        Ui.AddRow(t, "Supplier *", _sup);
        Ui.AddRow(t, "Delivery time (days)", _days);
        Ui.AddRow(t, "Notes (quality, warranty...)", _notes);

        Controls.Add(_grid);
        Controls.Add(hint);
        Controls.Add(t);
        Ui.AddButtons(this, Save);
    }

    bool Save()
    {
        _grid.EndEdit();
        if (_sup.SelectedValue == null)
        {
            Ui.Warn("Choose a supplier. Add suppliers in the Suppliers tab first.");
            return false;
        }
        foreach (DataRow r in _table.Rows)
        {
            if (r["Unit Price"] == DBNull.Value || Convert.ToDecimal(r["Unit Price"]) <= 0)
            {
                Ui.Warn("Enter a unit price (more than 0) for every item.");
                return false;
            }
        }

        try
        {
            Db.Run(tx =>
            {
                int qid = tx.ScalarInt(@"
                    INSERT INTO Quotations(PrId, SupplierId, DeliveryDays, Notes, CreatedBy)
                    VALUES(@pr, @s, @d, @n, @u);
                    SELECT SCOPE_IDENTITY();",
                    Db.P("@pr", _prId), Db.P("@s", Convert.ToInt32(_sup.SelectedValue)), Db.P("@d", (int)_days.Value),
                    Db.P("@n", string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim()),
                    Db.P("@u", Session.UserId));

                foreach (DataRow r in _table.Rows)
                {
                    tx.Exec("INSERT INTO QuotationItems(QuotationId, PrItemId, UnitPrice, Specs) VALUES(@q, @i, @p, @s)",
                        Db.P("@q", qid), Db.P("@i", Convert.ToInt32(r["PrItemId"])),
                        Db.P("@p", Convert.ToDecimal(r["Unit Price"])),
                        Db.P("@s", string.IsNullOrWhiteSpace(Convert.ToString(r["Specs"])) ? null : Convert.ToString(r["Specs"])));
                }
            });
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            Ui.Warn("This supplier already has a quotation for this request.");
            return false;
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }
    }
}

/// <summary>Side-by-side comparison of prices (with totals) and specifications across suppliers.</summary>
public class CompareForm : Form
{
    public CompareForm(int prId, string prNo)
    {
        Text = "Compare quotations - " + prNo;
        Size = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var quotes = Db.Query(@"
            SELECT q.QuotationId, s.Name, q.DeliveryDays, q.Notes
            FROM Quotations q JOIN Suppliers s ON s.SupplierId = q.SupplierId
            WHERE q.PrId = @p ORDER BY q.QuotationId", Db.P("@p", prId));
        var items = Db.Query(@"
            SELECT i.PrItemId, p.Name, i.Quantity, i.Specs
            FROM PurchaseRequestItems i JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PrId = @p ORDER BY i.PrItemId", Db.P("@p", prId));
        var lines = Db.Query(@"
            SELECT qi.QuotationId, qi.PrItemId, qi.UnitPrice, qi.Specs
            FROM QuotationItems qi JOIN Quotations q ON q.QuotationId = qi.QuotationId
            WHERE q.PrId = @p", Db.P("@p", prId));

        // ----- prices -----
        var prices = new DataTable();
        prices.Columns.Add("Item", typeof(string));
        prices.Columns.Add("Qty", typeof(string));
        foreach (DataRow q in quotes.Rows) prices.Columns.Add("Q" + q["QuotationId"], typeof(string));

        var totals = new Dictionary<int, decimal>();
        foreach (DataRow q in quotes.Rows) totals[Convert.ToInt32(q["QuotationId"])] = 0m;

        foreach (DataRow it in items.Rows)
        {
            var row = prices.NewRow();
            row["Item"] = it["Name"];
            row["Qty"] = it["Quantity"].ToString();
            foreach (DataRow q in quotes.Rows)
            {
                int qid = Convert.ToInt32(q["QuotationId"]);
                var line = lines.Select($"QuotationId = {qid} AND PrItemId = {it["PrItemId"]}");
                if (line.Length > 0)
                {
                    decimal price = Convert.ToDecimal(line[0]["UnitPrice"]);
                    row["Q" + qid] = price.ToString("N2");
                    totals[qid] += price * Convert.ToInt32(it["Quantity"]);
                }
                else row["Q" + qid] = "-";
            }
            prices.Rows.Add(row);
        }

        var totalRow = prices.NewRow();
        totalRow["Item"] = "TOTAL";
        var daysRow = prices.NewRow();
        daysRow["Item"] = "Delivery (days)";
        var notesRow = prices.NewRow();
        notesRow["Item"] = "Notes";
        foreach (DataRow q in quotes.Rows)
        {
            int qid = Convert.ToInt32(q["QuotationId"]);
            totalRow["Q" + qid] = totals[qid].ToString("N2");
            daysRow["Q" + qid] = Convert.ToString(q["DeliveryDays"]);
            notesRow["Q" + qid] = Convert.ToString(q["Notes"]);
        }
        prices.Rows.Add(totalRow);
        prices.Rows.Add(daysRow);
        prices.Rows.Add(notesRow);

        var gPrices = Ui.Grid();
        gPrices.DataSource = prices;
        gPrices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        foreach (DataRow q in quotes.Rows)
            gPrices.Columns["Q" + q["QuotationId"]].HeaderText = Convert.ToString(q["Name"]);

        // Highlight the lowest total in green.
        if (totals.Count > 0)
        {
            decimal min = totals.Values.Min();
            int totalIndex = prices.Rows.Count - 3;
            foreach (var kv in totals.Where(k => k.Value == min))
            {
                var cell = gPrices.Rows[totalIndex].Cells["Q" + kv.Key];
                cell.Style.BackColor = Color.FromArgb(200, 240, 200);
                cell.Style.Font = new Font(gPrices.Font, FontStyle.Bold);
            }
            gPrices.Rows[totalIndex].Cells["Item"].Style.Font = new Font(gPrices.Font, FontStyle.Bold);
        }

        // ----- specs / quality -----
        var specs = new DataTable();
        specs.Columns.Add("Item", typeof(string));
        specs.Columns.Add("Required specs", typeof(string));
        foreach (DataRow q in quotes.Rows) specs.Columns.Add("Q" + q["QuotationId"], typeof(string));
        foreach (DataRow it in items.Rows)
        {
            var row = specs.NewRow();
            row["Item"] = it["Name"];
            row["Required specs"] = Convert.ToString(it["Specs"]);
            foreach (DataRow q in quotes.Rows)
            {
                int qid = Convert.ToInt32(q["QuotationId"]);
                var line = lines.Select($"QuotationId = {qid} AND PrItemId = {it["PrItemId"]}");
                row["Q" + qid] = line.Length > 0 ? Convert.ToString(line[0]["Specs"]) : "-";
            }
            specs.Rows.Add(row);
        }
        var gSpecs = Ui.Grid();
        gSpecs.DataSource = specs;
        foreach (DataRow q in quotes.Rows)
            gSpecs.Columns["Q" + q["QuotationId"]].HeaderText = Convert.ToString(q["Name"]);

        var top = new GroupBox { Text = "Prices per unit and totals (lowest total in green)", Dock = DockStyle.Fill };
        top.Controls.Add(gPrices);
        var bottom = new GroupBox { Text = "Specifications offered by each supplier", Dock = DockStyle.Fill };
        bottom.Controls.Add(gSpecs);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(bottom, 0, 1);

        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.Cancel };
        Ui.Style(close, BtnKind.Normal);
        BackColor = Color.White;
        CancelButton = close;
        Controls.Add(layout);
        Controls.Add(close);
    }
}

/// <summary>Ask for the payment method and the expected delivery date of a new purchase order.</summary>
public class CreatePoForm : Form
{
    readonly ComboBox _method = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly DateTimePicker _date = new DateTimePicker { Format = DateTimePickerFormat.Short };

    public string PaymentMethod => Convert.ToString(_method.SelectedItem);
    public DateTime ExpectedDate => _date.Value;

    public CreatePoForm(string supplier, decimal total, int deliveryDays)
    {
        Ui.Setup(this, "Create Purchase Order", 460, 250);
        _method.Items.AddRange(new object[] { "COD", "Bank Transfer" });
        _method.SelectedIndex = 0;
        _date.Value = DateTime.Today.AddDays(deliveryDays);

        var t = Ui.FormTable();
        Ui.AddRow(t, "Supplier", new Label { Text = supplier, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        Ui.AddRow(t, "Order total", new Label { Text = total.ToString("N2"), AutoSize = true });
        Ui.AddRow(t, "Payment method *", _method);
        Ui.AddRow(t, "Expected delivery *", _date);
        Controls.Add(t);
        Ui.AddButtons(this, () => true, "Create PO");
    }
}
