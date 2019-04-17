using Serilog.Events;

namespace DataBackup
{
    public interface ICommonOptions
    {
        LogEventLevel Verbosity { get; }

        string Folder { get; }
    }
}
