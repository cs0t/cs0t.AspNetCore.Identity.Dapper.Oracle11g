namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Infrastructure;

public static class TestDataFactory
{
    private static int _callNumber;

    public static ApplicationUser SupplyValidUser()
    {
        int newVal = Interlocked.Increment(ref _callNumber);
        return new()
        {
            Id = newVal,
            UserName = $"testuser{newVal}@example.com",
            NormalizedUserName = $"TESTUSER{newVal}@EXAMPLE.COM",
            Email = $"testuser{newVal}@example.com",
            NormalizedEmail = $"TESTUSER{newVal}@EXAMPLE.COM",
            EmailConfirmed = true,
            PhoneNumber = $"+{newVal}234567890",
            PhoneNumberConfirmed = true,
            TwoFactorEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            AccessFailedCount = 0
        };
    }


    public static ApplicationRole SupplyValidRole()
    {
        int newVal = Interlocked.Increment(ref _callNumber);
        return new()
        {
            Id = newVal,
            Name = $"Administrator{newVal}",
            NormalizedName = $"Administrator{newVal}".ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
    }
       
}