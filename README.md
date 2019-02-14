# cosmosdb-backup

Backup and restore a CosmosDB database to or from files.

The files should be named `collection name`.cosmosbak and contain an array of JSON objects.

Command line options:

```
  -a, --action                          Required. The action to perform (Backup or Restore)

  -c, --connectionstring                Required. The connection string to CosmosDb

  -d, --databasename                    Required. The database name to backup or restore

  -p, --partitionkey                    The partition key field

  -k, --defaultkey                      The default partition key if not present in the document

  -f, --folder                          The folder used for the backup or restore. Defaults to the current directory.

  -t, --databasethroughput              The throughput (RUs) for the database when being created.

  -r, --collectionthroughput            The throughput (RUs) for the collection when being created.

  -e, --collectionreserverthroughput    Name of collection and required throughput for it. Format: "CollectionName:throughput;..."

  --help                                Display this help screen.

  --version                             Display version information.
```