using System.Data;
using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>Inventory tab: product list, low-stock colours, stock adjustments and history, restock report.</summary>
public class InventoryControl : UserControl, IRefreshable
{
    readonly DataGridView _grid = Ui.Grid();
    readonly TextBox _search = new TextBox { Width = 220 };
    readonly CheckBox _lowOnly = new CheckBox { Text = "Low / out of stock only", AutoSize = true, Margin = new Padding(8, 8, 14, 0) };

    const string Sql = @"
        SELECT p.ProductId, p.Code, p.Name, c.Name AS Category, p.Brand,
               p.CompatibleModel AS [Fits Model], p.Quantity, p.MinStock AS [Min Stock],
               p.ReorderQty AS [Reorder Qty], p.UnitCost AS [Unit Cost], p.SellingPrice AS [Selling Price],
               CASE WHEN p.Quantity = 0 THEN 'OUT' WHEN p.Quantity <= p.MinStock THEN 'LOW' ELSE 'OK' END AS Stock,
               CASE WHEN p.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status
        FROM Products p
        JOIN Categories c ON c.CategoryId = p.CategoryId
        WHERE (@s = '' OR p.Code LIKE '%' + @s + '%' OR p.Name LIKE '%' + @s + '%'
                       OR p.Brand LIKE '%' + @s + '%' OR p.CompatibleModel LIKE '%' + @s + '%')
          AND (@low = 0 OR (p.Quantity <= p.MinStock AND p.IsActive = 1))
        ORDER BY CASE WHEN p.Quantity = 0 THEN 0 WHEN p.Quantity <= p.MinStock THEN 1 ELSE 2 END, p.Name";

    public InventoryControl()
    {
        Dock = DockStyle.Fill;

        var bar = Ui.Bar();
        bar.Controls.Add(Ui.Lbl("Search:"));
        bar.Controls.Add(_search);
        bar.Controls.Add(_lowOnly);
        bar.Controls.Add(Ui.Btn("Add Product", (s, e) => AddProduct()));
        bar.Controls.Add(Ui.Btn("Edit Product", (s, e) => EditProduct()));
        bar.Controls.Add(Ui.Btn("Adjust Stock", (s, e) => AdjustStock()));
        bar.Controls.Add(Ui.Btn("Stock History", (s, e) => History()));
        bar.Controls.Add(Ui.Btn("Activate / Deactivate", (s, e) => ToggleActive(), 150));
        bar.Controls.Add(Ui.Btn("Restock Report", (s, e) => Restock()));

        Controls.Add(_grid);
        Controls.Add(bar);

        _search.TextChanged += (s, e) => RefreshData();
        _lowOnly.CheckedChanged += (s, e) => RefreshData();
        _grid.DataBindingComplete += (s, e) => Colour();
    }

    public void RefreshData()
    {
        _grid.DataSource = Db.Query(Sql,
            Db.P("@s", _search.Text.Trim()),
            Db.P("@low", _lowOnly.Checked ? 1 : 0));
        Ui.Hide(_grid, "ProductId");
        Ui.Fmt(_grid, "N2", "Unit Cost", "Selling Price");
    }

    // Red = out of stock, light red = at or below minimum, grey text = inactive.
    void Colour()
    {
        foreach (DataGridViewRow r in _grid.Rows)
        {
            var stock = Convert.ToString(r.Cells["Stock"].Value);
            var status = Convert.ToString(r.Cells["Status"].Value);
            if (status == "Inactive") r.DefaultCellStyle.ForeColor = Color.Gray;
            else if (stock == "OUT") r.DefaultCellStyle.BackColor = Color.FromArgb(255, 190, 190);
            else if (stock == "LOW") r.DefaultCellStyle.BackColor = Color.FromArgb(255, 228, 228);
        }
    }

    int? NeedProduct()
    {
        var id = Ui.SelectedId(_grid, "ProductId");
        if (id == null) Ui.Warn("Select a product in the list first.");
        return id;
    }

    void AddProduct()
    {
        using var f = new ProductForm(null);
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void EditProduct()
    {
        var id = NeedProduct();
        if (id == null) return;
        using var f = new ProductForm(id);
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void AdjustStock()
    {
        var id = NeedProduct();
        if (id == null) return;
        int current = Convert.ToInt32(_grid.CurrentRow.Cells["Quantity"].Value);
        using var f = new AdjustStockForm(id.Value, Ui.SelectedText(_grid, "Name"), current);
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void History()
    {
        var id = NeedProduct();
        if (id == null) return;
        var dt = Db.Query(@"
            SELECT m.CreatedAt AS [Date], m.QtyChange AS [Change], m.QtyAfter AS [Balance],
                   m.Reason, m.RefType AS [Type], u.FullName AS [By]
            FROM StockMovements m
            JOIN Users u ON u.UserId = m.CreatedBy
            WHERE m.ProductId = @p
            ORDER BY m.MovementId DESC", Db.P("@p", id));
        Ui.ShowGrid(FindForm(), "Stock history - " + Ui.SelectedText(_grid, "Name"), dt);
    }

    void ToggleActive()
    {
        var id = NeedProduct();
        if (id == null) return;
        bool active = Ui.SelectedText(_grid, "Status") == "Active";
        if (!Ui.Confirm(active ? "Deactivate this product? It will no longer be used in new requests."
                               : "Activate this product again?")) return;
        Db.Exec("UPDATE Products SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE ProductId = @p",
            Db.P("@p", id));
        RefreshData();
    }

    void Restock()
    {
        using var f = new RestockReportForm();
        f.ShowDialog(FindForm());
        RefreshData();
    }
}

/// <summary>Add / edit a product. Quantity can only be set when adding (opening stock); later use Adjust Stock.</summary>
public class ProductForm : Form
{
    readonly int? _id;
    readonly TextBox _code = new TextBox();
    readonly TextBox _name = new TextBox();
    readonly TextBox _brand = new TextBox();
    readonly TextBox _model = new TextBox();
    readonly ComboBox _cat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
    readonly NumericUpDown _qty = Ui.Num(0, 1000000);
    readonly NumericUpDown _min = Ui.Num(0, 1000000, 5);
    readonly NumericUpDown _reorder = Ui.Num(0, 1000000, 10);
    readonly NumericUpDown _cost = Ui.Num(0, 100000000, 0, 2);
    readonly NumericUpDown _price = Ui.Num(0, 100000000, 0, 2);

    public ProductForm(int? id)
    {
        _id = id;
        Ui.Setup(this, id == null ? "Add Product" : "Edit Product", 480, 450);

        foreach (DataRow r in Db.Query("SELECT Name FROM Categories ORDER BY Name").Rows)
            _cat.Items.Add(r["Name"]);

        var t = Ui.FormTable();
        Ui.AddRow(t, "Product code *", _code);
        Ui.AddRow(t, "Product name *", _name);
        Ui.AddRow(t, "Category * (type to add new)", _cat);
        Ui.AddRow(t, "Brand", _brand);
        Ui.AddRow(t, "Fits motorcycle model", _model);
        Ui.AddRow(t, id == null ? "Opening quantity" : "Current quantity", _qty);
        Ui.AddRow(t, "Minimum stock", _min);
        Ui.AddRow(t, "Reorder quantity", _reorder);
        Ui.AddRow(t, "Unit cost", _cost);
        Ui.AddRow(t, "Selling price", _price);
        Controls.Add(t);

        if (id != null) LoadProduct(id.Value);
        Ui.AddButtons(this, Save);
    }

    void LoadProduct(int id)
    {
        var r = Db.Query(@"SELECT p.*, c.Name AS CategoryName
                           FROM Products p JOIN Categories c ON c.CategoryId = p.CategoryId
                           WHERE p.ProductId = @id", Db.P("@id", id)).Rows[0];
        _code.Text = Convert.ToString(r["Code"]);
        _name.Text = Convert.ToString(r["Name"]);
        _cat.Text = Convert.ToString(r["CategoryName"]);
        _brand.Text = Convert.ToString(r["Brand"]);
        _model.Text = Convert.ToString(r["CompatibleModel"]);
        _qty.Value = Convert.ToDecimal(r["Quantity"]);
        _qty.Enabled = false; // change it with "Adjust Stock" so the change is logged
        _min.Value = Convert.ToDecimal(r["MinStock"]);
        _reorder.Value = Convert.ToDecimal(r["ReorderQty"]);
        _cost.Value = Convert.ToDecimal(r["UnitCost"]);
        _price.Value = Convert.ToDecimal(r["SellingPrice"]);
    }

    static object NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    bool Save()
    {
        if (_code.Text.Trim() == "" || _name.Text.Trim() == "" || _cat.Text.Trim() == "")
        {
            Ui.Warn("Product code, name and category are required.");
            return false;
        }

        try
        {
            Db.Run(tx =>
            {
                var catName = _cat.Text.Trim();
                var found = tx.Scalar("SELECT CategoryId FROM Categories WHERE Name = @n", Db.P("@n", catName));
                int catId = found == null || found is DBNull
                    ? tx.ScalarInt("INSERT INTO Categories(Name) VALUES(@n); SELECT SCOPE_IDENTITY();", Db.P("@n", catName))
                    : Convert.ToInt32(found);

                if (_id == null)
                {
                    int pid = tx.ScalarInt(@"
                        INSERT INTO Products(Code, Name, CategoryId, Brand, CompatibleModel, Quantity, MinStock, ReorderQty, UnitCost, SellingPrice)
                        VALUES(@c, @n, @cat, @b, @m, 0, @min, @re, @uc, @sp);
                        SELECT SCOPE_IDENTITY();",
                        Db.P("@c", _code.Text.Trim()), Db.P("@n", _name.Text.Trim()), Db.P("@cat", catId),
                        Db.P("@b", NullIfBlank(_brand.Text)), Db.P("@m", NullIfBlank(_model.Text)),
                        Db.P("@min", (int)_min.Value), Db.P("@re", (int)_reorder.Value),
                        Db.P("@uc", _cost.Value), Db.P("@sp", _price.Value));

                    if (_qty.Value > 0)
                        StockService.Apply(tx, pid, (int)_qty.Value, "Initial stock", "Initial", null);
                }
                else
                {
                    tx.Exec(@"
                        UPDATE Products
                        SET Code = @c, Name = @n, CategoryId = @cat, Brand = @b, CompatibleModel = @m,
                            MinStock = @min, ReorderQty = @re, UnitCost = @uc, SellingPrice = @sp
                        WHERE ProductId = @id",
                        Db.P("@c", _code.Text.Trim()), Db.P("@n", _name.Text.Trim()), Db.P("@cat", catId),
                        Db.P("@b", NullIfBlank(_brand.Text)), Db.P("@m", NullIfBlank(_model.Text)),
                        Db.P("@min", (int)_min.Value), Db.P("@re", (int)_reorder.Value),
                        Db.P("@uc", _cost.Value), Db.P("@sp", _price.Value), Db.P("@id", _id.Value));
                }
            });
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            Ui.Warn("That product code already exists. Use a different code.");
            return false;
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }
    }
}

/// <summary>Manual stock correction (damage, count difference, ...). Always logged.</summary>
public class AdjustStockForm : Form
{
    readonly int _id;
    readonly int _current;
    readonly NumericUpDown _change = Ui.Num(-1000000, 1000000);
    readonly TextBox _reason = new TextBox();

    public AdjustStockForm(int productId, string name, int current)
    {
        _id = productId;
        _current = current;
        Ui.Setup(this, "Adjust Stock", 480, 250);

        var t = Ui.FormTable();
        Ui.AddRow(t, "Product", new Label { Text = name, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        Ui.AddRow(t, "Current stock", new Label { Text = current.ToString(), AutoSize = true });
        Ui.AddRow(t, "Change (+ add, - remove)", _change);
        Ui.AddRow(t, "Reason *", _reason);
        Controls.Add(t);
        Ui.AddButtons(this, Save, "Apply");
    }

    bool Save()
    {
        int change = (int)_change.Value;
        if (change == 0)
        {
            Ui.Warn("Enter a change other than 0.");
            return false;
        }
        if (_current + change < 0)
        {
            Ui.Warn("Stock cannot go below 0.");
            return false;
        }
        if (_reason.Text.Trim() == "")
        {
            Ui.Warn("Please give a reason.");
            return false;
        }

        try
        {
            Db.Run(tx => StockService.Apply(tx, _id, change, "Manual adjustment: " + _reason.Text.Trim(), "Adjustment", null));
            return true;
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }
    }
}

/// <summary>
/// Restock report: every active product at or below its minimum stock.
/// "Send to Requisition" turns the items into a Draft Purchase Request with details auto-filled.
/// </summary>
public class RestockReportForm : Form
{
    readonly DataGridView _grid = Ui.Grid();

    public RestockReportForm()
    {
        Text = "Restock Report - items at or below minimum stock";
        Size = new Size(1040, 540);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8, 8, 8, 0),
            Text = "Items already in an open purchase request or on order are shown but will not be added again.",
        };

        BackColor = Color.White;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(12, 11, 12, 8), FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface };
        var close = new Button { Text = "Close", Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        Ui.Style(close, BtnKind.Normal);
        bar.Controls.Add(close);
        bar.Controls.Add(Ui.Btn("Export CSV", (s, e) => Ui.ExportCsv(_grid, "RestockReport"), 120));
        bar.Controls.Add(Ui.Btn("Send to Requisition", (s, e) => SendToRequisition(), 170));
        CancelButton = close;

        Controls.Add(_grid);
        Controls.Add(note);
        Controls.Add(bar);
        LoadReport();
    }

    void LoadReport()
    {
        _grid.DataSource = Db.Query(@"
            SELECT p.ProductId, p.Code, p.Name, c.Name AS Category, p.Brand, p.CompatibleModel AS [Fits Model],
                   p.Quantity, p.MinStock AS [Min Stock],
                   CASE WHEN p.ReorderQty > 0 THEN p.ReorderQty
                        WHEN p.MinStock * 2 - p.Quantity > 0 THEN p.MinStock * 2 - p.Quantity
                        ELSE 1 END AS [Suggested Qty],
                   CASE WHEN EXISTS (SELECT 1 FROM PurchaseRequestItems i
                                     JOIN PurchaseRequests r ON r.PrId = i.PrId
                                     WHERE i.ProductId = p.ProductId
                                       AND r.Status IN ('Draft', 'Pending Approval', 'Approved'))
                          OR EXISTS (SELECT 1 FROM PurchaseOrderItems oi
                                     JOIN PurchaseOrders o ON o.PoId = oi.PoId
                                     WHERE oi.ProductId = p.ProductId
                                       AND o.Status IN ('Ordered', 'Partially Delivered')
                                       AND oi.ReceivedQty < oi.Quantity)
                        THEN 'Yes' ELSE 'No' END AS [Already Requested/Ordered]
            FROM Products p
            JOIN Categories c ON c.CategoryId = p.CategoryId
            WHERE p.IsActive = 1 AND p.Quantity <= p.MinStock
            ORDER BY p.Quantity, p.Name");
        Ui.Hide(_grid, "ProductId");
    }

    void SendToRequisition()
    {
        var table = _grid.DataSource as DataTable;
        if (table == null || table.Rows.Count == 0)
        {
            Ui.Info("No items need restocking right now.");
            return;
        }

        var rows = table.Rows.Cast<DataRow>()
            .Where(r => Convert.ToString(r["Already Requested/Ordered"]) == "No").ToList();
        if (rows.Count == 0)
        {
            Ui.Info("Nothing to send. Every low-stock item is already in an open request or on order.");
            return;
        }
        if (!Ui.Confirm($"Create a draft purchase request with {rows.Count} item(s)?")) return;

        string prNo = null;
        try
        {
            Db.Run(tx =>
            {
                int prId = tx.ScalarInt(
                    "INSERT INTO PurchaseRequests(Status, Remarks, CreatedBy) VALUES('Draft', @r, @u); SELECT SCOPE_IDENTITY();",
                    Db.P("@r", "Created from restock report"), Db.P("@u", Session.UserId));
                tx.Exec(Docs.NumberSql("PurchaseRequests", "PrId", "PrNumber", "PR"), Db.P("@id", prId));

                foreach (var r in rows)
                {
                    tx.Exec("INSERT INTO PurchaseRequestItems(PrId, ProductId, Quantity, Specs) VALUES(@pr, @p, @q, @s)",
                        Db.P("@pr", prId), Db.P("@p", Convert.ToInt32(r["ProductId"])),
                        Db.P("@q", Convert.ToInt32(r["Suggested Qty"])),
                        Db.P("@s", Docs.Specs(r["Brand"], r["Fits Model"])));
                }

                prNo = Convert.ToString(tx.Scalar("SELECT PrNumber FROM PurchaseRequests WHERE PrId = @id", Db.P("@id", prId)));
            });
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return;
        }

        Ui.Info($"Draft {prNo} created.\nOpen the Requisition tab to review it and submit it for approval.");
        LoadReport();
    }
}
