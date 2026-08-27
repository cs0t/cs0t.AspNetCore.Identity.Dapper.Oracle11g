namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    public class DBProviderOptions
    {
        public string DbSchema { get; set; } = "dbo";

        public string ConnectionString { get; set; }
    }
}
