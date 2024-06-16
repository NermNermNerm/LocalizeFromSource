using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Xml.Schema;
using Mono.Cecil;
using NermNermNerm.Stardew.LocalizeFromSource;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    [Description("Reads translation files written by translators and builds a dataset that the BuildI18n command will later use to generate translations that the app will use.")]
    public class IngestTranslationsCommand : Command<IngestTranslationsCommand.Settings>
    {
        private readonly static Regex localePattern = new Regex(@"^(?<locale>[a-z][a-z](-[a-z]+[a-z]+)?)\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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



            //[Description("If set, trust that the translation is complete even if there were changes in the source without corresponding changes in the translation.")]
            //[CommandOption("--trust")]
            //public bool IsTrusted { get; init; } = false;

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

                if (!localePattern.IsMatch(Path.GetFileName(this.TranslationPath)))
                {
                    return ValidationResult.Error($"The path specified with --translation should have its filename be the locale, like 'de.json' or 'es-mx.json': {this.TranslationPath}");
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
            Dictionary<string, string> keyToSourceStringMap;
            // This path is SDV-specific.
            string keyToSourceStringFile = Path.Combine(settings.SourceRoot, "i18n", "default.json");
            try
            {
                // This file format is sdv-specific.
                keyToSourceStringMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keyToSourceStringFile), ReaderOptions)
                    ?? throw new JsonException("File should not contain just null");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: Could not read {keyToSourceStringFile}: {ex.Message}[/]");
                return 1;
            }

            // The fact that the locale comes from the filename is sdv-specific
            string translationPath = settings.TranslationPath;
            var match = localePattern.Match(Path.GetFileName(translationPath));
            string locale = match.Groups["locale"].Value;

            Dictionary<string, string> keyToNewTranslationMap;
            List<string> keyOrderPerNewTranslations;
            string? commitTranslationFileWasBuiltFrom = null;
            try
            {
                // This method of fetching the commit is sdv-specific
                var newTranslationFileContents = File.ReadAllText(translationPath);
                var m = new Regex(@"//\s+Built from commit:\s+(?<commit>[a-z0-9]{40})\b", RegexOptions.CultureInvariant).Match(newTranslationFileContents);
                if (!m.Success)
                {
                    Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.MungedTranslationFile:d4}: {settings.TranslationPath} no longer has the comment on which commit built the file they used as a basis of the translation.");
                    commitTranslationFileWasBuiltFrom = "<unknown>";
                }
                else
                {
                    commitTranslationFileWasBuiltFrom = m.Groups["commit"].Value;
                }

                // The file format is sdv-specific
                (keyToNewTranslationMap, keyOrderPerNewTranslations) = this.ParseInOrder<string>(newTranslationFileContents);
                keyToNewTranslationMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(translationPath), ReaderOptions)
                    ?? throw new JsonException("file must not be just 'null'");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: Could not read {translationPath}: {ex.Message}[/]");
                return 1;
            }

            // vv -- "i18nSource" subject to sdv preferences.
            string i18nSourceFolder = Path.Combine(settings.SourceRoot, "i18nSource");
            Directory.CreateDirectory(i18nSourceFolder);

            string keyToTranslationEntryPath = Path.Combine(i18nSourceFolder, locale + ".json");
            Dictionary<string, TranslationEntry> keyToOldTranslationEntryMap = new Dictionary<string, TranslationEntry>();
            List<string> keyOrderPerOldTranslationEntries = new List<string>();
            if (File.Exists(keyToTranslationEntryPath))
            {
                try
                {
                    // This file format is not target-specific
                    string oldContent = File.ReadAllText(keyToTranslationEntryPath);
                    (keyToOldTranslationEntryMap, keyOrderPerOldTranslationEntries) = this.ParseInOrder<TranslationEntry>(oldContent);
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteLine($"[red]Error: Could not read {keyToTranslationEntryPath}: {ex.Message}[/]");
                    return 1;
                }
            }

            // The idea is to minimize the churn in this file

            // Discard keys that are no longer present so they don't confound us
            keyOrderPerNewTranslations.RemoveAll(k => !keyToSourceStringMap.ContainsKey(k));
            keyOrderPerOldTranslationEntries.RemoveAll(k => !keyToSourceStringMap.ContainsKey(k));
            List<string> newKeyOrder = GetOrder(keyOrderPerNewTranslations, keyOrderPerOldTranslationEntries);

            Dictionary<string, TranslationEntry> newEntries = new();
            foreach (var key in newKeyOrder)
            {
                keyToOldTranslationEntryMap.TryGetValue(key, out var oldTranslationEntry);
                keyToNewTranslationMap.TryGetValue(key, out string? newTranslation);

                TranslationEntry toWrite;
                if (newTranslation is null)
                {
                    Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.IncompleteTranslation:d4}: The translations given for {locale} lack a translation for '{key}'");
                    toWrite = oldTranslationEntry ?? throw new Exception("Key should not be missing from both new and old entry files");
                }
                else if (oldTranslationEntry is not null
                    && oldTranslationEntry.translation == newTranslation
                    && !oldTranslationEntry.author.StartsWith("automation:"))
                {
                    if (oldTranslationEntry.source != keyToSourceStringMap[key])
                    {
                        Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.IncompleteTranslation:d4}: The translations given for {locale} did not update the translation for '{key}' -- perhaps it just didn't need updating?");
                    }
                    toWrite = oldTranslationEntry;
                }
                else
                {
                    toWrite = new TranslationEntry(keyToSourceStringMap[key], newTranslation, settings.Author, ingestionDate);
                }

                newEntries.Add(key, toWrite);
            }

            string prefix = @"// Do not manually edit this file!
// Instead, collect updates to the translation files distributed with your package and
// use the tooling to merge the changes like this:
//
// msbuild /target:IngestTranslations /p:TranslatedFile=<path-to-file>.json;TranslationAuthor=<author-id>
//
// Where 'author-id' is '<platform>:<moniker>' where '<platform>' is something like 'nexus' or 'github' and
// '<id>' is the identity of the person who supplied the translations on that platform.";
            KeyValuePairTranslationCompiler.WriteJsonDictionary(keyToTranslationEntryPath, newEntries, key => newKeyOrder.IndexOf(key).ToString("8d"), prefix);

            Console.WriteLine($"Ingested \"{translationPath}\" into {settings.SourceRoot} - it can be deleted now");

            return 0;
        }

        private static List<string> GetOrder(List<string> keyOrderPerNewTranslations, List<string> keyOrderPerOldTranslationEntries)
        {
            List<string> newKeyOrder = new();
            List<string> tackOntoTheEnd = new();
            int nextToTakeIndex = 0;
            for (int indexInOldTranslations = 0; indexInOldTranslations < keyOrderPerNewTranslations.Count; ++indexInOldTranslations)
            {
                string key = keyOrderPerNewTranslations[indexInOldTranslations];

                // Look for a sequence of keys that are not in the the old translation map
                int endOfString = nextToTakeIndex;
                while (endOfString < keyOrderPerNewTranslations.Count && !keyOrderPerOldTranslationEntries.Contains(keyOrderPerNewTranslations[endOfString]))
                {
                    ++endOfString;
                }

                // copy up to endOfString into newKeyOrder if it fits into our ordering, else use tackOntoTheEnd.
                if (endOfString > nextToTakeIndex
                 && endOfString < keyOrderPerNewTranslations.Count
                 && indexInOldTranslations + 1 < keyOrderPerNewTranslations.Count
                 && keyOrderPerNewTranslations[endOfString] == keyOrderPerOldTranslationEntries[indexInOldTranslations + 1])
                {
                    // We have an intervening sequence - copy it in
                    for (; nextToTakeIndex < endOfString; ++nextToTakeIndex)
                    {
                        newKeyOrder.Add(keyOrderPerNewTranslations[nextToTakeIndex]);
                    }
                }
                else
                {
                    for (; nextToTakeIndex < endOfString; ++nextToTakeIndex)
                    {
                        tackOntoTheEnd.Add(keyOrderPerNewTranslations[nextToTakeIndex]);
                    }
                }

                newKeyOrder.Add(key);
            }

            for (; nextToTakeIndex < keyOrderPerNewTranslations.Count; ++nextToTakeIndex)
            {
                newKeyOrder.Add(keyOrderPerNewTranslations[nextToTakeIndex]);
            }

            return newKeyOrder;
        }

        private static string EscapeString(string value)
        {
            return JsonSerializer.Serialize(value).Trim('"');
        }
    
        private (Dictionary<string,T> map, List<string> keyOrder) ParseInOrder<T>(string content)
        {
            var keyValuePairs = new List<KeyValuePair<string, string>>();
            Dictionary<string, T> map = new();
            List<string> keyOrder = new();
            using (JsonDocument doc = JsonDocument.Parse(content))
            {
                foreach (JsonProperty property in doc.RootElement.EnumerateObject())
                {
                    var value = property.Value.Deserialize<T>();
                    if (value is null)
                    {
                        throw new JsonException($"Value for '{property.Name}' should not be null");
                    }

                    keyOrder.Add(property.Name);
                    map.Add(property.Name, value);
                }
            }

            return (map, keyOrder);
        }

        private Dictionary<string, TranslationEntry> IngestTranslations(
            string locale,
            string preferredCommit,
            Dictionary<string, string> keyToSourceStringMap,
            Dictionary<string, TranslationEntry> keyToOldTranslationEntryMap,
            Dictionary<string, string> keyToNewTranslationMap,
            string author)
        {
            DateTime ingestionDate = DateTime.Now;

            if (string.Join("|", keyToSourceStringMap.Keys.Order()) != string.Join("|", keyToNewTranslationMap.Keys.Order()))
            {
                Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.IncompatibleSource:d4}: The translation keys from the imported translation aren't the same as what was just built.  It could be that the translator missed some of the strings, or it could also be because the code has changed.  When ingesting translations, you should branch from the release commit - in this case '{preferredCommit}'.  Doing so will produce the most accurate translation comments.");
            }

            Dictionary<string, TranslationEntry> newTranslationEntryMap = new();
            foreach (var pair in keyToSourceStringMap)
            {
                string key = pair.Key;
                string sourceString = pair.Value;

                keyToOldTranslationEntryMap.TryGetValue(key, out var oldTranslationEntry);
                keyToNewTranslationMap.TryGetValue(key, out string? newTranslation);

                if (newTranslation is null)
                {
                    Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.IncompleteTranslation:d4}: The translations given for {locale} lack a translation for '{key}'");
                }
                else if (oldTranslationEntry is not null
                        && oldTranslationEntry.translation == newTranslation)
                {
                    if (oldTranslationEntry.source != sourceString)
                    {
                        Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.IncompleteTranslation:d4}: The translations given for {locale} did not update the translation for '{key}' -- perhaps it just didn't need updating?");
                    }
                    newTranslationEntryMap.Add(key, oldTranslationEntry);
                }
                else
                {
                    newTranslationEntryMap.Add(key, new TranslationEntry(sourceString, newTranslation, author, ingestionDate));
                }
            }

            return newTranslationEntryMap;
        }
    }
}
