using System.Data;
using Microsoft.Data.SqlClient;

namespace Shop357.Data;

/// <summary>
/// Small helper around SQL Server. Change ConnectionString if your SQL Server
/// instance is not called SQLEXPRESS.
/// </summary>
public static class Db
{
    public const string ConnectionString =
        @"Server=.\SQLEXPRESS;Database=Shop357DB;Integrated Security=True;TrustServerCertificate=True;";

    // Always create fresh parameters for every call (a SqlParameter cannot be reused).
    public static SqlParameter P(string name, object value) =>
        new SqlParameter(name, value ?? DBNull.Value);

    internal static SqlCommand Cmd(SqlConnection c, SqlTransaction t, string sql, SqlParameter[] ps)
    {
        var cmd = new SqlCommand(sql, c, t);
        if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
        return cmd;
    }

    public static DataTable Query(string sql, params SqlParameter[] ps)
    {
        using var c = new SqlConnection(ConnectionString);
        c.Open();
        using var cmd = Cmd(c, null, sql, ps);
        var dt = new DataTable();
        using var da = new SqlDataAdapter(cmd);
        da.Fill(dt);
        return dt;
    }

    public static int Exec(string sql, params SqlParameter[] ps)
    {
        using var c = new SqlConnection(ConnectionString);
        c.Open();
        using var cmd = Cmd(c, null, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    public static object Scalar(string sql, params SqlParameter[] ps)
    {
        using var c = new SqlConnection(ConnectionString);
        c.Open();
        using var cmd = Cmd(c, null, sql, ps);
        return cmd.ExecuteScalar();
    }

    public static int ScalarInt(string sql, params SqlParameter[] ps) =>
        Convert.ToInt32(Scalar(sql, ps));

    /// <summary>Runs several statements as one all-or-nothing transaction.</summary>
    public static void Run(Action<Tx> work)
    {
        using var c = new SqlConnection(ConnectionString);
        c.Open();
        using var t = c.BeginTransaction();
        try
        {
            work(new Tx(c, t));
            t.Commit();
        }
        catch
        {
            t.Rollback();
            throw;
        }
    }
}

/// <summary>A database transaction handed to Db.Run.</summary>
public sealed class Tx
{
    readonly SqlConnection _c;
    readonly SqlTransaction _t;

    internal Tx(SqlConnection c, SqlTransaction t)
    {
        _c = c;
        _t = t;
    }

    public int Exec(string sql, params SqlParameter[] ps)
    {
        using var cmd = Db.Cmd(_c, _t, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    public object Scalar(string sql, params SqlParameter[] ps)
    {
        using var cmd = Db.Cmd(_c, _t, sql, ps);
        return cmd.ExecuteScalar();
    }

    public int ScalarInt(string sql, params SqlParameter[] ps) =>
        Convert.ToInt32(Scalar(sql, ps));

    public DataTable Query(string sql, params SqlParameter[] ps)
    {
        using var cmd = Db.Cmd(_c, _t, sql, ps);
        var dt = new DataTable();
        using var da = new SqlDataAdapter(cmd);
        da.Fill(dt);
        return dt;
    }
}
