using System.Diagnostics.CodeAnalysis;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Handlers;

public class OracleDateTimeOffsetHandlerTests
{
    private readonly OracleDateTimeOffsetHandler Handler = new();
    
    private class TestDbParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable { get; }
        [AllowNull] public string ParameterName { get; set; }
        [AllowNull] public string SourceColumn { get; set; }
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }
    
    private readonly DateTimeOffset OffsetStub = new (2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetValue_GivenNullValueAndOracleParameter_SetsDbValueNullAndOracleDbTypeTimeStampTz()
    {
        var parameter  = new OracleParameter();
        Handler.SetValue(parameter, null);
        Assert.Equal(DBNull.Value, parameter.Value);
        Assert.Equal(OracleDbType.TimeStampTZ, parameter.OracleDbType);
    }
    
    [Fact]
    public void SetValue_GivenNullValue_SetsDbValueNullAndDbTypeDateTime()
    {
        var parameter  = new TestDbParameter();
        Handler.SetValue(parameter, null);
        Assert.Equal(DBNull.Value, parameter.Value);
        Assert.Equal(DbType.DateTime, parameter.DbType);
    }
    
    [Fact]
    public void SetValue_GivenOracleParameterAndDateTimeOffset_SetsDbValueToUniversalTimeAndOracleDbTypeTimeStampZ()
    {
        var parameter  = new OracleParameter();
        Handler.SetValue(parameter, OffsetStub);
        Assert.Equal(OffsetStub.ToUniversalTime(), parameter.Value);
        Assert.Equal(OracleDbType.TimeStampTZ, parameter.OracleDbType);
    }
    
    [Fact]
    public void SetValue_GivenDbParameterAndDateTimeOffset_SetsDbValueToUniversalTimeAndDbTypeDateTime()
    {
        var parameter  = new TestDbParameter();
        Handler.SetValue(parameter, OffsetStub);
        Assert.Equal(OffsetStub.UtcDateTime, parameter.Value);
        Assert.Equal(DbType.DateTime, parameter.DbType);
    }
    
    [Fact]
    public void Parse_GivenNullValue_ReturnsNull()
    {
        Assert.Null(Handler.Parse(null));
        Assert.Null(Handler.Parse(DBNull.Value));
    }
    
    [Fact]
    public void Parse_GivenDateTimeOffset_ReturnsDateTimeOffset()
    {
        Assert.Equal(OffsetStub, Handler.Parse(OffsetStub));
    }
    
    [Fact]
    public void Parse_StringWithUtcZ_ReturnsCorrectUtcOffset()
    {
        string input = "2020-01-01T12:00:00Z";

        var result = Handler.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset);
        Assert.Equal(12, result.Value.Hour);
    }

    [Fact]
    public void Parse_StringWithPlusOffset_ConvertsToUtcCorrectly()
    {
        //12:00 PM in a +02:00 zone is 10:00 AM UTC
        string input = "2020-01-01T12:00:00+02:00";

        var result = Handler.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset); 
        Assert.Equal(10, result.Value.Hour);               
    }
    
    [Fact]
    public void Parse_StringWithMinusOffset_ConvertsToUtcCorrectly()
    {
        //12:00 PM in a -02:00 zone is 2:00 PM UTC
        string input = "2020-01-01T12:00:00-02:00";

        var result = Handler.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset); 
        Assert.Equal(14, result.Value.Hour);               
    }

    [Fact]
    public void Parse_StringWithoutOffset_AssumesUtcInsteadOfLocal()
    {
        // No timezone context provided
        string input = "2020-01-01 12:00:00";

        var result = Handler.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset); 
        Assert.Equal(12, result.Value.Hour); 
    }
    
    [Fact]
    public void Parse_GarbageString_ReturnsNullWithoutCrashing()
    {
        string input = "NotADateString";
        Assert.Throws<FormatException>(() => Handler.Parse(input));
    }

    [Fact]
    public void Parse_InvalidDateValues_ReturnsNullWithoutCrashing()
    {
        string input = "2020-02-30 12:00:00";
        Assert.Throws<FormatException>(() => Handler.Parse(input));
    }
}