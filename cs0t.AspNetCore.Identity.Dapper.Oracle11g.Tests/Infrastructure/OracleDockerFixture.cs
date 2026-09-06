using DotNet.Testcontainers.Builders;
using Testcontainers.Oracle;
using OracleConfiguration = Oracle.ManagedDataAccess.Client.OracleConfiguration;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;


namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Infrastructure;

public sealed class OracleDockerFixture : IAsyncLifetime
{
    private readonly OracleContainer _container = 
        new OracleBuilder("gvenzl/oracle-xe:11.2.0.2")
        .WithPassword("P@ssw0rd")
        .WithWaitStrategy(
            Wait
                .ForUnixContainer()
                .UntilMessageIsLogged("DATABASE IS READY TO USE!", waitStrategyModifier: config =>
                    {
                        config.WithTimeout(TimeSpan.FromMinutes(5));
                    })
                )
        .Build();
    
    public string ConnectionString { get; private set; } = string.Empty;

    static OracleDockerFixture()
    {
        OracleConfiguration.BindByName = true;
        OracleConfiguration.SqlNetAllowedLogonVersionClient = OracleAllowedLogonVersionClient.Version11;
        
        SqlMapper.AddTypeHandler(typeof(DateTimeOffset), new OracleDateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(typeof(DateTimeOffset?), new OracleDateTimeOffsetHandler());

        SqlMapper.AddTypeHandler(typeof(Guid), new OracleGuidHandler());
        SqlMapper.AddTypeHandler(typeof(Guid?), new OracleGuidHandler());

        SqlMapper.AddTypeHandler(typeof(bool), new OracleBoolHandler());
        SqlMapper.AddTypeHandler(typeof(bool?), new OracleBoolHandler());
    }
    
    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        await InitDbSchema().ConfigureAwait(false);
        
        var builder = new OracleConnectionStringBuilder(_container.GetConnectionString())
        {
            UserID = "APP_USER",
            Password = "P@ssw0rd" 
        };
        
        ConnectionString = builder.ConnectionString;
    }

    private async Task InitDbSchema()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var schemaPath = Path.Combine(projectRoot, "Scripts", "schema.sql");        
        
        if(!File.Exists(schemaPath))
            throw new FileNotFoundException($"Schema file not found at path: {schemaPath}");
        
        var sqlScript = await File.ReadAllTextAsync(schemaPath).ConfigureAwait(false);
        
        var sqlCommands = sqlScript.Split(';',StringSplitOptions.RemoveEmptyEntries);
        
        var adminBuilder = new OracleConnectionStringBuilder(_container.GetConnectionString())
        {
            UserID = "SYSTEM",
            Password = "P@ssw0rd" 
        };
        
        await using var connection = new OracleConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        
        foreach (var command in sqlCommands)
        {
            var commandTrimmed = command.Trim();
            if(string.IsNullOrWhiteSpace(commandTrimmed))
                continue;
            
            if (commandTrimmed.EndsWith(';'))
            {
                commandTrimmed = commandTrimmed.TrimEnd(';').Trim();
            }
            
            await connection.ExecuteAsync(commandTrimmed).ConfigureAwait(false);
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}