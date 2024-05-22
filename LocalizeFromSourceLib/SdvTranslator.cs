using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LocalizeFromSourceLib
{
    public class SdvTranslator : Translator
    {
        private readonly Lazy<string?> folderPath;
        private readonly Lazy<Dictionary<string, string>?> defaultJson;
        private readonly Dictionary<string, List<Dictionary<string,string>>> translations = new();

        public SdvTranslator()
        {
            this.folderPath = new(this.GetI18nFolder);
            this.defaultJson = new(this.GetSourceLanguageReverseLookup);
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
                    this.RaiseBadTranslation($"{defaultJsonPath} was not built by this compiler!  It has multiple keys with the same translation key: {pair.Key} value: '{pair.Value}'.  Translations of this string will not yield accurate results.");
                }
                else
                {
                    reverseLookup.Add(pair.Value, pair.Key);
                }
            };

            return reverseLookup;
        }

        private List<Dictionary<string,string>> ReadTranslation(string localeId)
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

        public override string Translate(string stringInSourceLocale)
        {
            var reverseLookup = this.GetSourceLanguageReverseLookup();
            if (this.Locale == null || this.Locale == this.SourceLocale || reverseLookup is null)
            {
                return stringInSourceLocale;
            }

            if (!reverseLookup.TryGetValue(stringInSourceLocale, out string? key))
            {
                this.RaiseBadTranslation($"The following string is not in the default.json: '{stringInSourceLocale}'");
                return stringInSourceLocale;
            }

            var translations = this.ReadTranslation(this.Locale);
            foreach (var localeSpecificTranslation in translations)
            {
                if (localeSpecificTranslation.TryGetValue(key, out var translation))
                {
                    return translation;
                }
            }

            this.RaiseBadTranslation($"The following string does not have a translation in {this.Locale}: '{stringInSourceLocale}'");
            return stringInSourceLocale;
        }

        public override void Translate(string formatStringInSourceLocale, params object[] formatArgs)
        {
            throw new NotImplementedException();
        }
    }
}
