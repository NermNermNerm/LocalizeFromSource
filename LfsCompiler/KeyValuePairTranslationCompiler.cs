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

        protected abstract IEnumerable<string> GetLocalesWithTranslations();
        protected abstract string I18nBuildOutputFolder { get; }
        protected abstract string I18nSourceFolder { get; }
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

            foreach (var discoveredString in this.GetLegacyStrings())
            {
                string sdvFormatString = discoveredString.isFormat ? SdvTranslator.TransformCSharpFormatStringToSdvFormatString(discoveredString.localizedString) : discoveredString.localizedString;
                // If a string is both discovered and coming from the legacy source, we'll prefer the key from the legacy source.
                foundStringMap[sdvFormatString] = discoveredString;
            }

            Dictionary<string, string> sourceStringToKeyMap = new();
            Dictionary<string, string> keyToSourceStringMap = new();
            foreach (var (sourceString,discoveredString) in foundStringMap)
            {
                var key = discoveredString.key ?? GenerateUniqueKeyForSourceString(sourceString);
                sourceStringToKeyMap[sourceString] = key;
                keyToSourceStringMap[key] = sourceString;
            }

            var keysInOrder = keyToSourceStringMap.Keys.OrderBy(key =>
            {
                var discoveredString = foundStringMap[keyToSourceStringMap[key]];
                return $"{discoveredString.file ?? ""}:{discoveredString.line ?? 0:8d}";
            }).ToList();
            var lastKey = keysInOrder.LastOrDefault();
            string? headCommit = this.Config.GetHeadCommit();

            Directory.CreateDirectory(this.I18nBuildOutputFolder);
            using (var writer = new StreamWriter(File.Create(this.GetPathToBuildOutputForLocale(locale: null))))
            {
                writer.WriteLine("// This file is generated - and the only reason to change it is if you are a translator.");
                writer.WriteLine("// To translate this to another language, copy it to the language file name (e.g. 'es.json'");
                writer.WriteLine("// for a Spanish translation), and replace all the English values with translated ones.");
                writer.WriteLine("// Test your translations in-game and send this file back to the author so they can integrate");
                writer.WriteLine("// and publish it.  Instructions on how to integrate it into the source repo can be found here:");
                writer.WriteLine("// https://github.com/NermNermNerm/LocalizeFromSource?tab=readme-ov-file#ingesting-translations");
                writer.WriteLine("//");
                if (headCommit is not null)
                {
                    writer.WriteLine($"// Built from commit: {headCommit}  !! DO NOT DELETE THIS LINE !!");
                }
                writer.WriteLine("{");
                foreach (var key in keysInOrder)
                {
                    string sourceString = keyToSourceStringMap[key];
                    var discoveredString = foundStringMap[sourceString];
                    Uri? link = this.Config.TryMakeGithubLink(discoveredString.file, discoveredString.line);
                    if (link is not null)
                    {
                        writer.WriteLine($"    // {link}");
                    }
                    writer.Write($"    {JsonSerializer.Serialize(key, this.JsonWriterOptions)}: {JsonSerializer.Serialize(sourceString, this.JsonWriterOptions)}");
                    if (key != lastKey)
                    {
                        writer.WriteLine(",");
                    }
                    writer.WriteLine();
                }
                writer.WriteLine("}");
            }

            foreach (string locale in this.GetLocalesWithTranslations())
            {
                var translations = this.ReadTranslationEntryList(locale);
                var sourceStringToTranslationsMap = translations.ToDictionary(e => e.source);

                using (var writer = new StreamWriter(File.Create(this.GetPathToBuildOutputForLocale(locale))))
                {
                    writer.WriteLine("// Please help make this translation better!  Search for '>>>' to find known issues with the");
                    writer.WriteLine("// translation.  Be careful to remove the '//' before the generated on missing translations!");
                    writer.WriteLine("// When you've corrected everything and tested it locally, send your copy of this file back to");
                    writer.WriteLine("// mod author for integration into the mod.  Instructions on how to integrate it into the source");
                    writer.WriteLine("// repository can be found here:");
                    writer.WriteLine("// https://github.com/NermNermNerm/LocalizeFromSource?tab=readme-ov-file#ingesting-translations");
                    writer.WriteLine("//");
                    if (headCommit is not null)
                    {
                        writer.WriteLine($"// Built from commit: {headCommit}  !! DO NOT DELETE THIS LINE !!");
                    }
                    writer.WriteLine("{");
                    foreach (var key in keysInOrder)
                    {
                        string sourceString = keyToSourceStringMap[key];
                        var discoveredString = foundStringMap[sourceString];

                        Uri? link = this.Config.TryMakeGithubLink(discoveredString.file, discoveredString.line);
                        if (link is not null)
                        {
                            writer.WriteLine($"    // {link}");
                        }

                        if (sourceStringToTranslationsMap.TryGetValue(sourceString, out var translation))
                        {
                            if (translation.IsMachineGenerated)
                            {
                                writer.WriteLine($"    // >>>MACHINE GENERATED by {translation.author} on {translation.ingestionDate:d}");
                            }
                            else
                            {
                                writer.WriteLine($"    // Translated by {translation.author} on {translation.ingestionDate:d}");
                            }
                            writer.WriteLine($"    // source: {JsonSerializer.Serialize(sourceString, this.JsonWriterOptions)}");
                        }
                        else
                        {
                            var closeMatch = FindNearestSourceString(sourceString, sourceStringToTranslationsMap.Keys, minimumScore: 90);
                            if (closeMatch is not null)
                            {
                                translation = sourceStringToTranslationsMap[closeMatch];
                                writer.WriteLine($"    // >>>SOURCE STRING CHANGED - originally translated by {translation.author} on {translation.ingestionDate:d}");
                                writer.WriteLine($"    // old: {JsonSerializer.Serialize(closeMatch, this.JsonWriterOptions)}");
                                writer.WriteLine($"    // new: {JsonSerializer.Serialize(sourceString, this.JsonWriterOptions)}");
                            }
                            else
                            {
                                writer.WriteLine($"    // >>>MISSING TRANSLATION");
                                writer.WriteLine($"    // source: {JsonSerializer.Serialize(sourceString, this.JsonWriterOptions)}");
                                // Missing Translation comment
                            }
                        }

                        if (translation is not null)
                        {
                            writer.WriteLine($"    {JsonSerializer.Serialize(key, this.JsonWriterOptions)}: {JsonSerializer.Serialize(translation.translation, this.JsonWriterOptions)}{(key == lastKey ? "" : ",")}");
                        }
                        else
                        {
                            writer.WriteLine($"    // {JsonSerializer.Serialize(key, this.JsonWriterOptions)}: \"\"{(key == lastKey ? "" : ",")}");
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


        public void IngestTranslatedFile(string translationPath, string author)
        {
            DateTime ingestionDate = DateTime.Now;
            Dictionary<string, string> keyToSourceStringMap = this.ReadKeyToSourceMapFile();
            string locale = this.GetLocaleOfIncomingTranslationFile(translationPath);
            (var keyToNewTranslationMap, var keyOrderPerNewTranslations, var commitTranslationWasBuiltFrom) = this.ReadIncomingTranslationFile(translationPath);
            var oldTranslationEntryList = this.ReadTranslationEntryList(locale);

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
                    finishedList.Add(new TranslationEntry(oldTranslation.source, newTranslation, author, ingestionDate));
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
                string sourceString = keyToSourceStringMap[key];
                if (!alreadyTranslatedStrings.Contains(sourceString))
                {
                    // keyToSourceStringMap[key] exists because we threw the "The incoming translation file contains translations..." exception above if it wasn't there.
                    // keyToNewTranslationMap[key] exists because ReadIncomingTranslationFile produced it and guarantees it.
                    finishedList.Add(new TranslationEntry(sourceString, keyToNewTranslationMap[key], author, ingestionDate));
                }
            }

            this.WriteTranslationEntryList(locale, this.Config.ProjectPath, finishedList);
        }


        private static readonly SHA256 sha256 = SHA256.Create();

        private static string GenerateUniqueKeyForSourceString(string input)
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            string base64String = Convert.ToBase64String(bytes);

            // The character set used by the above is A-Z, a-z, 0-9, + and /.  In order to make nice looking
            // JSON and to make it compatible with SMAPI's generator, we want to convert this into a valid
            // C# identifier.  We're also happy to just use part of the string to make it not such a long pile
            // of gibberish.

            StringBuilder result = new StringBuilder();
            foreach (char c in base64String)
            {
                if (char.IsLetter(c) || (result.Length > 0 && char.IsDigit(c)))
                {
                    result.Append(c);
                    if (result.Length == 10)
                    {
                        break;
                    }
                }
            }

            return result.ToString();
        }

        protected virtual JsonSerializerOptions JsonReaderOptions => CombinedConfig.JsonReaderOptions;

        protected virtual JsonSerializerOptions JsonWriterOptions => CombinedConfig.JsonReaderOptions;

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
            => Path.Combine(this.I18nSourceFolder, locale + ".json");

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

            writer.WriteLine("// Do not manually edit this file!  Follow these instructions to import changes:");
            writer.WriteLine("// https://github.com/NermNermNerm/LocalizeFromSource?tab=readme-ov-file#ingesting-translations");
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
            using (JsonDocument doc = JsonDocument.Parse(content, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip }))
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
