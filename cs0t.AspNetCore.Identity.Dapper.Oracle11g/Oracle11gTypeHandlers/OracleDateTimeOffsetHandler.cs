using System;
using System.Data;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g.Oracle11gTypeHandlers;

sealed class OracleDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset?>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
            if (parameter is OracleParameter op)
            {
                op.OracleDbType = OracleDbType.TimeStampTZ;
            }
            else
            {
                parameter.DbType = DbType.DateTime;
            }
            return;    
        }
        
        if (parameter is OracleParameter oracleParameter)
        {
            oracleParameter.OracleDbType = OracleDbType.TimeStampTZ;
            parameter.Value = value.Value.ToUniversalTime();
        }
        else
        {
            parameter.Value = value.Value.UtcDateTime;
            parameter.DbType = DbType.DateTime;
        }
        
    }

    public override DateTimeOffset? Parse(object? value)
    {
        if(value is null || value == DBNull.Value)
            return null;
        
        if(value is DateTimeOffset dateTimeOffset)
            return dateTimeOffset;
        
        return new DateTimeOffset(Convert.ToDateTime(value), TimeSpan.Zero);
    }
}