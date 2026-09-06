namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Stores;

[Collection(nameof(OracleDatabaseCollectionFixture))]
public class OracleRoleStoreTests : IAsyncLifetime
{
    private readonly TestDatabaseFactory _dbFactory;
    private readonly RoleStore _roleStore;

    public OracleRoleStoreTests(OracleDockerFixture fixture)
    {
        _dbFactory = new TestDatabaseFactory(fixture.ConnectionString);
        _roleStore = new RoleStore(_dbFactory);
    }

    public Task InitializeAsync() => _dbFactory.ClearIdentityTablesAsync();

    public Task DisposeAsync() => _dbFactory.ClearIdentityTablesAsync();

    [Fact]
    public async Task PropertyMethods_WhenCalled_OnlyChangeAndReadTheInMemoryRole()
    {
        var role = TestDataFactory.SupplyValidRole();

        Assert.Equal(role.Id.ToString(), await _roleStore.GetRoleIdAsync(role, CancellationToken.None));
        Assert.Equal(role.Name, await _roleStore.GetRoleNameAsync(role, CancellationToken.None));
        Assert.Equal(role.NormalizedName,
            await _roleStore.GetNormalizedRoleNameAsync(role, CancellationToken.None));

        await _roleStore.SetRoleNameAsync(role, "Auditor", CancellationToken.None);
        await _roleStore.SetNormalizedRoleNameAsync(role, "AUDITOR", CancellationToken.None);

        Assert.Equal("Auditor", role.Name);
        Assert.Equal("AUDITOR", role.NormalizedName);
        Assert.False(await _dbFactory.RoleExistsAsync(role.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithValidRole_PersistsAndSynchronizesTheRole()
    {
        var role = TestDataFactory.SupplyValidRole();
        var originalId = role.Id;
        var originalStamp = role.ConcurrencyStamp;

        var result = await _roleStore.CreateAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(role.Id > 0);
        Assert.NotEqual(originalId, role.Id);
        Assert.NotNull(role.ConcurrencyStamp);
        Assert.NotEqual(originalStamp, role.ConcurrencyStamp);

        var persisted = await _dbFactory.GetRoleInTableAsync(role.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(role.Name, persisted.Name);
        Assert.Equal(role.NormalizedName, persisted.NormalizedName);
        Assert.Equal(role.ConcurrencyStamp, persisted.ConcurrencyStamp);
    }

    [Fact]
    public async Task CreateAsync_WithClaims_PersistsRootAndClaimsAtomically()
    {
        var role = TestDataFactory.SupplyValidRole();
        role.Claims =
        [
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Orders.Read" },
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Orders.Write" }
        ];

        var result = await _roleStore.CreateAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.All(role.Claims, claim => Assert.Equal(role.Id, claim.RoleId));

        var persistedClaims = await _dbFactory.GetRoleClaimsInTableAsync(role.Id, CancellationToken.None);
        Assert.Equal(2, persistedClaims.Count);
        Assert.Contains(persistedClaims, claim => ClaimEquals(claim, "Permission", "Orders.Read"));
        Assert.Contains(persistedClaims, claim => ClaimEquals(claim, "Permission", "Orders.Write"));
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateClaims_ThrowsWithoutPersistingRole()
    {
        var role = TestDataFactory.SupplyValidRole();
        role.Claims =
        [
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Orders.Read" },
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Orders.Read" }
        ];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleStore.CreateAsync(role, CancellationToken.None));

        Assert.False(await _dbFactory.RoleExistsAsync(role.Id, CancellationToken.None));
    }

    [Fact]
    public async Task FindByIdAsync_WhenRoleExists_ReturnsOnlyTheCoreRole()
    {
        var role = TestDataFactory.SupplyValidRole();
        var roleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);
        await _dbFactory.AddRoleClaimInTableAsync(roleId, new Claim("Permission", "Users.Read"));

        var returnedRole = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);

        Assert.NotNull(returnedRole);
        Assert.Equal(roleId, returnedRole.Id);
        Assert.Equal(role.Name, returnedRole.Name);
        Assert.Equal(role.NormalizedName, returnedRole.NormalizedName);
        Assert.Equal(role.ConcurrencyStamp, returnedRole.ConcurrencyStamp);
        Assert.Null(returnedRole.Claims);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("999999")]
    public async Task FindByIdAsync_WhenIdIsInvalidOrMissing_ReturnsNull(string roleId)
    {
        var result = await _roleStore.FindByIdAsync(roleId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByNameAsync_WhenRoleExists_FindsNormalizedNameCaseInsensitively()
    {
        var role = TestDataFactory.SupplyValidRole();
        var roleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);

        var returnedRole = await _roleStore.FindByNameAsync(
            $"  {role.NormalizedName!.ToLowerInvariant()}  ", CancellationToken.None);

        Assert.NotNull(returnedRole);
        Assert.Equal(roleId, returnedRole.Id);
        Assert.Equal(role.Name, returnedRole.Name);
        Assert.Null(returnedRole.Claims);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MISSING_ROLE")]
    public async Task FindByNameAsync_WhenNameIsBlankOrMissing_ReturnsNull(string normalizedName)
    {
        var result = await _roleStore.FindByNameAsync(normalizedName, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllRolesAsync_ReturnsAllCoreRolesOnly()
    {
        var role1 = TestDataFactory.SupplyValidRole();
        var role2 = TestDataFactory.SupplyValidRole();
        var role1Id = await _dbFactory.AddRoleInTableAsync(role1, CancellationToken.None);
        var role2Id = await _dbFactory.AddRoleInTableAsync(role2, CancellationToken.None);
        await _dbFactory.AddRoleClaimInTableAsync(role1Id, new Claim("Permission", "Users.Read"));

        var roles = (await _roleStore.GetAllRolesAsync(CancellationToken.None)).ToList();

        Assert.Equal(2, roles.Count);
        Assert.Contains(roles, role => role.Id == role1Id);
        Assert.Contains(roles, role => role.Id == role2Id);
        Assert.All(roles, role => Assert.Null(role.Claims));
    }

    [Fact]
    public async Task UpdateAsync_WhenRoleExists_PersistsChangesAndRefreshesConcurrencyStamp()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        var originalStamp = role.ConcurrencyStamp;

        role.Name = "UpdatedRole";
        role.NormalizedName = "UPDATEDROLE";
        var result = await _roleStore.UpdateAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(originalStamp, role.ConcurrencyStamp);
        var persisted = await _dbFactory.GetRoleInTableAsync(roleId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("UpdatedRole", persisted.Name);
        Assert.Equal("UPDATEDROLE", persisted.NormalizedName);
        Assert.Equal(role.ConcurrencyStamp, persisted.ConcurrencyStamp);
    }

    [Fact]
    public async Task UpdateAsync_WhenClaimsAreNull_LeavesDatabaseClaimsUntouched()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var existingClaim = new Claim("Permission", "Reports.Read");
        await _dbFactory.AddRoleClaimInTableAsync(roleId, existingClaim);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        Assert.Null(role.Claims);

        role.Name = "ReportsReader";
        role.NormalizedName = "REPORTSREADER";
        var result = await _roleStore.UpdateAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(role.Claims);
        var persistedClaims = await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None);
        Assert.Single(persistedClaims);
        Assert.True(ClaimEquals(persistedClaims[0], existingClaim.Type, existingClaim.Value));
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyClaims_RemovesAllDatabaseClaims()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        await _dbFactory.AddRoleClaimInTableAsync(roleId, new Claim("Permission", "Reports.Read"));
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        role.Claims = [];

        var result = await _roleStore.UpdateAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(role.Claims);
        Assert.Empty(await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithPopulatedClaims_ReplacesDatabaseClaims()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var oldClaim = new Claim("Permission", "Reports.Read");
        await _dbFactory.AddRoleClaimInTableAsync(roleId, oldClaim);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        role.Claims =
        [
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Reports.Write" },
            new IdentityRoleClaim<long> { ClaimType = "Scope", ClaimValue = "BackOffice" }
        ];

        var result = await _roleStore.UpdateAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.All(role.Claims, claim => Assert.Equal(roleId, claim.RoleId));
        var persistedClaims = await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None);
        Assert.Equal(2, persistedClaims.Count);
        Assert.DoesNotContain(persistedClaims, claim => ClaimEquals(claim, oldClaim.Type, oldClaim.Value));
        Assert.Contains(persistedClaims, claim => ClaimEquals(claim, "Permission", "Reports.Write"));
        Assert.Contains(persistedClaims, claim => ClaimEquals(claim, "Scope", "BackOffice"));
    }

    [Fact]
    public async Task UpdateAsync_WhenConcurrencyStampConflicts_FailsAndRestoresMemoryStamp()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var firstCopy = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        var staleCopy = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(firstCopy);
        Assert.NotNull(staleCopy);
        var staleStamp = staleCopy.ConcurrencyStamp;

        firstCopy.Name = "FirstUpdate";
        firstCopy.NormalizedName = "FIRSTUPDATE";
        Assert.True((await _roleStore.UpdateAsync(firstCopy, CancellationToken.None)).Succeeded);

        staleCopy.Name = "StaleUpdate";
        staleCopy.NormalizedName = "STALEUPDATE";
        var result = await _roleStore.UpdateAsync(staleCopy, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
        Assert.Equal(staleStamp, staleCopy.ConcurrencyStamp);
        var persisted = await _dbFactory.GetRoleInTableAsync(roleId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("FirstUpdate", persisted.Name);
        Assert.Equal(firstCopy.ConcurrencyStamp, persisted.ConcurrencyStamp);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoleDoesNotExist_FailsAndRestoresMemoryStamp()
    {
        var role = TestDataFactory.SupplyValidRole();
        var originalStamp = role.ConcurrencyStamp;

        var result = await _roleStore.UpdateAsync(role, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(originalStamp, role.ConcurrencyStamp);
        Assert.False(await _dbFactory.RoleExistsAsync(role.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateClaims_ThrowsAndLeavesDatabaseUnchanged()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        var originalName = role.Name;
        var originalStamp = role.ConcurrencyStamp;
        role.Name = "MustNotPersist";
        role.NormalizedName = "MUSTNOTPERSIST";
        role.Claims =
        [
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Duplicate" },
            new IdentityRoleClaim<long> { ClaimType = "Permission", ClaimValue = "Duplicate" }
        ];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roleStore.UpdateAsync(role, CancellationToken.None));

        Assert.Equal(originalStamp, role.ConcurrencyStamp);
        var persisted = await _dbFactory.GetRoleInTableAsync(roleId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(originalName, persisted.Name);
        Assert.Equal(originalStamp, persisted.ConcurrencyStamp);
        Assert.Empty(await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WhenRoleExists_DeletesRoleAndCascadesRelationships()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var userId = await _dbFactory.AddUserInTableAsync(
            TestDataFactory.SupplyValidUser(), CancellationToken.None);
        await _dbFactory.AddRoleClaimInTableAsync(roleId, new Claim("Permission", "Users.Read"));
        await _dbFactory.AddUserRoleInTableAsync(userId, roleId, CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);

        var result = await _roleStore.DeleteAsync(role, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(await _dbFactory.RoleExistsAsync(roleId, CancellationToken.None));
        Assert.Empty(await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None));
        Assert.Null(await _dbFactory.GetUserRoleByUserIdRoleIdAsync(userId, roleId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WithStaleConcurrencyStamp_FailsWithoutDeletingRole()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        await _dbFactory.SetRoleConcurrencyStampInTableAsync(roleId, Guid.NewGuid().ToString());

        var result = await _roleStore.DeleteAsync(role, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
        Assert.True(await _dbFactory.RoleExistsAsync(roleId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClaimsAsync_WhenFirstCalled_LoadsDatabaseClaimsAndThenUsesMemory()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var initialClaim = new Claim("Permission", "Users.Read");
        await _dbFactory.AddRoleClaimInTableAsync(roleId, initialClaim);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        Assert.Null(role.Claims);

        var firstRead = await _roleStore.GetClaimsAsync(role, CancellationToken.None);

        Assert.Single(firstRead);
        Assert.NotNull(role.Claims);
        Assert.Single(role.Claims);
        Assert.True(ClaimEquals(role.Claims[0], initialClaim.Type, initialClaim.Value));

        await _dbFactory.AddRoleClaimInTableAsync(roleId, new Claim("Permission", "Users.Write"));
        var cachedRead = await _roleStore.GetClaimsAsync(role, CancellationToken.None);

        Assert.Single(cachedRead);
        Assert.DoesNotContain(cachedRead,
            claim => claim.Type == "Permission" && claim.Value == "Users.Write");
        Assert.Equal(2, (await _dbFactory.GetRoleClaimsInTableAsync(roleId)).Count);
    }

    [Fact]
    public async Task GetClaimsAsync_WhenRoleHasNoClaims_InitializesAnEmptyCollection()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);

        var claims = await _roleStore.GetClaimsAsync(role, CancellationToken.None);

        Assert.Empty(claims);
        Assert.NotNull(role.Claims);
        Assert.Empty(role.Claims);
    }

    [Fact]
    public async Task AddClaimAsync_WithUniqueClaim_PersistsInDatabaseAndMemoryWithoutDuplicates()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);
        var claim = new Claim("Permission", "Invoices.Read");

        await _roleStore.AddClaimAsync(role, claim, CancellationToken.None);
        await _roleStore.AddClaimAsync(role, claim, CancellationToken.None);

        Assert.NotNull(role.Claims);
        Assert.Single(role.Claims);
        Assert.True(ClaimEquals(role.Claims[0], claim.Type, claim.Value));
        var persistedClaims = await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None);
        Assert.Single(persistedClaims);
        Assert.True(ClaimEquals(persistedClaims[0], claim.Type, claim.Value));
    }

    [Fact]
    public async Task AddClaimAsync_WhenClaimAlreadyExistsInDatabase_LoadsAndSkipsDuplicate()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var claim = new Claim("Permission", "Invoices.Read");
        await _dbFactory.AddRoleClaimInTableAsync(roleId, claim);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);

        await _roleStore.AddClaimAsync(role, claim, CancellationToken.None);

        Assert.NotNull(role.Claims);
        Assert.Single(role.Claims);
        Assert.Single(await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None));
    }

    [Fact]
    public async Task AddClaimAsync_WithSameValueButDifferentType_PersistsBothClaims()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);

        await _roleStore.AddClaimAsync(role, new Claim("Permission", "Read"), CancellationToken.None);
        await _roleStore.AddClaimAsync(role, new Claim("Scope", "Read"), CancellationToken.None);

        Assert.NotNull(role.Claims);
        Assert.Equal(2, role.Claims.Count);
        var persistedClaims = await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None);
        Assert.Equal(2, persistedClaims.Count);
        Assert.Contains(persistedClaims, claim => ClaimEquals(claim, "Permission", "Read"));
        Assert.Contains(persistedClaims, claim => ClaimEquals(claim, "Scope", "Read"));
    }

    [Fact]
    public async Task RemoveClaimAsync_WhenClaimExists_RemovesItFromDatabaseAndMemory()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var removedClaim = new Claim("Permission", "Invoices.Read");
        var retainedClaim = new Claim("Permission", "Invoices.Write");
        await _dbFactory.AddRoleClaimInTableAsync(roleId, removedClaim);
        await _dbFactory.AddRoleClaimInTableAsync(roleId, retainedClaim);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);

        await _roleStore.RemoveClaimAsync(role, removedClaim, CancellationToken.None);

        Assert.NotNull(role.Claims);
        Assert.Single(role.Claims);
        Assert.True(ClaimEquals(role.Claims[0], retainedClaim.Type, retainedClaim.Value));
        var persistedClaims = await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None);
        Assert.Single(persistedClaims);
        Assert.True(ClaimEquals(persistedClaims[0], retainedClaim.Type, retainedClaim.Value));
    }

    [Fact]
    public async Task RemoveClaimAsync_WhenClaimDoesNotExist_SucceedsAndKeepsCollectionsSynchronized()
    {
        var roleId = await _dbFactory.AddRoleInTableAsync(
            TestDataFactory.SupplyValidRole(), CancellationToken.None);
        var role = await _roleStore.FindByIdAsync(roleId.ToString(), CancellationToken.None);
        Assert.NotNull(role);

        var exception = await Record.ExceptionAsync(() => _roleStore.RemoveClaimAsync(
            role, new Claim("Permission", "Missing"), CancellationToken.None));

        Assert.Null(exception);
        Assert.NotNull(role.Claims);
        Assert.Empty(role.Claims);
        Assert.Empty(await _dbFactory.GetRoleClaimsInTableAsync(roleId, CancellationToken.None));
    }

    [Fact]
    public void Dispose_WhenCalled_DoesNotThrow()
    {
        var exception = Record.Exception(_roleStore.Dispose);

        Assert.Null(exception);
    }

    private static bool ClaimEquals(IdentityRoleClaim<long> claim, string type, string value)
        => claim.ClaimType == type && claim.ClaimValue == value;
}