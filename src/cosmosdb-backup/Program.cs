namespace CosmosBackup
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using Serilog;

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                           .WriteTo.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate)
                           .Enrich.FromLogContext()
                           .CreateLogger();

            var helpWriter = new StringWriter();

            var commandLineParser = new Parser(s =>
            {
                s.HelpWriter = helpWriter;
                s.CaseSensitive = false;
                s.CaseInsensitiveEnumValues = true;
            });

            try
            {
                var result = await commandLineParser
                    .ParseArguments<Options>(args)
                    .MapResult(
                    (parsed) =>
                    {
                        return (Task<int>)new Operations(parsed).RunAsync();
                    },
                    (errs) =>
                    {
                        Console.WriteLine(helpWriter.ToString());
                        return Task.FromResult(2);
                    }).ConfigureAwait(false);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected exception");
                return 1;
            }
        }
    }
}
