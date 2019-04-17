using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json.Linq;
using Serilog;

namespace DataBackup
{
    public class CosmosBackupOperation : CosmosOperationsBase
    {
        private readonly CosmosBackupOptions options;

        public CosmosBackupOperation(CosmosBackupOptions options)
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

            if (Collections.Count == 0)
            {
                Log.Error("Database {Name} contains no collections", DatabaseName);
                return false;
            }

            Log.Verbose("Backing up to {Folder}", Directory.FullName);

            Log.Information("Backing up database {DatabaseName}", Database.Id);

            foreach (var collection in Collections)
            {
                Log.Information("Backing up collection {CollectionName}", collection.Id);

                var collectionUri = UriFactory.CreateDocumentCollectionUri(Database.Id, collection.Id);

                var query = Client.CreateDocumentQuery(collectionUri).AsDocumentQuery();

                var list = new JArray();

                do
                {
                    var response = await query.ExecuteNextAsync().ConfigureAwait(false);

                    foreach (var item in response)
                    {
                        list.Add(JToken.FromObject(item));
                    }
                }
                while (query.HasMoreResults);

                await CreateBackupFile(collection.Id).WriteAsync(list).ConfigureAwait(false);
            }

            return true;
        }
    }
}
