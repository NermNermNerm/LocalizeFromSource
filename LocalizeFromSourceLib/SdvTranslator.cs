using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   The Stardew Valley translation mechanism.
    /// </summary>
    public class SdvTranslator : Translator
    {
        private readonly Func<string> localeGetter;
        private readonly string sourceLocale;
        private readonly Lazy<Dictionary<string, string>?> defaultJsonReversed;
        private readonly Dictionary<string, List<Dictionary<string,string>>> translations = new();

        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        /// <summary>
        ///   Constructor for test overrides.
        /// </summary>
        internal protected SdvTranslator(Func<string> localeGetter, string sourceLocale = "en-us")
        {
            this.defaultJsonReversed = new(this.GetSourceLanguageReverseLookup);
            this.localeGetter = localeGetter;
            this.sourceLocale = sourceLocale;
        }

        /// <inheritDoc/>
        protected override string GetTranslationOfFormatString(string formatStringInSourceLocale)
        {
            string sourceLanguageFormatString = TransformCSharpFormatStringToSdvFormatString(formatStringInSourceLocale);
            var targetLanguageFormatString = this.GetTranslation(sourceLanguageFormatString);
            return TransformSdvFormatStringToCSharpFormatString(targetLanguageFormatString, sourceLanguageFormatString);
        }

        /// <summary>
        ///   Gets the folder containing the SDV localization files.
        /// </summary>
        /// <remarks>This is a test insertion point.</remarks>
        protected virtual string? GetI18nFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "i18n");
        }

        private Dictionary<string,string>? GetSourceLanguageReverseLookup()
        {
            string? folder = this.GetI18nFolder();
            if (folder is null)
            {
                return null;
            }

            string defaultJsonPath = Path.Combine(folder, "default.json");
            Dictionary<string, string>? keyToSourceStringDictionary;
            try
            {
                keyToSourceStringDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(defaultJsonPath), jsonSerializerOptions);
                if (keyToSourceStringDictionary is null)
                {
                    this.RaiseTranslationFilesCorrupt($"Unable to read '{defaultJsonPath}' - translation will not work.  The file contains null.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                this.RaiseTranslationFilesCorrupt($"Unable to read '{defaultJsonPath}' - translation will not work.  Error was: {ex}");
                return null;
            }

            Dictionary<string, string> reverseLookup = new Dictionary<string, string>();
            foreach (var pair in keyToSourceStringDictionary)
            {
                if (reverseLookup.ContainsKey(pair.Value))
                {
                    this.RaiseTranslationFilesCorrupt($"{defaultJsonPath} was not built by this compiler!  It has multiple keys with the same translation key: {pair.Key} value: '{pair.Value}'.  Translations of this string will not yield accurate results.");
                }
                else
                {
                    reverseLookup.Add(pair.Value, pair.Key);
                }
            };

            return reverseLookup;
        }

        /// <summary>
        ///  Gets a list of translation_key to translated value dictionaries for the given locale, which should
        ///  be tried in order to get the translation.
        /// </summary>
        private List<Dictionary<string,string>> ReadTranslationTables(string localeId)
        {
            if (this.translations.TryGetValue(localeId, out var translations))
            {
                return translations;
            }

            translations = new();
            this.translations.Add(localeId, translations);

            string? folder = this.GetI18nFolder();
            if (folder is null)
            {
                return translations;
            }

            string partial = localeId;
            do
            {
                string translationPath = Path.Combine(folder, partial + ".json");
                if (File.Exists(translationPath))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(translationPath), jsonSerializerOptions);
                        if (dict is null)
                        {
                            this.RaiseTranslationFilesCorrupt($"{translationPath} has null contents");
                        }
                        else
                        {
                            translations.Add(dict);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.RaiseTranslationFilesCorrupt($"{translationPath} cannot be read: {ex}");
                    }
                }

                int lastDash = partial.LastIndexOf('-');
                if (lastDash < 0)
                {
                    partial = "";
                }
                else
                {
                    partial = partial.Substring(0, lastDash);
                }
            } while (partial != "");

            return translations;
        }

        /// <inheritDoc/>
        protected override string GetTranslation(string stringInSourceLocale)
        {
            var reverseLookup = defaultJsonReversed.Value;
            string currentLocale = localeGetter();
            if (currentLocale == "")
            {
                // SDV sets the locale kinda midway through the loading process - or at least not at the very start.
                //  Some mod assets just get loaded earlier than that, and so you have to force them to be loaded again,
                //  after the locale is set.  That's a known problem (at least since 1.6).  See TractorMod as an example.
                //  It has a LocaleChanged event handler that reloads the one thing that it knows gets loaded before
                //  the locale is set.  Mods that use this will have to do the same...  In fact, mods that don't use this
                //  library will have to do the same, as the issue is with the game.
                return stringInSourceLocale;
            }

            if (reverseLookup is null)
            {
                // Not throwing a InvalidOperation here because it could be a mod-deployment problem rather than a code fault.
                // Assuming the events are correctly routed, there'll be SMAPI complaints telling the user more about it.
                return stringInSourceLocale;
            }

            if (currentLocale.Equals(sourceLocale, StringComparison.OrdinalIgnoreCase))
            {
                return stringInSourceLocale;
            }

            if (!reverseLookup.TryGetValue(stringInSourceLocale, out string? key))
            {
                this.RaiseTranslationFilesCorrupt($"The following string is not in the default.json: '{stringInSourceLocale}'");
                return stringInSourceLocale;
            }

            var translationTables = this.ReadTranslationTables(currentLocale);
            foreach (var localeSpecificTranslationTable in translationTables)
            {
                if (localeSpecificTranslationTable.TryGetValue(key, out var translation))
                {
                    return translation;
                }
            }

            this.RaiseBadTranslation($"The following string does not have a translation in {currentLocale}: '{stringInSourceLocale}'");
            return stringInSourceLocale;
        }

        private static readonly Regex sdvEventLocalizableParts = new Regex(
            @"""(?<localizablePart>[^""]+)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex sdvAssetPathPattern = new Regex(
            @"^(\([A-Z]+\))?\w+[\./\\][\w\./\\]*\w$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        public string SdvEvent(FormattableString formattableString)
        {
            // The question is whether to do the format conversion before or after the localization pass.
            // The risk of doing it before is that formatting it will bring in something that looks localizable.
            // The risk of doing it after is that the localization brings in something that looks like a
            // format argument.
            //
            // Realistically, neither should happen, but the thing the developer is most in control of is
            // formatting, and so if something gets screwed up, it'll be just as likely to show itself in
            // the source language as any other, so overall, the risk of a post-shipping bug appearing is
            // reduced by doing the formatting first.
            string sourceLanguageEventCode = formattableString.ToString();
            string translated = sdvEventLocalizableParts.Replace(sourceLanguageEventCode, m =>
            {
                var localizablePart = m.Groups["localizablePart"];
                if (sdvAssetPathPattern.IsMatch(localizablePart.Value))
                {
                    return m.Value;
                }
                else
                {
                    return sourceLanguageEventCode.Substring(m.Index, localizablePart.Index - m.Index)
                        + this.GetTranslation(localizablePart.Value)
                        + sourceLanguageEventCode.Substring(localizablePart.Index + localizablePart.Length, m.Index + m.Length - localizablePart.Index - localizablePart.Length);
                }
            });
            return translated;
        }

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        public string SdvQuest(string questString)
        {
            var splits = questString.Split('/', 5);
            string loc(string s) => s == "" ? "" : this.GetTranslation(s);
            return $"{splits[0]}/{this.GetTranslation(splits[1])}/{loc(splits[2])}/{loc(splits[3])}/{splits[4]}";
        }

        private static readonly Regex dotnetFormatStringPattern = new(@"{(?<argNumber>\d+)(?<formatSpecifier>:[^}]+)?}(\|(?<argName>\w+)\|)?", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex sdvFormatStringPattern = new Regex(@"{{(?<argName>\w+)(?<formatSpecifier>:[^}]+)?}}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


        /// <summary>
        ///   Converts a string like "yow {0} ee" to "yow {{arg0}} ee"
        /// </summary>
        public static string TransformCSharpFormatStringToSdvFormatString(string csharpFormatString)
        {
            // Not done - validity checking
            //  given names do not conflict or repeat - e.g. "foo {0}|a| bar {0}|b|"  "foo {0}|a| bar {1}|a|"
            return dotnetFormatStringPattern.Replace(csharpFormatString, (m) =>
            {
                var number = m.Groups["argNumber"].Value;

                string givenName = m.Groups["argName"].Value;
                var name = string.IsNullOrEmpty(givenName) ? ("arg" + number) : givenName;
                var fmtSpecifier = m.Groups["formatSpecifier"].Value; // note - includes the :

                return "{{" + name + fmtSpecifier + "}}";
            });
        }

        /// <summary>
        ///   Converts a string like "yow {{arg0}} ee" to "yow {0} ee"
        /// </summary>
        public static string TransformSdvFormatStringToCSharpFormatString(string translatedSdvFormatString, string sourceSdvFormatString)
        {
            // Note that we assume the original format string was ordered like "foo {0} {1}" and not "foo {1} {0}".
            //  That *is* a valid thing to assume when your format strings are all coming from generated code from
            //  interpolated strings, rather than from calls to string.Format.

            Dictionary<string, int> argNameToIndexMap = new Dictionary<string, int>();
            int counter = 0;
            foreach (Match match in sdvFormatStringPattern.Matches(sourceSdvFormatString))
            {
                string argName = match.Groups["argName"].Value;
                if (!argNameToIndexMap.ContainsKey(argName))
                {
                    argNameToIndexMap[argName] = counter;
                    ++counter;
                }
            }

            return sdvFormatStringPattern.Replace(translatedSdvFormatString, (m) =>
            {
                var number = argNameToIndexMap[m.Groups["argName"].Value]; // Can throw if translation is bad!
                var fmtSpecifier = m.Groups["formatSpecifier"].Value; // note - includes the :

                return "{" + number.ToString(CultureInfo.InvariantCulture) + fmtSpecifier + "}";
            });
        }

    }
}
