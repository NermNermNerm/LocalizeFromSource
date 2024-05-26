﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FuzzySharp;

namespace LocalizeFromSource
{
    public class SdvTranslationCompiler
        : TranslationCompiler
    {
        private record SourceChange(string? key, string? oldString, string? newString);

        private static Regex formatStringRegex = new(@"{(?<argNumber>\d+)(?<formatSpecifier>:[^}]+)}(\|(?<argName>\w+)\|)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static string transformCSharpFormatStringToSdvFormatString(DiscoveredString csharpFormatString)
        {
            // we pass the full DiscoveredString in case we want to complain about something.
            return formatStringRegex.Replace(csharpFormatString.localizedString, (m) =>
            {
                var number = m.Groups["argNumber"].Value;
                var name = m.Groups["argName"].Value ?? "arg"+number;
                var fmtSpecifier = m.Groups["formatSpecifier"].Value ?? ""; // note - includes the :

                return "{{" + name + fmtSpecifier + "}}";
            });
        }

        public override bool GenerateI18nFiles(string sourceProjectPath, bool verifyOnly, IReadOnlyCollection<DiscoveredString> discoveredStrings)
        {
            this.anyErrorsReported = false;

            // Note that discoveredStrings might contain the same string twice, discovered at different points in the code.
            //  We're only going to look at one of them.
            Dictionary<string, DiscoveredString> foundStringMap = new();
            foreach (var discoveredString in discoveredStrings)
            {
                string sdvFormatString = discoveredString.isFormat ? transformCSharpFormatStringToSdvFormatString(discoveredString) : discoveredString.localizedString;
                foundStringMap[sdvFormatString] = discoveredString;
            }


            string? i18nFolder = GetI18nFolder(sourceProjectPath, verifyOnly, foundStringMap.Any(), out i18nFolder);
            if (i18nFolder is null)
            {
                return false;
            }

            Dictionary<string, string>? oldKeyToStringDict = ReadDefaultJson(i18nFolder) ?? new Dictionary<string, string>();
            Dictionary<string, string> oldStringToKeyDict = this.ReverseDefaultJsonDict(oldKeyToStringDict);

            var newStrings = foundStringMap.Keys.Where(s => !oldStringToKeyDict.ContainsKey(s)).ToList();
            var deletedStrings = oldStringToKeyDict.Keys.Where(s => !foundStringMap.ContainsKey(s)).ToList();

            var matchedStringChanges = this.TryMakeMatches(newStrings, deletedStrings);
            Dictionary<string, string> newStringToKeyDict = buildSourceLanguageStringToKeyDict(oldStringToKeyDict, matchedStringChanges);
            var newDefaultJson = newStringToKeyDict.ToDictionary(p => p.Value, p => p.Key);

            var keySortOrderFunction = this.GenerateKeySortFunction(newDefaultJson, discoveredStrings);

            if (newStrings.Any() || deletedStrings.Any())
            {
                if (verifyOnly)
                {
                    this.Error(TranslationRequired, "Localized strings have been changed - rebuild locally and commit any localization changes.");
                    return false;
                }
                else
                {
                    WriteJsonDictionary(Path.Combine(i18nFolder, "default.json"), newDefaultJson, keySortOrderFunction);
                }
            }

            foreach (string locale in this.GetTranslatedLocales(i18nFolder))
            {
                string translationPath = Path.Combine(i18nFolder, locale + ".json");
                Dictionary<string, string>? translations;
                try
                {
                    translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(translationPath));
                    if (translations is null)
                    {
                        this.Error(LocaleJsonUnusable, $"{translationPath} contains 'null'");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    this.Error(LocaleJsonUnusable, $"Translation file, {locale}.json, is unreadable: {ex.Message}");
                    continue;
                }

                Dictionary<string, TranslationEdit> edits;
                string editsPath = TranslationEdit.MakePath(i18nFolder, locale);
                try
                {
                    edits = TranslationEdit.Read(editsPath);
                }
                catch (Exception ex)
                {
                    this.Error(LocaleEditsJsonUnusable, $"The translation edits file, {editsPath}, is unreadable: {ex.Message}");
                    continue;
                }

                if (verifyOnly && edits.Any(e => e.Value.newTarget is not null))
                {
                    this.Error(TranslationRequired, $"The translation edits file, {editsPath}, contains new translations that have not been applied.  Run the build locally and commit the resulting changes and resubmit the build.");
                    return false;
                }

                // Apply any new translations that are still valid
                var appliedEditKeys = new HashSet<string>();
                foreach (var edit in edits)
                {
                    if (edit.Value.newTarget is not null && newDefaultJson.TryGetValue(edit.Key, out string? currentSource) && edit.Value.newSource == currentSource)
                    {
                        translations[edit.Key] = edit.Value.newTarget;
                        appliedEditKeys.Add(edit.Key);
                    }
                }

                // Delete any translations that aren't used anymore
                foreach (var irrelevantKey in translations.Keys.Where(k => !newDefaultJson.ContainsKey(k)).ToArray())
                {
                    translations.Remove(irrelevantKey);
                }

                // Start creating the edits json file.
                var newEdits = new Dictionary<string, TranslationEdit>();
                foreach (var edit in matchedStringChanges)
                {
                    if (edit.deletedString is null && edit.newString is not null)
                    {
                        // We added a new thing to translate.
                        newEdits.Add(
                            newStringToKeyDict[edit.newString],
                            new TranslationEdit(oldSource: null, newSource: edit.newString, oldTarget: null, newTarget: null));
                        // No need to check for an old translation edit, this string is new.
                    }
                    if (edit.deletedString is not null && edit.newString is not null)
                    {
                        string key = oldStringToKeyDict[edit.deletedString];
                        if (!appliedEditKeys.Contains(key)) // false when the translator changed the source at the same time.
                        {
                            string? oldTranslatedValue;
                            string? oldSource;
                            if (edits.TryGetValue(key, out var oldEdit))
                            {
                                // This is a change to the source where the translation hasn't even caught up to the previous one yet.
                                oldSource = oldEdit.oldSource;
                                oldTranslatedValue = oldEdit.oldTarget;
                            }
                            else
                            {
                                oldSource = oldKeyToStringDict[key];
                                oldTranslatedValue = translations[key];
                                translations.Remove(key);
                            }
                            newEdits.Add(
                                key,
                                new TranslationEdit(oldSource: oldSource, newSource: edit.newString, oldTarget: oldTranslatedValue, newTarget: null));
                            translations.Remove(key);
                        }
                    }
                }

                // Retain any old edits that are still needed
                foreach (var editPair in edits)
                {
                    if (!newEdits.ContainsKey(editPair.Key) && newDefaultJson.ContainsKey(editPair.Key) && !appliedEditKeys.Contains(editPair.Key))
                    {
                        newEdits.Add(editPair.Key, editPair.Value);
                    }
                }

                if (!verifyOnly)
                {
                    WriteJsonDictionary(translationPath, translations, keySortOrderFunction);
                    if (newEdits.Any())
                    {
                        WriteJsonDictionary(editsPath, newEdits, keySortOrderFunction);
                    }
                    else
                    {
                        File.Delete(editsPath);
                    }
                }
            }

            return !this.anyErrorsReported;
        }

        public static void WriteJsonDictionary<TValue>(string path, Dictionary<string, TValue> dictionary, Func<string,string> stringToSortOrder)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(dictionary, new JsonSerializerOptions()
            {
                WriteIndented = true,
                Converters =
                {
                    new SortedDictionaryJsonConverter<TValue>(stringToSortOrder)
                }
            }));
        }

        private Func<string,string> GenerateKeySortFunction(Dictionary<string, string> newDefaultJson, IReadOnlyCollection<DiscoveredString> discoveredStrings)
        {
            var stringToSortOrderMap = discoveredStrings.ToDictionary(k => k.localizedString, k => $"{k.file}:{k.line:8d}");
            return (s) => stringToSortOrderMap[newDefaultJson[s]];
        }

        private static readonly Regex LocalePattern = new Regex(@"^\w\w(-\w\w)?$");

        private IEnumerable<string> GetTranslatedLocales(string folder)
        {
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                var baseName = Path.GetFileNameWithoutExtension(file);
                if (LocalePattern.IsMatch(baseName))
                {
                    yield return baseName;
                }
            }
        }

        private Dictionary<string, string> buildSourceLanguageStringToKeyDict(Dictionary<string, string> oldStringToKeyDict, List<(string? newString, string? deletedString)> matchedStringChanges)
        {
            var newStringToKeyDict = new Dictionary<string, string>(oldStringToKeyDict);
            int maxKeyValue = this.GetMaxKey(oldStringToKeyDict.Values);
            foreach (var match in matchedStringChanges)
            {
                if (match.newString is not null)
                {
                    if (match.deletedString is not null)
                    {
                        newStringToKeyDict[match.newString] = newStringToKeyDict[match.deletedString];
                    }
                    else
                    {
                        ++maxKeyValue;
                        string newKey = maxKeyValue.ToString("x6");
                        newStringToKeyDict[match.newString] = newKey;
                    }
                }
                if (match.deletedString is not null)
                {
                    newStringToKeyDict.Remove(match.deletedString);
                }
            }

            return newStringToKeyDict;
        }

        private const int MinimumFuzzyMatchScore = 65;

        private static readonly Regex hexSixPattern = new Regex(@"[0-9a-f]{6}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private int GetMaxKey(IEnumerable<string> keys)
        {
            int maxKey = 0;
            foreach (var key in keys)
            {
                if (hexSixPattern.IsMatch(key))
                {
                    int value = int.Parse(key, System.Globalization.NumberStyles.HexNumber);
                    if (value > maxKey)
                    {
                        maxKey = value;
                    }
                }
            }
            return maxKey;
        }

        private List<(string? newString, string? deletedString)> TryMakeMatches(List<string> givenNewStrings, List<string> givenDeletedStrings)
        {
            List<(string newString, string deletedString, int score)> matches = new();
            foreach (var newString in givenNewStrings)
            {
                foreach (var deletedString in givenDeletedStrings)
                {
                    var score = Fuzz.TokenSetRatio(newString, deletedString);
                    if (score >= MinimumFuzzyMatchScore)
                    {
                        matches.Add((newString, deletedString, score));
                    }
                }
            }
            matches.Sort((t1, t2) => t2.score.CompareTo(t1.score));

            List<(string? newString, string? deletedString)> result = new();
            var actualNewStrings = new HashSet<string>(givenNewStrings);
            var actualDeletedStrings = new HashSet<string>(givenDeletedStrings);
            while (matches.Any())
            {
                var match = matches.First();
                actualNewStrings.Remove(match.newString);
                actualDeletedStrings.Remove(match.deletedString);
                matches.RemoveAll(t => t.newString == match.newString);
                matches.RemoveAll(t => t.deletedString == match.deletedString);
                result.Add((match.newString, match.deletedString));
            }
            result.AddRange(actualNewStrings.Select(s => ((string?)s, (string?)null)));
            result.AddRange(actualDeletedStrings.Select(s => ((string?)null, (string?)s)));
            return result;
        }

        private Dictionary<string, string> ReverseDefaultJsonDict(Dictionary<string, string> oldKeyToStringDict)
        {
            Dictionary<string, string> result = new();
            foreach (var pair in oldKeyToStringDict)
            {
                if (result.ContainsKey(pair.Key))
                {
                    this.Error(DefaultJsonInvalidUserEdit, $"default.json contains two keys that translate to the same string - discarding {pair.Key} => \"{pair.Value}\"");
                }
                else
                {
                    result[pair.Value] = pair.Key;
                }
            }

            return result;
        }

        private Dictionary<string, string> ReadDefaultJson(string i18nFolder)
        {
            string defaultJsonPath = Path.Combine(i18nFolder, "default.json");
            Dictionary<string, string>? oldKeyToStringDict = null;
            if (File.Exists(defaultJsonPath))
            {
                try
                {
                    oldKeyToStringDict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(defaultJsonPath));
                    if (oldKeyToStringDict is null)
                    {
                        this.Error(DefaultJsonUnusable, $"Unable to read default.json: Its content is null");
                    }
                }
                catch (Exception ex)
                {
                    this.Error(DefaultJsonUnusable, $"Unable to read default.json: {ex.Message}");
                }
            }

            if (oldKeyToStringDict is null)
            {
                oldKeyToStringDict = new();
            }

            return oldKeyToStringDict;
        }

        private string? GetI18nFolder(string sourceProjectPath, bool verifyOnly, bool hasAnyTranslations, out string i18nFolder)
        {
            i18nFolder = Path.Combine(sourceProjectPath, "i18n");
            if (!Directory.Exists(i18nFolder))
            {
                if (hasAnyTranslations)
                {
                    if (verifyOnly)
                    {
                        this.Error(TranslationRequired, "The i18n folder does not exist and there are new localizable strings");
                        return null;
                    }
                    else
                    {
                        Directory.CreateDirectory(i18nFolder);
                    }
                }
                else
                {
                    // TODO: I think the thing to do is delete the folder contents, but that seems like a strong step.
                    //  Maybe it should just raise an error.
                    return null;
                }
            }

            return i18nFolder;
        }

        private string? GetI18nFolder()
        {
            string actualPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "i18n");
            if (Directory.Exists(actualPath))
            {
                return actualPath;
            }
            // TODO: Delete this - just here to make life easy.
            return @"E:\repos\LocalizeFromSource\LocalizeFromSource";
        }

        /*
         * 
         * Starting condition: Empty folder
         * Run compiler, produces 'default.json'
         * German translator produces 'de.json'
         * Developer changes a string and adds a new one and runs compiler again
         *   Compiler has the old default.json and the new one, plus the de.json.
         *   It can see use fuzzy-matching to match the old key to the new one, and so
         *   can generate a new de.json that may use the stale translation.  It would have
         *   to produce a new file, call it 'de-out-of-date.json' that lists the key-value-pair
         *   of the new string that's missing, say:
         *   { out-of-date: [ { "key": "1afe", "old-en": null, "new-en": "I'm a little teapot", "old-de": null, "new-de": null } ] }
         * The German translator can then refer to that one file and know all the translations
         *   that need to be touched up - they'd update 'new-de' with new values and run
         *   a utility to move all the 'new-de' values to 'de.json'
         * 
         */
    }
}
