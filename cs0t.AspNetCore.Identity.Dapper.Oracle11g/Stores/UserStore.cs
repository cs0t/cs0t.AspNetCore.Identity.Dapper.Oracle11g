using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores
{
      public class UserStore :
        IUserPasswordStore<ApplicationUser>,
        IUserSecurityStampStore<ApplicationUser>,
        IUserEmailStore<ApplicationUser>,
        IUserPhoneNumberStore<ApplicationUser>,
        IUserTwoFactorStore<ApplicationUser>,
        IUserLockoutStore<ApplicationUser>,
        IUserClaimStore<ApplicationUser>,
        IUserLoginStore<ApplicationUser>,
        IUserRoleStore<ApplicationUser>,
        IUserAuthenticationTokenStore<ApplicationUser>
    {
        private readonly UsersProvider _usersProvider;
        private readonly UserClaimsProvider _userClaimsProvider;
        private readonly UserLoginsProvider _userLoginsProvider;
        private readonly UserRolesProvider _userRolesProvider;
        private readonly UserTokensProvider _userTokensProvider;
        private readonly RolesProvider _rolesProvider; 
 
        public UserStore(IDatabaseConnectionFactory databaseConnectionFactory)
        {
            databaseConnectionFactory.ThrowIfNull(nameof(databaseConnectionFactory));
            _usersProvider = new UsersProvider(databaseConnectionFactory);
            _userClaimsProvider = new UserClaimsProvider(databaseConnectionFactory);
            _userLoginsProvider = new UserLoginsProvider(databaseConnectionFactory);
            _userRolesProvider = new UserRolesProvider(databaseConnectionFactory);
            _userTokensProvider = new UserTokensProvider(databaseConnectionFactory);
            _rolesProvider = new RolesProvider(databaseConnectionFactory);
        }
 
        //=== IUserStore - simple property access, no DB round trip ===
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.Id.ToString());
        }
 
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.UserName);
        }
 
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.UserName = userName;
            return Task.CompletedTask;
        }
 
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.NormalizedUserName);
        }
 
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }
 
        //=== IUserStore - DB-backed, delegate to UsersProvider ===
        public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
 
            // Store owns concurrency-stamp generation - the provider just persists it.
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
 
            return await _usersProvider.CreateAsync(user, ct).ConfigureAwait(false);
        }
 
        public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
 
            var originalConcurrencyStamp = user.ConcurrencyStamp;
            originalConcurrencyStamp.ThrowIfNull(nameof(originalConcurrencyStamp));
            
            user.ConcurrencyStamp = Guid.NewGuid().ToString();

            try
            {
                var result = await _usersProvider.UpdateAsync(user, originalConcurrencyStamp!, ct).ConfigureAwait(false);
                if (!result.Succeeded)
                    user.ConcurrencyStamp = originalConcurrencyStamp;

                return result;
            }
            catch
            {
                user.ConcurrencyStamp = originalConcurrencyStamp;
                throw;
            }
        }
 
        public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return await _usersProvider.DeleteAsync(user, ct).ConfigureAwait(false);
        }
 
        public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct)
        {
            if (!long.TryParse(userId, out var id)) return null;
            return await _usersProvider.FindByIdAsync(id, ct).ConfigureAwait(false);
        }
 
        public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
            => await _usersProvider.FindByNameAsync(normalizedUserName, ct).ConfigureAwait(false);
 
        //=== IUserPasswordStore - in-memory only, persisted on the next UpdateAsync ===
        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }
 
        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.PasswordHash);
        }
 
        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        }
 
        //=== IUserSecurityStampStore - in-memory only ===
        // UserManager regenerates and sets this internally (e.g. after a password change) and
        // then calls UpdateAsync once - that single call persists the new SecurityStamp AND a
        // fresh ConcurrencyStamp together, atomically, with a proper concurrency check. Writing
        // the stamp straight to the DB from here would bypass that check entirely.
        public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            stamp.ThrowIfNull(nameof(stamp));
            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }
 
        public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.SecurityStamp);
        }
 
        //=== IUserEmailStore - in-memory except the lookup ===
        public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.Email = email;
            return Task.CompletedTask;
        }
 
        public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.Email);
        }
 
        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.EmailConfirmed);
        }
 
        public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }
 
        public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
            => await _usersProvider.FindByEmailAsync(normalizedEmail, ct).ConfigureAwait(false);
 
        public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.NormalizedEmail);
        }
 
        public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }
 
        //=== IUserPhoneNumberStore - in-memory only ===
        public Task SetPhoneNumberAsync(ApplicationUser user, string? phoneNumber, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.PhoneNumber = phoneNumber;
            return Task.CompletedTask;
        }
 
        public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.PhoneNumber);
        }
 
        public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.PhoneNumberConfirmed);
        }
 
        public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.PhoneNumberConfirmed = confirmed;
            return Task.CompletedTask;
        }
 
        //=== IUserTwoFactorStore - in-memory only ===
        public Task SetTwoFactorEnabledAsync(ApplicationUser user, bool enabled, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.TwoFactorEnabled = enabled;
            return Task.CompletedTask;
        }
 
        public Task<bool> GetTwoFactorEnabledAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.TwoFactorEnabled);
        }
 
        //=== IUserLockoutStore - in-memory only ===
        public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.LockoutEnd);
        }
 
        public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }
 
        public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.AccessFailedCount++;
            return Task.FromResult(user.AccessFailedCount);
        }
 
        public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }
 
        public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.AccessFailedCount);
        }
 
        public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            return Task.FromResult(user.LockoutEnabled);
        }
 
        public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            user.LockoutEnabled = enabled;
            return Task.CompletedTask;
        }
 
        //=== Relationship stores - lazy read, targeted write-through, then memory synchronization ===
        public async Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken ct)
        {
            var claims = await EnsureClaimsLoadedAsync(user, ct).ConfigureAwait(false);
            return claims.Select(x => new Claim(x.ClaimType!, x.ClaimValue!)).ToList();
        }

        public async Task AddClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            claims.ThrowIfNull(nameof(claims));

            var loaded = await EnsureClaimsLoadedAsync(user, ct).ConfigureAwait(false);
            var additions = claims
                .Where(x => !loaded.Any(y => ClaimEquals(y, x)))
                .GroupBy(x => new { x.Type, x.Value })
                .Select(x => x.First())
                .ToList();

            if (additions.Count == 0) return;
            await _userClaimsProvider.AddClaimsAsync(user, additions, ct).ConfigureAwait(false);
            loaded.AddRange(additions.Select(x => new IdentityUserClaim<long>
            {
                UserId = user.Id,
                ClaimType = x.Type,
                ClaimValue = x.Value
            }));
        }

        public async Task ReplaceClaimAsync(ApplicationUser user, Claim claim, Claim newClaim, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            claim.ThrowIfNull(nameof(claim));
            newClaim.ThrowIfNull(nameof(newClaim));

            var loaded = await EnsureClaimsLoadedAsync(user, ct).ConfigureAwait(false);
            var matches = loaded.Where(x => ClaimEquals(x, claim)).ToList();
            if (matches.Count == 0 || (claim.Type == newClaim.Type && claim.Value == newClaim.Value)) return;

            if (loaded.Any(x => ClaimEquals(x, newClaim)))
            {
                await _userClaimsProvider.RemoveClaimAsync(user, claim, ct).ConfigureAwait(false);
                loaded.RemoveAll(x => ClaimEquals(x, claim));
                return;
            }

            await _userClaimsProvider.ReplaceClaimAsync(user, claim, newClaim, ct).ConfigureAwait(false);
            foreach (var item in matches)
            {
                item.ClaimType = newClaim.Type;
                item.ClaimValue = newClaim.Value;
            }
        }

        public async Task RemoveClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            claims.ThrowIfNull(nameof(claims));

            var loaded = await EnsureClaimsLoadedAsync(user, ct).ConfigureAwait(false);
            var removals = claims
                .Where(x => loaded.Any(y => ClaimEquals(y, x)))
                .GroupBy(x => new { x.Type, x.Value })
                .Select(x => x.First())
                .ToList();

            if (removals.Count == 0) return;
            await _userClaimsProvider.RemoveClaimsAsync(user, removals, ct).ConfigureAwait(false);
            loaded.RemoveAll(x => removals.Any(y => ClaimEquals(x, y)));
        }
 
        public async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken ct)
            => await _userClaimsProvider.GetUsersByClaimAsync(claim, ct).ConfigureAwait(false);
 
        public async Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            login.ThrowIfNull(nameof(login));
            var loaded = await EnsureLoginsLoadedAsync(user, ct).ConfigureAwait(false);
            if (loaded.Any(x => x.LoginProvider == login.LoginProvider && x.ProviderKey == login.ProviderKey)) return;

            await _userLoginsProvider.AddLoginAsync(user, login, ct).ConfigureAwait(false);
            loaded.Add(new IdentityUserLogin<long>
            {
                UserId = user.Id,
                LoginProvider = login.LoginProvider,
                ProviderKey = login.ProviderKey,
                ProviderDisplayName = login.ProviderDisplayName
            });
        }

        public async Task RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            var loaded = await EnsureLoginsLoadedAsync(user, ct).ConfigureAwait(false);
            if (!loaded.Any(x => x.LoginProvider == loginProvider && x.ProviderKey == providerKey)) return;

            await _userLoginsProvider.RemoveLoginAsync(user, loginProvider, providerKey, ct).ConfigureAwait(false);
            loaded.RemoveAll(x => x.LoginProvider == loginProvider && x.ProviderKey == providerKey);
        }

        public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken ct)
        {
            var loaded = await EnsureLoginsLoadedAsync(user, ct).ConfigureAwait(false);
            return loaded.Select(x => new UserLoginInfo(x.LoginProvider!, x.ProviderKey!, x.ProviderDisplayName)).ToList();
        }
 
        public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken ct)
            => await _userLoginsProvider.FindByLoginAsync(loginProvider, providerKey, ct).ConfigureAwait(false);
 
        //=== IUserRoleStore - roleName here arrives already normalized by UserManager ===
        public async Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            var loaded = await EnsureRolesLoadedAsync(user, ct).ConfigureAwait(false);
            if (loaded.Any(x => string.Equals(x.NormalizedRoleName, roleName, StringComparison.OrdinalIgnoreCase))) return;

            var role = await _rolesProvider.FindByNameAsync(roleName, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

            await _userRolesProvider.AddToRoleAsync(user, role.Id, ct).ConfigureAwait(false);
            loaded.Add(new ApplicationUserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                RoleName = role.Name,
                NormalizedRoleName = role.NormalizedName
            });
        }
 
        public async Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            var loaded = await EnsureRolesLoadedAsync(user, ct).ConfigureAwait(false);
            var role = loaded.FirstOrDefault(x => string.Equals(x.NormalizedRoleName, roleName, StringComparison.OrdinalIgnoreCase));
            if (role is null) return;

            await _userRolesProvider.RemoveFromRoleAsync(user, role.RoleId, ct).ConfigureAwait(false);
            loaded.RemoveAll(x => x.RoleId == role.RoleId);
        }
 
        public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
 
            var roles = await EnsureRolesLoadedAsync(user, ct).ConfigureAwait(false);
            return roles.Select(r => r.RoleName!).ToList();
        }
 
        public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
 
            var roles = await EnsureRolesLoadedAsync(user, ct).ConfigureAwait(false);
            return roles.Any(x => string.Equals(x.NormalizedRoleName, roleName, StringComparison.OrdinalIgnoreCase));
        }
 
        public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
            => await _userRolesProvider.GetUsersInRoleAsync(roleName, ct).ConfigureAwait(false);
 
        //=== IUserAuthenticationTokenStore - each of these persists immediately via UserTokensProvider ===
        public async Task SetTokenAsync(ApplicationUser user, string loginProvider, string name, string? value, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            var loaded = await EnsureTokensLoadedAsync(user, ct).ConfigureAwait(false);
            var token = loaded.FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);
            var persistedToken = new IdentityUserToken<long>
            {
                UserId = user.Id,
                LoginProvider = loginProvider,
                Name = name,
                Value = value
            };

            await _userTokensProvider.ReplaceTokenAsync(persistedToken, ct).ConfigureAwait(false);
            if (token is null)
                loaded.Add(persistedToken);
            else
                token.Value = value;
        }
 
        public async Task RemoveTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            var loaded = await EnsureTokensLoadedAsync(user, ct).ConfigureAwait(false);
            if (!loaded.Any(x => x.LoginProvider == loginProvider && x.Name == name)) return;

            await _userTokensProvider.DeleteTokenAsync(user.Id, loginProvider, name, ct).ConfigureAwait(false);
            loaded.RemoveAll(x => x.LoginProvider == loginProvider && x.Name == name);
        }
 
        public async Task<string?> GetTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            var loaded = await EnsureTokensLoadedAsync(user, ct).ConfigureAwait(false);
            return loaded.FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name)?.Value;
        }

        private async Task<List<IdentityUserClaim<long>>> EnsureClaimsLoadedAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            if (user.Claims is not null) return user.Claims;

            var claims = await _userClaimsProvider.GetClaimsAsync(user, ct).ConfigureAwait(false);
            user.Claims = claims.Select(x => new IdentityUserClaim<long>
            {
                UserId = user.Id,
                ClaimType = x.Type,
                ClaimValue = x.Value
            }).ToList();
            return user.Claims;
        }

        private async Task<List<ApplicationUserRole>> EnsureRolesLoadedAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            if (user.Roles is not null) return user.Roles;
            user.Roles = await _userRolesProvider.GetRolesAsync(user, ct).ConfigureAwait(false);
            return user.Roles;
        }

        private async Task<List<IdentityUserLogin<long>>> EnsureLoginsLoadedAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            if (user.Logins is not null) return user.Logins;

            var logins = await _userLoginsProvider.GetLoginsAsync(user, ct).ConfigureAwait(false);
            user.Logins = logins.Select(x => new IdentityUserLogin<long>
            {
                UserId = user.Id,
                LoginProvider = x.LoginProvider,
                ProviderKey = x.ProviderKey,
                ProviderDisplayName = x.ProviderDisplayName
            }).ToList();
            return user.Logins;
        }

        private async Task<List<IdentityUserToken<long>>> EnsureTokensLoadedAsync(ApplicationUser user, CancellationToken ct)
        {
            user.ThrowIfNull(nameof(user));
            if (user.Tokens is not null) return user.Tokens;
            user.Tokens = (await _userTokensProvider.GetTokensAsync(user.Id, ct).ConfigureAwait(false)).ToList();
            return user.Tokens;
        }

        private static bool ClaimEquals(IdentityUserClaim<long> item, Claim claim)
            => item.ClaimType == claim.Type && item.ClaimValue == claim.Value;
 
        public void Dispose()
        {
           
        }
    }
}
