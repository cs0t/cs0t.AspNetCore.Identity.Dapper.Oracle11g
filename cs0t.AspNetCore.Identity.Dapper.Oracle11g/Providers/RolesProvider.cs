using System;
using System.Collections.Generic;
using System.Data;
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
            ValidateClaims(role);
            var originalId = role.Id;
            
            var command = $"""
                           INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                           (Id, Name, NormalizedName, ConcurrencyStamp)
                           VALUES ({databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesSequence}.NEXTVAL, :Name, :NormalizedName, :ConcurrencyStamp)
                           RETURNING Id INTO :GeneratedId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await oracleConnection.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                var parameters = new DynamicParameters(role);
                parameters.Add("GeneratedId", dbType: DbType.Int64, direction: ParameterDirection.Output);

                var rowsInserted = await oracleConnection.ExecuteAsync(
                    new CommandDefinition(command, parameters, transaction, cancellationToken: ct)
                ).ConfigureAwait(false);

                if (rowsInserted != 1)
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return IdentityResult.Failed(new IdentityError { Description = $"The role with name {role.Name} could not be inserted." });
                }

                role.Id = parameters.Get<long>("GeneratedId");
                await SynchronizeClaimsAsync(oracleConnection, transaction, role, ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return IdentityResult.Success;
            }
            catch
            {
                role.Id = originalId;
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }
        
        public async Task<IdentityResult> UpdateAsync(ApplicationRole role, string originalConcurrencyStamp ,CancellationToken ct = default) 
        {
            role.ThrowIfNull(nameof(role));
            ValidateClaims(role);
            
            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await oracleConnection.BeginTransactionAsync(ct).ConfigureAwait(false);

            try 
            { 
                var updateRoleCommand = $"""
                                       UPDATE {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                                       SET Name = :Name, NormalizedName = :NormalizedName, ConcurrencyStamp = :ConcurrencyStamp 
                                       WHERE Id = :Id AND (ConcurrencyStamp IS NULL OR ConcurrencyStamp = :DatabaseConcurrencyStamp)
                                       """;
                
                //update role
                var rowsUpdated = await oracleConnection.ExecuteAsync(
                    new CommandDefinition(updateRoleCommand, new {
                        role.Name,
                        role.NormalizedName,
                        role.ConcurrencyStamp,
                        role.Id,
                        DatabaseConcurrencyStamp = originalConcurrencyStamp
                    }, transaction: transaction, cancellationToken: ct)
                ).ConfigureAwait(false);

                if (rowsUpdated == 0)
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return IdentityResult.Failed(new IdentityError 
                    { 
                        Code = "ConcurrencyFailure", 
                        Description = "Optimistic concurrency failure. The role has been modified by another process." 
                    });
                }
                
                await SynchronizeClaimsAsync(oracleConnection, transaction, role, ct).ConfigureAwait(false);

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

        private async Task SynchronizeClaimsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            ApplicationRole role,
            CancellationToken ct)
        {
            if (role.Claims is null) return;

            var deleteClaimsCommand = $"DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTable} WHERE RoleId = :RoleId";
            await connection.ExecuteAsync(
                new CommandDefinition(deleteClaimsCommand, new { RoleId = role.Id }, transaction, cancellationToken: ct)
            ).ConfigureAwait(false);

            var claims = role.Claims
                .GroupBy(x => new { x.ClaimType, x.ClaimValue })
                .Select(x => x.First())
                .ToList();

            foreach (var claim in claims) claim.RoleId = role.Id;
            if (claims.Count == 0) return;

            var insertClaimsCommand = 
                $"""
                 INSERT INTO 
                 {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsTable} 
                     (Id, RoleId, ClaimType, ClaimValue) 
                 VALUES 
                     ({databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRoleClaimsSequence}.NEXTVAL, :RoleId, :ClaimType, :ClaimValue)
                 """;
            await connection.ExecuteAsync(
                new CommandDefinition(insertClaimsCommand, claims, transaction, cancellationToken: ct)
            ).ConfigureAwait(false);
        }

        private static void ValidateClaims(ApplicationRole role)
        {
            if (role.Claims is not null && role.Claims.GroupBy(x => new { x.ClaimType, x.ClaimValue }).Any(x => x.Count() > 1))
                throw new InvalidOperationException("The role contains duplicate claims.");
        }
        
        public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken ct = default) 
        {
            role.ThrowIfNull(nameof(role));
            
            var command = $"""
                           DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} 
                           WHERE Id = :Id AND (ConcurrencyStamp IS NULL OR ConcurrencyStamp = :ConcurrencyStamp)
                           """;
 
            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
 
            var rowsDeleted = await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { role.Id, role.ConcurrencyStamp }, cancellationToken: ct)
            ).ConfigureAwait(false);
 
            return rowsDeleted == 1
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError
                {
                    Code = "ConcurrencyFailure",
                    Description = $"The role with name {role.Name} could not be deleted - it may have been modified or already removed."
                });
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
            var command = 
                $"""
                 SELECT 
                 Id, Name, NormalizedName, ConcurrencyStamp 
                 FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName}
                 """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QueryAsync<ApplicationRole>(
                new CommandDefinition(command, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
    }
}
