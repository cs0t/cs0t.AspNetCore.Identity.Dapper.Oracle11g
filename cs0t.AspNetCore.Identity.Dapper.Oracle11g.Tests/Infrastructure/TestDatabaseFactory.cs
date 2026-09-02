using Oracle.ManagedDataAccess.Client;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Infrastructure;

public class TestDatabaseFactory : IDatabaseConnectionFactory
{
    private readonly string _connectionString;
    
    public DbProviderOptions Options { get; }
    
    public TestDatabaseFactory(string connectionString)
    {
        _connectionString = connectionString;
        Options = new DbProviderOptions
        {
            ConnectionString = _connectionString,
            DbSchema = "APP_USER",
        };
    }
    
    public async Task<OracleConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }
}