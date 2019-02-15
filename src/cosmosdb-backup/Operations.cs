namespace CosmosBackup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Serilog;

    public class Operations
    {
        private readonly Options options;

        private DocumentClient client;
        private string databaseName = null;
        private DirectoryInfo directory;
        private Database database;
        private List<DocumentCollection> collections;

        public Operations(Options options)
        {
            this.options = options;
        }

        public async Task<int> RunAsync()
        {
            directory = new DirectoryInfo(options.Folder ?? ".");
            if (!directory.Exists)
            {
                Log.Error("Missing folder {Name}", directory.Name);
                return 1;
            }

            client = GetDocumentClient();
            if (client == null)
            {
                return 1;
            }

            database = client.CreateDatabaseQuery()
#pragma warning disable CA1304 // Specify CultureInfo
                             .Where(x => x.Id.ToLower() == options.Database.ToLower())
#pragma warning restore CA1304 // Specify CultureInfo
                             .AsEnumerable()
                             .FirstOrDefault();

            collections = database == null
                ? new List<DocumentCollection>()
                : client.CreateDocumentCollectionQuery(database.SelfLink).AsEnumerable().ToList();

            switch (options.Action)
            {
                case CosmosAction.Backup:
                    return await Backup().ConfigureAwait(false);

                case CosmosAction.Restore:
                    return await Restore().ConfigureAwait(false);

                default:
                    Log.Error("Unexpected action {Action}", options.Action);
                    return 1;
            }
        }

        private async Task<int> Backup()
        {
            if (database == null)
            {
                Log.Error("Database {Name} does not exist", databaseName);
                return 1;
            }

            if (collections.Count == 0)
            {
                Log.Error("Database {Name} contains no collections", databaseName);
                return 1;
            }

            Log.Verbose("Backing up to {Folder}", directory.FullName);

            var serializer = new JsonSerializer { Formatting = Formatting.Indented };

            Log.Information("Backing up database {DatabaseName}", database.Id);

            foreach (var collection in collections)
            {
                Log.Information("Backing up collection {CollectionName}", collection.Id);

                var collectionUri = UriFactory.CreateDocumentCollectionUri(database.Id, collection.Id);

                var query = client.CreateDocumentQuery(collectionUri).AsDocumentQuery();

                var list = new List<dynamic>();

                do
                {
                    var response = await query.ExecuteNextAsync().ConfigureAwait(false);
                    list.AddRange(response);
                }
                while (query.HasMoreResults);

                var file = Path.Combine(directory.FullName, collection.Id + ".cosmosbak");

                using (var sw = File.CreateText(file))
                {
                    serializer.Serialize(sw, list);
                }
            }

            return 0;
        }

        private async Task<int> Restore()
        {
            var jsonFiles = directory.GetFiles("*.cosmosbak", new EnumerationOptions { RecurseSubdirectories = false });
            if (jsonFiles.Length == 0)
            {
                Log.Error("No *.cosmosbak files found in {Folder}", directory.FullName);
                return 1;
            }

            if (database == null)
            {
                var databaseOptions = options.DatabaseThroughput == null
                    ? null
                    : new RequestOptions
                        {
                            OfferEnableRUPerMinuteThroughput = true,
                            OfferThroughput = options.DatabaseThroughput
                        };

                var response = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = options.Database }, databaseOptions).ConfigureAwait(false);

                database = response.Resource;
            }

            Log.Information("Restoring database {DatabaseName}", database.Id);

            var serializer = new JsonSerializer();
            var checkPartitionKey = !string.IsNullOrEmpty(options.PartitionKey)
                                    && !string.IsNullOrEmpty(options.PartitionKeyDefault);

            foreach (var file in jsonFiles)
            {
                var collectionName = Path.GetFileNameWithoutExtension(file.Name);

                Log.Information("Restoring collection {CollectionName}", collectionName);

                var documentCollection = new DocumentCollection
                {
                    Id = collectionName,
                    DefaultTimeToLive = -1,
                    PartitionKey = string.IsNullOrEmpty(options.PartitionKey)
                                    ? null
                                    : new PartitionKeyDefinition { Paths = { $"/{options.PartitionKey}" } }
                };

                var reservedTroughput = options.ReserverdTroughput.Split(';').FirstOrDefault(x => x.Split(':')[0] == collectionName);

                RequestOptions collectionOptions = null;

                if (reservedTroughput != null)
                {
                    collectionOptions = new RequestOptions() { OfferThroughput = int.Parse(reservedTroughput.Split(':')[1]) };
                }

                if (options.CollectionThroughput != null)
                {
                    collectionOptions = new RequestOptions
                    {
                        OfferEnableRUPerMinuteThroughput = true,
                        OfferThroughput = options.CollectionThroughput
                    };
                }

                await client.CreateDocumentCollectionIfNotExistsAsync(database.SelfLink, documentCollection, collectionOptions).ConfigureAwait(false);

                JArray data;
                using (var sr = file.OpenText())
                {
                    data = await JArray.LoadAsync(new JsonTextReader(sr)).ConfigureAwait(false);
                }

                var collectionUri = UriFactory.CreateDocumentCollectionUri(database.Id, collectionName);

                Log.Information("Found {Count} objects to restore", data.Count);

                foreach (JObject entity in data)
                {
                    if (checkPartitionKey && !entity.ContainsKey(options.PartitionKey))
                    {
                        entity.Add(options.PartitionKey, options.PartitionKeyDefault);
                    }

                    await client.UpsertDocumentAsync(collectionUri, entity, disableAutomaticIdGeneration: true).ConfigureAwait(false);
                }
            }

            return 0;
        }

        private DocumentClient GetDocumentClient()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var segment in options.ConnectionString.Split(';'))
            {
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                var pos = segment.IndexOf('=', StringComparison.Ordinal);

                if (pos <= 0)
                {
                    Log.Error("Unexpected connection string segment: {Segment}", segment);
                    return null;
                }

                dict.Add(segment.Substring(0, pos), segment.Substring(pos + 1, segment.Length - pos - 1));
            }

            if (!dict.TryGetValue("AccountEndpoint", out string accountEndpoint))
            {
                Log.Error("Missing connection string segment: AccountEndpoint");
                return null;
            }

            if (!dict.TryGetValue("AccountKey", out string accountKey))
            {
                Log.Error("Missing connection string segment: AccountKey");
                return null;
            }

            var policy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConnectionProtocol = Protocol.Https
            };

            return new DocumentClient(new Uri(accountEndpoint, UriKind.Absolute), accountKey, policy);
        }
    }
}
