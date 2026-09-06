using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Compliance;

[Collection(nameof(OracleDatabaseCollectionFixture))]
public class RoleManagerComplianceTests(OracleDockerFixture fixture) : IAsyncLifetime
{
    private readonly TestDatabaseFactory _dbFactory = new(fixture.ConnectionString);

    private RoleManager<ApplicationRole> _roleManager = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        await _dbFactory.ClearIdentityTablesAsync();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services
            .AddIdentity<ApplicationUser, ApplicationRole>()
            .AddDapperStores();
        services.AddScoped<IDatabaseConnectionFactory>(_ => _dbFactory);

        _serviceProvider = services.BuildServiceProvider();
        _roleManager = _serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task DisposeAsync()
    {
        await _dbFactory.ClearIdentityTablesAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_WithValidRole_Succeeds()
    {
        var role = TestDataFactory.SupplyValidRole();

        AssertSuccess(await _roleManager.CreateAsync(role));

        Assert.True(role.Id > 0);
        Assert.NotNull(role.ConcurrencyStamp);
        var persisted = await ReloadRoleAsync(role);
        Assert.Equal(role.Name, persisted.Name);
        Assert.Equal(role.NormalizedName, persisted.NormalizedName);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateRoleName_Fails()
    {
        var original = await CreateRoleAsync();
        var duplicate = TestDataFactory.SupplyValidRole();
        duplicate.Name = original.Name;

        var result = await _roleManager.CreateAsync(duplicate);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.DuplicateRoleName));
        Assert.Equal(original.Id, (await _roleManager.FindByNameAsync(original.Name!))!.Id);
    }

    [Fact]
    public async Task CreateAsync_WithNormalizedRoleNameCollision_Fails()
    {
        var original = TestDataFactory.SupplyValidRole();
        original.Name = "CaseSensitiveRole";
        AssertSuccess(await _roleManager.CreateAsync(original));
        var collision = TestDataFactory.SupplyValidRole();
        collision.Name = "casesensitiverole";

        var result = await _roleManager.CreateAsync(collision);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.DuplicateRoleName));
        Assert.Equal(original.Id, (await _roleManager.FindByNameAsync("CASESENSITIVEROLE"))!.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidRoleName_Fails(string? invalidName)
    {
        var role = TestDataFactory.SupplyValidRole();
        role.Name = invalidName;

        var result = await _roleManager.CreateAsync(role);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.InvalidRoleName));
    }

    [Fact]
    public async Task FindByIdAsync_WhenRoleExists_ReturnsRole()
    {
        var role = await CreateRoleAsync();

        var found = await _roleManager.FindByIdAsync(role.Id.ToString());

        Assert.NotNull(found);
        Assert.Equal(role.Id, found.Id);
        Assert.Equal(role.Name, found.Name);
        Assert.Equal(role.NormalizedName, found.NormalizedName);
        Assert.Null(found.Claims);
    }

    [Fact]
    public async Task FindByIdAsync_WhenRoleDoesNotExist_ReturnsNull()
    {
        Assert.Null(await _roleManager.FindByIdAsync(long.MaxValue.ToString()));
    }

    [Fact]
    public async Task FindByNameAsync_WhenRoleExists_ReturnsRole()
    {
        var role = await CreateRoleAsync();

        var found = await _roleManager.FindByNameAsync(role.Name!.ToLowerInvariant());

        Assert.NotNull(found);
        Assert.Equal(role.Id, found.Id);
        Assert.Equal(role.Name, found.Name);
    }

    [Fact]
    public async Task FindByNameAsync_WhenRoleDoesNotExist_ReturnsNull()
    {
        Assert.Null(await _roleManager.FindByNameAsync("MissingRole"));
    }

    [Fact]
    public async Task GetRoleNameAsync_ReturnsRoleName()
    {
        var role = await CreateRoleAsync();

        Assert.Equal(role.Name, await _roleManager.GetRoleNameAsync(await ReloadRoleAsync(role)));
    }

    [Fact]
    public async Task SetRoleNameAsync_UpdatesRoleName()
    {
        var role = await CreateRoleAsync();

        AssertSuccess(await _roleManager.SetRoleNameAsync(role, "UpdatedRoleName"));
        Assert.Equal("UpdatedRoleName", role.Name);
        Assert.Equal("UPDATEDROLENAME", role.NormalizedName);
        AssertSuccess(await _roleManager.UpdateAsync(role));

        var persisted = await ReloadRoleAsync(role);
        Assert.Equal("UpdatedRoleName", persisted.Name);
        Assert.Equal("UPDATEDROLENAME", persisted.NormalizedName);
    }

    [Fact]
    public async Task GetNormalizedRoleNameAsync_ReturnsNormalizedRoleName()
    {
        var role = TestDataFactory.SupplyValidRole();
        role.Name = "MixedCaseRole";
        AssertSuccess(await _roleManager.CreateAsync(role));

        var persisted = await ReloadRoleAsync(role);

        Assert.Equal(_roleManager.NormalizeKey(role.Name), persisted.NormalizedName);
    }

    [Fact]
    public async Task SetNormalizedRoleNameAsync_UpdatesNormalizedRoleName()
    {
        var role = await CreateRoleAsync();

        AssertSuccess(await _roleManager.SetRoleNameAsync(role, "AnotherMixedCaseRole"));
        Assert.Equal(_roleManager.NormalizeKey("AnotherMixedCaseRole"), role.NormalizedName);
        AssertSuccess(await _roleManager.UpdateAsync(role));

        var persisted = await ReloadRoleAsync(role);
        Assert.Equal(_roleManager.NormalizeKey("AnotherMixedCaseRole"), persisted.NormalizedName);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRole_Succeeds()
    {
        var role = await CreateRoleAsync();
        var originalStamp = role.ConcurrencyStamp;
        role.Name = "UpdatedThroughManager";

        AssertSuccess(await _roleManager.UpdateAsync(role));

        var persisted = await ReloadRoleAsync(role);
        Assert.Equal("UpdatedThroughManager", persisted.Name);
        Assert.Equal("UPDATEDTHROUGHMANAGER", persisted.NormalizedName);
        Assert.NotEqual(originalStamp, persisted.ConcurrencyStamp);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleConcurrencyStamp_Fails()
    {
        var role = await CreateRoleAsync();
        var firstCopy = await ReloadRoleAsync(role);
        var staleCopy = await ReloadRoleAsync(role);
        var staleStamp = staleCopy.ConcurrencyStamp;

        firstCopy.Name = "FirstRoleUpdate";
        AssertSuccess(await _roleManager.UpdateAsync(firstCopy));
        staleCopy.Name = "StaleRoleUpdate";

        var staleResult = await _roleManager.UpdateAsync(staleCopy);

        Assert.False(staleResult.Succeeded);
        Assert.Contains(staleResult.Errors, error => error.Code == "ConcurrencyFailure");
        Assert.Equal(staleStamp, staleCopy.ConcurrencyStamp);
        Assert.Equal("FirstRoleUpdate", (await ReloadRoleAsync(role)).Name);
    }

    [Fact]
    public async Task DeleteAsync_WhenRoleExists_Succeeds()
    {
        var role = await CreateRoleAsync();

        AssertSuccess(await _roleManager.DeleteAsync(role));

        Assert.Null(await _roleManager.FindByIdAsync(role.Id.ToString()));
        Assert.False(await _roleManager.RoleExistsAsync(role.Name!));
    }

    [Fact]
    public async Task DeleteAsync_WhenRoleDoesNotExist_Fails()
    {
        var role = TestDataFactory.SupplyValidRole();

        var result = await _roleManager.DeleteAsync(role);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
    }

    [Fact]
    public async Task DeleteAsync_RemovesExpectedRelatedData()
    {
        var role = await CreateRoleAsync();
        var user = TestDataFactory.SupplyValidUser();
        AssertSuccess(await _userManager.CreateAsync(user));
        var claim = new Claim("Permission", "DeletedRole.Test");
        AssertSuccess(await _roleManager.AddClaimAsync(role, claim));
        AssertSuccess(await _userManager.AddToRoleAsync(user, role.Name!));

        AssertSuccess(await _roleManager.DeleteAsync(role));

        Assert.Null(await _roleManager.FindByIdAsync(role.Id.ToString()));
        var reloadedUser = await _userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(reloadedUser);
        Assert.False(await _userManager.IsInRoleAsync(reloadedUser, role.Name!));
        Assert.DoesNotContain(await _userManager.GetUsersInRoleAsync(role.Name!), item => item.Id == user.Id);
        var detachedRole = new ApplicationRole { Id = role.Id };
        Assert.Empty(await _roleManager.GetClaimsAsync(detachedRole));
    }

    [Fact]
    public async Task RoleNameNormalization_IsConsistentThroughRoleManager()
    {
        var role = TestDataFactory.SupplyValidRole();
        role.Name = "mIxEdCaSeRoLe";
        AssertSuccess(await _roleManager.CreateAsync(role));

        var byLowerCase = await _roleManager.FindByNameAsync("mixedcaserole");
        var byUpperCase = await _roleManager.FindByNameAsync("MIXEDCASEROLE");

        Assert.NotNull(byLowerCase);
        Assert.NotNull(byUpperCase);
        Assert.Equal(role.Id, byLowerCase.Id);
        Assert.Equal(role.Id, byUpperCase.Id);
        Assert.Equal(_roleManager.NormalizeKey(role.Name), byLowerCase.NormalizedName);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateNormalizedRoleName_Fails()
    {
        var original = TestDataFactory.SupplyValidRole();
        original.Name = "OperationsManager";
        AssertSuccess(await _roleManager.CreateAsync(original));
        var duplicate = TestDataFactory.SupplyValidRole();
        duplicate.Name = "OPERATIONSMANAGER";

        var result = await _roleManager.CreateAsync(duplicate);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.DuplicateRoleName));
        Assert.Single(new[] { await _roleManager.FindByNameAsync("operationsmanager") }
            .Where(found => found is not null));
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateNormalizedRoleName_Fails()
    {
        var existing = TestDataFactory.SupplyValidRole();
        existing.Name = "ExistingRole";
        AssertSuccess(await _roleManager.CreateAsync(existing));
        var updated = TestDataFactory.SupplyValidRole();
        updated.Name = "UpdatedRole";
        AssertSuccess(await _roleManager.CreateAsync(updated));
        var originalStamp = updated.ConcurrencyStamp;
        updated.Name = "existingrole";

        var result = await _roleManager.UpdateAsync(updated);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.DuplicateRoleName));
        Assert.Equal(originalStamp, updated.ConcurrencyStamp);
        Assert.Equal("UpdatedRole", (await ReloadRoleAsync(updated)).Name);
    }

    private async Task<ApplicationRole> CreateRoleAsync()
    {
        var role = TestDataFactory.SupplyValidRole();
        AssertSuccess(await _roleManager.CreateAsync(role));
        return role;
    }

    private async Task<ApplicationRole> ReloadRoleAsync(ApplicationRole role)
    {
        var persisted = await _roleManager.FindByIdAsync(role.Id.ToString());
        Assert.NotNull(persisted);
        return persisted;
    }

    private static void AssertSuccess(IdentityResult result)
        => Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
}