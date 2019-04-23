# data-backup

Backup and restore a [CosmosDB](https://docs.microsoft.com/en-us/azure/cosmos-db/introduction) or [PostgreSQL/Marten](http://jasperfx.github.io/marten/) database to or from files.

The files should be named `collection name.jsonbak` and contain an array of JSON objects.

Supported actions:

```
  CosmosBackup     Backup a CosmosDB database.

  CosmosRestore    Restore a CosmosDB database.

  CosmosFeed       Listen to the change feed from the CosmosDB database.

  MartenBackup     Backup a PostgreSQL/Marten database.

  MartenRestore    Restore a PostgreSQL/Marten database.

  help             Display more information on a specific command.

  version          Display version information.
```

## CosmosBackup

```
  -v, --verbosity           (Default: Information) The logging level.

  -f, --folder              The folder used for the backup or restore. Defaults to the current directory.

  -c, --connectionstring    Required. The connection string to CosmosDb.

  -m, --connectionmode      (Default: Gateway) The connection mode (Gateway or Direct).

  -d, --databasename        Required. The database name to backup or restore.
```

Generates a file per collection in the database.

## CosmosRestore

```
  -p, --partitionkey            The partition key field.

  -k, --defaultkey              The default partition key if not present in the document.

  -t, --databasethroughput      The throughput (RUs) for the database when being created.

  -r, --reservedthroughput      The throughput (RUs) reserved for a collection when using database throughput. Format:
                                "CollectionName:throughput;...".

  -x, --collectionthroughput    The throughput (RUs) for the collection when being created.

  -v, --verbosity               (Default: Information) The logging level.

  -f, --folder                  The folder used for the backup or restore. Defaults to the current directory.

  -c, --connectionstring        Required. The connection string to CosmosDb.

  -m, --connectionmode          (Default: Gateway) The connection mode (Gateway or Direct).

  -d, --databasename            Required. The database name to backup or restore.
```

Creates the database if it is missing. For each file, it creates a corresponding collection, setting the `partitionkey` of each object if required. The use of a `partitionkey` is required by CosmosDB when using the `databasethroughput` option.

## CosmosFeed

```
  -h, --host                (Default: DataBackup) The unique host name for this stream reader.

  -b, --beginning           (Default: false) Start the feed from the beginning.

  -w, --wait                (Default: 1000) The time to wait in ms between feed checks.

  -r, --rangescan           (Default: 0) How often to rescan for new ranges, in multiples of the wait time. 0 =
                            disable.

  -v, --verbosity           (Default: Information) The logging level.

  -f, --folder              The folder used for the backup or restore. Defaults to the current directory.

  -c, --connectionstring    Required. The connection string to CosmosDb.

  -m, --connectionmode      (Default: Gateway) The connection mode (Gateway or Direct).

  -d, --databasename        Required. The database name to backup or restore.
```

Listens to the change feed for a CosmosDB database. Seperate output files are created for each collection. Since the change feed is read by partitionkey ranges, the output may be out of order but any changes with the same partitionkey will be ordered. A single record may appear multiple times, once for each change detected.

## MartenBackup

```
  -v, --verbosity           (Default: Information) The logging level.

  -f, --folder              The folder used for the backup or restore. Defaults to the current directory.

  -c, --connectionstring    Required. The connection string to PostgreSQL.
```

Generates a file per table. The `$type` field is guaranteed to be the first property (PostgreSQL `jsonb` fields reorder properties) and the `mt_last_modified` field is converted to a UNIX timestamp and added as the `_ts` property to match CosmosDB.

Please be aware of the following limitations:

1. There is no support for soft deletes (i.e. `mt_deleted` and `mt_deleted_at`). Any record which has been soft deleted will also be exported as if it hadn't been deleted.
2. The tables are all lower case so the files are also lower case. This may not match the case of the desired collections in CosmosDB.

## MartenRestore

```
  -v, --verbosity           (Default: Information) The logging level.

  -f, --folder              The folder used for the backup or restore. Defaults to the current directory.

  -c, --connectionstring    Required. The connection string to PostgreSQL.
```

Restores data from each file to the corresponding table.

Please be aware of the following limitations:

1. The schema must already exist for the restoration process to succeed.
2. There is no attempt to maintain the last modified timestamp.
3. The `mt_dotnet_type` field is set from the `$type` property, or from the file name if not present. This may not match what Marten itself sets this field to. Since this field is for information only, it is not considered significant.
4. There is limited support for [one level heirarchies](http://jasperfx.github.io/marten/documentation/documents/advanced/hierarchies/). The restore operation attempts to emulate Marten by setting the `mt_doc_type` field from class name in `$type`, converted to [snake_case](https://en.wikipedia.org/wiki/Snake_case).