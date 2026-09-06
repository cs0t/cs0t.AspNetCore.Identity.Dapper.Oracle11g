namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Infrastructure;

[Collection(nameof(OracleDatabaseCollectionFixture))]
public class OracleInfrastructureTests(OracleDockerFixture fixture)
{
    private readonly TestDatabaseFactory _dbFactory = new(fixture.ConnectionString);

    [Fact]
    public async Task Oracle_Is_Accessible()
    {
        await using var connection = await _dbFactory.CreateConnectionAsync();
        
        var result = await connection.ExecuteScalarAsync<int>("SELECT 1 FROM DUAL");
        Assert.Equal(1, result);
    }
}