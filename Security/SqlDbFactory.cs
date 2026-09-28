using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace SaccoManagementSystem.Security;

/// <summary>SQL Server connections built from ConnectionStrings:DefaultConnection.</summary>
public sealed class SqlDbFactory : IDbFactory
{
    private readonly string _connectionString;

    public SqlDbFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
    }

    public DbConnection Create() => new SqlConnection(_connectionString);
}
