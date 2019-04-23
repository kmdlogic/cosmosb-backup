namespace DataBackup
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using Serilog;

    public static class Program
    {
        [SuppressMessage("CodeAnalysis", "CA1031", Justification = "Main catch")]
        public static async Task<int> Main(string[] args)
        {
            var minLogLevelSwitch = new Serilog.Core.LoggingLevelSwitch();

            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.ControlledBy(minLogLevelSwitch)
                            .WriteTo.Console()
                            .CreateLogger();

            try
            {
                var helpWriter = new StringWriter();

                var commandLineParser = new Parser(s =>
                {
                    s.HelpWriter = helpWriter;
                    s.CaseSensitive = false;
                    s.CaseInsensitiveEnumValues = true;
                });

                var result = await commandLineParser
                        .ParseArguments<
                            CosmosBackupOptions,
                            CosmosRestoreOptions,
                            CosmosFeedOptions,
                            MartenBackupOptions,
                            MartenRestoreOptions
                        >(args)
                        .WithParsed((ICommonOptions o) =>
                        {
                            minLogLevelSwitch.MinimumLevel = o.Verbosity;
                            Log.Verbose("Started with arguments {Arguments}", args);
                            Log.Verbose("The parsed options are {@Parsed}", o);
                        })
                        .MapResult(
                          (CosmosBackupOptions opts) => new CosmosBackupOperation(opts).ExecuteAsync(),
                          (CosmosRestoreOptions opts) => new CosmosRestoreOperation(opts).ExecuteAsync(),
                          (CosmosFeedOptions opts) => new CosmosFeedOperation(opts).ExecuteAsync(),
                          (MartenBackupOptions opts) => new MartenBackupOperation(opts).ExecuteAsync(),
                          (MartenRestoreOptions opts) => new MartenRestoreOperation(opts).ExecuteAsync(),
                          errs =>
                          {
                              Console.WriteLine(helpWriter.ToString());
                              return Task.FromResult(false);
                          }).ConfigureAwait(false);

                return result ? 0 : 1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error: {Message}", ex.Message);
                return -1;
            }
        }
    }
}
