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
        public SdvTranslationCompiler(CombinedConfig config, string projectPath)
            : base(config)
        {
            this.I18nBuildOutputFolder = Path.Combine(projectPath, "i18n");
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
            => Directory.GetFiles(this.I18nBuildOutputFolder, "*.json")
                .Select(fullPath => Path.GetFileNameWithoutExtension(fullPath))
                .Select(baseFileName => baseFileName.Replace(".edits", ""))
                .Where(baseFileName => LocalePattern.IsMatch(baseFileName))
                .Select(localeName => localeName.ToLower(CultureInfo.InvariantCulture))
                .Distinct();

        protected override string I18nBuildOutputFolder { get; }

        protected override string GetPathToBuildOutputForLocale(string? locale)
            => Path.Combine(this.I18nBuildOutputFolder, locale is null ? "default.json" : (locale + ".json"));

        public override Dictionary<string, string> ReadKeyToSourceMapFile()
        {
            // This path is SDV-specific.
            string keyToSourceStringFile = Path.Combine(this.Config.ProjectPath, "i18n", "default.json");
            try
            {
                // This file format is sdv-specific.
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keyToSourceStringFile), this.JsonReaderOptions)
                    ?? throw new JsonException("File should not contain just null");
            }
            catch (Exception ex)
            {
                throw new FatalErrorException($"Could not read {keyToSourceStringFile}: {ex.Message}", TranslationCompiler.BadFile, ex);
            }
        }


    }
}
