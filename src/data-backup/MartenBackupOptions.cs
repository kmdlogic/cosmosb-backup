using CommandLine;

namespace DataBackup
{
    [Verb("MartenBackup", HelpText = "Backup a PostgreSQL/Marten database")]
    public class MartenBackupOptions : PgsqlOptions
    {
    }
}
