using System.Data;
using Microsoft.Data.SqlClient;
using Shop357.Data;

namespace Shop357.UI;

public class SuppliersControl : UserControl, IRefreshable
{
    readonly DataGridView _grid = Ui.Grid();

    public SuppliersControl()
    {
        Dock = DockStyle.Fill;

        var bar = Ui.Bar();
        bar.Controls.Add(Ui.Btn("Add Supplier", (s, e) => Open(null)));
        bar.Controls.Add(Ui.Btn("Edit Supplier", (s, e) => EditSelected()));
        bar.Controls.Add(Ui.Btn("Activate / Deactivate", (s, e) => Toggle(), 150));

        Controls.Add(_grid);
        Controls.Add(bar);
        _grid.DoubleClick += (s, e) => EditSelected();
    }

    public void RefreshData()
    {
        _grid.DataSource = Db.Query(@"
            SELECT SupplierId, Name, ContactPerson AS [Contact Person], Phone, Address,
                   CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status
            FROM Suppliers ORDER BY Name");
        Ui.Hide(_grid, "SupplierId");
    }

    void EditSelected()
    {
        var id = Ui.SelectedId(_grid, "SupplierId");
        if (id == null)
        {
            Ui.Warn("Select a supplier first.");
            return;
        }
        Open(id);
    }

    void Open(int? id)
    {
        using var f = new SupplierForm(id);
        if (f.ShowDialog(FindForm()) == DialogResult.OK) RefreshData();
    }

    void Toggle()
    {
        var id = Ui.SelectedId(_grid, "SupplierId");
        if (id == null)
        {
            Ui.Warn("Select a supplier first.");
            return;
        }
        Db.Exec("UPDATE Suppliers SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE SupplierId = @id", Db.P("@id", id));
        RefreshData();
    }
}

public class SupplierForm : Form
{
    readonly int? _id;
    readonly TextBox _name = new TextBox();
    readonly TextBox _contact = new TextBox();
    readonly TextBox _phone = new TextBox();
    readonly TextBox _address = new TextBox();

    public SupplierForm(int? id)
    {
        _id = id;
        Ui.Setup(this, id == null ? "Add Supplier" : "Edit Supplier", 480, 280);

        var t = Ui.FormTable();
        Ui.AddRow(t, "Supplier name *", _name);
        Ui.AddRow(t, "Contact person", _contact);
        Ui.AddRow(t, "Phone", _phone);
        Ui.AddRow(t, "Address", _address);
        Controls.Add(t);

        if (id != null)
        {
            var r = Db.Query("SELECT * FROM Suppliers WHERE SupplierId = @id", Db.P("@id", id)).Rows[0];
            _name.Text = Convert.ToString(r["Name"]);
            _contact.Text = Convert.ToString(r["ContactPerson"]);
            _phone.Text = Convert.ToString(r["Phone"]);
            _address.Text = Convert.ToString(r["Address"]);
        }

        Ui.AddButtons(this, Save);
    }

    static object Nb(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    bool Save()
    {
        if (_name.Text.Trim() == "")
        {
            Ui.Warn("Supplier name is required.");
            return false;
        }
        try
        {
            if (_id == null)
                Db.Exec("INSERT INTO Suppliers(Name, ContactPerson, Phone, Address) VALUES(@n, @c, @p, @a)",
                    Db.P("@n", _name.Text.Trim()), Db.P("@c", Nb(_contact.Text)), Db.P("@p", Nb(_phone.Text)), Db.P("@a", Nb(_address.Text)));
            else
                Db.Exec("UPDATE Suppliers SET Name = @n, ContactPerson = @c, Phone = @p, Address = @a WHERE SupplierId = @id",
                    Db.P("@n", _name.Text.Trim()), Db.P("@c", Nb(_contact.Text)), Db.P("@p", Nb(_phone.Text)), Db.P("@a", Nb(_address.Text)),
                    Db.P("@id", _id.Value));
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            Ui.Warn("A supplier with that name already exists.");
            return false;
        }
        catch (SqlException ex)
        {
            Ui.Error(ex.Message);
            return false;
        }
    }
}
