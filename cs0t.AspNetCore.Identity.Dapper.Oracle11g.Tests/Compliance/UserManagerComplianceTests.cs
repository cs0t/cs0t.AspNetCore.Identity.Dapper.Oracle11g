using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Compliance;

[Collection(nameof(OracleDatabaseCollectionFixture))]
public class UserManagerComplianceTests(OracleDockerFixture fixture) : IAsyncLifetime
{
    private readonly TestDatabaseFactory _dbFactory = new(fixture.ConnectionString);

    private UserManager<ApplicationUser> _userManager = null!;
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        await _dbFactory.ClearIdentityTablesAsync();
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddDataProtection();

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.Tokens.EmailConfirmationTokenProvider = TestEmailTokenProvider.ProviderName;
            })
            .AddDapperStores()
            .AddDefaultTokenProviders()
            .AddTokenProvider<TestEmailTokenProvider>(TestEmailTokenProvider.ProviderName);

        services.AddScoped<IDatabaseConnectionFactory>(_ => _dbFactory);

        _serviceProvider = services.BuildServiceProvider();

        _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task DisposeAsync()
    {
        await _dbFactory.ClearIdentityTablesAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_WithValidUserAndPassword_Succeeds()
    {
        var user = TestDataFactory.SupplyValidUser();
        var password = "P@ssw0rd";
        var result = await _userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));

        var createdUser = await _userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(createdUser);

        //assert that password was actually saved properly
        var passwordHashesMatch = await _userManager.CheckPasswordAsync(createdUser, password);
        Assert.True(passwordHashesMatch);

        //try to match wrong password should return false
        var wrongPassword = "wrongP@ssw0rd";
        var wrongPasswordHash = await _userManager.CheckPasswordAsync(createdUser, wrongPassword);
        Assert.False(wrongPasswordHash);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateUserName_Fails()
    {
        var userOriginal = TestDataFactory.SupplyValidUser();
        var userDuplicate = TestDataFactory.SupplyValidUser();
        userDuplicate.UserName = userOriginal.UserName;
        
        var result = await _userManager.CreateAsync(userOriginal);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        
        //assert that user with duplicate username fails
        var resultDuplicate = await _userManager.CreateAsync(userDuplicate);
        Assert.False(resultDuplicate.Succeeded, string.Join(", ", resultDuplicate.Errors.Select(e => e.Description)));
    }
    
    [Fact]
    public async Task CreateAsync_WithDuplicateEmail_Fails()
    {
        var userOriginal = TestDataFactory.SupplyValidUser();
        var userDuplicate = TestDataFactory.SupplyValidUser();
        userDuplicate.Email = userOriginal.Email;
        
        var result = await _userManager.CreateAsync(userOriginal);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        
        //assert that user with duplicate email fails
        var resultDuplicate = await _userManager.CreateAsync(userDuplicate);
        Assert.False(resultDuplicate.Succeeded, string.Join(", ", resultDuplicate.Errors.Select(e => e.Description)));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase")]
    [InlineData("ALLUPPERCASE")]
    [InlineData("12345678")]
    [InlineData("password")]
    [InlineData("  ")]
    [InlineData("")]
    public async Task CreateAsync_WithInvalidPassword_Fails(string invalidPassword)
    {
        var user = TestDataFactory.SupplyValidUser();
        var result =  await _userManager.CreateAsync(user, invalidPassword);
        Assert.False(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        
        
        var userPersisted = await _userManager.FindByIdAsync(user.Id.ToString());
        Assert.Null(userPersisted);
    }
    
    [Fact]
    public async Task CreateAsync_WithNormalizedCollisions_Fails()
    {
        var userOriginal = TestDataFactory.SupplyValidUser();
        var userEmailDuplicate = TestDataFactory.SupplyValidUser();
        var userUsernameDuplicate = TestDataFactory.SupplyValidUser();
        
        userOriginal.Email = "hErEisMyEmail@example.com";
        userEmailDuplicate.Email = "HereIsmyEMaiL@examPle.coM";

        userOriginal.UserName = "hereIsMyName";
        userUsernameDuplicate.UserName = "HEREIsMyName";
        
        var originalSaveResult =  await _userManager.CreateAsync(userOriginal);
        Assert.True(originalSaveResult.Succeeded, string.Join(", ", originalSaveResult.Errors.Select(e => e.Description)));
        
        var emailDuplicateSaveResult = await _userManager.CreateAsync(userEmailDuplicate);
        Assert.False(emailDuplicateSaveResult.Succeeded, string.Join(", ", emailDuplicateSaveResult.Errors.Select(e => e.Description)));
        
        var usernameDuplicateSaveResult = await _userManager.CreateAsync(userUsernameDuplicate);
        Assert.False(usernameDuplicateSaveResult.Succeeded, string.Join(", ", usernameDuplicateSaveResult.Errors.Select(e => e.Description)));
    }

    [Fact]
    public async Task CreateAsync_WithInvalidUserName_Fails()
    {
        var user = TestDataFactory.SupplyValidUser();
        user.UserName = "invalid user!";

        var result = await _userManager.CreateAsync(user);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.InvalidUserName));
        Assert.Null(await _userManager.FindByNameAsync(user.UserName));
    }

    [Fact]
    public async Task CreateAsync_WithInvalidEmail_Fails()
    {
        var user = TestDataFactory.SupplyValidUser();
        user.Email = "not-an-email";

        var result = await _userManager.CreateAsync(user);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.InvalidEmail));
        Assert.Null(await _userManager.FindByEmailAsync(user.Email));
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserExists_ReturnsUser()
    {
        var user = await CreateUserAsync();

        var found = await _userManager.FindByIdAsync(user.Id.ToString());

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal(user.UserName, found.UserName);
        Assert.Equal(user.Email, found.Email);
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        Assert.Null(await _userManager.FindByIdAsync(long.MaxValue.ToString()));
    }

    [Fact]
    public async Task FindByNameAsync_WhenUserExists_ReturnsUser()
    {
        var user = await CreateUserAsync();

        var found = await _userManager.FindByNameAsync(user.UserName!.ToLowerInvariant());

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal(user.NormalizedUserName, found.NormalizedUserName);
    }

    [Fact]
    public async Task FindByNameAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        Assert.Null(await _userManager.FindByNameAsync("missing-user@example.com"));
    }

    [Fact]
    public async Task CheckPasswordAsync_WithCorrectPassword_ReturnsTrue()
    {
        const string password = "P@ssw0rd";
        var user = await CreateUserAsync(password);
        var persisted = await ReloadUserAsync(user);

        Assert.True(await _userManager.CheckPasswordAsync(persisted, password));
    }

    [Fact]
    public async Task CheckPasswordAsync_WithIncorrectPassword_ReturnsFalse()
    {
        var user = await CreateUserAsync("P@ssw0rd");
        var persisted = await ReloadUserAsync(user);

        Assert.False(await _userManager.CheckPasswordAsync(persisted, "Wr0ngP@ssword"));
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_Succeeds()
    {
        var user = await CreateUserAsync("P@ssw0rd");

        var result = await _userManager.ChangePasswordAsync(user, "P@ssw0rd", "N3wP@ssword");

        AssertSuccess(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithIncorrectCurrentPassword_Fails()
    {
        var user = await CreateUserAsync("P@ssw0rd");

        var result = await _userManager.ChangePasswordAsync(user, "Wr0ngP@ssword", "N3wP@ssword");

        Assert.False(result.Succeeded);
        var persisted = await ReloadUserAsync(user);
        Assert.True(await _userManager.CheckPasswordAsync(persisted, "P@ssw0rd"));
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidNewPassword_Fails()
    {
        var user = await CreateUserAsync("P@ssw0rd");

        var result = await _userManager.ChangePasswordAsync(user, "P@ssw0rd", "short");

        Assert.False(result.Succeeded);
        var persisted = await ReloadUserAsync(user);
        Assert.True(await _userManager.CheckPasswordAsync(persisted, "P@ssw0rd"));
    }

    [Fact]
    public async Task ChangePasswordAsync_PersistsNewPassword()
    {
        var user = await CreateUserAsync("P@ssw0rd");
        AssertSuccess(await _userManager.ChangePasswordAsync(user, "P@ssw0rd", "N3wP@ssword"));

        var persisted = await ReloadUserAsync(user);

        Assert.True(await _userManager.CheckPasswordAsync(persisted, "N3wP@ssword"));
    }

    [Fact]
    public async Task ChangePasswordAsync_InvalidatesOldPassword()
    {
        var user = await CreateUserAsync("P@ssw0rd");
        AssertSuccess(await _userManager.ChangePasswordAsync(user, "P@ssw0rd", "N3wP@ssword"));

        var persisted = await ReloadUserAsync(user);

        Assert.False(await _userManager.CheckPasswordAsync(persisted, "P@ssw0rd"));
    }

    [Fact]
    public async Task AddPasswordAsync_WhenUserHasNoPassword_Succeeds()
    {
        var user = await CreateUserAsync();

        var result = await _userManager.AddPasswordAsync(user, "P@ssw0rd");

        AssertSuccess(result);
        var persisted = await ReloadUserAsync(user);
        Assert.True(await _userManager.HasPasswordAsync(persisted));
        Assert.True(await _userManager.CheckPasswordAsync(persisted, "P@ssw0rd"));
    }

    [Fact]
    public async Task AddPasswordAsync_WhenUserAlreadyHasPassword_Fails()
    {
        var user = await CreateUserAsync("P@ssw0rd");

        var result = await _userManager.AddPasswordAsync(user, "An0therP@ssword");

        Assert.False(result.Succeeded);
        var persisted = await ReloadUserAsync(user);
        Assert.True(await _userManager.CheckPasswordAsync(persisted, "P@ssw0rd"));
    }

    [Fact]
    public async Task RemovePasswordAsync_WhenUserHasPassword_Succeeds()
    {
        var user = await CreateUserAsync("P@ssw0rd");

        var result = await _userManager.RemovePasswordAsync(user);

        AssertSuccess(result);
        Assert.False(await _userManager.HasPasswordAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task RemovePasswordAsync_WhenUserHasNoPassword_IsIdempotent()
    {
        var user = await CreateUserAsync();

        var result = await _userManager.RemovePasswordAsync(user);

        AssertSuccess(result);
        Assert.False(await _userManager.HasPasswordAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task SetUserNameAsync_UpdatesUserName()
    {
        var user = await CreateUserAsync();

        AssertSuccess(await _userManager.SetUserNameAsync(user, "updated.user@example.com"));

        var persisted = await ReloadUserAsync(user);
        Assert.Equal("updated.user@example.com", persisted.UserName);
        Assert.Equal("UPDATED.USER@EXAMPLE.COM", persisted.NormalizedUserName);
    }

    [Fact]
    public async Task GetUserNameAsync_ReturnsUserName()
    {
        var user = await CreateUserAsync();

        Assert.Equal(user.UserName, await _userManager.GetUserNameAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task SetEmailAsync_UpdatesEmail()
    {
        var user = await CreateUserAsync();

        AssertSuccess(await _userManager.SetEmailAsync(user, "updated.email@example.com"));

        var persisted = await ReloadUserAsync(user);
        Assert.Equal("updated.email@example.com", persisted.Email);
        Assert.Equal("UPDATED.EMAIL@EXAMPLE.COM", persisted.NormalizedEmail);
    }

    [Fact]
    public async Task GetEmailAsync_ReturnsEmail()
    {
        var user = await CreateUserAsync();

        Assert.Equal(user.Email, await _userManager.GetEmailAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task SetEmailConfirmedAsync_UpdatesConfirmationState()
    {
        var user = await CreateUserAsync();
        user.EmailConfirmed = false;
        AssertSuccess(await _userManager.UpdateAsync(user));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        AssertSuccess(await _userManager.ConfirmEmailAsync(user, token));

        Assert.True((await ReloadUserAsync(user)).EmailConfirmed);
    }

    [Fact]
    public async Task GetEmailConfirmedAsync_ReturnsConfirmationState()
    {
        var user = await CreateUserAsync();

        Assert.True(await _userManager.IsEmailConfirmedAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task AddClaimAsync_PersistsClaim()
    {
        var user = await CreateUserAsync();
        var claim = new Claim("Permission", "Orders.Read");

        AssertSuccess(await _userManager.AddClaimAsync(user, claim));

        Assert.NotNull(user.Claims);
        Assert.Contains(user.Claims, item => ClaimEquals(item, claim));
        var persistedClaims = await _userManager.GetClaimsAsync(await ReloadUserAsync(user));
        Assert.Contains(persistedClaims, item => ClaimEquals(item, claim));
    }

    [Fact]
    public async Task AddClaimsAsync_PersistsMultipleClaims()
    {
        var user = await CreateUserAsync();
        var claims = new[]
        {
            new Claim("Permission", "Orders.Read"),
            new Claim("Permission", "Orders.Write"),
            new Claim("Scope", "BackOffice")
        };

        AssertSuccess(await _userManager.AddClaimsAsync(user, claims));

        var persistedClaims = await _userManager.GetClaimsAsync(await ReloadUserAsync(user));
        Assert.Equal(claims.Length, persistedClaims.Count);
        Assert.All(claims, expected =>
            Assert.Contains(persistedClaims, actual => ClaimEquals(actual, expected)));
    }

    [Fact]
    public async Task GetClaimsAsync_ReturnsUserClaims()
    {
        var user = await CreateUserAsync();
        var claim = new Claim("Department", "Engineering");
        AssertSuccess(await _userManager.AddClaimAsync(user, claim));

        var claims = await _userManager.GetClaimsAsync(await ReloadUserAsync(user));

        Assert.Single(claims);
        Assert.True(ClaimEquals(claims[0], claim));
    }

    [Fact]
    public async Task ReplaceClaimAsync_ReplacesClaim()
    {
        var user = await CreateUserAsync();
        var oldClaim = new Claim("Permission", "Orders.Read");
        var newClaim = new Claim("Permission", "Orders.Write");
        AssertSuccess(await _userManager.AddClaimAsync(user, oldClaim));

        AssertSuccess(await _userManager.ReplaceClaimAsync(user, oldClaim, newClaim));

        Assert.NotNull(user.Claims);
        Assert.DoesNotContain(user.Claims, item => ClaimEquals(item, oldClaim));
        Assert.Contains(user.Claims, item => ClaimEquals(item, newClaim));
        var persistedClaims = await _userManager.GetClaimsAsync(await ReloadUserAsync(user));
        Assert.DoesNotContain(persistedClaims, item => ClaimEquals(item, oldClaim));
        Assert.Contains(persistedClaims, item => ClaimEquals(item, newClaim));
    }

    [Fact]
    public async Task RemoveClaimAsync_RemovesClaim()
    {
        var user = await CreateUserAsync();
        var removedClaim = new Claim("Permission", "Orders.Read");
        var retainedClaim = new Claim("Permission", "Orders.Write");
        AssertSuccess(await _userManager.AddClaimsAsync(user, [removedClaim, retainedClaim]));

        AssertSuccess(await _userManager.RemoveClaimAsync(user, removedClaim));

        Assert.NotNull(user.Claims);
        Assert.DoesNotContain(user.Claims, item => ClaimEquals(item, removedClaim));
        var persistedClaims = await _userManager.GetClaimsAsync(await ReloadUserAsync(user));
        Assert.DoesNotContain(persistedClaims, item => ClaimEquals(item, removedClaim));
        Assert.Contains(persistedClaims, item => ClaimEquals(item, retainedClaim));
    }

    [Fact]
    public async Task AddLoginAsync_PersistsLogin()
    {
        var user = await CreateUserAsync();
        var login = CreateLogin("GitHub", "github-user-1");

        AssertSuccess(await _userManager.AddLoginAsync(user, login));

        Assert.NotNull(user.Logins);
        Assert.Contains(user.Logins, item => LoginEquals(item, login));
        var persistedLogins = await _userManager.GetLoginsAsync(await ReloadUserAsync(user));
        Assert.Contains(persistedLogins, item => LoginEquals(item, login));
    }

    [Fact]
    public async Task GetLoginsAsync_ReturnsUserLogins()
    {
        var user = await CreateUserAsync();
        var github = CreateLogin("GitHub", "github-user-2");
        var google = CreateLogin("Google", "google-user-2");
        AssertSuccess(await _userManager.AddLoginAsync(user, github));
        AssertSuccess(await _userManager.AddLoginAsync(user, google));

        var logins = await _userManager.GetLoginsAsync(await ReloadUserAsync(user));

        Assert.Equal(2, logins.Count);
        Assert.Contains(logins, item => LoginEquals(item, github));
        Assert.Contains(logins, item => LoginEquals(item, google));
    }

    [Fact]
    public async Task FindByLoginAsync_ReturnsCorrectUser()
    {
        var expectedUser = await CreateUserAsync();
        var otherUser = await CreateUserAsync();
        var expectedLogin = CreateLogin("GitHub", "github-target");
        AssertSuccess(await _userManager.AddLoginAsync(expectedUser, expectedLogin));
        AssertSuccess(await _userManager.AddLoginAsync(otherUser, CreateLogin("Google", "google-other")));

        var found = await _userManager.FindByLoginAsync(expectedLogin.LoginProvider, expectedLogin.ProviderKey);

        Assert.NotNull(found);
        Assert.Equal(expectedUser.Id, found.Id);
        Assert.Equal(expectedUser.UserName, found.UserName);
    }

    [Fact]
    public async Task RemoveLoginAsync_RemovesLogin()
    {
        var user = await CreateUserAsync();
        var login = CreateLogin("GitHub", "github-user-3");
        AssertSuccess(await _userManager.AddLoginAsync(user, login));

        AssertSuccess(await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey));

        Assert.NotNull(user.Logins);
        Assert.DoesNotContain(user.Logins, item => LoginEquals(item, login));
        Assert.Empty(await _userManager.GetLoginsAsync(await ReloadUserAsync(user)));
        Assert.Null(await _userManager.FindByLoginAsync(login.LoginProvider, login.ProviderKey));
    }

    [Fact]
    public async Task SetAuthenticationTokenAsync_PersistsToken()
    {
        var user = await CreateUserAsync();

        AssertSuccess(await _userManager.SetAuthenticationTokenAsync(
            user, "TestProvider", "RefreshToken", "token-value-1"));

        Assert.NotNull(user.Tokens);
        Assert.Contains(user.Tokens, token => TokenEquals(token, "TestProvider", "RefreshToken", "token-value-1"));
        var persisted = await ReloadUserAsync(user);
        Assert.Equal("token-value-1", await _userManager.GetAuthenticationTokenAsync(
            persisted, "TestProvider", "RefreshToken"));
    }

    [Fact]
    public async Task GetAuthenticationTokenAsync_ReturnsToken()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.SetAuthenticationTokenAsync(
            user, "TestProvider", "AccessToken", "token-value-2"));

        var value = await _userManager.GetAuthenticationTokenAsync(
            await ReloadUserAsync(user), "TestProvider", "AccessToken");

        Assert.Equal("token-value-2", value);
    }

    [Fact]
    public async Task SetAuthenticationTokenAsync_ReplacesExistingToken()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.SetAuthenticationTokenAsync(
            user, "TestProvider", "RefreshToken", "old-value"));

        AssertSuccess(await _userManager.SetAuthenticationTokenAsync(
            user, "TestProvider", "RefreshToken", "new-value"));

        Assert.NotNull(user.Tokens);
        Assert.Single(user.Tokens, token =>
            token.LoginProvider == "TestProvider" && token.Name == "RefreshToken");
        var persisted = await ReloadUserAsync(user);
        Assert.Equal("new-value", await _userManager.GetAuthenticationTokenAsync(
            persisted, "TestProvider", "RefreshToken"));
    }

    [Fact]
    public async Task RemoveAuthenticationTokenAsync_RemovesToken()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.SetAuthenticationTokenAsync(
            user, "TestProvider", "RefreshToken", "token-value"));

        AssertSuccess(await _userManager.RemoveAuthenticationTokenAsync(
            user, "TestProvider", "RefreshToken"));

        Assert.NotNull(user.Tokens);
        Assert.DoesNotContain(user.Tokens,
            token => token.LoginProvider == "TestProvider" && token.Name == "RefreshToken");
        Assert.Null(await _userManager.GetAuthenticationTokenAsync(
            await ReloadUserAsync(user), "TestProvider", "RefreshToken"));
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_ReturnsCorrectState()
    {
        var user = await CreateUserAsync();

        Assert.True(await _userManager.GetLockoutEnabledAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task SetLockoutEnabledAsync_UpdatesState()
    {
        var user = await CreateUserAsync();

        AssertSuccess(await _userManager.SetLockoutEnabledAsync(user, false));

        Assert.False(await _userManager.GetLockoutEnabledAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task GetAccessFailedCountAsync_ReturnsCurrentCount()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.AccessFailedAsync(user));
        AssertSuccess(await _userManager.AccessFailedAsync(user));

        Assert.Equal(2, await _userManager.GetAccessFailedCountAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task AccessFailedAsync_IncrementsAccessFailedCount()
    {
        var user = await CreateUserAsync();

        AssertSuccess(await _userManager.AccessFailedAsync(user));

        Assert.Equal(1, user.AccessFailedCount);
        Assert.Equal(1, await _userManager.GetAccessFailedCountAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task ResetAccessFailedCountAsync_ResetsCount()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.AccessFailedAsync(user));
        AssertSuccess(await _userManager.AccessFailedAsync(user));

        AssertSuccess(await _userManager.ResetAccessFailedCountAsync(user));

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Equal(0, await _userManager.GetAccessFailedCountAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task SetLockoutEndDateAsync_SetsLockoutEnd()
    {
        var user = await CreateUserAsync();
        var expected = DateTimeOffset.UtcNow.AddMinutes(15);

        AssertSuccess(await _userManager.SetLockoutEndDateAsync(user, expected));

        AssertSameInstant(expected, (await ReloadUserAsync(user)).LockoutEnd);
    }

    [Fact]
    public async Task GetLockoutEndDateAsync_ReturnsLockoutEnd()
    {
        var user = await CreateUserAsync();
        var expected = DateTimeOffset.UtcNow.AddMinutes(20);
        AssertSuccess(await _userManager.SetLockoutEndDateAsync(user, expected));

        var actual = await _userManager.GetLockoutEndDateAsync(await ReloadUserAsync(user));

        AssertSameInstant(expected, actual);
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenLockoutEndIsInFuture_ReturnsTrue()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10)));

        Assert.True(await _userManager.IsLockedOutAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenLockoutEndIsInPast_ReturnsFalse()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-10)));

        Assert.False(await _userManager.IsLockedOutAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenLockoutEndIsNull_ReturnsFalse()
    {
        var user = await CreateUserAsync();
        AssertSuccess(await _userManager.SetLockoutEndDateAsync(user, null));

        Assert.False(await _userManager.IsLockedOutAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task AccessFailedAsync_WhenThresholdReached_LocksOutUser()
    {
        var user = await CreateUserAsync();
        var threshold = _userManager.Options.Lockout.MaxFailedAccessAttempts;

        for (var attempt = 0; attempt < threshold; attempt++)
            AssertSuccess(await _userManager.AccessFailedAsync(user));

        var persisted = await ReloadUserAsync(user);
        Assert.True(await _userManager.IsLockedOutAsync(persisted));
        Assert.NotNull(await _userManager.GetLockoutEndDateAsync(persisted));
        Assert.Equal(0, await _userManager.GetAccessFailedCountAsync(persisted));
    }

    [Fact]
    public async Task SuccessfulAuthentication_ResetsAccessFailedCount()
    {
        const string password = "P@ssw0rd";
        var user = await CreateUserAsync(password);
        AssertSuccess(await _userManager.AccessFailedAsync(user));
        AssertSuccess(await _userManager.AccessFailedAsync(user));

        Assert.True(await _userManager.CheckPasswordAsync(user, password));
        AssertSuccess(await _userManager.ResetAccessFailedCountAsync(user));

        Assert.Equal(0, await _userManager.GetAccessFailedCountAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task LockoutEnd_PreservesExpectedInstant()
    {
        var user = await CreateUserAsync();
        var expected = new DateTimeOffset(2032, 6, 15, 12, 30, 45, TimeSpan.FromHours(4));

        AssertSuccess(await _userManager.SetLockoutEndDateAsync(user, expected));

        var actual = await _userManager.GetLockoutEndDateAsync(await ReloadUserAsync(user));
        AssertSameInstant(expected, actual);
    }

    [Fact]
    public async Task GetSecurityStampAsync_ReturnsSecurityStamp()
    {
        var user = await CreateUserAsync();
        var persisted = await ReloadUserAsync(user);

        Assert.Equal(persisted.SecurityStamp, await _userManager.GetSecurityStampAsync(persisted));
    }

    [Fact]
    public async Task UpdateSecurityStampAsync_UpdatesSecurityStamp()
    {
        var user = await CreateUserAsync();
        var originalStamp = user.SecurityStamp;

        AssertSuccess(await _userManager.UpdateSecurityStampAsync(user));

        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Equal(user.SecurityStamp,
            await _userManager.GetSecurityStampAsync(await ReloadUserAsync(user)));
    }

    [Fact]
    public async Task UpdateAsync_WithValidUser_Succeeds()
    {
        var user = await CreateUserAsync();
        user.FirstName = "Updated";
        user.LastName = "User";
        user.IsActive = false;

        AssertSuccess(await _userManager.UpdateAsync(user));

        var persisted = await ReloadUserAsync(user);
        Assert.Equal("Updated", persisted.FirstName);
        Assert.Equal("User", persisted.LastName);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleConcurrencyStamp_Fails()
    {
        var user = await CreateUserAsync();
        var firstCopy = await ReloadUserAsync(user);
        var staleCopy = await ReloadUserAsync(user);
        var staleStamp = staleCopy.ConcurrencyStamp;

        firstCopy.FirstName = "First update";
        AssertSuccess(await _userManager.UpdateAsync(firstCopy));
        staleCopy.FirstName = "Stale update";

        var staleResult = await _userManager.UpdateAsync(staleCopy);

        Assert.False(staleResult.Succeeded);
        Assert.Contains(staleResult.Errors, error => error.Code == "ConcurrencyFailure");
        Assert.Equal(staleStamp, staleCopy.ConcurrencyStamp);
        Assert.Equal("First update", (await ReloadUserAsync(user)).FirstName);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserExists_Succeeds()
    {
        var user = await CreateUserAsync();

        AssertSuccess(await _userManager.DeleteAsync(user));

        Assert.Null(await _userManager.FindByIdAsync(user.Id.ToString()));
    }

    [Fact]
    public async Task DeleteAsync_WhenUserDoesNotExist_Fails()
    {
        var user = TestDataFactory.SupplyValidUser();

        var result = await _userManager.DeleteAsync(user);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
    }

    [Fact]
    public async Task DeleteAsync_RemovesExpectedRelatedData()
    {
        var user = await CreateUserAsync();
        var roleManager = _serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = TestDataFactory.SupplyValidRole();
        AssertSuccess(await roleManager.CreateAsync(role));
        var claim = new Claim("Permission", "Delete.Test");
        var login = CreateLogin("GitHub", "delete-test-login");
        AssertSuccess(await _userManager.AddClaimAsync(user, claim));
        AssertSuccess(await _userManager.AddLoginAsync(user, login));
        AssertSuccess(await _userManager.AddToRoleAsync(user, role.Name!));
        AssertSuccess(await _userManager.SetAuthenticationTokenAsync(
            user, "TestProvider", "DeleteToken", "delete-value"));

        AssertSuccess(await _userManager.DeleteAsync(user));

        Assert.Null(await _userManager.FindByIdAsync(user.Id.ToString()));
        Assert.Null(await _userManager.FindByLoginAsync(login.LoginProvider, login.ProviderKey));
        Assert.DoesNotContain(await _userManager.GetUsersForClaimAsync(claim), item => item.Id == user.Id);
        Assert.DoesNotContain(await _userManager.GetUsersInRoleAsync(role.Name!), item => item.Id == user.Id);
        var detachedProbe = new ApplicationUser { Id = user.Id };
        Assert.Null(await _userManager.GetAuthenticationTokenAsync(
            detachedProbe, "TestProvider", "DeleteToken"));
    }

    private async Task<ApplicationUser> CreateUserAsync(string? password = null)
    {
        var user = TestDataFactory.SupplyValidUser();
        var result = password is null
            ? await _userManager.CreateAsync(user)
            : await _userManager.CreateAsync(user, password);
        AssertSuccess(result);
        return user;
    }

    private async Task<ApplicationUser> ReloadUserAsync(ApplicationUser user)
    {
        var persisted = await _userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(persisted);
        return persisted;
    }

    private static UserLoginInfo CreateLogin(string provider, string key)
        => new(provider, key, $"{provider} display name");

    private static bool ClaimEquals(Claim actual, Claim expected)
        => actual.Type == expected.Type && actual.Value == expected.Value;

    private static bool ClaimEquals(IdentityUserClaim<long> actual, Claim expected)
        => actual.ClaimType == expected.Type && actual.ClaimValue == expected.Value;

    private static bool LoginEquals(UserLoginInfo actual, UserLoginInfo expected)
        => actual.LoginProvider == expected.LoginProvider && actual.ProviderKey == expected.ProviderKey;

    private static bool LoginEquals(IdentityUserLogin<long> actual, UserLoginInfo expected)
        => actual.LoginProvider == expected.LoginProvider && actual.ProviderKey == expected.ProviderKey;

    private static bool TokenEquals(IdentityUserToken<long> actual, string provider, string name, string value)
        => actual.LoginProvider == provider && actual.Name == name && actual.Value == value;

    private static void AssertSameInstant(DateTimeOffset expected, DateTimeOffset? actual)
    {
        Assert.NotNull(actual);
        Assert.InRange(Math.Abs((expected.UtcDateTime - actual.Value.UtcDateTime).TotalMilliseconds), 0, 1);
    }

    private static void AssertSuccess(IdentityResult result)
        => Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

    public sealed class TestEmailTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public const string ProviderName = "TestEmailConfirmation";
        private const string Token = "valid-email-confirmation-token";

        public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager,
            ApplicationUser user) => Task.FromResult(Token);

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager,
            ApplicationUser user) => Task.FromResult(token == Token);

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager,
            ApplicationUser user) => Task.FromResult(false);
    }
}