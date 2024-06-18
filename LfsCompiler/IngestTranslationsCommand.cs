using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
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
                if (!string.IsNullOrWhiteSpace(this.TranslationPath))
                {
                    return ValidationResult.Error($"--translation must be supplied.  If calling from msbuild, follow this example: 'msbuild -target:IngestTranslations -p:TranslatedFile=es.json;TranslationAuthor=nexus:nermnermnerm'");
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
                    return ValidationResult.Error("--author must be supplied");
                }
                if (!new Regex(@"^[a-z]+:[^:]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).IsMatch(this.Author))
                {
                    return ValidationResult.Error("the 'Author' must be of the form 'source:id' where source is an identifier for the service where the person can be contacted (e.g. nexus, github) and 'id' is the identity of the person on that service");
                }

                return ValidationResult.Success();
            }
        }

        private static readonly JsonSerializerOptions ReaderOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

        public override int Execute(CommandContext context, Settings settings)
        {
            // the user config doesn't have any bearing (yet) on this command, but perhaps later it could
            var userConfig = Config.ReadFromFile(settings.SourceRoot);
            var config = new CombinedConfig(settings.SourceRoot, userConfig);
            var sdvTranslator = new SdvTranslationCompiler(config, settings.SourceRoot);

            DateTime ingestionDate = DateTime.Now;
            Dictionary<string, string> keyToSourceStringMap = sdvTranslator.ReadKeyToSourceMapFile();
            string locale = sdvTranslator.GetLocaleOfIncomingTranslationFile(settings.TranslationPath);
            (var keyToNewTranslationMap, var keyOrderPerNewTranslations, var commitTranslationWasBuiltFrom) = sdvTranslator.ReadIncomingTranslationFile(settings.TranslationPath);
            var oldTranslationEntryList = sdvTranslator.ReadTranslationEntryList(locale);

            var keysThatHaveTranslationsButAreNotPresentAnymore = keyToNewTranslationMap.Keys.Where(k => !keyToSourceStringMap.ContainsKey(k)).ToArray();
            if (keysThatHaveTranslationsButAreNotPresentAnymore.Length > 0)
            {
                // This we consider fatal because our only recourse is to throw away the translation because we have no sure way to map the translation to a source string
                throw new FatalErrorException($"The incoming translation file contains translations for strings that are not present in your compile.  That probably means that you are not checked out to the commit that the translator used to write the translation ({commitTranslationWasBuiltFrom}).  You need to create a branch from that commit and ingest there.  Missing key(s): {string.Join(", ", keysThatHaveTranslationsButAreNotPresentAnymore)}", TranslationCompiler.IngestingOutOfSync);
            }
            bool translationIsComplete = keyToSourceStringMap.Keys.All(keyToNewTranslationMap.ContainsKey);

            Dictionary<string, string> newSourceToTranslationMap = new(); // TODO:
            Dictionary<string, string> sourceToKeyMap = keyToSourceStringMap.ToDictionary(pair => pair.Value, pair => pair.Key);

            HashSet<string> alreadyTranslatedStrings = new();
            List<TranslationEntry> finishedList = new();
            foreach (var oldTranslation in oldTranslationEntryList)
            {
                if (!sourceToKeyMap.ContainsKey(oldTranslation.source) && translationIsComplete)
                {
                    // discard the old translation
                }
                else if (newSourceToTranslationMap.TryGetValue(oldTranslation.source, out string? newTranslation)
                    && (newTranslation != oldTranslation.translation || oldTranslation.author.StartsWith("automation:")))
                {
                    // update the translation
                    alreadyTranslatedStrings.Add(oldTranslation.source);
                    finishedList.Add(new TranslationEntry(oldTranslation.source, newTranslation, settings.Author, ingestionDate));
                }
                else
                {
                    // leave the old translation alone
                    alreadyTranslatedStrings.Add(oldTranslation.source);
                    finishedList.Add(oldTranslation);
                }
            }

            // Going in the order it was found in the source file seems like useless pedantry, but I suppose it's better than hash-order
            foreach (string key in keyOrderPerNewTranslations)
            {
                if (!alreadyTranslatedStrings.Contains(key))
                {
                    // keyToSourceStringMap[key] exists because we threw the "The incoming translation file contains translations..." exception above if it wasn't there.
                    // keyToNewTranslationMap[key] exists because ReadIncomingTranslationFile produced it and guarantees it.
                    finishedList.Add(new TranslationEntry(keyToSourceStringMap[key], keyToNewTranslationMap[key], settings.Author, ingestionDate));
                }
            }

            sdvTranslator.WriteTranslationEntryList(locale, settings.SourceRoot, finishedList);

            Console.WriteLine($"Ingested \"{settings.TranslationPath}\" into {settings.SourceRoot}");
            return 0;
        }
    }
}
