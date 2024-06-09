using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NermNermNerm.Stardew.LocalizeFromSource
{
    /// <summary>
    ///   The Stardew Valley translation mechanism.
    /// </summary>
    internal class SdvTranslator : KeyValuePairTranslator
    {
        private readonly string i18nFolder;

        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        /// <summary>
        ///   Constructor for test overrides.
        /// </summary>
        internal protected SdvTranslator(Func<string> localeGetter, string sourceLocale, string i18nFolder)
            : base(() => SdvLocaleGetter(localeGetter, sourceLocale), sourceLocale)
        {
            this.i18nFolder = i18nFolder;
        }

        static string SdvLocaleGetter(Func<string> localGetter, string sourceLocale)
        {
            // SDV has a problem where the usual method of getting the locale will be wrong and return ""
            // early in the startup process - but not so early as translations are not asked for already.
            // Implementations have to basically invalidate all that too-early work, so all we can really
            // do here is cobble something up so that if the invalidation fails, there's still something shown.
            string locale = localGetter();
            return string.IsNullOrEmpty(locale) ? sourceLocale : locale;
        }

        protected override Dictionary<string, string>? ReadSourceLanguageTable()
        {
            string defaultJsonPath = Path.Combine(this.i18nFolder, "default.json");
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
            return keyToSourceStringDictionary;
        }

        /// <inheritdoc/>
        /// <remarks>
        ///  SMAPI has the strategy of merging translations from most-specific to least, e.g. "pt-BR" and then "pt".
        ///  I don't know that it is ever actually used, and I'm not sure that it would really work well in practice
        ///  to have them split out like that, but this attempts to replicate that idea.
        /// </remarks>
        protected override Dictionary<string, string> ReadTranslationTable(string localeId)
        {
            var table = new Dictionary<string,string>();

            void mergeIntoTable(Dictionary<string,string> d)
            {
                foreach (var pair in d)
                {
                    if (!table.ContainsKey(pair.Key))
                    {
                        table.Add(pair.Key, pair.Value);
                    }
                }
            }

            string partial = localeId;
            do
            {
                string translationPath = Path.Combine(this.i18nFolder, partial + ".json");
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
                            mergeIntoTable(dict);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.RaiseTranslationFilesCorrupt($"{translationPath} cannot be read: {ex}");
                    }
                }

                partial = partial.Substring(0, Math.Max(0, partial.LastIndexOf('-')));
            } while (partial != "");

            return table;
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
                        + this.ApplyPseudo(this.GetTranslation(localizablePart.Value))
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
            string loc(string s) => s == "" ? "" : this.ApplyPseudo(this.GetTranslation(s));
            return $"{splits[0]}/{this.ApplyPseudo(this.GetTranslation(splits[1]))}/{loc(splits[2])}/{loc(splits[3])}/{splits[4]}";
        }

        private static readonly Regex sdvMailParser = new Regex(
            @"^(?<content>[^%]+?)(?<code>%.*?)?(\[#\](?<title>.*))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        ///   Localizes the localizable part of SDV mail data.
        /// </summary>
        public string SdvMail(FormattableString mailFormat)
        {
            // See SdvEvent for a discussion of the merits of before vs. after format application.  Same reasoning applies here.
            string afterFormat = mailFormat.ToString();

            var match = sdvMailParser.Match(afterFormat);
            // The pattern is loose enough that it will never fail to match.

            string content = ApplyPseudo(this.Translate(match.Groups["content"].Value));
            string code = match.Groups["code"].Value; // Will equal '' if there is no %item block
            string title = match.Groups["title"].Value;
            if (!string.IsNullOrEmpty(title))
            {
                title = "[#]" + ApplyPseudo(this.Translate(title));
            }
            return content + code + title;
        }


        private static readonly Regex dotNetFormatStringPattern = new(@"{(?<argNumber>\d+)(?<formatSpecifier>:[^}]+)?}(\|(?<argName>\w+)\|)?", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex sdvFormatStringPattern = new Regex(@"{{(?<argName>\w+)(?<formatSpecifier>:[^}]+)?}}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        ///   Converts a string like "yow {0} ee" to "yow {{arg0}} ee"
        /// </summary>
        protected override string TransformFormatStringFromDotNet(string dotNetFormatString)
            => TransformCSharpFormatStringToSdvFormatString(dotNetFormatString);

        /// <summary>
        ///   Shared with the Compiler.
        /// </summary>
        /// <param name="dotNetFormatString"></param>
        /// <returns></returns>
        public static string TransformCSharpFormatStringToSdvFormatString(string dotNetFormatString)
        {
            // Not done - validity checking
            //  given names do not conflict or repeat - e.g. "foo {0}|a| bar {0}|b|"  "foo {0}|a| bar {1}|a|"
            return dotNetFormatStringPattern.Replace(dotNetFormatString, (m) =>
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
        protected override string TransformFormatStringToDotNet(string translatedSdvFormatString, string sourceSdvFormatString)
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
