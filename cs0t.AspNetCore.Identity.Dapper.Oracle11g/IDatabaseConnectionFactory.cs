using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    public interface IDatabaseConnectionFactory
    {
        Task<OracleConnection> CreateConnectionAsync(CancellationToken ct = default);
        DbProviderOptions Options { get; }
    }
}
