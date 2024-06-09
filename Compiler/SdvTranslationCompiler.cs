using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalizeFromSource
{
    /// <summary>
    ///   This class contains the compiler code that is specific to how Stardew Valley handles localization.
    /// </summary>
    public class SdvTranslationCompiler
        : KeyValuePairTranslationCompiler
    {
        private readonly string i18nFolder;

        public SdvTranslationCompiler(CombinedConfig config, string projectPath)
            : base(config)
        {
            this.i18nFolder = Path.Combine(projectPath, "i18n");
        }

        private static readonly Regex[] stardewValleySpecificIdentifierPatterns = [
                // dot and slash-separated identifiers
                new Regex(@"^\w+[\./\\_][\w\./\\_]*\w$", RegexOptions.Compiled | RegexOptions.CultureInvariant),

                // CamelCase or pascalCase identifiers - identified by all letters and digits with a lowercase letter right before an uppercase one.
                new Regex(@"^\w*[\p{Ll}\d][\p{Lu}]\w*$", RegexOptions.Compiled | RegexOptions.CultureInvariant),

                // Qualified item id's (O)blah.blah or (BC)Chest or just (O)
                new Regex(@"^\([A-Z][A-Z]?\)[\p{L}\d\.]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant),

                // Character Ids - Not case-insensitive, as identifier matching is case-insensitive too
                new Regex(
                    @"^(Alex|Elliot|Harvey|Sam|Sebastian|Shane|Abigail|Emily|Haley|Leah|Maru|Penny|Caroline|Clint|Demetrius|Dwarf|Evelyn|George|Gus|Jas|Jodi|Kent|Krobus|Leo|Lewis|Linus|Marnie|Pam|Pierre|Robin|Sandy|Vincent|Willy|Wizard)$"
                    , RegexOptions.Compiled | RegexOptions.CultureInvariant),

                // One-Word Location Ids - also case-insensitive
                new Regex(
                    @"^(Farm|Mine|Forest|Woods|Town|Mountain|Beach|Desert|Museum|Saloon)$"
                    , RegexOptions.Compiled | RegexOptions.CultureInvariant),
            ];

        public override bool IsKnownInvariantString(string s)
        {
            return base.IsKnownInvariantString(s) || stardewValleySpecificIdentifierPatterns.Any(p => p.IsMatch(s));
        }

        public override IEnumerable<string> DomainSpecificInvariantMethodNames
        {
            get
            {
                return base.DomainSpecificInvariantMethodNames.Union([
                    "StardewModdingAPI.IAssetName.IsEquivalentTo",
                    "StardewModdingAPI.IAssetName.IsEquivalentTo",
                    "StardewModdingAPI.IAssetName.StartsWith",
                    "StardewModdingAPI.IAssetName.IsDirectlyUnderPath",
                    "StardewValley.Farmer.getFriendshipHeartLevelForNPC",
                    "StardewValley.Game1.playSound",
                    "StardewValley.GameLocation.playSound",
                    "Netcode.NetFields.AddField",
                    "StardewModdingAPI.Events.AssetRequestedEventArgs.LoadFromModFile"
                ]);
            }
        }

        private static readonly Regex LocalePattern = new Regex(@"^\w\w(-\w\w)?$");

        protected override IEnumerable<string> GetActiveLocales()
            => Directory.GetFiles(this.i18nFolder, "*.json")
                .Select(fullPath => Path.GetFileNameWithoutExtension(fullPath))
                .Select(baseFileName => baseFileName.Replace(".edits", ""))
                .Where(baseFileName => LocalePattern.IsMatch(baseFileName))
                .Select(localeName => localeName.ToLower(CultureInfo.InvariantCulture))
                .Distinct();

        protected override string GetPathToEditsFile(string locale) => Path.Combine(i18nFolder, locale + ".edits.json");
        protected override string GetPathToNewLanguageTemplate() => Path.Combine(i18nFolder, "new-language-template.json");
        protected override Dictionary<string, string> ReadTranslationTable(string? locale)
        {
            string path = Path.Combine(this.i18nFolder, locale is null ? "default.json" : (locale + ".json"));
            Dictionary<string, string>? contents = null;
            if (File.Exists(path))
            {
                try
                {
                    contents = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), this.GetJsonReaderOptions());
                    if (contents is null)
                    {
                        this.Error(DefaultJsonUnusable, $"Unable to read {path}: Its content is null");
                    }
                }
                catch (Exception ex)
                {
                    this.Error(DefaultJsonUnusable, $"Unable to read {path}: {ex.Message}");
                }
            }

            if (contents is null)
            {
                contents = new();
            }

            contents.Remove("place-holder");

            return contents;
        }

        protected override void SaveTranslationTable(string? locale, Dictionary<string, string> newTranslations, Func<string, string> keyToSortOrder)
        {
            string path = Path.Combine(this.i18nFolder, locale is null ? "default.json" : (locale + ".json"));
            if (newTranslations.Any())
            {
                WriteJsonDictionary(path, newTranslations, keyToSortOrder, DoNotEditComment);
            }
            else // Empty translation
            {
                if (locale is null)
                {
                    WriteJsonDictionary(path, new Dictionary<string, string>() { { "place-holder", "this mod is not ready to be localized" } }, keyToSortOrder, DoNotEditComment);
                }
                else
                {
                    File.Delete(path);
                }
            }
        }
    }
}
