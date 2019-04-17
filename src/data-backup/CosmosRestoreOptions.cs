using CommandLine;

namespace DataBackup
{
    [Verb("CosmosRestore", HelpText = "Restore a CosmosDB database")]
    public class CosmosRestoreOptions : CosmosOptions
    {
        [Option('p', "partitionkey", Required = false, HelpText = "The partition key field")]
        public string PartitionKey { get; set; }

        [Option('k', "defaultkey", Required = false, HelpText = "The default partition key if not present in the document")]
        public string PartitionKeyDefault { get; set; }

        [Option('t', "databasethroughput", Required = false, HelpText = "The throughput (RUs) for the database when being created.")]
        public int? DatabaseThroughput { get; set; }

        [Option('r', "reservedthroughput", Required = false, HelpText = "The throughput (RUs) reserved for a collection when using database throughput. Format: \"CollectionName:throughput;...\" ")]
        public string CollectionReservedThroughput { get; set; }

        [Option('x', "collectionthroughput", Required = false, HelpText = "The throughput (RUs) for the collection when being created.")]
        public int? CollectionThroughput { get; set; }
    }
}
