using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace DataBackup
{
    public class MartenRestoreOperation : PgsqlOperationsBase
    {
        private readonly MartenRestoreOptions options;

        public MartenRestoreOperation(MartenRestoreOptions options)
            : base(options)
        {
            this.options = options;
        }

        [SuppressMessage("CodeAnalysis", "CA1308", Justification = "Marten uses lower case table names")]
        [SuppressMessage("CodeAnalysis", "CA2100", Justification = "No SQL injection applies")]
        public override async Task<bool> ExecuteAsync()
        {
            await InitialiseAsync().ConfigureAwait(false);

            Log.Information("Restoring database {DatabaseName}", Connection.Database);

            var files = GetFiles();
            if (files.Count == 0)
            {
                Log.Error("No {Wildcard} files found in {Folder}", $"*{BackupExtension}", Directory.FullName);
                return false;
            }

            var upsertFunctions = await GetFunctions("mt_upsert_").ConfigureAwait(false);

            foreach (var file in files)
            {
                var aliasName = file.EntityName.ToLowerInvariant();

                var tableName = $"mt_doc_{aliasName}";
                var upsertName = $"mt_upsert_{aliasName}";

                List<FunctionColumn> funcParams;
                if (!upsertFunctions.TryGetValue(upsertName, out funcParams))
                {
                    Log.Warning("Missing upsert function {FunctionName} for entity {EntityName}", upsertName, file.EntityName);
                    continue;
                }

                Log.Information("Restoring table {TableName} via upsert function {FunctionName}", tableName, upsertName);

                var data = await file.ReadAsync().ConfigureAwait(false);

                Log.Information("Found {Count} objects to restore", data.Count);

                using (var cmd = Connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = upsertName;

                    var pDict = new Dictionary<string, NpgsqlParameter>(StringComparer.OrdinalIgnoreCase);

                    foreach (var p in funcParams)
                    {
                        if (p.DbType == NpgsqlDbType.Unknown)
                        {
                            throw new InvalidOperationException($"Unsupported data type {p.DataType} for parameter {p.ParameterName}");
                        }

                        Log.Verbose("Registering parameter {ParameterName} with type {Type}", p.ParameterName, p.DbType);
                        pDict.Add(p.ParameterName, cmd.Parameters.Add(p.ParameterName, p.DbType));
                    }

                    foreach (JObject entity in data)
                    {
                        entity.Remove("_pk");
                        entity.Remove("_ts");
                        entity.Remove("_rid");
                        entity.Remove("_etag");
                        entity.Remove("_self");
                        entity.Remove("_attachments");

                        pDict["doc"].Value = entity;
                        pDict["docversion"].Value = Guid.NewGuid();

                        SetParameterFromObject(pDict["docid"], entity, "id");
                        SetParameterFromObject(pDict["docdotnettype"], entity, "$type", file.EntityName);

                        if (pDict.TryGetValue("doctype", out var typeParam))
                        {
                            var type = GetPropertyValue(entity, "$type", file.EntityName);

                            // Pull the central type out
                            type = Regex.Replace(type, @"^.*\[", string.Empty);
                            type = Regex.Replace(type, @"\].*$", string.Empty);

                            // Remove the assembly
                            type = Regex.Replace(type, @"\s*,.*$", string.Empty);

                            // Remove the namespace
                            type = Regex.Replace(type, @"^.*\.", string.Empty);

                            // Convert the class name from PascalCase into snake_case
                            type = Regex.Replace(type, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1_$2");
                            type = Regex.Replace(type, @"(\p{Ll})(\P{Ll})", "$1_$2");

                            // Change to lower case
                            type = type.ToLowerInvariant();

                            typeParam.Value = type;
                        }

                        foreach (var pair in pDict.Where(x => x.Key.StartsWith("arg_", StringComparison.OrdinalIgnoreCase)))
                        {
                            var propName = pair.Key.Substring("arg_".Length).Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase);

                            SetParameterFromObject(pair.Value, entity, propName);
                        }

                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }

            return true;
        }
    }
}
