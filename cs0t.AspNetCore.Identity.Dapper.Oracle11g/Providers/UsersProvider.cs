using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UsersProvider(IDatabaseConnectionFactory databaseConnectionFactory)
    {
        //CRUD
        public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            ValidateRelationships(user);
            var originalId = user.Id;
            
            var sql = $"""
                       INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName}
                       (
                           Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                           PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, 
                           TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                           FirstName, LastName, IsActive, CreatedAtUtc, LastLoggedInAtUtc, PasswordChangedAtUtc
                       )
                       VALUES
                       (
                           {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersSequence}.NEXTVAL, 
                           :UserName, :NormalizedUserName, :Email, :NormalizedEmail, :EmailConfirmed, 
                           :PasswordHash, :SecurityStamp, :ConcurrencyStamp, :PhoneNumber, :PhoneNumberConfirmed, 
                           :TwoFactorEnabled, :LockoutEnd, :LockoutEnabled, :AccessFailedCount,
                           :FirstName, :LastName, :IsActive, :CreatedAtUtc, :LastLoggedInAtUtc, :PasswordChangedAtUtc
                       )
                       RETURNING Id INTO :GeneratedId
                       """;
 
            await using var connection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                var parameters = CreateUserDynamicParameters(user);
                parameters.Add("GeneratedId", dbType: DbType.Int64, direction: ParameterDirection.Output);

                var rowsInserted = await connection.ExecuteAsync(
                    new CommandDefinition(sql, parameters, transaction, cancellationToken: ct)
                ).ConfigureAwait(false);
                
                var generatedId = parameters.Get<long>("GeneratedId");

                if (generatedId <= 0)
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return IdentityResult.Failed(new IdentityError { Description = $"The user with name {user.UserName} could not be inserted." });
                }

                user.Id = generatedId;
                await SynchronizeRelationshipsAsync(connection, transaction, user, ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return IdentityResult.Success;
            }
            catch
            {
                user.Id = originalId;
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }

         public async Task<IdentityResult> UpdateAsync(ApplicationUser user, string originalConcurrencyStamp, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            ValidateRelationships(user);
 
            var sql = $"""
                       UPDATE {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName}
                       SET 
                           UserName = :UserName, 
                           NormalizedUserName = :NormalizedUserName, 
                           Email = :Email, 
                           NormalizedEmail = :NormalizedEmail, 
                           EmailConfirmed = :EmailConfirmed, 
                           PasswordHash = :PasswordHash, 
                           SecurityStamp = :SecurityStamp, 
                           ConcurrencyStamp = :ConcurrencyStamp, 
                           PhoneNumber = :PhoneNumber, 
                           PhoneNumberConfirmed = :PhoneNumberConfirmed, 
                           TwoFactorEnabled = :TwoFactorEnabled, 
                           LockoutEnd = :LockoutEnd, 
                           LockoutEnabled = :LockoutEnabled, 
                           AccessFailedCount = :AccessFailedCount,
                           FirstName = :FirstName, 
                           LastName = :LastName, 
                           IsActive = :IsActive, 
                           CreatedAtUtc = :CreatedAtUtc, 
                           LastLoggedInAtUtc = :LastLoggedInAtUtc, 
                           PasswordChangedAtUtc = :PasswordChangedAtUtc
                       WHERE Id = :Id AND (ConcurrencyStamp IS NULL OR ConcurrencyStamp = :DatabaseConcurrencyStamp)
                       """;
 
            await using var connection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                var parameters = CreateUserDynamicParameters(user);
                parameters.Add("DatabaseConcurrencyStamp", originalConcurrencyStamp);

                var rowsUpdated = await connection.ExecuteAsync(
                    new CommandDefinition(sql, parameters, transaction, cancellationToken: ct)
                ).ConfigureAwait(false);

                if (rowsUpdated != 1)
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "ConcurrencyFailure",
                        Description = "Optimistic concurrency failure. The user has been modified by another process."
                    });
                }

                await SynchronizeRelationshipsAsync(connection, transaction, user, ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return IdentityResult.Success;
            }
            catch
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }

        private async Task SynchronizeRelationshipsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            ApplicationUser user,
            CancellationToken ct)
        {
            if (user.Claims is not null)
            {
                var deleteSql = $"DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsTableName} WHERE UserId = :UserId";
                await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { UserId = user.Id }, transaction, cancellationToken: ct)).ConfigureAwait(false);

                var claims = user.Claims
                    .GroupBy(x => new { x.ClaimType, x.ClaimValue })
                    .Select(x => x.First())
                    .ToList();

                foreach (var claim in claims) claim.UserId = user.Id;
                if (claims.Count > 0)
                {
                    var insertSql = $"INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsTableName} (Id, UserId, ClaimType, ClaimValue) VALUES ({databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserClaimsSequence}.NEXTVAL, :UserId, :ClaimType, :ClaimValue)";
                    await connection.ExecuteAsync(new CommandDefinition(insertSql, claims, transaction, cancellationToken: ct)).ConfigureAwait(false);
                }
            }

            if (user.Roles is not null)
            {
                var deleteSql = $"DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} WHERE UserId = :UserId";
                await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { UserId = user.Id }, transaction, cancellationToken: ct)).ConfigureAwait(false);

                var roles = user.Roles.GroupBy(x => x.RoleId).Select(x => x.First()).ToList();
                foreach (var role in roles) role.UserId = user.Id;
                if (roles.Count > 0)
                {
                    var insertSql = $"INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserRolesTableName} (UserId, RoleId) VALUES (:UserId, :RoleId)";
                    await connection.ExecuteAsync(new CommandDefinition(insertSql, roles, transaction, cancellationToken: ct)).ConfigureAwait(false);
                }
            }

            if (user.Logins is not null)
            {
                var deleteSql = $"DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName} WHERE UserId = :UserId";
                await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { UserId = user.Id }, transaction, cancellationToken: ct)).ConfigureAwait(false);

                var logins = user.Logins
                    .GroupBy(x => new { x.LoginProvider, x.ProviderKey })
                    .Select(x => x.First())
                    .ToList();

                foreach (var login in logins) login.UserId = user.Id;
                if (logins.Count > 0)
                {
                    var insertSql = $"INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserLoginsTableName} (UserId, LoginProvider, ProviderKey, ProviderDisplayName) VALUES (:UserId, :LoginProvider, :ProviderKey, :ProviderDisplayName)";
                    await connection.ExecuteAsync(new CommandDefinition(insertSql, logins, transaction, cancellationToken: ct)).ConfigureAwait(false);
                }
            }

            if (user.Tokens is not null)
            {
                var deleteSql = $"DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserTokensTableName} WHERE UserId = :UserId";
                await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { UserId = user.Id }, transaction, cancellationToken: ct)).ConfigureAwait(false);

                var tokens = user.Tokens
                    .GroupBy(x => new { x.LoginProvider, x.Name })
                    .Select(x => x.First())
                    .ToList();

                foreach (var token in tokens) token.UserId = user.Id;
                if (tokens.Count > 0)
                {
                    var insertSql = $"INSERT INTO {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UserTokensTableName} (UserId, LoginProvider, Name, Value) VALUES (:UserId, :LoginProvider, :Name, :Value)";
                    await connection.ExecuteAsync(new CommandDefinition(insertSql, tokens, transaction, cancellationToken: ct)).ConfigureAwait(false);
                }
            }
        }

        private static void ValidateRelationships(ApplicationUser user)
        {
            if (user.Claims is not null && user.Claims.GroupBy(x => new { x.ClaimType, x.ClaimValue }).Any(x => x.Count() > 1))
                throw new InvalidOperationException("The user contains duplicate claims.");

            if (user.Roles is not null && user.Roles.GroupBy(x => x.RoleId).Any(x => x.Count() > 1))
                throw new InvalidOperationException("The user contains duplicate roles.");

            if (user.Logins is not null && user.Logins.GroupBy(x => new { x.LoginProvider, x.ProviderKey }).Any(x => x.Count() > 1))
                throw new InvalidOperationException("The user contains duplicate external logins.");

            if (user.Tokens is not null && user.Tokens.GroupBy(x => new { x.LoginProvider, x.Name }).Any(x => x.Count() > 1))
                throw new InvalidOperationException("The user contains duplicate authentication tokens.");
        }
 
        public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct = default)
        {
            user.ThrowIfNull(nameof(user));
            
            var sql = $"""
                       DELETE FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName}
                       WHERE Id = :Id AND (ConcurrencyStamp IS NULL OR ConcurrencyStamp = :ConcurrencyStamp)
                       """;
 
            await using var connection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
 
            var rowsDeleted = await connection.ExecuteAsync(
                new CommandDefinition(sql, new { user.Id, user.ConcurrencyStamp }, cancellationToken: ct)
            ).ConfigureAwait(false);
 
            return rowsDeleted == 1
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError
                {
                    Code = "ConcurrencyFailure",
                    Description = $"The user with name {user.UserName} could not be deleted - it may have been modified or already removed."
                });
        }
 
        //LOOKUPS
        public async Task<ApplicationUser?> FindByIdAsync(long userId, CancellationToken ct = default)
        {
            var sql = $"""
                       SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                       PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, 
                       TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                       FirstName, LastName, IsActive, CreatedAtUtc, LastLoggedInAtUtc, PasswordChangedAtUtc
                       FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName}
                       WHERE Id = :Id
                       """;
 
            await using var connection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
 
            return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(
                new CommandDefinition(sql, new { Id = userId }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
 
        public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(normalizedUserName)) return null;
 
            var sql = $"""
                       SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                       PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, 
                       TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                       FirstName, LastName, IsActive, CreatedAtUtc, LastLoggedInAtUtc, PasswordChangedAtUtc
                       FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName}
                       WHERE NormalizedUserName = :NormalizedUserName
                       """;
 
            await using var connection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
 
            return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(
                new CommandDefinition(sql, new { NormalizedUserName = normalizedUserName }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }
 
        public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(normalizedEmail)) return null;
 
            var sql = $"""
                       SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                       PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, 
                       TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                       FirstName, LastName, IsActive, CreatedAtUtc, LastLoggedInAtUtc, PasswordChangedAtUtc
                       FROM {databaseConnectionFactory.Options.DbSchema}.{databaseConnectionFactory.Options.UsersTableName}
                       WHERE NormalizedEmail = :NormalizedEmail
                       """;
 
            await using var connection = await databaseConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
 
            return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(
                new CommandDefinition(sql, new { NormalizedEmail = normalizedEmail }, cancellationToken: ct)
            ).ConfigureAwait(false);
        }

        private static DynamicParameters CreateUserDynamicParameters(ApplicationUser user)
        {
            var parameters = new DynamicParameters();
            parameters.Add("Id", user.Id);
            parameters.Add("UserName", user.UserName);
            parameters.Add("NormalizedUserName", user.NormalizedUserName);
            parameters.Add("Email", user.Email);
            parameters.Add("NormalizedEmail", user.NormalizedEmail);
            parameters.Add("EmailConfirmed", user.EmailConfirmed ? 1 : 0); 
            parameters.Add("PasswordHash", user.PasswordHash);
            parameters.Add("SecurityStamp", user.SecurityStamp);
            parameters.Add("ConcurrencyStamp", user.ConcurrencyStamp);
            parameters.Add("PhoneNumber", user.PhoneNumber);
            parameters.Add("PhoneNumberConfirmed", user.PhoneNumberConfirmed ? 1 : 0);
            parameters.Add("TwoFactorEnabled", user.TwoFactorEnabled ? 1 : 0);
            parameters.Add("LockoutEnd", user.LockoutEnd?.UtcDateTime); 
            parameters.Add("LockoutEnabled", user.LockoutEnabled ? 1 : 0);
            parameters.Add("AccessFailedCount", user.AccessFailedCount);
            parameters.Add("FirstName", user.FirstName);
            parameters.Add("LastName", user.LastName);
            parameters.Add("IsActive", user.IsActive ? 1 : 0);
            parameters.Add("CreatedAtUtc", user.CreatedAtUtc);
            parameters.Add("LastLoggedInAtUtc", user.LastLoggedInAtUtc);
            parameters.Add("PasswordChangedAtUtc", user.PasswordChangedAtUtc);
            
            return parameters;
        }
    }
}
