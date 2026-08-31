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
     public class RoleStore : IRoleClaimStore<ApplicationRole>
    {
        private readonly RolesProvider _rolesProvider;
        private readonly RoleClaimsProvider _roleClaimsProvider;
 
        public RoleStore(IDatabaseConnectionFactory databaseConnectionFactory)
        {
            databaseConnectionFactory.ThrowIfNull(nameof(databaseConnectionFactory));
            _rolesProvider = new RolesProvider(databaseConnectionFactory);
            _roleClaimsProvider = new RoleClaimsProvider(databaseConnectionFactory);
        }
 
        //=== IRoleStore - simple property access, no DB round trip ===
        public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            role.ThrowIfNull(nameof(role));
            return Task.FromResult(role.Id.ToString());
        }
 
        public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            role.ThrowIfNull(nameof(role));
            return Task.FromResult(role.Name);
        }
 
        public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken)
        {
            role.ThrowIfNull(nameof(role));
            role.Name = roleName;
            return Task.CompletedTask;
        }
 
        public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            role.ThrowIfNull(nameof(role));
            return Task.FromResult(role.NormalizedName);
        }
 
        public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            role.ThrowIfNull(nameof(role));
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }
 
        //=== IRoleStore - DB-backed, delegate to RolesProvider ===
        public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ThrowIfNull(nameof(role));
 
            //create fresh concurrency token
            role.ConcurrencyStamp = Guid.NewGuid().ToString();
 
            return await _rolesProvider.CreateAsync(role, cancellationToken).ConfigureAwait(false);
        }
 
        public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ThrowIfNull(nameof(role));
 
            var originalConcurrencyStamp = role.ConcurrencyStamp;
            
            originalConcurrencyStamp.ThrowIfNull(nameof(originalConcurrencyStamp));
            role.ConcurrencyStamp = Guid.NewGuid().ToString();

            try
            {
                var result = await _rolesProvider.UpdateAsync(role, originalConcurrencyStamp!, cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded)
                    role.ConcurrencyStamp = originalConcurrencyStamp;

                return result;
            }
            catch
            {
                role.ConcurrencyStamp = originalConcurrencyStamp;
                throw;
            }
        }
 
        public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ThrowIfNull(nameof(role));
            return await _rolesProvider.DeleteAsync(role, cancellationToken).ConfigureAwait(false);
        }
 
        public async Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!long.TryParse(roleId, out var id)) return null;
            return await _rolesProvider.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
 
        public async Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
            => await _rolesProvider.FindByNameAsync(normalizedRoleName, cancellationToken).ConfigureAwait(false);
 
        //=== IRoleClaimStore - lazy read, targeted write-through, then memory synchronization ===
        public async Task<IList<Claim>> GetClaimsAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            var claims = await EnsureClaimsLoadedAsync(role, cancellationToken).ConfigureAwait(false);
            return claims.Select(x => new Claim(x.ClaimType!, x.ClaimValue!)).ToList();
        }
 
        public async Task AddClaimAsync(ApplicationRole role, Claim claim, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ThrowIfNull(nameof(role));
            claim.ThrowIfNull(nameof(claim));
            var claims = await EnsureClaimsLoadedAsync(role, cancellationToken).ConfigureAwait(false);
            if (claims.Any(x => ClaimEquals(x, claim))) return;

            await _roleClaimsProvider.AddClaimAsync(role.Id, claim, cancellationToken).ConfigureAwait(false);
            claims.Add(new IdentityRoleClaim<long>
            {
                RoleId = role.Id,
                ClaimType = claim.Type,
                ClaimValue = claim.Value
            });
        }
 
        public async Task RemoveClaimAsync(ApplicationRole role, Claim claim, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ThrowIfNull(nameof(role));
            claim.ThrowIfNull(nameof(claim));
            var claims = await EnsureClaimsLoadedAsync(role, cancellationToken).ConfigureAwait(false);
            if (!claims.Any(x => ClaimEquals(x, claim))) return;

            await _roleClaimsProvider.RemoveClaimAsync(role.Id, claim, cancellationToken).ConfigureAwait(false);
            claims.RemoveAll(x => ClaimEquals(x, claim));
        }
 
        public async Task<IEnumerable<ApplicationRole>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _rolesProvider.GetAllRolesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<IdentityRoleClaim<long>>> EnsureClaimsLoadedAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.ThrowIfNull(nameof(role));
            if (role.Claims is not null) return role.Claims;

            var claims = await _roleClaimsProvider.GetClaimsAsync(role.Id, cancellationToken).ConfigureAwait(false);
            role.Claims = claims.Select(x => new IdentityRoleClaim<long>
            {
                RoleId = role.Id,
                ClaimType = x.Type,
                ClaimValue = x.Value
            }).ToList();
            return role.Claims;
        }

        private static bool ClaimEquals(IdentityRoleClaim<long> item, Claim claim)
            => item.ClaimType == claim.Type && item.ClaimValue == claim.Value;
 
        public void Dispose()
        {
            
        }
    }
}
