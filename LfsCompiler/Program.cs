using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<BuildI18nCommand>("buildI18n");
                config.AddCommand<IngestTranslationsCommand>("ingest");
            });
            app.Run(args);
        }
    }
}
