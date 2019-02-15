# cosmosdb-backup

Backup and restore a CosmosDB database to or from files.

The files should be named `collection name`.cosmosbak and contain an array of JSON objects.

Command line options:

```
  -a, --action                  Required. The action to perform (Backup or Restore)

  -c, --connectionstring        Required. The connection string to CosmosDb

  -m, --connectionmode          The connection mode to CosmosDb (Gateway or Direct)

  -d, --databasename            Required. The database name to backup or restore

  -p, --partitionkey            The partition key field

  -k, --defaultkey              The default partition key if not present in the document

  -f, --folder                  The folder used for the backup or restore. Defaults to the current directory.

  -t, --databasethroughput      The throughput (RUs) for the database when being created.

  -r, --reservedthroughput      The throughput (RUs) reserved for a collection when using database throughput. Format:
                                "CollectionName:throughput;..."

  -x, --collectionthroughput    The throughput (RUs) for the collection when being created.

  --help                        Display this help screen.

  --version                     Display version information.
```