using System;
using System.Data;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;

sealed class OracleGuidHandler : SqlMapper.TypeHandler<Guid?>
{
    //.NET uses little-endian format different from big-endian format inside oracle 11g.
    public override void SetValue(IDbDataParameter parameter, Guid? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
            parameter.DbType = DbType.Binary;
            return;
        }
        
        //flip before writing to database
        parameter.Value = FlipGuidBytes(value.Value.ToByteArray());
        parameter.DbType = DbType.Binary;
    }

    public override Guid? Parse(object? value)
    {
        if(value is null || value == DBNull.Value)
            return null;
        
        //if raw byte stream is read, flip and create guid
        if(value is byte[] bytes)
            return new Guid(FlipGuidBytes(bytes));
        
        //if we read it in string format
        var stringValue = value.ToString()?.Trim();

        if (!string.IsNullOrEmpty(stringValue))
        {
            //case1: regular guid format
            if(stringValue.Contains("-"))
                return Guid.Parse(stringValue);
            
            //case2: string but in hexadecimal format 
            var stringBytes = HexStringToBytesArray(stringValue);
            return new Guid(FlipGuidBytes(stringBytes));
        }
        return null;
    }

    private static byte[] HexStringToBytesArray(string hexString)
    {
        if(hexString.Length % 2 != 0)
            hexString = "0" + hexString;
        
        //after length is assured to be even number, create bytes array, 1byte - 2chars
        var charNumbers = hexString.Length;
        var bytes = new byte[charNumbers/2];

        for (int i = 0; i < charNumbers; i += 2)
        {
            //grab each byte from string (grab 2 chars each iteration)
            var hexSegment = hexString.Substring(i, 2);
            bytes[i / 2] = Convert.ToByte(hexSegment, 16);
        }
        return bytes;
    }

    private static byte[] FlipGuidBytes(byte[] bytes)
    { 
        //flip first 8 bytes
        if (bytes.Length != 16)
            return bytes;

        var flipped = new byte[16];
        Array.Copy(bytes, flipped,  bytes.Length);
        
        flipped[0] = bytes[3];
        flipped[1] = bytes[2];
        flipped[2] = bytes[1];
        flipped[3] = bytes[0];
        
        flipped[4] = bytes[5];
        flipped[5] = bytes[4];
        
        flipped[6] = bytes[7];
        flipped[7] = bytes[6];
        
        return flipped;
    }           
}