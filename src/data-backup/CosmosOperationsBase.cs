using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Serilog;

namespace DataBackup
{
    public abstract class CosmosOperationsBase : OperationsBase
    {
        private readonly CosmosOptions options;

        protected DocumentClient Client { get; private set; }

        protected string DatabaseName { get; private set; }

        protected Database Database { get; set; }

        protected List<DocumentCollection> Collections { get; private set; }

        protected CosmosOperationsBase(CosmosOptions options)
            : base(options)
        {
            this.options = options;
        }

        protected bool Initialise()
        {
            Client = GetDocumentClient();
            if (Client == null)
            {
                return false;
            }

            Database = Client.CreateDatabaseQuery()
#pragma warning disable CA1304 // Specify CultureInfo - CosmosDB Linq doesn't support this
                             .Where(x => x.Id.ToLower() == options.Database.ToLower())
#pragma warning restore CA1304 // Specify CultureInfo
                             .AsEnumerable()
                             .FirstOrDefault();

            Collections = Database == null
                ? new List<DocumentCollection>()
                : Client.CreateDocumentCollectionQuery(Database.SelfLink).AsEnumerable().ToList();

            return true;
        }

        private DocumentClient GetDocumentClient()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var segment in options.ConnectionString.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)))
            {
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

            var policy = new ConnectionPolicy();

            if (options.ConnectionMode == CosmosConnection.Direct)
            {
                policy.ConnectionMode = ConnectionMode.Direct;
                policy.ConnectionProtocol = Protocol.Tcp;
            }
            else
            {
                policy.ConnectionMode = ConnectionMode.Gateway;
                policy.ConnectionProtocol = Protocol.Https;
            }

            return new DocumentClient(new Uri(accountEndpoint, UriKind.Absolute), accountKey, policy);
        }
    }
}
