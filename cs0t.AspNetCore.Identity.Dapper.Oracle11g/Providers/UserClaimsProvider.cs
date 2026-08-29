using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UserClaimsProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    {
        public async Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            
            var command = $"""
                           SELECT ClaimType, ClaimValue 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsTable} 
                           WHERE UserId = :UserId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var result = await oracleConnection.QueryAsync<(string ClaimType, string ClaimValue)>(
                new CommandDefinition(command, new { UserId = user.Id }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return result
                .Select(x => new Claim(x.ClaimType, x.ClaimValue))
                .ToList();
        }
        
        public async Task AddClaimAsync(ApplicationUser user, Claim claim, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            claim.ThrowIfNull(nameof(claim));

            var command = $"""
                           INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsTable} 
                           (Id, UserId, ClaimType, ClaimValue) 
                           VALUES ({databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsSequence}.NEXTVAL, :UserId, :ClaimType, :ClaimValue)
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { UserId = user.Id, ClaimType = claim.Type, ClaimValue = claim.Value }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task RemoveClaimAsync(ApplicationUser user, Claim claim, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            claim.ThrowIfNull(nameof(claim));

            var command = $"""
                           DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsTable} 
                           WHERE UserId = :UserId AND ClaimType = :ClaimType AND ClaimValue = :ClaimValue
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { UserId = user.Id, ClaimType = claim.Type, ClaimValue = claim.Value }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task<IList<ApplicationUser>> GetUsersByClaimAsync(Claim claim, CancellationToken ct = default)
        {
            claim.ThrowIfNull(nameof(claim));

            var command = $"""
                           SELECT u.* 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName} u
                           INNER JOIN {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsTable} uc ON u.Id = uc.UserId
                           WHERE uc.ClaimType = :ClaimType AND uc.ClaimValue = :ClaimValue
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var result = await oracleConnection.QueryAsync<ApplicationUser>(
                new CommandDefinition(command, new { ClaimType = claim.Type, ClaimValue = claim.Value }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return result.ToList();
        }
    }
}
