using CommandLine;
using Serilog.Events;

namespace DataBackup
{
    public class PgsqlOptions : ICommonOptions
    {
        [Option('v', "verbosity", Required = false, Default = LogEventLevel.Information, HelpText = "The logging level.")]
        public LogEventLevel Verbosity { get; set; }

        [Option('f', "folder", Required = false, HelpText = "The folder used for the backup or restore. Defaults to the current directory.")]
        public string Folder { get; set; }

        [Option('c', "connectionstring", Required = true, HelpText = "The connection string to PostgreSQL.")]
        public string ConnectionString { get; set; }
    }
}
