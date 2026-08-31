using Microsoft.AspNetCore.Identity;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models
{
    /// <summary>
    /// A user-role link enriched with role names so a loaded relationship collection
    /// can service Identity role reads without another database round trip.
    /// </summary>
    public class ApplicationUserRole : IdentityUserRole<long>
    {
        public string? RoleName { get; set; }
        public string? NormalizedRoleName { get; set; }
    }
}

