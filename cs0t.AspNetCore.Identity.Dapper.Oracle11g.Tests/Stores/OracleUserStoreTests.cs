namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Stores;

[Collection(nameof(OracleDatabaseCollectionFixture))]
public class OracleUserStoreTests : IAsyncLifetime
{
    private readonly TestDatabaseFactory _dbFactory;
    private readonly UserStore _userStore;
    
    public OracleUserStoreTests(OracleDockerFixture fixture)
    {
        _dbFactory = new TestDatabaseFactory(fixture.ConnectionString);
        _userStore = new UserStore(_dbFactory);
    }
    
    public Task InitializeAsync() => _dbFactory.ClearIdentityTablesAsync();

    public Task DisposeAsync() => _dbFactory.ClearIdentityTablesAsync();
    
    [Fact]
    public async Task CreateAsync_WithValidUser_PersistsAndCanBeReadBack()
    {
        var user = TestDataFactory.SupplyValidUser();
        var result = await _userStore.CreateAsync(user, CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.Succeeded, $"Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        var persist = await _userStore.FindByIdAsync(user.Id.ToString(), CancellationToken.None);

        Assert.NotNull(persist);
        Assert.Equal(user.Id, persist.Id);
        Assert.Equal(user.UserName, persist.UserName);
        Assert.Equal(user.NormalizedUserName, persist.NormalizedUserName);
        Assert.Equal(user.Email, persist.Email);
        Assert.Equal(user.NormalizedEmail, persist.NormalizedEmail);
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserExists_ReturnsUser()
    {
        var user = TestDataFactory.SupplyValidUser();
        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);

        var returnedUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(returnedUser);
        Assert.Equal(existingUserId, returnedUser.Id);
        Assert.Equal(user.UserName, returnedUser.UserName);
        Assert.Equal(user.NormalizedUserName, returnedUser.NormalizedUserName);
        Assert.Equal(user.Email, returnedUser.Email);
        Assert.Equal(user.NormalizedEmail, returnedUser.NormalizedEmail);
        Assert.Equal(user.EmailConfirmed, returnedUser.EmailConfirmed);
        Assert.Equal(user.SecurityStamp, returnedUser.SecurityStamp);
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        long nonExistentId = 999999;
        var result = await _userStore.FindByIdAsync(nonExistentId.ToString(), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task FindByNameAsync_WhenUserExists_ReturnsUser()
    {
        var user = TestDataFactory.SupplyValidUser();
        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);

        var returnedUser = await _userStore.FindByNameAsync(user.NormalizedUserName!, CancellationToken.None);
        Assert.NotNull(returnedUser);
        Assert.Equal(existingUserId, returnedUser.Id);
        Assert.Equal(user.UserName, returnedUser.UserName);
        Assert.Equal(user.NormalizedUserName, returnedUser.NormalizedUserName);
        Assert.Equal(user.Email, returnedUser.Email);
        Assert.Equal(user.NormalizedEmail, returnedUser.NormalizedEmail);
        Assert.Equal(user.EmailConfirmed, returnedUser.EmailConfirmed);
        Assert.Equal(user.SecurityStamp, returnedUser.SecurityStamp);
    }

    [Fact]
    public async Task FindByNameAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        string nonExistentUserName = "nonexistentuser";
        var result = await _userStore.FindByNameAsync(nonExistentUserName, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WhenUserExists_PersistsAndCanBeReadBack()
    {
        var user = TestDataFactory.SupplyValidUser();
        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(existingUser);

        var newEmail = "newemail@example.com";
        var newNormalizedEmail = newEmail.ToUpperInvariant();
        var oppositeEmailConfirmed = !existingUser.EmailConfirmed;

        existingUser.Email = newEmail;
        existingUser.NormalizedEmail = newNormalizedEmail;
        existingUser.PhoneNumber = null;
        existingUser.EmailConfirmed = oppositeEmailConfirmed;

        var result = await _userStore.UpdateAsync(existingUser, CancellationToken.None);
        Assert.True(result.Succeeded);

        var updatedUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);

        Assert.NotNull(updatedUser);
        Assert.Equal(existingUser.Id, updatedUser.Id);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.Equal(newNormalizedEmail, updatedUser.NormalizedEmail);
        Assert.Null(updatedUser.PhoneNumber);
        Assert.Equal(oppositeEmailConfirmed, updatedUser.EmailConfirmed);
    }

    [Fact]
    public async Task UpdateAsync_WhenUserDoesNotExist_ReturnsFailure()
    {
        var user = TestDataFactory.SupplyValidUser();
        var result = await _userStore.UpdateAsync(user, CancellationToken.None);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateAsync_WhenConcurrencyStampConflict_ReturnsFailure()
    {
        var existingUserId =
            await _dbFactory.AddUserInTableAsync(TestDataFactory.SupplyValidUser(), CancellationToken.None);

        var sameUserPtr1 = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        var sameUserPtr2 = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);

        Assert.NotNull(sameUserPtr1);
        Assert.NotNull(sameUserPtr2);
        Assert.Equal(sameUserPtr1.Id, sameUserPtr2.Id);
        Assert.Equal(sameUserPtr1.ConcurrencyStamp, sameUserPtr2.ConcurrencyStamp);

        var newName1 = "NewName1";
        sameUserPtr1.UserName = newName1;
        var firstUpdateResult = await _userStore.UpdateAsync(sameUserPtr1, CancellationToken.None);
        Assert.True(firstUpdateResult.Succeeded);

        var newName2 = "NewName2";
        sameUserPtr2.UserName = newName2;
        var secondUpdateResult = await _userStore.UpdateAsync(sameUserPtr2, CancellationToken.None);
        Assert.False(secondUpdateResult.Succeeded);

        //first update persists
        var sameUserPtr3 = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(sameUserPtr3);
        Assert.Equal(newName1, sameUserPtr3.UserName);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserExists_Succeeds()
    {
        var existingUserId =
            await _dbFactory.AddUserInTableAsync(TestDataFactory.SupplyValidUser(), CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);

        Assert.NotNull(existingUser);

        var deletionResult = await _userStore.DeleteAsync(existingUser, CancellationToken.None);
        Assert.True(deletionResult.Succeeded);

        var deletedUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserDoesNotExist_Fails()
    { 
        var user = TestDataFactory.SupplyValidUser();
        var result = await _userStore.DeleteAsync(user, CancellationToken.None);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AddToRoleAsync_WhenUserAndRoleExists_CreatesMembership()
    {
        var user = TestDataFactory.SupplyValidUser();
        var role = TestDataFactory.SupplyValidRole();

        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(existingUser);

        var existingRoleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);


        await _userStore.AddToRoleAsync(existingUser, role.Name!, CancellationToken.None);
        Assert.NotNull(existingUser.Roles);

        //check in memory persistence
        var userRolesNormalizedNames = existingUser.Roles.Select(x => x.NormalizedRoleName).ToList();
        Assert.Contains(userRolesNormalizedNames, x => x!.Equals(role.NormalizedName));

        //check database persistence
        var createdUserRoleRelationship =
            await _dbFactory.GetUserRoleByUserIdRoleIdAsync(existingUserId, existingRoleId, CancellationToken.None);
        Assert.NotNull(createdUserRoleRelationship);
        Assert.Equal(existingRoleId, createdUserRoleRelationship.RoleId);
        Assert.Equal(existingUserId, createdUserRoleRelationship.UserId);

        // check store reading persistence (Many-to-Many Read)
        var storeRoles = await _userStore.GetRolesAsync(existingUser, CancellationToken.None);
        Assert.Contains(storeRoles, r => r.Equals(role.Name));
    }

    [Fact]
    public async Task AddToRoleAsync_WhenUserExistsButRoleDoesNotExist_Throws()
    {
        var user = TestDataFactory.SupplyValidUser();
        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);

        Assert.NotNull(existingUser);

        var nonExistingRoleName = "NonExistingRoleName";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userStore.AddToRoleAsync(existingUser, nonExistingRoleName, CancellationToken.None));

        //relationship non-existent in db
        var storeRoles = await _userStore.GetRolesAsync(existingUser, CancellationToken.None);
        Assert.DoesNotContain(storeRoles, r => r.Equals(nonExistingRoleName));

        //make sure that in memory-object was not polluted
        Assert.False(existingUser.Roles?.Any(r => r.NormalizedRoleName == nonExistingRoleName.ToUpperInvariant()));
    }

    [Fact]
    public async Task AddToRoleAsync_WhenDualMembershipAttempted_SucceedsSilently()
    {
        var user = TestDataFactory.SupplyValidUser();
        var role = TestDataFactory.SupplyValidRole();

        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(existingUser);

        var existingRoleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);

        await _userStore.AddToRoleAsync(existingUser, role.Name!, CancellationToken.None);

        //in-memory persistence
        Assert.Contains(existingUser.Roles!, r =>
            string.Equals(r.NormalizedRoleName, role.NormalizedName, StringComparison.OrdinalIgnoreCase));

        var createdUserRoleRelationship =
            await _dbFactory.GetUserRoleByUserIdRoleIdAsync(existingUserId, existingRoleId, CancellationToken.None);

        //db persistence
        Assert.NotNull(createdUserRoleRelationship);

        //attempt 2nd addition in the same role
        await _userStore.AddToRoleAsync(existingUser, role.Name!, CancellationToken.None);

        //no in-memory duplication
        Assert.Equal(1, existingUser.Roles?.Count(r =>
            string.Equals(r.NormalizedRoleName, role.NormalizedName, StringComparison.OrdinalIgnoreCase)));

        //no in-db duplication
        var existingUserRoles = await _userStore.GetRolesAsync(existingUser, CancellationToken.None);
        Assert.Equal(1, existingUserRoles.Count(r => r == role.Name));
    }

    [Fact]
    public async Task RemoveFromRoleAsync_WhenUserInRole_RemovesRelationshipSuccessfully()
    {
        var user = TestDataFactory.SupplyValidUser();
        var role = TestDataFactory.SupplyValidRole();

        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(existingUser);

        var existingRoleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);

        await _userStore.AddToRoleAsync(existingUser, role.Name!, CancellationToken.None);

        await _userStore.RemoveFromRoleAsync(existingUser, role.Name!, CancellationToken.None);

        //removed in-memory
        Assert.NotNull(existingUser.Roles);
        Assert.Equal(0, existingUser.Roles.Count(r
            => string.Equals(r.NormalizedRoleName, role.NormalizedName, StringComparison.OrdinalIgnoreCase)));

        var relationship =
            await _dbFactory.GetUserRoleByUserIdRoleIdAsync(existingUserId, existingRoleId, CancellationToken.None);
        Assert.Null(relationship);

        var storeRoles = await _userStore.GetRolesAsync(existingUser, CancellationToken.None);
        Assert.DoesNotContain(storeRoles, r => string.Equals(r, role.Name, StringComparison.OrdinalIgnoreCase));

        var userStillExists = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(userStillExists);

        var roleStillExists = await _dbFactory.RoleExistsAsync(existingRoleId, CancellationToken.None);
        Assert.True(roleStillExists, "Role was deleted when it should have been persisted in the table !");
    }

    [Fact]
    public async Task RemoveFromRoleAsync_WhenUserNotInRole_SucceedsSilently()
    {
        var user = TestDataFactory.SupplyValidUser();
        var role = TestDataFactory.SupplyValidRole();

        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        var existingRoleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);

        await _userStore.RemoveFromRoleAsync(existingUser!, role.Name!, CancellationToken.None);

        Assert.DoesNotContain(existingUser!.Roles ?? new(), r => r.NormalizedRoleName == role.NormalizedName);

        var relationship =
            await _dbFactory.GetUserRoleByUserIdRoleIdAsync(existingUserId, existingRoleId, CancellationToken.None);
        Assert.Null(relationship);
    }

    [Fact]
    public async Task RemoveFromRoleAsync_WhenRoleDoesNotExistOrUserNotInRole_SucceedsSilently()
    {
        var user = TestDataFactory.SupplyValidUser();
        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        Assert.NotNull(existingUser);

        var nonExistingRoleName = "NonExistingRoleName";

        var exception = await Record.ExceptionAsync(() =>
            _userStore.RemoveFromRoleAsync(existingUser, nonExistingRoleName, CancellationToken.None));

        Assert.Null(exception);

        Assert.False(existingUser.Roles?
            .Any(r => string.Equals(r.NormalizedRoleName, nonExistingRoleName, StringComparison.OrdinalIgnoreCase)));

        var storeRoles = await _userStore.GetRolesAsync(existingUser, CancellationToken.None);
        Assert.Empty(storeRoles);
    }

    [Fact]
    public async Task IsInRoleAsync_ChecksCaseInsensitively_ReturnsCorrectValue()
    {
        var user = TestDataFactory.SupplyValidUser();
        var role = TestDataFactory.SupplyValidRole();

        var existingUserId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var existingUser = await _userStore.FindByIdAsync(existingUserId.ToString(), CancellationToken.None);
        await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);

        await _userStore.AddToRoleAsync(existingUser!, role.Name!, CancellationToken.None);

        Assert.True(await _userStore.IsInRoleAsync(existingUser!, role.Name!, CancellationToken.None));

        var lowerCaseRoleName = role.Name!.ToLowerInvariant();
        Assert.True(await _userStore.IsInRoleAsync(existingUser!, lowerCaseRoleName, CancellationToken.None));

        Assert.False(await _userStore.IsInRoleAsync(existingUser!, "NonExistent", CancellationToken.None));
    }

    [Fact]
    public async Task GetRolesAsync_WhenUserHasRoles_ReturnsRoleNamesOnlyForThatUser()
    { 
        var user1 = TestDataFactory.SupplyValidUser();
        var user2 = TestDataFactory.SupplyValidUser();
        var role1 = TestDataFactory.SupplyValidRole();
        var role2 = TestDataFactory.SupplyValidRole();

        var u1Id = await _dbFactory.AddUserInTableAsync(user1, CancellationToken.None);
        var u2Id = await _dbFactory.AddUserInTableAsync(user2, CancellationToken.None);
        var r1Id = await _dbFactory.AddRoleInTableAsync(role1, CancellationToken.None);
        var r2Id = await _dbFactory.AddRoleInTableAsync(role2, CancellationToken.None);

        //assign user1-role1 "admin", user2- role2 "user"
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var insertLinkSql = $@"
            INSERT INTO {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserRolesTableName} (UserId, RoleId) 
            VALUES (:UserId, :RoleId)";

        await connection.ExecuteAsync(insertLinkSql, new { UserId = u1Id, RoleId = r1Id });
        await connection.ExecuteAsync(insertLinkSql, new { UserId = u2Id, RoleId = r2Id });


        user1.Id = u1Id;
        var user1Roles = await _userStore.GetRolesAsync(user1, CancellationToken.None);

        //assert that user1 was only assigned role1
        Assert.NotNull(user1Roles);
        Assert.Single(user1Roles);
        Assert.Contains(role1.Name!, user1Roles);
        Assert.DoesNotContain(role2.Name!, user1Roles);

        //make sure that when we add fresh user it has no roles assigned
        var cleanUser = TestDataFactory.SupplyValidUser();
        var cleanUserId = await _dbFactory.AddUserInTableAsync(cleanUser, CancellationToken.None);
        cleanUser.Id = cleanUserId;

        var cleanUserRoles = await _userStore.GetRolesAsync(cleanUser, CancellationToken.None);
        Assert.NotNull(cleanUserRoles);
        Assert.Empty(cleanUserRoles);
    }

    [Fact]
    public async Task GetUsersInRoleAsync_WhenRoleHasUsers_ReturnsUsersCaseInsensitively()
    {
        var role = TestDataFactory.SupplyValidRole();
        var user1 = TestDataFactory.SupplyValidUser();
        var user2 = TestDataFactory.SupplyValidUser();

        var roleId = await _dbFactory.AddRoleInTableAsync(role, CancellationToken.None);
        var u1Id = await _dbFactory.AddUserInTableAsync(user1, CancellationToken.None);
        var u2Id = await _dbFactory.AddUserInTableAsync(user2, CancellationToken.None);

        //assign 2 users to the same role
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var insertLinkSql = $@"
            INSERT INTO {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserRolesTableName} (UserId, RoleId) 
            VALUES (:UserId, :RoleId)";

        await connection.ExecuteAsync(insertLinkSql, new { UserId = u1Id, RoleId = roleId });
        await connection.ExecuteAsync(insertLinkSql, new { UserId = u2Id, RoleId = roleId });

        var lowerCaseRoleName = role.Name!.ToLowerInvariant();
        var usersInRole = await _userStore.GetUsersInRoleAsync(lowerCaseRoleName, CancellationToken.None);

        Assert.NotNull(usersInRole);
        Assert.Equal(2, usersInRole.Count);

        var returnedUser1 = usersInRole.FirstOrDefault(u => u.Id == u1Id);
        var returnedUser2 = usersInRole.FirstOrDefault(u => u.Id == u2Id);

        //make sure we retrieve correct user1
        Assert.NotNull(returnedUser1);
        Assert.Equal(user1.UserName, returnedUser1.UserName);
        Assert.Equal(user1.FirstName, returnedUser1.FirstName);
        Assert.Equal(user1.Email, returnedUser1.Email);

        //make sure we retrieve correct user2
        Assert.NotNull(returnedUser2);
        Assert.Equal(user2.UserName, returnedUser2.UserName);
        Assert.Equal(user2.LastName, returnedUser2.LastName);
        Assert.Equal(user2.Email, returnedUser2.Email);

        //for nonexistent role we should get empty list
        var emptyResult = await _userStore.GetUsersInRoleAsync("NonExistentRole", CancellationToken.None);
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult);
    }

    //User claims test
    [Fact]
    public async Task AddClaimsAsync_WhenClaimsListPassed_AddsUniqueClaimsInDbAndInMemory()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);

        Assert.NotNull(createdUser);

        //create claims list with duplicate values
        var dupeClaim = new Claim(ClaimTypes.Name, "TestName");
        var uniqueClaim1 = new Claim(ClaimTypes.Email, "test@example.com");
        var uniqueClaim2 = new Claim(ClaimTypes.Role, "Admin");

        var claims = new List<Claim>
        {
            dupeClaim,
            uniqueClaim1,
            dupeClaim,
            dupeClaim,
            uniqueClaim2
        };

        int uniqueClaimsCount = 3;

        await _userStore.AddClaimsAsync(createdUser, claims, CancellationToken.None);

        //check in memory persistence
        Assert.NotNull(createdUser.Claims);
        Assert.Equal(uniqueClaimsCount, createdUser.Claims.Count);
        Assert.Equal(1,
            createdUser.Claims.Count(c => c.ClaimType == dupeClaim.Type && c.ClaimValue == dupeClaim.Value));
        Assert.Equal(1,
            createdUser.Claims.Count(c => c.ClaimType == uniqueClaim1.Type && c.ClaimValue == uniqueClaim1.Value));
        Assert.Equal(1,
            createdUser.Claims.Count(c => c.ClaimType == uniqueClaim2.Type && c.ClaimValue == uniqueClaim2.Value));

        //check in database persistence
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var sql = $"""
                    SELECT ClaimType, ClaimValue 
                    FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserClaimsTableName} 
                    WHERE UserId = :UserId
                   """;

        var inDatabaseUserClaims = (await connection.QueryAsync<(string ClaimType, string ClaimValue)>(
            new CommandDefinition(sql, new { UserId = createdUser.Id }, cancellationToken: CancellationToken.None)
        )).ToList();

        Assert.Equal(uniqueClaimsCount, inDatabaseUserClaims.Count);
        Assert.Equal(1,
            inDatabaseUserClaims.Count(c => c.ClaimType == dupeClaim.Type && c.ClaimValue == dupeClaim.Value));
        Assert.Equal(1,
            inDatabaseUserClaims.Count(c => c.ClaimType == uniqueClaim1.Type && c.ClaimValue == uniqueClaim1.Value));
        Assert.Equal(1,
            inDatabaseUserClaims.Count(c => c.ClaimType == uniqueClaim2.Type && c.ClaimValue == uniqueClaim2.Value));
    }

    [Fact]
    public async Task ReplaceClaimAsync_WhenClaimExists_UpdatesDbAndInMemoryCorrectly()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);
        Assert.NotNull(createdUser);

        var oldClaim = new Claim("GivenName", "George");
        var newClaim = new Claim("GivenName", "Geo");


        await _userStore.AddClaimsAsync(createdUser, new List<Claim> { oldClaim }, CancellationToken.None);

        await _userStore.ReplaceClaimAsync(createdUser, oldClaim, newClaim, CancellationToken.None);

        //checking in-memory persistence after replacing claim
        Assert.NotNull(createdUser.Claims);
        Assert.Contains(createdUser.Claims, c => c.ClaimType == newClaim.Type && c.ClaimValue == newClaim.Value);
        Assert.DoesNotContain(createdUser.Claims, c => c.ClaimType == oldClaim.Type && c.ClaimValue == oldClaim.Value);

        await using var connection = await _dbFactory.CreateConnectionAsync();
        var sql = $"""
                        SELECT ClaimType, ClaimValue 
                        FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserClaimsTableName} 
                        WHERE UserId = :UserId
                   """;

        var dbClaims = (await connection.QueryAsync<(string ClaimType, string ClaimValue)>(
            new CommandDefinition(sql, new { UserId = createdUser.Id }, cancellationToken: CancellationToken.None)
        )).ToList();

        Assert.Single(dbClaims);
        Assert.Contains(dbClaims, c => c.ClaimType == newClaim.Type && c.ClaimValue == newClaim.Value);
        Assert.DoesNotContain(dbClaims, c => c.ClaimType == oldClaim.Type && c.ClaimValue == oldClaim.Value);


        var fakeClaim = new Claim("Age", "99");
        var dummyClaim = new Claim("Age", "100");

        var exception = await Record.ExceptionAsync(() =>
            _userStore.ReplaceClaimAsync(createdUser, fakeClaim, dummyClaim, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveClaimsAsync_WhenClaimsExist_RemovesOnlyTargetedClaims()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);
        Assert.NotNull(createdUser);

        var claim1 = new Claim(ClaimTypes.Role, "Manager");
        var claim2 = new Claim(ClaimTypes.Locality, "NY city");
        var claim3 = new Claim("Department", "IT");

        await _userStore.AddClaimsAsync(createdUser, new List<Claim> { claim1, claim2, claim3 },
            CancellationToken.None);

        var claimsToRemove = new List<Claim> { claim1, new("FakeType", "FakeValue") };

        await _userStore.RemoveClaimsAsync(createdUser, claimsToRemove, CancellationToken.None);

        //check in memory persistence : nonexisting claim ignored, real one removed.
        Assert.NotNull(createdUser.Claims);
        Assert.Equal(2, createdUser.Claims.Count);
        Assert.DoesNotContain(createdUser.Claims, c => c.ClaimType == claim1.Type && c.ClaimValue == claim1.Value);
        Assert.Contains(createdUser.Claims, c => c.ClaimType == claim2.Type && c.ClaimValue == claim2.Value);
        Assert.Contains(createdUser.Claims, c => c.ClaimType == claim3.Type && c.ClaimValue == claim3.Value);

        await using var connection = await _dbFactory.CreateConnectionAsync();
        var sql = $"""
                        SELECT ClaimType, ClaimValue 
                        FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserClaimsTableName} 
                        WHERE UserId = :UserId
                   """;

        var dbClaims = (await connection.QueryAsync<(string ClaimType, string ClaimValue)>(
            new CommandDefinition(sql, new { UserId = createdUser.Id }, cancellationToken: CancellationToken.None)
        )).ToList();

        //same check but for database persistence
        Assert.Equal(2, dbClaims.Count);
        Assert.DoesNotContain(dbClaims, c => c.ClaimType == claim1.Type && c.ClaimValue == claim1.Value);
        Assert.Contains(dbClaims, c => c.ClaimType == claim2.Type && c.ClaimValue == claim2.Value);
        Assert.Contains(dbClaims, c => c.ClaimType == claim3.Type && c.ClaimValue == claim3.Value);
    }

    [Fact]
    public async Task GetUsersForClaimAsync_WhenUsersHaveClaim_ReturnsTargetedUsersOnly()
    {
        var user1 = TestDataFactory.SupplyValidUser();
        var user2 = TestDataFactory.SupplyValidUser();
        var user3 = TestDataFactory.SupplyValidUser();

        var u1Id = await _dbFactory.AddUserInTableAsync(user1, CancellationToken.None);
        var u2Id = await _dbFactory.AddUserInTableAsync(user2, CancellationToken.None);
        var u3Id = await _dbFactory.AddUserInTableAsync(user3, CancellationToken.None);

        var targetClaim = new Claim("Department", "IT");
        var partialMatchClaim = new Claim("Department", "HR");

        await using var connection = await _dbFactory.CreateConnectionAsync();

        var insertClaimSql =
            $"""
                 INSERT INTO {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserClaimsTableName} 
                 (Id, UserId, ClaimType, ClaimValue) 
                 VALUES ({_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserClaimsSequence}.NEXTVAL, :UserId, :ClaimType, :ClaimValue)
             """;

        //add 2 users to target claim and 3rd user no another claim
        await connection.ExecuteAsync(insertClaimSql,
            new { UserId = u1Id, ClaimType = targetClaim.Type, ClaimValue = targetClaim.Value });

        await connection.ExecuteAsync(insertClaimSql,
            new { UserId = u2Id, ClaimType = targetClaim.Type, ClaimValue = targetClaim.Value });

        await connection.ExecuteAsync(insertClaimSql,
            new { UserId = u3Id, ClaimType = partialMatchClaim.Type, ClaimValue = partialMatchClaim.Value });

        var usersWithClaim = await _userStore.GetUsersForClaimAsync(targetClaim, CancellationToken.None);

        //user count for target claim should be exactly 2
        Assert.NotNull(usersWithClaim);
        Assert.Equal(2, usersWithClaim.Count);

        var returnedUser1 = usersWithClaim.FirstOrDefault(u => u.Id == u1Id);
        var returnedUser2 = usersWithClaim.FirstOrDefault(u => u.Id == u2Id);

        //assert that we retrieved those exact 2 users
        Assert.NotNull(returnedUser1);
        Assert.Equal(user1.UserName, returnedUser1.UserName);
        Assert.Equal(user1.FirstName, returnedUser1.FirstName);

        Assert.NotNull(returnedUser2);
        Assert.Equal(user2.UserName, returnedUser2.UserName);
        Assert.Equal(user2.LastName, returnedUser2.LastName);

        //making sure we did not overfetch with that 3rd user
        Assert.DoesNotContain(usersWithClaim, u => u.Id == u3Id);

        var fakeClaim = new Claim("Clearance", "Level9");
        var emptyResult = await _userStore.GetUsersForClaimAsync(fakeClaim, CancellationToken.None);

        //when passing nonexisting claim we should expect an empty list
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult);
    }

    [Fact]
    public async Task AddLoginAsync_WhenLoginInfoPassed_PersistsInDbAndInMemory()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);
        Assert.NotNull(createdUser);

        var loginInfo = new UserLoginInfo("Google", "google-sub-123456", "Google Provider Display Name");

        await _userStore.AddLoginAsync(createdUser, loginInfo, CancellationToken.None);

        // Assert 1: In-Memory persistence
        Assert.NotNull(createdUser.Logins);
        Assert.Single(createdUser.Logins);
        var inMemoryLogin = createdUser.Logins.First();
        Assert.Equal(loginInfo.LoginProvider, inMemoryLogin.LoginProvider);
        Assert.Equal(loginInfo.ProviderKey, inMemoryLogin.ProviderKey);

        // Assert 2: Database persistence 
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var sql =
            $"""
                 SELECT LoginProvider, ProviderKey, ProviderDisplayName 
                 FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserLoginsTableName} 
                 WHERE UserId = :UserId
             """;

        var dbLogins =
            (await connection.QueryAsync<(string LoginProvider, string ProviderKey, string ProviderDisplayName)>(
                new CommandDefinition(sql, new { UserId = createdUser.Id }, cancellationToken: CancellationToken.None)
            )).ToList();

        Assert.Single(dbLogins);
        Assert.Equal(loginInfo.LoginProvider, dbLogins[0].LoginProvider);
        Assert.Equal(loginInfo.ProviderKey, dbLogins[0].ProviderKey);
        Assert.Equal(loginInfo.ProviderDisplayName, dbLogins[0].ProviderDisplayName);
    }

    [Fact]
    public async Task FindByLoginAsync_WhenLoginExists_ReturnsCorrectUserFullyHydrated()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);

        var provider = "Facebook";
        var providerKey = "fb-id-99999";
        var displayName = "Facebook User";

        await using var connection = await _dbFactory.CreateConnectionAsync();

        var insertLoginSql =
            $"""
                 INSERT INTO {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserLoginsTableName} 
                 (LoginProvider, ProviderKey, ProviderDisplayName, UserId) 
                 VALUES (:LoginProvider, :ProviderKey, :ProviderDisplayName, :UserId)
             """;

        await connection.ExecuteAsync(insertLoginSql,
            new
            {
                LoginProvider = provider, ProviderKey = providerKey, ProviderDisplayName = displayName, UserId = userId
            });

        var foundUser = await _userStore.FindByLoginAsync(provider, providerKey, CancellationToken.None);

        // Assert we got the exact user
        Assert.NotNull(foundUser);
        Assert.Equal(userId, foundUser.Id);
        Assert.Equal(user.UserName, foundUser.UserName);
        Assert.Equal(user.FirstName, foundUser.FirstName);

        //for nonexistent login info get return null
        var nonExistentUser = await _userStore.FindByLoginAsync("Apple", "fake-key", CancellationToken.None);
        Assert.Null(nonExistentUser);
    }

    [Fact]
    public async Task RemoveLoginAsync_WhenLoginExists_RemovesFromDbAndInMemory()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);

        var loginInfo = new UserLoginInfo("GitHub", "github-id-777", "GitHub Account");
        await _userStore.AddLoginAsync(createdUser!, loginInfo, CancellationToken.None);

        await _userStore.RemoveLoginAsync(createdUser!, loginInfo.LoginProvider, loginInfo.ProviderKey,
            CancellationToken.None);

        //assert in memory persistence with an empty list, even if collection property is loaded in memory, it should never become null,
        //if emptied it just stays as an empty list
        Assert.NotNull(createdUser!.Logins);
        Assert.DoesNotContain(createdUser.Logins, l => l.LoginProvider == loginInfo.LoginProvider);

        //in database persistence
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var countSql =
            $"""
                 SELECT COUNT(1) 
                 FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserLoginsTableName} 
                 WHERE UserId = :UserId AND LoginProvider = :LoginProvider AND ProviderKey = :ProviderKey
             """;

        var count = await connection.ExecuteScalarAsync<int>
        (countSql,
            new
            {
                UserId = createdUser.Id, LoginProvider = loginInfo.LoginProvider, ProviderKey = loginInfo.ProviderKey
            });

        Assert.Equal(0, count);

        //user object not removed by mistake
        var userStillExists = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);
        Assert.NotNull(userStillExists);
    }

    [Fact]
    public async Task GetLoginsAsync_WhenCalled_ReturnsOnlyTargetedUserLogins()
    {
        var user1 = TestDataFactory.SupplyValidUser();
        var user2 = TestDataFactory.SupplyValidUser();

        var u1Id = await _dbFactory.AddUserInTableAsync(user1, CancellationToken.None);
        var u2Id = await _dbFactory.AddUserInTableAsync(user2, CancellationToken.None);

        var createdUser1 = await _userStore.FindByIdAsync(u1Id.ToString(), CancellationToken.None);
        var createdUser2 = await _userStore.FindByIdAsync(u2Id.ToString(), CancellationToken.None);

        var googleLogin = new UserLoginInfo("Google", "g-1", "G");
        var msLogin = new UserLoginInfo("Microsoft", "ms-1", "MS");

        await _userStore.AddLoginAsync(createdUser1!, googleLogin, CancellationToken.None);
        await _userStore.AddLoginAsync(createdUser2!, msLogin, CancellationToken.None);

        var user1Logins = await _userStore.GetLoginsAsync(createdUser1!, CancellationToken.None);

        // Assert that we fetched exact login objects for target user
        Assert.NotNull(user1Logins);
        Assert.Single(user1Logins);
        Assert.Equal("Google", user1Logins[0].LoginProvider);

        var cleanUser = TestDataFactory.SupplyValidUser();
        var cleanUserId = await _dbFactory.AddUserInTableAsync(cleanUser, CancellationToken.None);
        var createdCleanUser = await _userStore.FindByIdAsync(cleanUserId.ToString(), CancellationToken.None);

        //if we demand in memory loading of relationship properties, even if nothing found we create an empty list
        var cleanLogins = await _userStore.GetLoginsAsync(createdCleanUser!, CancellationToken.None);
        Assert.NotNull(cleanLogins);
        Assert.Empty(cleanLogins);
    }

    [Fact]
    public async Task SetTokenAsync_WhenCalled_UpsertsTokenInDbAndInMemory()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);
        Assert.NotNull(createdUser);

        var provider = "Email";
        var tokenName = "ConfirmationToken";
        var initialValue = "token-value-111";
        var updatedValue = "token-value-222";

        await _userStore.SetTokenAsync(createdUser, provider, tokenName, initialValue, CancellationToken.None);

        // Assert 1: In-Memory persistence
        Assert.NotNull(createdUser.Tokens);
        Assert.Single(createdUser.Tokens);
        Assert.Contains(createdUser.Tokens,
            t => t.LoginProvider == provider && t.Name == tokenName && t.Value == initialValue);

        // Assert 2: Database-independent verification
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var selectSql = $"""
                            SELECT LoginProvider, Name, Value 
                            FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserTokensTableName} 
                            WHERE UserId = :UserId
                         """;

        var dbTokens = (await connection.QueryAsync<(string LoginProvider, string Name, string Value)>(
            new CommandDefinition(selectSql, new { UserId = createdUser.Id }, cancellationToken: CancellationToken.None)
        )).ToList();

        Assert.Single(dbTokens);
        Assert.Equal(initialValue, dbTokens[0].Value);

        // setting the same token should upsert (replace in place) 
        await _userStore.SetTokenAsync(createdUser, provider, tokenName, updatedValue, CancellationToken.None);

        // Assert 3: In-Memory update
        Assert.Single(createdUser.Tokens);
        Assert.Equal(updatedValue, createdUser.Tokens.First().Value);

        // Assert 4: Database update
        var dbTokensAfterUpsert = (await connection.QueryAsync<(string LoginProvider, string Name, string Value)>(
            new CommandDefinition(selectSql, new { UserId = createdUser.Id }, cancellationToken: CancellationToken.None)
        )).ToList();

        Assert.Single(dbTokensAfterUpsert); 
        Assert.Equal(updatedValue, dbTokensAfterUpsert[0].Value); 
    }

    [Fact]
    public async Task GetTokenAsync_WhenTokenExists_ReturnsCorrectTokenValue()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
    
        var provider = "Authenticator";
        var tokenName = "2FaSecret";
        var expectedValue = "secret-key-xyz";

        await using var connection = await _dbFactory.CreateConnectionAsync();
        var insertTokenSql = 
            $"""
                INSERT INTO {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserTokensTableName} 
                (UserId, LoginProvider, Name, Value) 
                VALUES (:UserId, :LoginProvider, :Name, :Value)
            """;
    
        await connection.ExecuteAsync(insertTokenSql, new { UserId = userId, LoginProvider = provider, Name = tokenName, Value = expectedValue });

        user.Id = userId;
    
        var returnedValue = await _userStore.GetTokenAsync(user, provider, tokenName, CancellationToken.None);

        Assert.NotNull(returnedValue);
        Assert.Equal(expectedValue, returnedValue);

        var missingValue = await _userStore.GetTokenAsync(user, "FakeProvider", "FakeName", CancellationToken.None);
        Assert.Null(missingValue);
    }
    
    [Fact]
    public async Task RemoveTokenAsync_WhenTokenExists_RemovesFromDbAndInMemory()
    {
        var user = TestDataFactory.SupplyValidUser();
        var userId = await _dbFactory.AddUserInTableAsync(user, CancellationToken.None);
        var createdUser = await _userStore.FindByIdAsync(userId.ToString(), CancellationToken.None);
    
        var provider = "ResetPassword";
        var tokenName = "ResetToken";
    
        await _userStore.SetTokenAsync(createdUser!, provider, tokenName, "some-value", CancellationToken.None);

        await _userStore.RemoveTokenAsync(createdUser!, provider, tokenName, CancellationToken.None);
        
        //in-memory persistence
        Assert.NotNull(createdUser!.Tokens);
        Assert.DoesNotContain(createdUser.Tokens, t => t.LoginProvider == provider && t.Name == tokenName);
        
        //asserting that token was removed from database
        await using var connection = await _dbFactory.CreateConnectionAsync();
        var countSql = 
            $"""
                SELECT COUNT(1) 
                FROM {_dbFactory.Options.DbSchema}.{_dbFactory.Options.UserTokensTableName} 
                WHERE UserId = :UserId AND LoginProvider = :LoginProvider AND Name = :Name
             """;

        var count = await connection
            .ExecuteScalarAsync<int>(countSql, new { UserId = createdUser.Id, LoginProvider = provider, Name = tokenName });
        
        Assert.Equal(0, count);
        
        //making sure no exception is thrown when removing a non-existing token
        var exception = await Record.ExceptionAsync(() => 
            _userStore.RemoveTokenAsync(createdUser!, provider, tokenName, CancellationToken.None));
    
        Assert.Null(exception); 
    }
    
}