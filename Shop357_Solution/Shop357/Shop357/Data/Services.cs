namespace Shop357.Data;

/// <summary>The logged-in user.</summary>
public static class Session
{
    public static int UserId;
    public static string Username = "";
    public static string FullName = "";
    public static string Role = "";

    public static bool IsOwner => Role == "Owner";

    public static void Clear()
    {
        UserId = 0;
        Username = FullName = Role = "";
    }
}

/// <summary>Writes rows to the Notifications table (shown by the bell button).</summary>
public static class Notify
{
    const string InsertSql =
        "INSERT INTO Notifications(ForRole, Message, RefType, RefId) VALUES(@r, @m, @t, @i)";

    public static void Send(string role, string message, string refType = null, int? refId = null) =>
        Db.Exec(InsertSql, Db.P("@r", role), Db.P("@m", message), Db.P("@t", refType), Db.P("@i", refId));

    public static void Send(Tx tx, string role, string message, string refType = null, int? refId = null) =>
        tx.Exec(InsertSql, Db.P("@r", role), Db.P("@m", message), Db.P("@t", refType), Db.P("@i", refId));
}

/// <summary>Rule: every stock change goes through here so it is always logged.</summary>
public static class StockService
{
    public static void Apply(Tx tx, int productId, int change, string reason, string refType, int? refId)
    {
        tx.Exec("UPDATE Products SET Quantity = Quantity + @c WHERE ProductId = @p",
            Db.P("@c", change), Db.P("@p", productId));

        int after = tx.ScalarInt("SELECT Quantity FROM Products WHERE ProductId = @p", Db.P("@p", productId));

        tx.Exec(@"INSERT INTO StockMovements(ProductId, QtyChange, QtyAfter, Reason, RefType, RefId, CreatedBy)
                  VALUES(@p, @c, @a, @r, @t, @i, @u)",
            Db.P("@p", productId), Db.P("@c", change), Db.P("@a", after), Db.P("@r", reason),
            Db.P("@t", refType), Db.P("@i", refId), Db.P("@u", Session.UserId));
    }
}

public static class Docs
{
    /// <summary>Builds the "Brand: X; Fits: Y" text used to auto-fill purchase request specs.</summary>
    public static string Specs(object brand, object model)
    {
        var b = Convert.ToString(brand)?.Trim();
        var m = Convert.ToString(model)?.Trim();
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(b)) parts.Add("Brand: " + b);
        if (!string.IsNullOrEmpty(m)) parts.Add("Fits: " + m);
        return string.Join("; ", parts);
    }

    /// <summary>SQL that stamps a document number such as PR-2026-0007 using its id (parameter @id).</summary>
    public static string NumberSql(string table, string idCol, string numCol, string prefix) =>
        $"UPDATE {table} SET {numCol} = '{prefix}-' + CAST(YEAR(SYSDATETIME()) AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST({idCol} AS VARCHAR(10)), 4) WHERE {idCol} = @id";
}
