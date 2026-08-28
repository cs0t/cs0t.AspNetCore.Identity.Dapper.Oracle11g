namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    public class DbProviderOptions
    {
        public string DbSchema { get; set; } = null!;

        public string ConnectionString { get; set; } = null!;
        
        public string UsersTableName { get; set; } = "ASPNET_USERS";
        public string RolesTableName { get; set; } =  "ASPNET_ROLES";
        public string UserRolesTableName { get; set; }  = "ASPNET_USER_ROLES";
        public string UserClaimsTable { get; set; } = "ASPNET_USER_CLAIMS";
        public string UserRoleClaimsTable { get; set; } =  "ASPNET_USER_ROLE_CLAIMS";
        public string UserLoginsTableName { get; set; } =  "ASPNET_USER_LOGINS";
        public string UserTokensTableName { get; set; } =   "ASPNET_USER_TOKENS";
    }
}
