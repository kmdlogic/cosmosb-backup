using CommandLine;

namespace DataBackup
{
    [Verb("CosmosBackup", HelpText = "Backup a CosmosDB database")]
    public class CosmosBackupOptions : CosmosOptions
    {
    }
}
