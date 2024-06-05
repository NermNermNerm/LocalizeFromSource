using System.Collections.Concurrent;

namespace NermNermNerm.Stardew.LocalizeFromSource
{
    /// <summary>
    ///   This is a base class for Translator's that are based on the idea that every translatable string gets a key
    ///   and that key can access translations in any language.
    /// </summary>
    internal abstract class KeyValuePairTranslator : Translator
    {
        private readonly Func<string> localeGetter;
        private readonly string sourceLocale;
        private readonly Lazy<Dictionary<string, string>?> defaultJsonReversed;
        private readonly ConcurrentDictionary<string, Dictionary<string,string>> translations = new();

        /// <summary>
        ///   Constructor for test overrides.
        /// </summary>
        internal protected KeyValuePairTranslator(Func<string> localeGetter, string sourceLocale)
        {
            this.defaultJsonReversed = new(this.GetSourceLanguageReverseLookup);
            this.localeGetter = localeGetter;
            this.sourceLocale = sourceLocale;
        }

        /// <inheritDoc/>
        protected override string GetTranslationOfFormatString(string formatStringInSourceLocale)
        {
            string sourceLanguageFormatString = this.TransformFormatStringFromDotNet(formatStringInSourceLocale);
            var targetLanguageFormatString = this.GetTranslation(sourceLanguageFormatString);
            return this.TransformFormatStringToDotNet(targetLanguageFormatString, sourceLanguageFormatString);
        }

        /// <summary>
        ///   If the translation system wants argument strings in some other format than .net's style, overriding
        ///   this method allows that conversion.
        /// </summary>
        /// <param name="dotNetFormatString">The format string with {0}-style argument replacements</param>
        /// <returns>The format string that is compatible with the format used by the underlying system.</returns>
        protected virtual string TransformFormatStringFromDotNet(string dotNetFormatString) => dotNetFormatString;


        /// <summary>
        ///   If the translation system wants argument strings in some other format than .net's style, overriding
        ///   this method allows that conversion.  It should convert from the domain-specific format to {0}-style arguments.
        /// </summary>
        /// <param name="domainFormatString">The format string in the domain-specific style.</param>
        /// <param name="sourceLanguageFormatString">The format string from the source language in the domain-specific style.</param>
        /// <returns>The <paramref name="domainFormatString"/> format string made compatible with <see cref="String.Format(string, object?[])"/>.</returns>
        protected virtual string TransformFormatStringToDotNet(string domainFormatString, string sourceLanguageFormatString) => domainFormatString;


        /// <inheritDoc/>
        protected override string GetTranslation(string stringInSourceLocale)
        {
            var reverseLookup = defaultJsonReversed.Value;
            string currentLocale = localeGetter();

            if (reverseLookup is null)
            {
                // Not throwing a InvalidOperation here because it could be a mod-deployment problem rather than a code fault.
                // Assuming the events are correctly routed, there'll be complaints telling the user more about it.
                return stringInSourceLocale;
            }

            if (currentLocale.Equals(this.sourceLocale, StringComparison.OrdinalIgnoreCase))
            {
                return stringInSourceLocale;
            }

            if (!reverseLookup.TryGetValue(stringInSourceLocale, out string? key))
            {
                this.RaiseTranslationFilesCorrupt($"The following string is not in the default.json: '{stringInSourceLocale}'");
                return stringInSourceLocale;
            }

            var translationsForCurrentLocale = this.translations.GetOrAdd(currentLocale, (_) => this.ReadTranslationTable(currentLocale));
            if (translationsForCurrentLocale.TryGetValue(key, out var translation))
            {
                return translation;
            }
            else
            {
                this.RaiseBadTranslation($"The following string does not have a translation in {currentLocale}: '{stringInSourceLocale}'");
                return stringInSourceLocale;
            }
        }


        /// <summary>
        ///   Tries to read the source language table (key-to-translation) and returns null if it fails to do so (say, due to files not copied.)
        /// </summary>
        protected abstract Dictionary<string, string>? ReadSourceLanguageTable();

        /// <summary>
        ///  Gets the translation table (key-to-translation) for a given locale or an empty table if no translation table exists.
        /// </summary>
        protected abstract Dictionary<string, string> ReadTranslationTable(string localeId);

        private Dictionary<string, string>? GetSourceLanguageReverseLookup()
        {
            var keyToSourceStringDictionary = this.ReadSourceLanguageTable();
            if (keyToSourceStringDictionary is null)
            {
                // this.Raise* should have been called
                return null;
            }

            Dictionary<string, string> reverseLookup = new Dictionary<string, string>();
            foreach (var pair in keyToSourceStringDictionary)
            {
                if (reverseLookup.ContainsKey(pair.Value))
                {
                    this.RaiseTranslationFilesCorrupt($"The source language file has been modified by something other than this compiler.  It has multiple keys with the same translation key: {pair.Key} value: '{pair.Value}'.  Translations of this string will not yield accurate results.");
                }
                else
                {
                    reverseLookup.Add(pair.Value, pair.Key);
                }
            };

            return reverseLookup;
        }

    }
}
