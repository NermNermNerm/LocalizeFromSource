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

            [Description("If set, trust that the translation is complete even if there were changes in the source without corresponding changes in the translation.")]
            [CommandOption("--trust")]
            public bool IsTrusted { get; init; } = false;

            public override ValidationResult Validate()
            {
                if (!string.IsNullOrEmpty(this.TranslationPath) && !File.Exists(this.TranslationPath))
                {
                    return ValidationResult.Error($"The path specified with --translation does not exist: {this.TranslationPath}");
                }

                if (!string.IsNullOrEmpty(this.TranslationPath) && !localePattern.IsMatch(Path.GetFileName(this.TranslationPath)))
                {
                    return ValidationResult.Error($"The path specified with --translation should include the locale in the name, like 'de.json' or 'es-mx.json': {this.TranslationPath}");
                }

                if (this.SourceRoot is null)
                {
                    return ValidationResult.Error("The --sourceRoot parameter must be supplied.");
                }
                if (!Directory.Exists(this.SourceRoot))
                {
                    return ValidationResult.Error($"The directory specified with --sourceRoot does not exist: {this.SourceRoot}");
                }

                return ValidationResult.Success();
            }
        }

        private static readonly JsonSerializerOptions ReaderOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

        public override int Execute(CommandContext context, Settings settings)
        {
            // TODO: Share this with BuildI18nCommand if this proves useful at all.
            //string configPath = Path.Combine(settings.SourceRoot, "LocalizeFromSourceConfig.json");
            //Config? userConfig = new Config();
            //if (File.Exists(configPath))
            //{
            //    try
            //    {
            //        var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
            //        options.Converters.Add(new RegexJsonConverter());
            //        userConfig = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath), options);
            //        if (userConfig is null)
            //        {
            //            throw new JsonException("null is not expected");
            //        }
            //    }
            //    catch(Exception ex)
            //    {
            //        Console.Error.WriteLine($"error {TranslationCompiler.ErrorPrefix}{TranslationCompiler.BadConfigFile:d4}: {ex.Message}");
            //        return 1;
            //    }
            //}

            var translationPath = settings.TranslationPath;
            if (string.IsNullOrEmpty(translationPath))
            {
                translationPath = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter the path to the translation file:")
                    .PromptStyle("green")
                    .Validate(path =>
                    {
                        if (!File.Exists(path))
                        {
                            return ValidationResult.Error("File does not exist.");
                        }
                        else if (!localePattern.IsMatch(Path.GetFileName(path)))
                        {
                            return ValidationResult.Error("The path should be a file with the locale in the name, like 'de.json' or 'es-mx.json'");
                        }
                        else
                        {
                            return ValidationResult.Success();
                        }
                    }));
            }

            var match = localePattern.Match(Path.GetFileName(translationPath));
            string locale = match.Groups["locale"].Value;

            Dictionary<string, string> keyToNewTranslationMap;
            try
            {
                keyToNewTranslationMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settings.TranslationPath), ReaderOptions)
                    ?? throw new JsonException("file must not be just 'null'");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: Could not read {translationPath}: {ex.Message}[/]");
                return 1;
            }

            // TODO: parse out the commit ID out of the comments of 'settings.TranslationPath' and validate that's the commit we're on.

            Dictionary<string, string> keyToSourceStringMap;
            string keyToSourceStringFile = Path.Combine(settings.SourceRoot, "i18n", "default.json");
            try
            {
                // vv -- That path is SDV-specific.
                keyToSourceStringMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keyToSourceStringFile), ReaderOptions)
                    ?? throw new JsonException("File should not contain just null");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: Could not read {keyToSourceStringFile}: {ex.Message}[/]");
                return 1;
            }

            Dictionary<string, string> keyToOldTranslationMap;
            string keyToOldTranslationPath = Path.Combine(settings.SourceRoot, "i18n", locale + ".json");
            if (!File.Exists(keyToOldTranslationPath))
            {
                keyToOldTranslationMap = new Dictionary<string, string>();
            }
            else
            {
                try
                {
                    // vv -- That path is SDV-specific.  So is the file format.
                    keyToOldTranslationMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keyToOldTranslationPath), ReaderOptions)
                        ?? throw new JsonException("File should not contain just null");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: Could not read {keyToOldTranslationPath}: {ex.Message}[/]");
                    return 1;
                }
            }

            Dictionary<string, TranslationEntry> keyToTranslationEntryMap;
            // vv -- "i18nSource" subject to sdv preferences.
            string keyToTranslationEntryPath = Path.Combine(settings.SourceRoot, "i18nSource", locale + ".json");
            if (!File.Exists(keyToTranslationEntryPath))
            {
                keyToTranslationEntryMap = new Dictionary<string, TranslationEntry>();
            }
            else
            {
                try
                {
                    keyToTranslationEntryMap = JsonSerializer.Deserialize<Dictionary<string, TranslationEntry>>(File.ReadAllText(keyToTranslationEntryPath), ReaderOptions)
                        ?? throw new JsonException("File should not contain just null");
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteLine($"[red]Error: Could not read {keyToTranslationEntryPath}: {ex.Message}[/]");
                    return 1;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(keyToTranslationEntryPath)!);
            var newTranslationEntries = this.IngestTranslations(keyToSourceStringMap, keyToOldTranslationMap, keyToTranslationEntryMap, keyToNewTranslationMap);

            var writeOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                // Somehow have to write them in the order that they appear in default.json
                /* Converters = { new SortedDictionaryJsonConverter<TValue>(stringToSortOrder) } */
            };
            File.WriteAllText(keyToTranslationEntryPath, JsonSerializer.Serialize(newTranslationEntries, writeOptions));

            Console.WriteLine($"Ingesting {settings.TranslationPath} into {settings.SourceRoot}");
            return 0;
        }

        private Dictionary<string, TranslationEntry> IngestTranslations(
            Dictionary<string, string> keyToSourceStringMap,
            Dictionary<string, string> keyToOldTranslationMap,
            Dictionary<string, TranslationEntry> keyToOldTranslationEntryMap,
            Dictionary<string, string> keyToNewTranslationMap)
        {
            return new Dictionary<string, TranslationEntry>();
        }
    }
}
