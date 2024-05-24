using System.Runtime.CompilerServices;

namespace LocalizeFromSourceLib
{
    public class LocalizeMethods
    {
        internal static Translator? Translator { get; set; } = new SdvTranslator();

        /// <summary>
        ///   Localizes either a string literal.
        /// </summary>
        /// <param name="formatString">
        ///   The string to be used - note that this must be a literal string, not an expression
        ///   that results in a string.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string L(string stringInSourceLocale)
            => Translator!.Translate(stringInSourceLocale);

        /// <summary>
        ///   Same as <see cref="<L"/> except for interpolated strings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string LF(FormattableString s)
            => string.Format(Translator!.TranslateFormatted(s.Format), s.GetArguments());

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
    }
}
