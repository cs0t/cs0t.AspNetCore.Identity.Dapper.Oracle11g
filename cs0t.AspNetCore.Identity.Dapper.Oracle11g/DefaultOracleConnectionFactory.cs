using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    public class DefaultSqlConnectionFactory : IDatabaseConnectionFactory
    {
        public string DbSchema { get; }
        
        public string UsersTableName { get; }
        public string RolesTableName { get; }
        public string UserRolesTableName { get; }
        public string UserClaimsTable { get; }
        public string UserRoleClaimsTable { get; }
        public string UserLoginsTableName { get; }
        public string UserTokensTableName { get; }
        
        private readonly string _connectionString;
        
        public DefaultSqlConnectionFactory(string connectionString, string schema)
        {
            schema = schema
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Replace("`", string.Empty)
                .Trim()
                .ToUpper();
            
            
            _connectionString = connectionString ?? string.Empty;
            DbSchema = schema;
            
            //SqlMapper.AddTypeHandler();
            //SqlMapper.AddTypeHandler();
            //SqlMapper.AddTypeHandler();
        }

        public async Task<OracleConnection> CreateConnectionAsync(CancellationToken ct = default) {
            var oracleConnection = new OracleConnection(_connectionString);
            
            oracleConnection.BindByName = true;
            
            if (oracleConnection.State != ConnectionState.Open)  
                await oracleConnection.OpenAsync(ct);
            
            return oracleConnection;
        }
    }
}
