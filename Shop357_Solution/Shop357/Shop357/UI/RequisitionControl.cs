using System.Data;
using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

/// <summary>
/// Requisition tab. Flow: Draft -> Submit -> Pending Approval -> (Owner) Approved or Rejected.
/// Rejected requests can be revised and resubmitted, or cancelled.
/// </summary>
public class RequisitionControl : UserControl, IRefreshable
{
    readonly DataGridView _prs = Ui.Grid();
    readonly DataGridView _items = Ui.Grid();
    readonly ComboBox _filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(0, 5, 14, 0) };

    public RequisitionControl()
    {
        Dock = DockStyle.Fill;

        _filter.Items.AddRange(new object[] { "All", "Draft", "Pending Approval", "Approved", "Rejected", "Cancelled", "Ordered" });
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (s, e) => RefreshData();

        var bar = Ui.Bar();
        bar.Controls.Add(Ui.Lbl("Status:"));
        bar.Controls.Add(_filter);
        bar.Controls.Add(Ui.Btn("New PR", (s, e) => NewPr()));
        bar.Controls.Add(Ui.Btn("Edit / Revise", (s, e) => EditPr()));
        bar.Controls.Add(Ui.Btn("Submit for Approval", (s, e) => Submit(), 150));
        if (Session.IsOwner)
        {
            bar.Controls.Add(Ui.Btn("Approve", (s, e) => Approve()));
            bar.Controls.Add(Ui.Btn("Reject", (s, e) => Reject()));
        }
        bar.Controls.Add(Ui.Btn("Cancel PR", (s, e) => CancelPr()));

        var top = new GroupBox { Text = "Purchase requests", Dock = DockStyle.Fill };
        top.Controls.Add(_prs);
        var bottom = new GroupBox { Text = "Items in the selected request", Dock = DockStyle.Fill };
        bottom.Controls.Add(_items);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(bottom, 0, 1);

        Controls.Add(layout);
        Controls.Add(bar);

        _prs.SelectionChanged += (s, e) => LoadItems();
        _prs.DataBindingComplete += (s, e) => Colour();
    }

    public void RefreshData()
    {
        _prs.DataSource = Db.Query(@"
            SELECT r.PrId, r.PrNumber AS [PR No.], r.Status, u.FullName AS [Requested By],
                   r.CreatedAt AS [Created],
                   (SELECT COUNT(*) FROM PurchaseRequestItems i WHERE i.PrId = r.PrId) AS Items,
                   r.Remarks, a.FullName AS [Approved By], r.RejectReason AS [Reject Reason]
            FROM PurchaseRequests r
            JOIN Users u ON u.UserId = r.CreatedBy
            LEFT JOIN Users a ON a.UserId = r.ApprovedBy
            WHERE @s = 'All' OR r.Status = @s
            ORDER BY r.PrId DESC", Db.P("@s", Convert.ToString(_filter.SelectedItem)));
        Ui.Hide(_prs, "PrId");
        LoadItems();
    }

    void Colour()
    {
        foreach (DataGridViewRow r in _prs.Rows)
        {
            var st = Convert.ToString(r.Cells["Status"].Value);
            if (st == "Pending Approval") r.DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 200);
            else if (st == "Rejected") r.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
            else if (st == "Approved") r.DefaultCellStyle.BackColor = Color.FromArgb(220, 245, 220);
            else if (st == "Cancelled") r.DefaultCellStyle.ForeColor = Color.Gray;
        }
    }

    void LoadItems()
    {
        var id = Ui.SelectedId(_prs, "PrId");
        _items.DataSource = id == null ? null : Db.Query(@"
            SELECT i.PrItemId, p.Code, p.Name AS Product, i.Quantity, i.Specs
            FROM PurchaseRequestItems i
            JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PrId = @id
            ORDER BY i.PrItemId", Db.P("@id", id));
        Ui.Hide(_items, "PrItemId");
    }

    // Returns the selected PR id, or null (with a message) when nothing is selected.
    int? NeedPr()
    {
        var id = Ui.SelectedId(_prs, "PrId");
        if (id == null) Ui.Warn("Select a purchase request first.");
        return id;
    }

    string Status() => Ui.SelectedText(_prs, "Status");
    string PrNo() => Ui.SelectedText(_prs, "PR No.");

    void NewPr()
    {
        using var f = new PrForm(null);
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void EditPr()
    {
        var id = NeedPr();
        if (id == null) return;
        if (Status() != "Draft" && Status() != "Rejected")
        {
            Ui.Warn("Only Draft or Rejected requests can be edited.");
            return;
        }
        using var f = new PrForm(id);
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void Submit()
    {
        var id = NeedPr();
        if (id == null) return;
        if (Status() != "Draft")
        {
            Ui.Warn("Only Draft requests can be submitted. (A rejected request must be revised first.)");
            return;
        }
        if (_items.Rows.Count == 0)
        {
            Ui.Warn("This request has no items.");
            return;
        }
        if (!Ui.Confirm($"Submit {PrNo()} for approval?")) return;

        string no = PrNo();
        Db.Run(tx =>
        {
            tx.Exec("UPDATE PurchaseRequests SET Status = 'Pending Approval' WHERE PrId = @id AND Status = 'Draft'", Db.P("@id", id));
            Notify.Send(tx, "Owner", $"{no} is waiting for your approval.", "PR", id);
        });
        RefreshData();
    }

    void Approve()
    {
        var id = NeedPr();
        if (id == null) return;
        if (Status() != "Pending Approval")
        {
            Ui.Warn("Only requests that are Pending Approval can be approved.");
            return;
        }
        if (!Ui.Confirm($"Approve {PrNo()}?")) return;

        string no = PrNo();
        Db.Run(tx =>
        {
            tx.Exec(@"UPDATE PurchaseRequests
                      SET Status = 'Approved', ApprovedBy = @u, ApprovedAt = SYSDATETIME(), RejectReason = NULL
                      WHERE PrId = @id AND Status = 'Pending Approval'",
                Db.P("@u", Session.UserId), Db.P("@id", id));
            Notify.Send(tx, "Staff", $"{no} was approved. It is ready for purchasing.", "PR", id);
        });
        RefreshData();
    }

    void Reject()
    {
        var id = NeedPr();
        if (id == null) return;
        if (Status() != "Pending Approval")
        {
            Ui.Warn("Only requests that are Pending Approval can be rejected.");
            return;
        }
        var reason = PromptForm.Ask(FindForm(), "Reject purchase request", "Reason for rejection *");
        if (reason == null) return;

        string no = PrNo();
        Db.Run(tx =>
        {
            tx.Exec("UPDATE PurchaseRequests SET Status = 'Rejected', RejectReason = @r WHERE PrId = @id AND Status = 'Pending Approval'",
                Db.P("@r", reason), Db.P("@id", id));
            Notify.Send(tx, "Staff", $"{no} was rejected: {reason}. Revise and resubmit, or cancel it.", "PR", id);
        });
        RefreshData();
    }

    void CancelPr()
    {
        var id = NeedPr();
        if (id == null) return;
        var st = Status();
        bool allowed = st == "Draft" || st == "Rejected" || st == "Pending Approval" || (st == "Approved" && Session.IsOwner);
        if (!allowed)
        {
            Ui.Warn(st == "Approved" ? "Only the Owner can cancel an approved request."
                                     : "This request can no longer be cancelled.");
            return;
        }
        if (!Ui.Confirm($"Cancel {PrNo()}? This cannot be undone.")) return;

        Db.Exec("UPDATE PurchaseRequests SET Status = 'Cancelled' WHERE PrId = @id", Db.P("@id", id));
        RefreshData();
    }
}

/// <summary>Create or revise a purchase request (always saved as Draft).</summary>
public class PrForm : Form
{
    readonly int? _id;
    readonly ComboBox _prod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly NumericUpDown _qty = Ui.Num(1, 1000000, 1);
    readonly TextBox _specs = new TextBox { Width = 230 };
    readonly TextBox _remarks = new TextBox();
    readonly DataGridView _grid = Ui.Grid();
    readonly DataTable _table = new DataTable();

    public PrForm(int? id)
    {
        _id = id;
        Ui.Setup(this, id == null ? "New Purchase Request" : "Edit / Revise Purchase Request", 900, 560);

        _table.Columns.Add("ProductId", typeof(int));
        _table.Columns.Add("Code", typeof(string));
        _table.Columns.Add("Product", typeof(string));
        _table.Columns.Add("Quantity", typeof(int));
        _table.Columns.Add("Specs", typeof(string));
        _grid.DataSource = _table;
        Ui.Hide(_grid, "ProductId");

        var products = Db.Query(@"SELECT ProductId, Code, Name, Code + ' - ' + Name AS Display, Brand, CompatibleModel, ReorderQty
                                  FROM Products WHERE IsActive = 1 ORDER BY Name");
        _prod.DataSource = products;
        _prod.DisplayMember = "Display";
        _prod.ValueMember = "ProductId";
        _prod.SelectedIndexChanged += (s, e) => Prefill();

        var addBar = Ui.Bar();
        addBar.Controls.Add(Ui.Lbl("Product"));
        addBar.Controls.Add(_prod);
        addBar.Controls.Add(Ui.Lbl("Qty"));
        _qty.Width = 80;
        _qty.Margin = new Padding(0, 5, 10, 0);
        addBar.Controls.Add(_qty);
        addBar.Controls.Add(Ui.Lbl("Specs"));
        _specs.Margin = new Padding(0, 5, 10, 0);
        addBar.Controls.Add(_specs);
        addBar.Controls.Add(Ui.Btn("Add Item", (s, e) => AddItem(), 90));
        addBar.Controls.Add(Ui.Btn("Remove Selected", (s, e) => RemoveItem(), 130));

        var top = Ui.FormTable();
        Ui.AddRow(top, "Remarks (optional)", _remarks);

        Controls.Add(_grid);
        Controls.Add(addBar);
        Controls.Add(top);

        if (id != null) LoadExisting(id.Value);
        Prefill();
        Ui.AddButtons(this, Save, "Save as Draft");
    }

    void LoadExisting(int id)
    {
        var pr = Db.Query("SELECT Remarks FROM PurchaseRequests WHERE PrId = @id", Db.P("@id", id));
        if (pr.Rows.Count > 0) _remarks.Text = Convert.ToString(pr.Rows[0]["Remarks"]);

        foreach (DataRow r in Db.Query(@"
            SELECT i.ProductId, p.Code, p.Name, i.Quantity, i.Specs
            FROM PurchaseRequestItems i JOIN Products p ON p.ProductId = i.ProductId
            WHERE i.PrId = @id ORDER BY i.PrItemId", Db.P("@id", id)).Rows)
        {
            _table.Rows.Add(r["ProductId"], r["Code"], r["Name"], r["Quantity"], r["Specs"]);
        }
    }

    // When a product is picked, fill in its specs and the usual reorder quantity.
    void Prefill()
    {
        if (_prod.SelectedItem is not DataRowView v) return;
        _specs.Text = Docs.Specs(v["Brand"], v["CompatibleModel"]);
        int re = Convert.ToInt32(v["ReorderQty"]);
        _qty.Value = Math.Min(Math.Max(re, 1), _qty.Maximum);
    }

    void AddItem()
    {
        if (_prod.SelectedItem is not DataRowView v) return;
        int pid = Convert.ToInt32(v["ProductId"]);
        var existing = _table.Rows.Cast<DataRow>().FirstOrDefault(r => Convert.ToInt32(r["ProductId"]) == pid);
        if (existing != null)
        {
            existing["Quantity"] = Convert.ToInt32(existing["Quantity"]) + (int)_qty.Value;
            return;
        }
        _table.Rows.Add(pid, v["Code"], v["Name"], (int)_qty.Value, _specs.Text.Trim());
    }

    void RemoveItem()
    {
        if (_grid.CurrentRow?.DataBoundItem is DataRowView drv)
        {
            drv.Row.Delete();
            _table.AcceptChanges();
        }
    }

    bool Save()
    {
        if (_table.Rows.Count == 0)
        {
            Ui.Warn("Add at least one item.");
            return false;
        }

        try
        {
            Db.Run(tx =>
            {
                int prId;
                if (_id == null)
                {
                    prId = tx.ScalarInt(
                        "INSERT INTO PurchaseRequests(Status, Remarks, CreatedBy) VALUES('Draft', @r, @u); SELECT SCOPE_IDENTITY();",
                        Db.P("@r", string.IsNullOrWhiteSpace(_remarks.Text) ? null : _remarks.Text.Trim()),
                        Db.P("@u", Session.UserId));
                    tx.Exec(Docs.NumberSql("PurchaseRequests", "PrId", "PrNumber", "PR"), Db.P("@id", prId));
                }
                else
                {
                    prId = _id.Value;
                    // Editing a rejected request turns it back into a Draft, ready to submit again.
                    int changed = tx.Exec(
                        "UPDATE PurchaseRequests SET Remarks = @r, Status = 'Draft' WHERE PrId = @id AND Status IN ('Draft', 'Rejected')",
                        Db.P("@r", string.IsNullOrWhiteSpace(_remarks.Text) ? null : _remarks.Text.Trim()),
                        Db.P("@id", prId));
                    if (changed == 0) throw new InvalidOperationException("This request can no longer be edited.");
                    tx.Exec("DELETE FROM PurchaseRequestItems WHERE PrId = @id", Db.P("@id", prId));
                }

                foreach (DataRow r in _table.Rows)
                {
                    tx.Exec("INSERT INTO PurchaseRequestItems(PrId, ProductId, Quantity, Specs) VALUES(@pr, @p, @q, @s)",
                        Db.P("@pr", prId), Db.P("@p", Convert.ToInt32(r["ProductId"])),
                        Db.P("@q", Convert.ToInt32(r["Quantity"])),
                        Db.P("@s", string.IsNullOrWhiteSpace(Convert.ToString(r["Specs"])) ? null : Convert.ToString(r["Specs"])));
                }
            });
            return true;
        }
        catch (InvalidOperationException ex)
        {
            Ui.Warn(ex.Message);
            return false;
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }
    }
}
