using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;

namespace DataBackup
{
    [SuppressMessage("CodeAnalysis", "CA1822", Justification = "Prefer the clarity of instance methods")]
    public abstract class PgsqlOperationsBase : OperationsBase
    {
        private readonly PgsqlOptions options;

        protected PgsqlOperationsBase(PgsqlOptions options)
            : base(options)
        {
            this.options = options;
        }

        public NpgsqlConnection Connection { get; private set; }

        protected async Task InitialiseAsync()
        {
            NpgsqlConnection.GlobalTypeMapper.UseJsonNet();

            Connection = new NpgsqlConnection(options.ConnectionString);

            await Connection.OpenAsync().ConfigureAwait(false);
        }

        protected async Task<Dictionary<string, List<FunctionColumn>>> GetFunctions(string prefix)
        {
            var dict = new Dictionary<string, List<FunctionColumn>>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = Connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT routines.routine_name, parameters.parameter_name, parameters.data_type, parameters.udt_name
FROM information_schema.routines
LEFT JOIN information_schema.parameters ON routines.specific_name=parameters.specific_name
WHERE routines.routine_name like @prefix
ORDER BY routines.routine_name, parameters.ordinal_position";

                cmd.Parameters.AddWithValue("prefix", $"{prefix}%");

                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var funcName = reader.GetString(0);
                        var paramName = reader.GetString(1);
                        var dataType = reader.GetString(2);
                        var baseType = reader.GetString(3);

                        NpgsqlDbType dbType;
                        if (!Enum.TryParse(baseType, true, out dbType))
                        {
                            dbType = NpgsqlDbType.Unknown;
                        }

                        List<FunctionColumn> paramList;
                        if (!dict.TryGetValue(funcName, out paramList))
                        {
                            paramList = new List<FunctionColumn>();
                            dict.Add(funcName, paramList);
                        }

                        paramList.Add(new FunctionColumn(paramName, dataType, dbType));
                    }
                }
            }

            return dict;
        }

        protected void SetParameterFromObject(NpgsqlParameter parameter, JObject entity, string propertyName, object defaultValue = null)
        {
            var prop = entity.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);

            if (prop == null)
            {
                parameter.Value = defaultValue ?? DBNull.Value;
                return;
            }

            if (parameter.NpgsqlDbType == NpgsqlDbType.Jsonb)
            {
                parameter.Value = prop;
                return;
            }

            var strValue = prop.Value<string>();
            if (string.IsNullOrEmpty(strValue))
            {
                parameter.Value = defaultValue ?? DBNull.Value;
                return;
            }

            switch (parameter.NpgsqlDbType)
            {
                case NpgsqlDbType.Char:
                    parameter.Value = strValue[0];
                    break;
                case NpgsqlDbType.Date:
                case NpgsqlDbType.Timestamp:
                    parameter.Value = DateTime.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.TimestampTz:
                case NpgsqlDbType.TimeTz:
                    parameter.Value = DateTimeOffset.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Time:
                    parameter.Value = TimeSpan.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Boolean:
                    parameter.Value = bool.Parse(strValue);
                    break;
                case NpgsqlDbType.Smallint:
                    parameter.Value = short.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Integer:
                    parameter.Value = int.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Bigint:
                    parameter.Value = long.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Real:
                case NpgsqlDbType.Double:
                    parameter.Value = double.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Numeric:
                case NpgsqlDbType.Money:
                    parameter.Value = decimal.Parse(strValue, CultureInfo.CurrentCulture);
                    break;
                case NpgsqlDbType.Uuid:
                    parameter.Value = Guid.Parse(strValue);
                    break;
                case NpgsqlDbType.Varchar:
                case NpgsqlDbType.Text:
                    parameter.Value = strValue;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported data type {parameter.NpgsqlDbType} for parameter {parameter.ParameterName}");
            }
        }

        protected class FunctionColumn
        {
            public FunctionColumn(string parameterName, string dataType, NpgsqlDbType dbType)
            {
                ParameterName = parameterName;
                DataType = dataType;
                DbType = dbType;
            }

            public string ParameterName { get; }

            public string DataType { get; }

            public NpgsqlDbType DbType { get; }
        }
    }
}
