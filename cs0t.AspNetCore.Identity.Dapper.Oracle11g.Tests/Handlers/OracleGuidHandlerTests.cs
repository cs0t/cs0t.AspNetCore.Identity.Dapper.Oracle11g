using System.Diagnostics.CodeAnalysis;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Tests.Handlers;

public class OracleGuidHandlerTests
{
    private readonly OracleGuidHandler Handler = new();

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

    [Fact]
    public void SetValue_WhenGivenNull_SetsNullAndBinaryType()
    {
        var parameter = new TestDbParameter();
        
        Handler.SetValue(parameter, null);
        
        Assert.Equal(DBNull.Value, parameter.Value);
        Assert.Equal(DbType.Binary, parameter.DbType);
    }

    [Fact]
    public void SetValue_GivenGuid_SetsFlippedBigEndianBytesAndBinaryType()
    {
        var parameter = new TestDbParameter();
        var guidPassed = new Guid("30dd879c-ee2f-11db-8314-0800200c9a66");
        
        var bigEndian = new byte[]
        {
            0x30, 0xdd, 0x87, 0x9c, 
            0xee, 0x2f,             
            0x11, 0xdb,             
            0x83, 0x14, 0x08, 0x00, 0x20, 0x0c, 0x9a, 0x66
        };

        Handler.SetValue(parameter, guidPassed);
        
        Assert.Equal(DbType.Binary, parameter.DbType);
        Assert.Equal(bigEndian, parameter.Value);
    }
    
    [Fact]
    public void Parse_WhenDbValueIsNull_ReturnsNull()
    {
        Assert.Null(Handler.Parse(null));
        
        Assert.Null(Handler.Parse(DBNull.Value));
    }

    [Fact]
    public void Parse_WhenRawBytesReturned_ReturnsCorrectGuid()
    {
        var bigEndian = new byte[]
        {
            0x30, 0xdd, 0x87, 0x9c, 
            0xee, 0x2f,             
            0x11, 0xdb,             
            0x83, 0x14, 0x08, 0x00, 0x20, 0x0c, 0x9a, 0x66
        };
        
        var shouldBeParsedAs = new Guid("30dd879c-ee2f-11db-8314-0800200c9a66");
        
        var returnedFromDb = Handler.Parse(bigEndian);
        Assert.Equal(shouldBeParsedAs, returnedFromDb);
    }

    [Fact]
    public void Parse_GivenStandardGuidString_ReturnsParsedGuid()
    {
        var returnedString = "30dd879c-ee2f-11db-8314-0800200c9a66";
        var shouldBeParsedAs = new Guid(returnedString);
        
        var returnedFromDb = Handler.Parse(returnedString);
        Assert.Equal(shouldBeParsedAs, returnedFromDb);
    }
    
    [Fact]
    public void Parse_GivenRawHexString_ReturnsParsedGuid()
    {
        var returnedString = "30dd879cee2f11db83140800200c9a66";
        var shouldBeParsedAs = new Guid("30dd879c-ee2f-11db-8314-0800200c9a66");
        
        var returnedFromDb = Handler.Parse(returnedString);
        Assert.Equal(shouldBeParsedAs, returnedFromDb);
    }
    
    //bytes negative scenarios
    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    public void Parse_GivenBytesArrayLengthNotSixteen_ReturnsNull(int returnedRawBytesLength)
    {
        var bytes = new byte[returnedRawBytesLength];
        
        var returnedFromDb = Handler.Parse(bytes);
        Assert.Null(returnedFromDb);
    }
    
    //string negative scenarios
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Parse_GivenEmptyString_ReturnsNull(string returnedString)
    {
        Assert.Null(Handler.Parse(returnedString));
    }
    
    [Theory]
    [InlineData("30dd879cee2f11db83140800200c9a301")]
    [InlineData("30dd879cee2f11db83140800200c9")]
    public void Parse_GivenInvalidStringLength_ThrowsFormatException(string returnedString)
    {
        Assert.Throws<FormatException>(() => Handler.Parse(returnedString));
    }

    [Theory]
    [InlineData("30dd879c-ee2f-11db-8314-0800200c9a6630")]
    [InlineData("30dd879c-ee2f-11db-8314-0800200c9a``")]
    [InlineData("30dd879c-ee2f-11db-8314-0800200c9a")]
    public void Parse_GivenMalformedGuidStringWithHyphens_ThrowsFormatException(string returnedString)
    {
        Assert.Throws<FormatException>(() => Handler.Parse(returnedString));
    }
    
    [Theory]
    [InlineData("30dd879cee2f11db83140800200c9a``")]
    [InlineData("30dd879cee2f11db83140800200Kc9a6")]
    [InlineData("30dd879cee2f11db831GH40800200c9a")]
    
    public void Parse_GivenMalformedRawHexString_ThrowsFormatException(string returnedString)
    {
        Assert.Throws<FormatException>(() => Handler.Parse(returnedString));
    }
    
}