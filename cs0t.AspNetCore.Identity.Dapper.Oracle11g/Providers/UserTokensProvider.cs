using System.Collections.Generic;
using System.Threading.Tasks;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Providers
{
    internal class UserTokensProvider
    {
        private readonly IDatabaseConnectionFactory _databaseConnectionFactory;

        public UserTokensProvider(IDatabaseConnectionFactory databaseConnectionFactory)
        {
            _databaseConnectionFactory = databaseConnectionFactory;
        }

        public async Task<IEnumerable<UserToken>> GetTokensAsync(string userId) {
            var command = "SELECT * " +
                                   $"FROM [{_databaseConnectionFactory.DbSchema}].[AspNetUserTokens] " +
                                   "WHERE UserId = @UserId;";

            await using var sqlConnection = await _databaseConnectionFactory.CreateConnectionAsync();
            return await sqlConnection.QueryAsync<UserToken>(command, new {
                UserId = userId
            });
        }
    }
}
