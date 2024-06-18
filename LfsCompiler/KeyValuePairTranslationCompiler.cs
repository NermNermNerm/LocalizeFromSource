using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using FuzzySharp;
using NermNermNerm.Stardew.LocalizeFromSource;

namespace LocalizeFromSource
{
    /// <summary>
    ///   This is the base class for translation compilers that store their translations in
    ///   tables where there's a key that's used to look up the translations in source and
    ///   target languages.
    /// </summary>
    public abstract class KeyValuePairTranslationCompiler
        : TranslationCompiler
    {
        protected KeyValuePairTranslationCompiler(CombinedConfig config)
        {
            this.Config = config;
        }

        public CombinedConfig Config { get; }

        protected abstract IEnumerable<string> GetActiveLocales();
        protected abstract string I18nBuildOutputFolder { get; }
        protected abstract string GetPathToBuildOutputForLocale(string? locale);

        public override bool GenerateI18nFiles(IReadOnlyCollection<DiscoveredString> discoveredStrings)
        {
            this.anyErrorsReported = false;

            // Note that discoveredStrings might contain the same string twice, discovered at different points in the code.
            //  We're only going to look at one of them.
            Dictionary<string, DiscoveredString> foundStringMap = new();
            foreach (var discoveredString in discoveredStrings)
            {
                string sdvFormatString = discoveredString.isFormat ? SdvTranslator.TransformCSharpFormatStringToSdvFormatString(discoveredString.localizedString) : discoveredString.localizedString;
                foundStringMap[sdvFormatString] = discoveredString;
            }

            Dictionary<string, string> sourceStringToKeyMap = new();
            Dictionary<string, string> keyToSourceStringMap = new();
            foreach (var (sourceString,discoveredString) in foundStringMap)
            {
                var key = GenerateUniqueKeyForSourceString(sourceString);
                sourceStringToKeyMap[sourceString] = key;
                keyToSourceStringMap[key] = sourceString;
            }

            var keysInOrder = keyToSourceStringMap.Keys.OrderBy(key =>
            {
                var discoveredString = foundStringMap[keyToSourceStringMap[key]];
                return $"{discoveredString.file ?? ""}:{discoveredString.line ?? 0:8d}";
            }).ToList();
            var lastKey = keysInOrder.LastOrDefault();

            Directory.CreateDirectory(this.I18nBuildOutputFolder);
            using (var writer = new StreamWriter(File.OpenWrite(this.GetPathToBuildOutputForLocale(locale: null))))
            {
                writer.WriteLine("// TODO: Notes on how to use it");
                writer.WriteLine("// TODO: Record the commit");
                writer.WriteLine("{");
                foreach (var key in keysInOrder)
                {
                    string sourceString = keyToSourceStringMap[key];
                    var discoveredString = foundStringMap[sourceString];
                    Uri? link = this.Config.TryMakeGithubLink(discoveredString.file, discoveredString.line);
                    if (link is null)
                    {
                        writer.WriteLine($"   // ? could not find associated source file ?");
                    }
                    else
                    {
                        writer.WriteLine($"    // {link}");
                    }
                    writer.Write($"   {JsonSerializer.Serialize(key, this.JsonWriterOptions)}: {JsonSerializer.Serialize(sourceString)}");
                    if (key != lastKey)
                    {
                        writer.WriteLine(",");
                    }
                    writer.WriteLine();
                }
                writer.WriteLine("}");
            }

            foreach (string locale in this.GetActiveLocales())
            {
                var translations = this.ReadTranslationEntryList(locale);
                var sourceStringToTranslationsMap = translations.ToDictionary(e => e.source);

                using (var writer = new StreamWriter(File.OpenWrite(this.GetPathToBuildOutputForLocale(locale: null))))
                {
                    writer.WriteLine("// TODO: Notes on how to use it");
                    writer.WriteLine("// TODO: Record the commit");
                    writer.WriteLine("{");
                    foreach (var key in keysInOrder)
                    {
                        string sourceString = keyToSourceStringMap[key];
                        if (sourceStringToTranslationsMap.TryGetValue(sourceString, out var translation))
                        {
                            if (translation.IsMachineGenerated)
                            {
                                // Machine generated comment
                                writer.WriteLine($"    // >>>MACHINE GENERATED by {translation.author}");
                            }
                            else
                            {
                                writer.WriteLine($"    // Translated by {translation.author}");
                            }
                        }
                        else
                        {
                            var closeMatch = FindNearestSourceString(sourceString, sourceStringToKeyMap.Keys, minimumScore: 90);
                            if (closeMatch is not null)
                            {
                                translation = sourceStringToTranslationsMap[closeMatch];
                                writer.WriteLine($"    // >>>SOURCE STRING CHANGED - originally translated by {translation.author}");
                                writer.WriteLine($"    //      old source string: {JsonSerializer.Serialize(sourceString, this.JsonWriterOptions)}");
                            }
                            else
                            {
                                writer.WriteLine($"    // >>>MISSING TRANSLATION");
                                // Missing Translation comment
                            }
                        }
                        writer.WriteLine($"    // source language string: {JsonSerializer.Serialize(sourceString, this.JsonWriterOptions)}");

                        if (translation is not null)
                        {
                            writer.WriteLine($"   {JsonSerializer.Serialize(key, this.JsonWriterOptions)}: {JsonSerializer.Serialize(translation.translation, this.JsonWriterOptions)}{(key == lastKey ? "" : ",")}");
                        }
                        else
                        {
                            writer.WriteLine($"   // {JsonSerializer.Serialize(key, this.JsonWriterOptions)}: \"\"{(key == lastKey ? "" : ",")}");
                        }

                        if (key != lastKey)
                        {
                            writer.WriteLine();
                        }
                    }
                    writer.WriteLine("}");
                }
            }

            return !this.anyErrorsReported;
        }

        private static readonly SHA256 sha256 = SHA256.Create();

        private static string GenerateUniqueKeyForSourceString(string input)
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            string base64String = Convert.ToBase64String(bytes);
            return base64String.Substring(0, 10);
        }

        protected virtual JsonSerializerOptions JsonReaderOptions => new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        protected virtual JsonSerializerOptions JsonWriterOptions => new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };


        public static void WriteJsonDictionary<TValue>(string path, Dictionary<string, TValue> dictionary, Func<string,string> stringToSortOrder, string? prefix)
        {
            var writeOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                Converters = { new SortedDictionaryJsonConverter<TValue>(stringToSortOrder) }
            };
            var content = (prefix is null ? "" : prefix + Environment.NewLine) + JsonSerializer.Serialize(dictionary, writeOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private Func<string,string> GenerateKeySortFunction(Dictionary<string, string> newDefaultJson, Dictionary<string,DiscoveredString> stringToDiscoveredString)
        {
            // newDefaultJson is key->string
            // stringToDiscoveredString is string->DiscoveredString
            // function is key->sortOrder
            return (key) =>
            {
                var sourceLangStr = newDefaultJson[key];
                var discoveredString = stringToDiscoveredString[sourceLangStr];
                return $"{discoveredString.file ?? ""}:{discoveredString.line ?? 0:8d}";
            };
        }


        private static string? FindNearestSourceString(string givenSourceString, IEnumerable<string> candidates, int minimumScore)
            => candidates
                .Select(s => new KeyValuePair<int,string>(Fuzz.TokenSetRatio(givenSourceString, s), s))
                .Where(pair => pair.Key > minimumScore)
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .FirstOrDefault();

        private record TranslationTable(List<TranslationEntry> translations);

        protected virtual string GetPathToTranslationEntries(string locale, string sourceFolder)
            => Path.Combine(sourceFolder, "i18nSource", locale + ".json");

        public virtual List<TranslationEntry> ReadTranslationEntryList(string locale)
        {
            var translationEntryListPath = this.GetPathToTranslationEntries(locale, this.Config.ProjectPath);
            if (File.Exists(translationEntryListPath))
            {
                try
                {
                    // This file format is not target-specific
                    var fullContent = JsonSerializer.Deserialize<TranslationTable>(File.ReadAllText(translationEntryListPath), this.JsonReaderOptions);
                    return fullContent?.translations ?? throw new JsonException("null 'translation' entry is not allowed");
                }
                catch (Exception ex)
                {
                    throw new FatalErrorException($"Could not read {translationEntryListPath}: {ex.Message}", TranslationCompiler.BadFile, ex);
                }
            }
            else
            {
                return new();
            }
        }

        public virtual void WriteTranslationEntryList(string locale, string sourceFolder, List<TranslationEntry> entries)
        {
            string content = JsonSerializer.Serialize(new TranslationTable(entries), this.JsonWriterOptions);

            var translationEntryListPath = this.GetPathToTranslationEntries(locale, sourceFolder);
            if (Path.GetDirectoryName(translationEntryListPath) is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(translationEntryListPath)!);
            }

            using StreamWriter writer = new StreamWriter(File.OpenWrite(translationEntryListPath));

            writer.WriteLine("// Do not manually edit this file!");
            writer.WriteLine("// Instead, collect updates to the translation files distributed with your package and");
            writer.WriteLine("// use the tooling to merge the changes like this:");
            writer.WriteLine("//");
            writer.WriteLine("// msbuild /target:IngestTranslations /p:TranslatedFile=<path-to-file>.json;TranslationAuthor=<author-id>");
            writer.WriteLine("//");
            writer.WriteLine("// Where 'author-id' is '<platform>:<moniker>' where '<platform>' is something like 'nexus' or 'github' and");
            writer.WriteLine("// '<id>' is the identity of the person who supplied the translations on that platform.");
            writer.WriteLine(content);
        }

        public virtual string GetLocaleOfIncomingTranslationFile(string translationPath)
        {
            string filename = Path.GetFileNameWithoutExtension(translationPath);
            Regex localePattern = new Regex(@"^[a-z][a-z](-[a-z]+[a-z]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!localePattern.IsMatch(filename))
            {
                throw new FatalErrorException($"The filename of the ingested translation ({Path.GetFileNameWithoutExtension(translationPath)}) file should be its language or locale.", TranslationCompiler.BadFile);
            }
            return filename.ToLowerInvariant();
        }

        public virtual (Dictionary<string,string> keyToTranslationMap, List<string> keyOrder, string commitTranslationWasBuiltFrom)
            ReadIncomingTranslationFile(string translationPath)
        {
            string? commitTranslationFileWasBuiltFrom = null;
            try
            {
                // This method of fetching the commit is sdv-specific
                var newTranslationFileContents = File.ReadAllText(translationPath);
                var m = new Regex(@"//\s+Built from commit:\s+(?<commit>[a-z0-9]{40})\b", RegexOptions.CultureInvariant).Match(newTranslationFileContents);
                if (!m.Success)
                {
                    Console.Error.WriteLine($"lfscompiler - warning {TranslationCompiler.ErrorPrefix}{TranslationCompiler.MungedTranslationFile:d4}: {translationPath} no longer has the comment on which commit built the file they used as a basis of the translation.");
                    commitTranslationFileWasBuiltFrom = "<unknown>";
                }
                else
                {
                    commitTranslationFileWasBuiltFrom = m.Groups["commit"].Value;
                }

                // The file format is sdv-specific
                var (map, order) = this.ParseInOrder<string>(newTranslationFileContents);
                return (map, order, commitTranslationFileWasBuiltFrom);
            }
            catch (Exception ex)
            {
                throw new FatalErrorException($"Could not read {translationPath}: {ex.Message}", TranslationCompiler.BadFile, ex);
            }
        }


        /// <summary>
        ///   Reads build-output file that maps between generated keys and the source string they are associated with.
        /// </summary>
        public abstract Dictionary<string, string> ReadKeyToSourceMapFile();

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
    }
}
