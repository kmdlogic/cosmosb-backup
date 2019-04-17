using CommandLine;
using Serilog.Events;

namespace DataBackup
{
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

    public class CosmosOptions : ICommonOptions
    {
        [Option('v', "verbosity", Required = false, Default = LogEventLevel.Information, HelpText = "The logging level.")]
        public LogEventLevel Verbosity { get; set; }

        [Option('f', "folder", Required = false, HelpText = "The folder used for the backup or restore. Defaults to the current directory.")]
        public string Folder { get; set; }

        [Option('c', "connectionstring", Required = true, HelpText = "The connection string to CosmosDb")]
        public string ConnectionString { get; set; }

        [Option('m', "connectionmode", Required = false, HelpText = "The connection mode (Gateway or Direct)")]
        public CosmosConnection ConnectionMode { get; set; }

        [Option('d', "databasename", Required = true, HelpText = "The database name to backup or restore")]
        public string Database { get; set; }
    }
}
