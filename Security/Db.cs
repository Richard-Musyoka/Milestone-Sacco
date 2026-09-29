using System.Data;
using System.Data.Common;

namespace SaccoManagementSystem.Security;

/// <summary>Creates open-able database connections. The SQL Server implementation lives in SqlDbFactory.cs.</summary>
public interface IDbFactory
{
    DbConnection Create();
}

/// <summary>Tiny ADO.NET helpers so the security layer has no ORM dependency.</summary>
public static class Db
{
    public static async Task<DbConnection> OpenAsync(this IDbFactory factory, CancellationToken ct = default)
    {
        var conn = factory.Create();
        await conn.OpenAsync(ct);
        return conn;
    }

    private static DbCommand Build(DbConnection conn, string sql, (string Name, object? Value)[] p)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in p)
        {
            var prm = cmd.CreateParameter();
            prm.ParameterName = name.StartsWith('@') ? name : "@" + name;
            prm.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(prm);
        }
        return cmd;
    }

    public static async Task<int> ExecAsync(this DbConnection conn, string sql, params (string, object?)[] p)
    {
        await using var cmd = Build(conn, sql, p);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T?> ScalarAsync<T>(this DbConnection conn, string sql, params (string, object?)[] p)
    {
        await using var cmd = Build(conn, sql, p);
        var o = await cmd.ExecuteScalarAsync();
        if (o == null || o is DBNull) return default;
        var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(o, t);
    }

    public static async Task<List<T>> QueryAsync<T>(this DbConnection conn, string sql, Func<DbDataReader, T> map, params (string, object?)[] p)
    {
        var list = new List<T>();
        await using var cmd = Build(conn, sql, p);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(map(r));
        return list;
    }

    public static async Task<T?> FirstAsync<T>(this DbConnection conn, string sql, Func<DbDataReader, T> map, params (string, object?)[] p) where T : class
    {
        await using var cmd = Build(conn, sql, p);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? map(r) : null;
    }

    // ---- reader helpers -------------------------------------------------
    public static string Str(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? "" : Convert.ToString(r[col]) ?? "";
    public static string? StrN(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? null : Convert.ToString(r[col]);
    public static int Int(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? 0 : Convert.ToInt32(r[col]);
    public static long Long(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? 0 : Convert.ToInt64(r[col]);
    public static bool Bool(this DbDataReader r, string col) => !r.IsDBNull(r.GetOrdinal(col)) && Convert.ToBoolean(r[col]);
    public static DateTime Date(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? DateTime.MinValue : DateTime.SpecifyKind(Convert.ToDateTime(r[col]), DateTimeKind.Utc);
    public static DateTime? DateN(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? null : DateTime.SpecifyKind(Convert.ToDateTime(r[col]), DateTimeKind.Utc);
    public static byte[] Bytes(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? Array.Empty<byte>() : (byte[])r[col];
    public static Guid GuidV(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? Guid.Empty : r.GetGuid(r.GetOrdinal(col));
}
