using CommandLine;

namespace DataBackup
{
    [Verb("MartenRestore", HelpText = "Restore to a PostgreSQL/Marten database")]
    public class MartenRestoreOptions : PgsqlOptions
    {
    }
}
