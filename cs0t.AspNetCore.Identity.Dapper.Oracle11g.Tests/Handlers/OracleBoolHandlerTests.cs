using System.Diagnostics.CodeAnalysis;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Handlers;

public class OracleBoolHandlerTests
{
    private readonly OracleBoolHandler _handler = new();

    private class TestDbParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        [AllowNull] public string ParameterName { get; set; }
        [AllowNull] public string SourceColumn { get; set; }
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    [Fact]
    public void SetValue_WhenGivenNull_SetsNullAndByteType()
    {
        var parameter = new TestDbParameter();

        _handler.SetValue(parameter, null);

        Assert.Equal(DBNull.Value, parameter.Value);
        Assert.Equal(DbType.Byte, parameter.DbType);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void SetValue_WhenGivenBoolean_SetsNumericValueAndByteType(bool value, int expectedValue)
    {
        var parameter = new TestDbParameter();

        _handler.SetValue(parameter, value);

        Assert.Equal(expectedValue, parameter.Value);
        Assert.Equal(DbType.Byte, parameter.DbType);
    }

    [Fact]
    public void Parse_WhenDbValueIsNull_ReturnsNull()
    {
        Assert.Null(_handler.Parse(null));
        Assert.Null(_handler.Parse(DBNull.Value));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(-1, false)]
    public void Parse_WhenNumericValueReturned_ReturnsExpectedBoolean(int value, bool expectedValue)
    {
        Assert.Equal(expectedValue, _handler.Parse(value));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("2", false)]
    public void Parse_WhenNumericStringReturned_ReturnsExpectedBoolean(string value, bool expectedValue)
    {
        Assert.Equal(expectedValue, _handler.Parse(value));
    }

    [Theory]
    [InlineData(1L, true)]
    [InlineData(0L, false)]
    public void Parse_WhenConvertibleNumericValueReturned_ReturnsExpectedBoolean(long value, bool expectedValue)
    {
        Assert.Equal(expectedValue, _handler.Parse(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("true")]
    [InlineData("not-a-number")]
    public void Parse_WhenValueIsNotNumeric_ReturnsNull(string value)
    {
        Assert.Null(_handler.Parse(value));
    }
}