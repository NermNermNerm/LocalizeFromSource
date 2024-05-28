using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   Localization methods.
    /// </summary>
    public static class LocalizeMethods
    {
        internal static Translator? Translator { get; set; } = new SdvTranslator();

        /// <summary>
        ///   Set this to true and strings will get tweaked before they are displayed so that it's
        ///   easier to spot un-translated strings and increase the chances that a thing that shouldn't 
        ///   have been translated but was will cause a visible break.
        /// </summary>
        public static bool DoPseudoLoc { get; set; } = false;

        /// <summary>
        ///   Localizes either a string literal.
        /// </summary>
        /// <param name="stringInSourceLocale">
        ///   The string to be used - note that this must be a literal string, not an expression
        ///   that results in a string.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string L(string stringInSourceLocale)
            => ApplyPseudo(Translator!.Translate(stringInSourceLocale));

        /// <summary>
        ///   Same as <see cref="L"/> except for interpolated strings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string LF(FormattableString s)
            => ApplyPseudo(string.Format(Translator!.TranslateFormatted(s.Format), s.GetArguments()));

        /// <summary>
        ///   Declares that the string is invariant - just here to make it so that you can be declarative
        ///   in your code that your use of the string does not need to be localized and it was not an
        ///   oversight.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string I(string stringInSourceLocale)
            => stringInSourceLocale;

        /// <summary>
        ///   Declares that the string is invariant - just here to make it so that you can be declarative
        ///   in your code that your use of the string does not need to be localized and it was not an
        ///   oversight.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string IF(FormattableString stringInSourceLocale)
            => FormattableString.Invariant(stringInSourceLocale);

        private static readonly Regex sdvLocalizableParts = new Regex(
            @"""(?<localizablePart>[^""]+)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex paths = new Regex(
            @"^(\([A-Z]+\))?\w+[\./\\][\w\./\\]*\w$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvEvent(FormattableString formattableString)
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
            string translated = sdvLocalizableParts.Replace(sourceLanguageEventCode, m =>
            {
                var localizablePart = m.Groups["localizablePart"];
                if (paths.IsMatch(localizablePart.Value))
                {
                    return m.Value;
                }
                else
                {
                    return sourceLanguageEventCode.Substring(m.Index, localizablePart.Index - m.Index)
                        + L(localizablePart.Value)
                        + sourceLanguageEventCode.Substring(localizablePart.Index + localizablePart.Length, m.Index + m.Length - localizablePart.Index - localizablePart.Length);
                }
            });
            return translated;
        }

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvQuest(string questString)
        {
            var splits = questString.Split('/', 5);
            string loc(string s) => s == "" ? "" : L(s);
            return $"{splits[0]}/{L(splits[1])}/{loc(splits[2])}/{loc(splits[3])}/{splits[4]}";
        }

        private static string ApplyPseudo(string s)
        {
            return DoPseudoLoc ? s.Replace('e', 'ê').Replace('E', 'É').Replace('a', 'ã').Replace('o', 'ö').Replace('B', 'ß') : s;
        }
    }
}
