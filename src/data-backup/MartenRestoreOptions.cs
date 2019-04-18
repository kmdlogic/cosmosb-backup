using CommandLine;

namespace DataBackup
{
    [Verb("MartenRestore", HelpText = "Restore a PostgreSQL/Marten database.")]
    public class MartenRestoreOptions : PgsqlOptions
    {
    }
}
