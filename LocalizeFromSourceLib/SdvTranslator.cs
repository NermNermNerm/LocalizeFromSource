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
        private readonly Lazy<Dictionary<string, string>?> defaultJsonReversed;
        private readonly Dictionary<string, List<Dictionary<string,string>>> translations = new();

        internal SdvTranslator()
        {
            this.defaultJsonReversed = new(this.GetSourceLanguageReverseLookup);
        }

        /// <summary>
        ///   This should be set in ModEntry to <code>() =&gt; helper.Translation.Locale</code>.
        /// </summary>
        public static Func<string>? GetLocale { get; set; } = null;

        /// <summary>
        ///   This should be set in ModEntry to the language that default.json is written in.
        ///   It simply prevents getting "missing translation" events for your source language.
        /// </summary>
        public static string SourceLocale { get; set; } = "en-us";

        /// <inheritDoc/>
        public override string Translate(string stringInSourceLocale)
            => this.GetBestTranslation(stringInSourceLocale);

        /// <inheritDoc/>
        public override string TranslateFormatted(string formatStringInSourceLocale)
        {
            var translation = this.GetBestTranslation(formatStringInSourceLocale);
            int counter = 0;
            return formatRegex.Replace(translation, m => counter++.ToString(CultureInfo.InvariantCulture));
        }

        private static readonly Regex formatRegex = new Regex(@"{{\w+(?<fmt>:[^}]+)}}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private string? GetI18nFolder()
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
                keyToSourceStringDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(defaultJsonPath));
                if (keyToSourceStringDictionary is null)
                {
                    RaiseTranslationFilesCorrupt($"Unable to read '{defaultJsonPath}' - translation will not work.  The file contains null.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                RaiseTranslationFilesCorrupt($"Unable to read '{defaultJsonPath}' - translation will not work.  Error was: {ex}");
                return null;
            }

            Dictionary<string, string> reverseLookup = new Dictionary<string, string>();
            foreach (var pair in keyToSourceStringDictionary)
            {
                if (reverseLookup.ContainsKey(pair.Value))
                {
                    RaiseTranslationFilesCorrupt($"{defaultJsonPath} was not built by this compiler!  It has multiple keys with the same translation key: {pair.Key} value: '{pair.Value}'.  Translations of this string will not yield accurate results.");
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
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(translationPath));
                        if (dict is null)
                        {
                            RaiseTranslationFilesCorrupt($"{translationPath} has null contents");
                        }
                        else
                        {
                            translations.Add(dict);
                        }
                    }
                    catch (Exception ex)
                    {
                        RaiseTranslationFilesCorrupt($"{translationPath} cannot be read: {ex}");
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

        private string GetBestTranslation(string stringInSourceLocale)
        {
            var reverseLookup = defaultJsonReversed.Value;
            if (GetLocale is null)
            {
                // This is the exception to the "translations never throw" claim of LocalizeMethods - because it indicates a code fault, not a translation error.
                throw new InvalidOperationException("LocalizeFromSourceLib requires you to set SdvTranslator.GetLocale to '() => helper.Translation.Locale' in ModEntry.  If you're doing that and you're still getting this error then perhaps you have a static initializer that's asking for a translated value.  That's not a good idea - the locale can change during gameplay.");
            }

            string currentLocale = GetLocale();
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

            if (currentLocale.Equals(SourceLocale, StringComparison.OrdinalIgnoreCase))
            {
                return stringInSourceLocale;
            }

            if (!reverseLookup.TryGetValue(stringInSourceLocale, out string? key))
            {
                RaiseTranslationFilesCorrupt($"The following string is not in the default.json: '{stringInSourceLocale}'");
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

            RaiseBadTranslation($"The following string does not have a translation in {currentLocale}: '{stringInSourceLocale}'");
            return stringInSourceLocale;
        }
    }
}
