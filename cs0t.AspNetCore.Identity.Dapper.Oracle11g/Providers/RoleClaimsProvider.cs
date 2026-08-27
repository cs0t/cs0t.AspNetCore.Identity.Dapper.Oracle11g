using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class RoleClaimsProvider
    {
        private readonly IDatabaseConnectionFactory _databaseConnectionFactory;

        public RoleClaimsProvider(IDatabaseConnectionFactory databaseConnectionFactory)
        {
            _databaseConnectionFactory = databaseConnectionFactory;
        }

        public async Task<IList<Claim>> GetClaimsAsync(string roleId)
        {
            var command = "SELECT * " +
                                   $"FROM [{_databaseConnectionFactory.DbSchema}].[AspNetRoleClaims] " +
                                   "WHERE RoleId = @RoleId;";

            IEnumerable<RoleClaim> roleClaims = new List<RoleClaim>();

            await using var sqlConnection = await _databaseConnectionFactory.CreateConnectionAsync();
            return (
                    await sqlConnection.QueryAsync<RoleClaim>(command, new { RoleId = roleId })
                )
                .Select(x => new Claim(x.ClaimType, x.ClaimValue))
                .ToList();
        }
    }
}
