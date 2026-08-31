using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UserRolesProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    {
        public async Task<List<ApplicationUserRole>> GetRolesAsync(ApplicationUser user, CancellationToken ct = default) 
        {
            user.ThrowIfNull(nameof(user));

            var command = $"""
                           SELECT ur.UserId, r.Id AS RoleId, r.Name AS RoleName, r.NormalizedName AS NormalizedRoleName
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} r 
                           INNER JOIN {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} ur ON ur.RoleId = r.Id 
                           WHERE ur.UserId = :UserId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var roles = await oracleConnection.QueryAsync<ApplicationUserRole>(
                new CommandDefinition(command, new { UserId = user.Id }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return roles.ToList();
        }
        
        public async Task AddToRoleAsync(ApplicationUser user, long roleId, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            
            var command = $"""
                           INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} 
                           (UserId, RoleId) 
                           SELECT :UserId, :RoleId FROM DUAL
                           WHERE NOT EXISTS (
                               SELECT 1 FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName}
                               WHERE UserId = :UserId AND RoleId = :RoleId
                           )
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { UserId = user.Id, RoleId = roleId }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task RemoveFromRoleAsync(ApplicationUser user, long roleId, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));

            var command = $"""
                           DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} 
                           WHERE UserId = :UserId AND RoleId = :RoleId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            await oracleConnection.ExecuteAsync(
                new CommandDefinition(command, new { UserId = user.Id, RoleId = roleId }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name cannot be null or empty.", nameof(roleName));

            var command = $"""
                           SELECT u.* 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName} u
                           INNER JOIN {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} ur ON u.Id = ur.UserId
                           INNER JOIN {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} r ON ur.RoleId = r.Id
                           WHERE r.NormalizedName = :NormalizedRoleName
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            var result = await oracleConnection.QueryAsync<ApplicationUser>(
                new CommandDefinition(command, new { NormalizedRoleName = roleName.ToUpper().Trim() }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return result.ToList();
        }
        
        public async Task<bool> IsInRoleAsync(ApplicationUser user, long roleId, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));

            var command = $"""
                           SELECT COUNT(1) 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName}
                           WHERE UserId = :UserId AND RoleId = :RoleId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);

            var result = await oracleConnection.ExecuteScalarAsync<int>(
                new CommandDefinition(command, new { UserId = user.Id, RoleId = roleId }, cancellationToken: ct)
            ).ConfigureAwait(false);

            return result > 0;
        }
    }
}
