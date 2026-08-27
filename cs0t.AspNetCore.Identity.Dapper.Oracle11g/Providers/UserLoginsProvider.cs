using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UserLoginsProvider
    {
        private readonly IDatabaseConnectionFactory _databaseConnectionFactory;

        public UserLoginsProvider(IDatabaseConnectionFactory databaseConnectionFactory)
        {
            _databaseConnectionFactory = databaseConnectionFactory;
        }

        public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user) {
            var command = "SELECT * " +
                                   $"FROM [{_databaseConnectionFactory.DbSchema}].[AspNetUserLogins] " +
                                   "WHERE UserId = @UserId;";

            await using var sqlConnection = await _databaseConnectionFactory.CreateConnectionAsync();
            return (
                    await sqlConnection.QueryAsync<UserLogin>(command, new { UserId = user.Id })
                )
                .Select(x => new UserLoginInfo(x.LoginProvider, x.ProviderKey, x.ProviderDisplayName))
                .ToList();
        }

        public async Task<ApplicationUser> FindByLoginAsync(string loginProvider, string providerKey) {
            string[] command =
            {
                "SELECT UserId " +
                $"FROM [{_databaseConnectionFactory.DbSchema}].[AspNetUserLogins] " +
                "WHERE LoginProvider = @LoginProvider AND ProviderKey = @ProviderKey;"
            };

            await using var sqlConnection = await _databaseConnectionFactory.CreateConnectionAsync();
            var userId = await sqlConnection.QuerySingleOrDefaultAsync<Guid?>(command[0], new {
                LoginProvider = loginProvider,
                ProviderKey = providerKey
            });

            if (userId == null) {
                return null;
            }

            command[0] = "SELECT * " +
                         $"FROM [{_databaseConnectionFactory.DbSchema}].[AspNetUsers] " +
                         "WHERE Id = @Id;";

            return await sqlConnection.QuerySingleAsync<ApplicationUser>(command[0], new { Id = userId });
        }
    }
}
