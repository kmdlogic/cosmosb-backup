using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;
using Serilog;

namespace DataBackup
{
    public class CosmosFeedOperation : CosmosOperationsBase
    {
        private readonly CosmosFeedOptions options;
        private readonly List<FeedHandler> handlers = new List<FeedHandler>();
        private bool hasCancelled;

        public CosmosFeedOperation(CosmosFeedOptions options)
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

            if (Database == null)
            {
                Log.Error("Database {Name} does not exist", DatabaseName);
                return false;
            }

            var colls = Client.CreateDocumentCollectionQuery(Database.SelfLink).AsEnumerable().ToList();

            foreach (var coll in colls)
            {
                var dataFile = CreateBackupFile(coll.Id);
                var handler = new FeedHandler(Client, coll, dataFile, options);

                handlers.Add(handler);
            }

            Console.CancelKeyPress += OnExit;
            Console.WriteLine("Press Ctrl+C to exit");

            await Task.WhenAll(handlers.Select(x => x.Task).ToArray()).ConfigureAwait(false);

            return true;
        }

        private void OnExit(object sender, ConsoleCancelEventArgs e)
        {
            if (!hasCancelled)
            {
                hasCancelled = true;
                Console.WriteLine("Requesting feed processors stop");

                foreach (var handler in handlers)
                {
                    handler.Cancel();
                }
            }
        }

        private sealed class FeedHandler : IDisposable
        {
            private readonly DocumentClient client;
            private readonly DocumentCollection collection;
            private readonly DataFile dataFile;
            private readonly CosmosFeedOptions options;
            private readonly CancellationTokenSource cancelToken;

            public Task Task { get; set; }

            public FeedHandler(DocumentClient client, DocumentCollection collection, DataFile dataFile, CosmosFeedOptions options)
            {
                this.client = client;
                this.collection = collection;
                this.dataFile = dataFile;
                this.options = options;

                cancelToken = new CancellationTokenSource();

                Task = Task.Run(Execute);
            }

            private async Task Execute()
            {
                Log.Information("Started streaming collection {Collection}", collection.Id);

                try
                {
                    var seenRanges = new Dictionary<string, string>();
                    var first = true;
                    IReadOnlyCollection<PartitionKeyRange> ranges = null;
                    var rangeLoop = 0;

                    using (var sw = dataFile.BeginWrite())
                    {
                        while (true)
                        {
                            if (first)
                            {
                                first = false;

                                ranges = await ReadPartitionKeyRangesAsync().ConfigureAwait(false);

                                Log.Information("Found {Count} partition key ranges in {Collection}", ranges.Count, collection.Id);
                            }
                            else if (options.RangeScan > 0)
                            {
                                rangeLoop++;

                                if (rangeLoop >= options.RangeScan)
                                {
                                    ranges = await ReadPartitionKeyRangesAsync().ConfigureAwait(false);

                                    rangeLoop = 0;
                                }
                            }

                            foreach (var range in ranges)
                            {
                                string continuation;
                                seenRanges.TryGetValue(range.Id, out continuation);
                                seenRanges[range.Id] = await ReadStreamAsync(range.Id, continuation, sw).ConfigureAwait(false);
                            }

                            await Task.Delay(options.WaitTime, cancelToken.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // do nothing
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected exception: {Message} handling collection {Collection}", ex.Message, collection.Id);
                }
#pragma warning restore CA1031 // Do not catch general exception types

                Log.Information("Finished streaming collection {Collection}", collection.Id);
            }

            private async Task<IReadOnlyCollection<PartitionKeyRange>> ReadPartitionKeyRangesAsync()
            {
                var list = new List<PartitionKeyRange>();

                string continuation = null;
                do
                {
                    var feedOptions = new FeedOptions
                    {
                        RequestContinuation = continuation
                    };

                    var response = await client.ReadPartitionKeyRangeFeedAsync(collection.SelfLink, feedOptions).ConfigureAwait(false);

                    list.AddRange(response);

                    continuation = response.ResponseContinuation;
                }
                while (!string.IsNullOrEmpty(continuation));

                return list;
            }

            private async Task<string> ReadStreamAsync(string partitionKeyRangeId, string continuation, DataWriter sw)
            {
                var feedOptions = new ChangeFeedOptions
                {
                    PartitionKeyRangeId = partitionKeyRangeId,
                    RequestContinuation = continuation,
                    StartFromBeginning = options.StartFromBeginning
                };

                var count = 0;
                using (var query = client.CreateDocumentChangeFeedQuery(collection.SelfLink, feedOptions))
                {
                    do
                    {
                        var response = await query.ExecuteNextAsync().ConfigureAwait(false);

                        if (response.Count > 0)
                        {
                            foreach (var item in response)
                            {
                                await sw.WriteAsync(JToken.FromObject(item)).ConfigureAwait(false);
                                count++;
                            }
                        }

                        continuation = response.ResponseContinuation;
                    }
                    while (query.HasMoreResults);
                }

                if (count > 0)
                {
                    Log.Information("Wrote {Count} changes to {Collection}", count, collection.Id);
                }

                return continuation;
            }

            public void Cancel()
            {
                cancelToken.Cancel();
            }

            public void Dispose()
            {
                cancelToken.Dispose();
                Task.Dispose();
            }
        }
    }
}
