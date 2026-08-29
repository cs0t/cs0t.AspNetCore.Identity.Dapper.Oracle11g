using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class RolesProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    {
        public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken ct = default) 
        {
            role.ThrowIfNull(nameof(role));
            
            var command = $"""
                           INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                           (Id, Name, NormalizedName, ConcurrencyStamp)
                           VALUES (:Id, :Name, :NormalizedName, :ConcurrencyStamp)
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var rowsInserted = await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new {
                    role.Id,
                    role.Name,
                    role.NormalizedName,
                    role.ConcurrencyStamp
                }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return rowsInserted == 1 
                ? IdentityResult.Success 
                : IdentityResult.Failed(new IdentityError { Description = $"The role with name {role.Name} could not be inserted." });
        }
        
        public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken ct = default) 
        {
            role.ThrowIfNull(nameof(role));
            
            var updateRoleCommand = $"""
                UPDATE {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                SET Name = :Name, NormalizedName = :NormalizedName, ConcurrencyStamp = :ConcurrencyStamp 
                WHERE Id = :Id
                """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await using var transaction = await oracleConnection.BeginTransactionAsync(ct).ConfigureAwait(false);

            try 
            {
                //update role
                await oracleConnection.ExecuteAsync(
                    new CommandDefinition(updateRoleCommand, new {
                        role.Name,
                        role.NormalizedName,
                        role.ConcurrencyStamp,
                        role.Id
                    }, transaction: transaction, cancellationToken: ct)
                ).ConfigureAwait(false);

                if (role.Claims.Count > 0) 
                {
                    var deleteClaimsCommand = $"""
                        DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTable} 
                        WHERE RoleId = :RoleId
                        """;
                    
                    //remove existing claims
                    await oracleConnection.ExecuteAsync(
                        new CommandDefinition(deleteClaimsCommand, new { RoleId = role.Id }, transaction: transaction, cancellationToken: ct)
                    ).ConfigureAwait(false);

                    var insertClaimsCommand = $"""
                        INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTable} 
                        (Id, RoleId, ClaimType, ClaimValue) 
                        VALUES ({databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsSequence}.NEXTVAL, :RoleId, :ClaimType, :ClaimValue)
                        """;
                    //insert new claims
                    await oracleConnection.ExecuteAsync(
                        new CommandDefinition(insertClaimsCommand, role.Claims.Select(x => new {
                            RoleId = role.Id,
                            ClaimType = x.ClaimType,
                            ClaimValue = x.ClaimValue
                        }), transaction: transaction, cancellationToken: ct)
                    ).ConfigureAwait(false);
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return IdentityResult.Success;
            } 
            catch (Exception primaryException)
            {
                try
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                }
                catch(Exception rollbackException)
                {
                    var combinedEx = new AggregateException("Transaction failed and rollback also failed.", 
                        primaryException, rollbackException);
            
                    return IdentityResult.Failed(new IdentityError { 
                        Code = nameof(UpdateAsync), 
                        Description = $"Critical error during rollback: {combinedEx.Message}" 
                    });
                }
                
                return IdentityResult.Failed(new IdentityError { 
                    Code = nameof(UpdateAsync), 
                    Description = $"Role with name {role.Name} could not be updated. Technical error: {primaryException.Message}" 
                });
            }
        }
        
        public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken ct = default) 
        {
            role.ThrowIfNull(nameof(role));
            
            var command = $"""
                           DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                           WHERE Id = :Id
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var rowsDeleted = await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { role.Id }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return rowsDeleted == 1 
                ? IdentityResult.Success 
                : IdentityResult.Failed(new IdentityError { Description = $"The role with name {role.Name} could not be deleted." });
        }
        
        public async Task<ApplicationRole?> FindByIdAsync(long roleId, CancellationToken ct = default) 
        {
            var command = $"""
                           SELECT Id, Name, NormalizedName, ConcurrencyStamp 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                           WHERE Id = :Id
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QuerySingleOrDefaultAsync<ApplicationRole>(
                new CommandDefinition(command, new { Id = roleId }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken ct = default) 
        {
            if (string.IsNullOrWhiteSpace(normalizedRoleName)) return null;

            var command = $"""
                           SELECT Id, Name, NormalizedName, ConcurrencyStamp 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                           WHERE NormalizedName = :NormalizedName
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QuerySingleOrDefaultAsync<ApplicationRole>(
                new CommandDefinition(command, new { NormalizedName = normalizedRoleName.Trim().ToUpper() }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task<IEnumerable<ApplicationRole>> GetAllRolesAsync(CancellationToken ct = default) 
        {
            var command = $"SELECT Id, Name, NormalizedName, ConcurrencyStamp FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName}";

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QueryAsync<ApplicationRole>(
                new CommandDefinition(command, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
    }
}
