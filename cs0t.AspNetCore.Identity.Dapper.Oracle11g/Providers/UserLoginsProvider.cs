using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UserLoginsProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    {
        public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken ct = default) 
        {
            user.ThrowIfNull(nameof(user));

            var command = $"""
                           SELECT LoginProvider, ProviderKey, ProviderDisplayName 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName} 
                           WHERE UserId = :UserId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var result = await oracleConnection.QueryAsync<(string LoginProvider, string ProviderKey, string ProviderDisplayName)>(
                new CommandDefinition(command, new { UserId = user.Id }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return result
                .Select(x => new UserLoginInfo(x.LoginProvider, x.ProviderKey, x.ProviderDisplayName))
                .ToList();
        }
        
        public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken ct = default) 
        {
            if (string.IsNullOrWhiteSpace(loginProvider)) throw new ArgumentException("Login provider cannot be empty.", nameof(loginProvider));
            if (string.IsNullOrWhiteSpace(providerKey)) throw new ArgumentException("Provider key cannot be empty.", nameof(providerKey));

            var command = $"""
                           SELECT u.* 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName} u
                           INNER JOIN {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName} ul ON u.Id = ul.UserId
                           WHERE ul.LoginProvider = :LoginProvider AND ul.ProviderKey = :ProviderKey
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QuerySingleOrDefaultAsync<ApplicationUser>(
                new CommandDefinition(command, new { LoginProvider = loginProvider, ProviderKey = providerKey }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            login.ThrowIfNull(nameof(login));
            
            var command = $"""
                           INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName} 
                           (UserId, LoginProvider, ProviderKey, ProviderDisplayName) 
                           SELECT :UserId, :LoginProvider, :ProviderKey, :ProviderDisplayName FROM DUAL
                           WHERE NOT EXISTS (
                               SELECT 1 FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName}
                               WHERE UserId = :UserId AND LoginProvider = :LoginProvider AND ProviderKey = :ProviderKey
                           )
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { 
                    UserId = user.Id, 
                    LoginProvider = login.LoginProvider, 
                    ProviderKey = login.ProviderKey, 
                    ProviderDisplayName = login.ProviderDisplayName 
                }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            if (string.IsNullOrWhiteSpace(loginProvider)) throw new ArgumentException("Login provider cannot be empty.", nameof(loginProvider));
            if (string.IsNullOrWhiteSpace(providerKey)) throw new ArgumentException("Provider key cannot be empty.", nameof(providerKey));

            var command = $"""
                           DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName} 
                           WHERE UserId = :UserId AND LoginProvider = :LoginProvider AND ProviderKey = :ProviderKey
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { UserId = user.Id, LoginProvider = loginProvider, ProviderKey = providerKey }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }

    }
}
