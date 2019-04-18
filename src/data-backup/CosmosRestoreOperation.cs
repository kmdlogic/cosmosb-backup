using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace DataBackup
{
    public class CosmosRestoreOperation : CosmosOperationsBase
    {
        private readonly CosmosRestoreOptions options;

        public CosmosRestoreOperation(CosmosRestoreOptions options)
            : base(options)
        {
            this.options = options;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!Initialise())
            {
                return false;
            }

            var reservedThroughput = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            if (!string.IsNullOrEmpty(options.CollectionReservedThroughput))
            {
                foreach (var reservedSegment in options.CollectionReservedThroughput.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var reservedParts = reservedSegment.Split(":");

                    if (reservedParts.Length != 2
                        || string.IsNullOrWhiteSpace(reservedParts[0])
                        || reservedThroughput.ContainsKey(reservedParts[0])
                        || !int.TryParse(reservedParts[1], out int throughput))
                    {
                        Log.Error("Unexpected reserved throughput segment: {ReservedSegment}", reservedSegment);
                        return false;
                    }

                    reservedThroughput.Add(reservedParts[0], throughput);
                }
            }

            if (Database == null)
            {
                var databaseOptions = options.DatabaseThroughput == null
                    ? null
                    : new RequestOptions
                        {
                            OfferEnableRUPerMinuteThroughput = true,
                            OfferThroughput = options.DatabaseThroughput
                        };

                var response = await Client.CreateDatabaseIfNotExistsAsync(new Database { Id = options.Database }, databaseOptions).ConfigureAwait(false);

                Database = response.Resource;
            }

            Log.Information("Restoring database {DatabaseName}", Database.Id);

            var serializer = new JsonSerializer();
            var checkPartitionKey = !string.IsNullOrEmpty(options.PartitionKey)
                                    && !string.IsNullOrEmpty(options.PartitionKeyDefault);

            var files = GetFiles();
            if (files.Count == 0)
            {
                Log.Error("No {Wildcard} files found in {Folder}", $"*{BackupExtension}", Directory.FullName);
                return false;
            }

            foreach (var file in files)
            {
                Log.Information("Restoring collection {CollectionName}", file.EntityName);

                var documentCollection = new DocumentCollection
                {
                    Id = file.EntityName,
                    DefaultTimeToLive = -1,
                    PartitionKey = string.IsNullOrEmpty(options.PartitionKey)
                                    ? null
                                    : new PartitionKeyDefinition { Paths = { $"/{options.PartitionKey}" } }
                };

                RequestOptions collectionOptions = null;

                if (reservedThroughput.TryGetValue(file.EntityName, out int throughput))
                {
                    collectionOptions = new RequestOptions
                    {
                        OfferThroughput = throughput
                    };
                }
                else if (options.CollectionThroughput != null)
                {
                    collectionOptions = new RequestOptions
                    {
                        OfferEnableRUPerMinuteThroughput = true,
                        OfferThroughput = options.CollectionThroughput
                    };
                }

                await Client.CreateDocumentCollectionIfNotExistsAsync(Database.SelfLink, documentCollection, collectionOptions).ConfigureAwait(false);

                var collectionUri = UriFactory.CreateDocumentCollectionUri(Database.Id, file.EntityName);

                var count = 0;
                foreach (var entity in file.Read())
                {
                    count++;

                    if (checkPartitionKey && !entity.ContainsKey(options.PartitionKey))
                    {
                        entity.Add(options.PartitionKey, options.PartitionKeyDefault);
                    }

                    await Client.UpsertDocumentAsync(collectionUri, entity, disableAutomaticIdGeneration: true).ConfigureAwait(false);
                }

                Log.Information("Restored {Count} objects", count);
            }

            return true;
        }
    }
}
