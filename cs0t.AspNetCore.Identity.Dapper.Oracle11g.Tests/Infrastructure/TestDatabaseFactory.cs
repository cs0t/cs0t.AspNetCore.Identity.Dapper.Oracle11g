namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Infrastructure;

public class TestDatabaseFactory : IDatabaseConnectionFactory
{
    private readonly string _connectionString;

    public DbProviderOptions Options { get; }

    public TestDatabaseFactory(string connectionString)
    {
        _connectionString = connectionString;
        Options = new DbProviderOptions
        {
            ConnectionString = _connectionString,
            DbSchema = "APP_USER",
        };
    }
    
    public async Task<OracleConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }

    public async Task<long> AddUserInTableAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

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
        parameters.Add("GeneratedId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        var rawSql =
            $"""
                 INSERT INTO {Options.DbSchema}.{Options.UsersTableName}
                 (
                     Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                     PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, 
                     TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
                     FirstName, LastName, IsActive, CreatedAtUtc, LastLoggedInAtUtc, PasswordChangedAtUtc
                 )
                 VALUES
                 (
                     {Options.DbSchema}.{Options.UsersSequence}.NEXTVAL, 
                     :UserName, :NormalizedUserName, :Email, :NormalizedEmail, :EmailConfirmed, 
                     :PasswordHash, :SecurityStamp, :ConcurrencyStamp, :PhoneNumber, :PhoneNumberConfirmed, 
                     :TwoFactorEnabled, :LockoutEnd, :LockoutEnabled, :AccessFailedCount,
                     :FirstName, :LastName, :IsActive, :CreatedAtUtc, :LastLoggedInAtUtc, :PasswordChangedAtUtc
                 )
                 RETURNING Id INTO :GeneratedId
             """;

        await connection.ExecuteAsync(rawSql, parameters);
        return parameters.Get<long>("GeneratedId");
    }

    public async Task<long> AddRoleInTableAsync(ApplicationRole role, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("Id", role.Id);
        parameters.Add("Name", role.Name);
        parameters.Add("NormalizedName", role.NormalizedName);
        parameters.Add("ConcurrencyStamp", role.ConcurrencyStamp);
        parameters.Add("GeneratedId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        var rawSql =
            $"""
                 INSERT INTO {Options.DbSchema}.{Options.RolesTableName}
                 (
                     Id, Name, NormalizedName, ConcurrencyStamp
                 )
                 VALUES
                 (
                     {Options.DbSchema}.{Options.RolesSequence}.NEXTVAL, 
                     :Name, :NormalizedName, :ConcurrencyStamp
                 )
                 RETURNING Id INTO :GeneratedId
             """;

        await connection.ExecuteAsync(rawSql, parameters);
        return parameters.Get<long>("GeneratedId");
    }

    public async Task<ApplicationRole?> GetRoleInTableAsync(long roleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
                      SELECT Id, Name, NormalizedName, ConcurrencyStamp
                      FROM {Options.DbSchema}.{Options.RolesTableName}
                      WHERE Id = :Id
                  """;

        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(
            new CommandDefinition(sql, new { Id = roleId }, cancellationToken: cancellationToken));
    }

    public async Task<long> AddRoleClaimInTableAsync(long roleId, Claim claim,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new DynamicParameters(new
        {
            RoleId = roleId,
            ClaimType = claim.Type,
            ClaimValue = claim.Value
        });
        parameters.Add("GeneratedId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        var sql = $"""
                      INSERT INTO {Options.DbSchema}.{Options.UserRoleClaimsTableName}
                          (Id, RoleId, ClaimType, ClaimValue)
                      VALUES
                          ({Options.DbSchema}.{Options.UserRoleClaimsSequence}.NEXTVAL, :RoleId, :ClaimType, :ClaimValue)
                      RETURNING Id INTO :GeneratedId
                  """;

        await connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return parameters.Get<long>("GeneratedId");
    }

    public async Task<IList<IdentityRoleClaim<long>>> GetRoleClaimsInTableAsync(long roleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
                      SELECT Id, RoleId, ClaimType, ClaimValue
                      FROM {Options.DbSchema}.{Options.UserRoleClaimsTableName}
                      WHERE RoleId = :RoleId
                      ORDER BY Id
                  """;

        var claims = await connection.QueryAsync<IdentityRoleClaim<long>>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken));
        return claims.ToList();
    }

    public async Task SetRoleConcurrencyStampInTableAsync(long roleId, string concurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
                      UPDATE {Options.DbSchema}.{Options.RolesTableName}
                      SET ConcurrencyStamp = :ConcurrencyStamp
                      WHERE Id = :Id
                  """;

        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { Id = roleId, ConcurrencyStamp = concurrencyStamp }, cancellationToken: cancellationToken));
    }

    public async Task AddUserRoleInTableAsync(long userId, long roleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
                      INSERT INTO {Options.DbSchema}.{Options.UserRolesTableName} (UserId, RoleId)
                      VALUES (:UserId, :RoleId)
                  """;

        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { UserId = userId, RoleId = roleId }, cancellationToken: cancellationToken));
    }

    public async Task ClearIdentityTablesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        var tables = new[]
        {
            Options.UserRolesTableName,
            Options.UserClaimsTableName,
            Options.UserRoleClaimsTableName,
            Options.UserLoginsTableName,
            Options.UserTokensTableName,
            Options.UsersTableName,
            Options.RolesTableName
        };

        foreach (var table in tables)
        {
            await connection.ExecuteAsync(
                new CommandDefinition($"DELETE FROM {Options.DbSchema}.{table}", cancellationToken: cancellationToken));
        }
    }

    public async Task<IdentityUserRole<long>?> GetUserRoleByUserIdRoleIdAsync(long userId, long roleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"""
                        SELECT * FROM {Options.DbSchema}.{Options.UserRolesTableName}
                        WHERE UserId = :UserId AND RoleId = :RoleId
                   """;

        var result =
            await connection.QuerySingleOrDefaultAsync<IdentityUserRole<long>>(sql,
                new { UserId = userId, RoleId = roleId });
        return result;
    }

    public async Task<bool> RoleExistsAsync(long roleId, CancellationToken ct)
    {
        await using var connection = await CreateConnectionAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {Options.DbSchema}.{Options.RolesTableName} WHERE Id = :Id",
            new { Id = roleId });
        return count > 0;
    }
}