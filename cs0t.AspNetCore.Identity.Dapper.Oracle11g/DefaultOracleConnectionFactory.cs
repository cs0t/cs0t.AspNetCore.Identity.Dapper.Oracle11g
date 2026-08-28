using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    public class DefaultOracleConnectionFactory : IDatabaseConnectionFactory
    {
        public DbProviderOptions Options { get; }
        
        private DefaultOracleConnectionFactory(DbProviderOptions options)
        {
            Options = options;
            
            //SqlMapper.AddTypeHandler();
            //SqlMapper.AddTypeHandler();
            //SqlMapper.AddTypeHandler();
        }

        public static DefaultOracleConnectionFactory Create(DbProviderOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options), "Options cannot be null.");

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new ArgumentException("Connection string must be provided.", nameof(options.ConnectionString));

            if (!string.IsNullOrWhiteSpace(options.DbSchema))
            {
                options.DbSchema = options.DbSchema
                    .Replace("[", string.Empty)
                    .Replace("]", string.Empty)
                    .Replace("`", string.Empty)
                    .Trim()
                    .ToUpper();
            }

            return new DefaultOracleConnectionFactory(options);
        }

        public async Task<OracleConnection> CreateConnectionAsync(CancellationToken ct = default) {
            var oracleConnection = new OracleConnection(Options.ConnectionString);
            
            oracleConnection.BindByName = true;
            
            if (oracleConnection.State != ConnectionState.Open)  
                await oracleConnection.OpenAsync(ct);
            
            return oracleConnection;
        }

    }
}
