using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using LfsCompiler;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    [Description("Reads translation files written by translators and builds a dataset that the BuildI18n command will later use to generate translations that the app will use.")]
    public class IngestTranslationsCommand : Command<IngestTranslationsCommand.Settings>
    {

        public class Settings : CommandSettings
        {
            [Description("Path to the translation file to ingest.")]
            [CommandOption("-t|--translation")]
            public string TranslationPath { get; init; } = null!;

            [Description("Path to the root of the source tree for the project aka the directory containing the .csproj file. (Required)")]
            [CommandOption("-p|--sourceRoot")]
            public string SourceRoot { get; init; } = null!;

            [Description("The identity of the person that supplied the translation.  This should be of the form 'source:id' where 'source' is the name of the service where the 'id' can be contacted, e.g. 'nexus:nermnermnerm' (Required)")]
            [CommandOption("-a|--author")]
            public string Author { get; init; } = null!;

            public override ValidationResult Validate()
            {
                if (string.IsNullOrWhiteSpace(this.TranslationPath))
                {
                    return ValidationResult.Error($"--translation must be supplied.  If calling from dotnet build, follow this example: 'dotnet build -target:IngestTranslations \"-p:TranslatedFile=es.json;TranslationAuthor=nexus:nermnermnerm\"'.");
                }

                if (!File.Exists(this.TranslationPath))
                {
                    return ValidationResult.Error($"The path specified with --translation does not exist: {this.TranslationPath}");
                }

                if (this.SourceRoot is null)
                {
                    return ValidationResult.Error("The --sourceRoot parameter must be supplied.");
                }
                if (!Directory.Exists(this.SourceRoot))
                {
                    return ValidationResult.Error($"The directory specified with --sourceRoot does not exist: {this.SourceRoot}");
                }

                if (string.IsNullOrEmpty(Author))
                {
                    return ValidationResult.Error("--author must be supplied.   If calling from dotnet build, follow this example: 'dotnet build -target:IngestTranslations \"-p:TranslatedFile=es.json;TranslationAuthor=nexus:nermnermnerm\"'.");
                }
                if (!new Regex(@"^[a-z]+:[^:]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).IsMatch(this.Author))
                {
                    return ValidationResult.Error("The 'Author' must be of the form 'source:id' where source is an identifier for the service where the person can be contacted (e.g. nexus, github) and 'id' is the identity of the person on that service");
                }

                return ValidationResult.Success();
            }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            // the user config doesn't have any bearing (yet) on this command, but perhaps later it could
            var userConfig = Config.ReadFromFile(settings.SourceRoot);
            var config = new CombinedConfig(settings.SourceRoot, userConfig, GitRepoInfo.CreateNull());
            var sdvTranslator = new SdvTranslationCompiler(config, settings.SourceRoot);
            sdvTranslator.IngestTranslatedFile(settings.TranslationPath, settings.Author);

            Console.WriteLine($"Ingested \"{settings.TranslationPath}\" into {settings.SourceRoot}");
            return 0;
        }
    }
}
