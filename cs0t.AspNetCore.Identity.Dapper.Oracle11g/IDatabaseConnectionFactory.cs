using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    public interface IDatabaseConnectionFactory
    {
        Task<SqlConnection> CreateConnectionAsync();
        string DbSchema { get; set; }
    }
}
