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
        public async Task<IEnumerable<(long RoleId, string RoleName)>> GetRolesAsync(ApplicationUser user, CancellationToken ct = default) 
        {
            user.ThrowIfNull(nameof(user));

            var command = $"""
                           SELECT r.Id AS RoleId, r.Name AS RoleName 
                           FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.RolesTableName} r 
                           INNER JOIN {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} ur ON ur.RoleId = r.Id 
                           WHERE ur.UserId = :UserId
                           """;

            await using var oracleConnection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            
            return await oracleConnection.QueryAsync<(long RoleId, string RoleName)>(
                new CommandDefinition(command, new { UserId = user.Id }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
        
        public async Task AddToRoleAsync(ApplicationUser user, long roleId, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            
            var command = $"""
                           INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} 
                           (UserId, RoleId) 
                           VALUES (:UserId, :RoleId)
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
    }
}
