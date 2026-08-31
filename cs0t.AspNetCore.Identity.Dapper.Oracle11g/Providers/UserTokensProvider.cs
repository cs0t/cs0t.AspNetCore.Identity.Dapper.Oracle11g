using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UserTokensProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    {
        public async Task<IEnumerable<IdentityUserToken<long>>> GetTokensAsync(long userId, CancellationToken ct = default) 
        {
            var command = $"""
                           SELECT UserId, LoginProvider, Name, Value 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserTokensTableName} 
                           WHERE UserId = :UserId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QueryAsync<IdentityUserToken<long>>(
                new CommandDefinition(command, new { UserId = userId }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task ReplaceTokenAsync(IdentityUserToken<long> token, CancellationToken ct = default)
        {
            token.ThrowIfNull(nameof(token));

            var command = $"""
                           MERGE INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserTokensTableName} t
                           USING (SELECT :UserId AS UserId, :LoginProvider AS LoginProvider, :Name AS Name FROM DUAL) src
                           ON (t.UserId = src.UserId AND t.LoginProvider = src.LoginProvider AND t.Name = src.Name)
                           WHEN MATCHED THEN
                               UPDATE SET t.Value = :Value
                           WHEN NOT MATCHED THEN
                               INSERT (UserId, LoginProvider, Name, Value)
                               VALUES (src.UserId, src.LoginProvider, src.Name, :Value)
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { 
                    UserId = token.UserId, 
                    LoginProvider = token.LoginProvider, 
                    Name = token.Name, 
                    Value = token.Value 
                }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task DeleteTokenAsync(long userId, string loginProvider, string name, CancellationToken ct = default)
        {
            var command = $"""
                           DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserTokensTableName} 
                           WHERE UserId = :UserId AND LoginProvider = :LoginProvider AND Name = :Name
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { UserId = userId, LoginProvider = loginProvider, Name = name }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task<string?> GetTokenAsync(long userId, string loginProvider, string name, CancellationToken ct = default)
        {
            var command = $"""
                           SELECT Value 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserTokensTableName} 
                           WHERE UserId = :UserId AND LoginProvider = :LoginProvider AND Name = :Name
                           """;
 
            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
 
            return await oracleConnection.QuerySingleOrDefaultAsync<string?>(
                new CommandDefinition(command, new { UserId = userId, LoginProvider = loginProvider, Name = name }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
    }
}
