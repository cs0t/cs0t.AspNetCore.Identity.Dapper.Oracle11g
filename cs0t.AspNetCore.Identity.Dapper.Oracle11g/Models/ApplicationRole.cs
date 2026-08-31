using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models
{
    public class ApplicationRole : IdentityRole<long>
    {
        public List<IdentityRoleClaim<long>>? Claims { get; set; } 
    }
}
