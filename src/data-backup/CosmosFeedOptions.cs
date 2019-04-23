using CommandLine;

namespace DataBackup
{
    [Verb("CosmosFeed", HelpText = "Listen to the change feed from the CosmosDB database.")]
    public class CosmosFeedOptions : CosmosOptions
    {
        [Option('h', "host", Required = false, Default = "DataBackup", HelpText = "The unique host name for this stream reader.")]
        public string HostName { get; set; }

        [Option('b', "beginning", Required = false, Default = false, HelpText = "Start the feed from the beginning.")]
        public bool StartFromBeginning { get; set; }

        [Option('w', "wait", Required = false, Default = 1000, HelpText = "The time to wait in ms between feed checks.")]
        public int WaitTime { get; set; }

        [Option('r', "rangescan", Required = false, Default = 0, HelpText = "How often to rescan for new ranges, in multiples of the wait time. 0 = disable.")]
        public int RangeScan { get; set; }
    }
}
