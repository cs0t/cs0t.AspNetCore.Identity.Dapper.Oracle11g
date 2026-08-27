using System.Collections.Generic;
using System.Security.Claims;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int UserType { get; set; } 
        public bool IsActive { get; set; } = true;

        internal List<Claim> Claims { get; set; }
        internal List<UserRole> Roles { get; set; }
        internal List<UserLoginInfo> Logins { get; set; }
        internal List<UserToken> Tokens { get; set; }
    }
}
