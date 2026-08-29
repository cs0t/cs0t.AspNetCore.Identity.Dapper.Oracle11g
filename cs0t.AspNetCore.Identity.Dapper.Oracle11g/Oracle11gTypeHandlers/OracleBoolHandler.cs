using System;
using System.Data;
using Dapper;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;

sealed class OracleBoolHandler : SqlMapper.TypeHandler<bool?>
{
    public override void SetValue(IDbDataParameter parameter, bool? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
            parameter.DbType = DbType.Byte;
            return;
        }
        parameter.Value = value.Value ? 1 : 0;
        parameter.DbType = DbType.Byte;
    }

    public override bool? Parse(object? value)
    {
        if (value is null || value == DBNull.Value)
            return null;
        
        //oracle sometimes might return oracledecimal which cant be directly converted to int32
        if (int.TryParse(value.ToString(), out int numericValue))
        {
            return numericValue == 1;
        }

        return null;
    }
}