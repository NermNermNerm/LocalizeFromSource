using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<BuildI18nCommand>("buildI18n");
                config.AddCommand<IngestTranslationsCommand>("ingest");
                config.PropagateExceptions();
            });

            try
            {
                return app.Run(args);
            }
            catch (FatalErrorException ex)
            {
                Console.Error.WriteLine($"LfsCompiler : Command line warning {TranslationCompiler.ErrorPrefix}{ex.ErrorCode:d4} : {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unhandled exception:");
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
