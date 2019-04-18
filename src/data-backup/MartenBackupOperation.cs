using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json.Linq;
using Serilog;

namespace DataBackup
{
    public class MartenBackupOperation : PgsqlOperationsBase
    {
        private readonly MartenBackupOptions options;

        public MartenBackupOperation(MartenBackupOptions options)
            : base(options)
        {
            this.options = options;
        }

        [SuppressMessage("CodeAnalysis", "CA2100", Justification = "No SQL injection applies")]
        public override async Task<bool> ExecuteAsync()
        {
            await InitialiseAsync().ConfigureAwait(false);

            var tables = new List<string>();

            using (var cmd = Connection.CreateCommand())
            {
                cmd.CommandText = @"
select table_name 
from information_schema.columns
where table_name like 'mt_doc_%' and column_name = 'data' and data_type = 'jsonb'
order by table_name";

                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            if (tables.Count == 0)
            {
                Log.Error("Database {DatabaseName} contains no collections", Connection.Database);
                return false;
            }

            Log.Verbose("Backing up to {Folder}", Directory.FullName);

            Log.Information("Backing up database {DatabaseName}", Connection.Database);

            foreach (var table in tables)
            {
                Log.Information("Backing up table {TableName}", table);

                using (var writer = CreateBackupFile(table.Substring("mt_doc_".Length)).BeginWrite())
                {
                    using (var cmd = Connection.CreateCommand())
                    {
                        cmd.CommandText = $"select data, mt_last_modified, mt_dotnet_type from {table}";

                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                var data = reader.GetFieldValue<JObject>(0);
                                var lastModified = reader.GetFieldValue<DateTimeOffset>(1);
                                var classType = reader.GetFieldValue<string>(2);

                                var orderedData = new JObject();

                                // The $type property must be first for CosmosDB but the jsonb type doesn't guarantee order
                                orderedData.Add("$type", GetPropertyValue(data, "$type", classType));
                                orderedData.Add("_ts", lastModified.ToUnixTimeSeconds());

                                foreach (var prop in data.Properties())
                                {
                                    if (!string.Equals(prop.Name, "$type", StringComparison.OrdinalIgnoreCase)
                                     && !string.Equals(prop.Name, "_ts", StringComparison.OrdinalIgnoreCase))
                                    {
                                        orderedData.Add(prop.Name, prop.Value);
                                    }
                                }

                                writer.Write(orderedData);
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
