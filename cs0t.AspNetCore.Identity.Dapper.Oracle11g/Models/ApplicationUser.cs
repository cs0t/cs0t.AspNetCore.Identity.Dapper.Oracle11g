using System;
using System.Collections.Generic;
using System.Security.Claims;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models
{
    public class ApplicationUser : IdentityUser<long>
    {
        public string FirstName { get; set; } =  string.Empty;
        public string LastName { get; set; } =  string.Empty;
        public bool IsActive { get; set; } 
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastLoggedInAtUtc { get; set; }
        public DateTime? PasswordChangedAtUtc { get; set; }

        public List<IdentityUserClaim<long>> Claims { get; set; } = [];
        public List<IdentityUserRole<long>> Roles { get; set; } = [];
        public List<IdentityUserLogin<long>> Logins { get; set; } = [];
        public List<IdentityUserToken<long>> Tokens { get; set; } = [];
    }
}
