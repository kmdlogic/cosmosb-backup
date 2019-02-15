using CommandLine;

namespace CosmosBackup
{
    public enum CosmosAction
    {
        /// <summary>
        /// Backup the database to files
        /// </summary>
        Backup,

        /// <summary>
        /// Restore the database from files
        /// </summary>
        Restore
    }

    public enum CosmosConnection
    {
        /// <summary>
        /// Use Gateway/HTTPS mode
        /// </summary>
        Gateway = 0,

        /// <summary>
        /// Use Direct/TCP mode
        /// </summary>
        Direct
    }

    public class Options
    {
        [Option('a', "action", Required = true, HelpText = "The action to perform (Backup or Restore)")]
        public CosmosAction Action { get; set; }

        [Option('c', "connectionstring", Required = true, HelpText = "The connection string to CosmosDb")]
        public string ConnectionString { get; set; }

        [Option('m', "connectionmode", Required = false, HelpText = "The connection mode to CosmosDb (Gateway or Direct)")]
        public CosmosConnection ConnectionMode { get; set; }

        [Option('d', "databasename", Required = true, HelpText = "The database name to backup or restore")]
        public string Database { get; set; }

        [Option('p', "partitionkey", Required = false, HelpText = "The partition key field")]
        public string PartitionKey { get; set; }

        [Option('k', "defaultkey", Required = false, HelpText = "The default partition key if not present in the document")]
        public string PartitionKeyDefault { get; set; }

        [Option('f', "folder", Required = false, HelpText = "The folder used for the backup or restore. Defaults to the current directory.")]
        public string Folder { get; set; }

        [Option('t', "databasethroughput", Required = false, HelpText = "The throughput (RUs) for the database when being created.")]
        public int? DatabaseThroughput { get; set; }

        [Option('r', "reservedthroughput", Required = false, HelpText = "The throughput (RUs) reserved for a collection when using database throughput. Format: \"CollectionName:throughput;...\" ")]
        public string CollectionReservedThroughput { get; set; }

        [Option('x', "collectionthroughput", Required = false, HelpText = "The throughput (RUs) for the collection when being created.")]
        public int? CollectionThroughput { get; set; }
    }
}
