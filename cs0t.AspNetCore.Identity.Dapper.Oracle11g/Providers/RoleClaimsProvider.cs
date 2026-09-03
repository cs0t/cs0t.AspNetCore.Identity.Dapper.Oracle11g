using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class RoleClaimsProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    
    {
        public async Task<IList<Claim>> GetClaimsAsync(long roleId, CancellationToken ct =  default)
        {
            var command = $"""
                SELECT ClaimType, ClaimValue 
                FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTableName} 
                WHERE RoleId = :RoleId
                """;
            
            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            var result = await oracleConnection.QueryAsync<(string ClaimType, string ClaimValue)>(
                new CommandDefinition(command, new { RoleId = roleId }, cancellationToken: ct)
            ).ConfigureAwait(false);
            
            //return dotnet claims 
            return result
                .Select(x => new Claim(x.ClaimType!, x.ClaimValue!))
                .ToList();
        }
        
         public async Task AddClaimAsync(long roleId, Claim claim, CancellationToken ct = default)
         {
             claim.ThrowIfNull(nameof(claim));
             
             var command = $"""
                            INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTableName} 
                            (Id, RoleId, ClaimType, ClaimValue) 
                             SELECT {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsSequence}.NEXTVAL, :RoleId, :ClaimType, :ClaimValue
                             FROM DUAL
                             WHERE NOT EXISTS (
                                 SELECT 1 FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTableName}
                                 WHERE RoleId = :RoleId AND ClaimType = :ClaimType AND ClaimValue = :ClaimValue
                             )
                            """;

             await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
             
             await oracleConnection.ExecuteAsync(
                 new CommandDefinition(command, new { RoleId = roleId, ClaimType = claim.Type, ClaimValue = claim.Value }, cancellationToken: ct)
             ).ConfigureAwait(false);
         }
         
         public async Task RemoveClaimAsync(long roleId, Claim claim, CancellationToken ct = default)
         {
             claim.ThrowIfNull(nameof(claim));
             
             var command = $"""
                 DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTableName} 
                 WHERE RoleId = :RoleId AND ClaimType = :ClaimType AND ClaimValue = :ClaimValue
                 """;

             await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
             
             await oracleConnection.ExecuteAsync(
                 new CommandDefinition(command, new { RoleId = roleId, ClaimType = claim.Type, ClaimValue = claim.Value }, cancellationToken: ct)
             ).ConfigureAwait(false);
         }
    }
}
