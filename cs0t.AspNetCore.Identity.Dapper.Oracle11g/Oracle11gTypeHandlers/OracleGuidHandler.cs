using System;
using System.Data;
using System.Globalization;
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
        var dotnetBytes = value.Value.ToByteArray();
        
        FlipGuidBytes(dotnetBytes);
        
        parameter.Value = dotnetBytes;
        parameter.DbType = DbType.Binary;
    }

    public override Guid? Parse(object? value)
    {
        if(value is null || value == DBNull.Value)
            return null;
        
        //if raw byte stream is read, flip and create guid
        if(value is byte[] bytes)
        {
            if (bytes.Length != 16)
                return null;
            
            FlipGuidBytes(bytes);
            return new Guid(bytes);
        }
        
        //if we read it in string format
        var stringValue = value.ToString()?.Trim();

        if (!string.IsNullOrEmpty(stringValue))
        {
            //case1: regular guid format "30dd879c-ee2f-..." dotnet handles itself
            if(stringValue.Contains("-"))
                return Guid.Parse(stringValue);
            
            //case2: oracle 11g returns raw string instead of bytes "30DD879CEE2F..."
            if (stringValue.Length == 32)
            {
                var hexBytes = HexStringToByteArray(stringValue);
                FlipGuidBytes(hexBytes);
                return new Guid(hexBytes);
            }
               
        }
        return null;
    }
    
    private static byte[] HexStringToByteArray(string hex)
    {
        var bytes = new byte[16]; 
        
        for (var i = 0; i < 32; i += 2)
        {
            if (byte.TryParse(hex.Substring(i, 2), NumberStyles.HexNumber, 
                    CultureInfo.InvariantCulture, out byte b))
            {
                bytes[i / 2] = b;
            }
        }
        return bytes;
    }

    private static void FlipGuidBytes(byte[] bytes)
    { 
        //flip first 8 bytes
        if (bytes.Length != 16)
            return;
        
        Array.Reverse(bytes,0,4);
        Array.Reverse(bytes,4,2);
        Array.Reverse(bytes,6,2);
    }           
}