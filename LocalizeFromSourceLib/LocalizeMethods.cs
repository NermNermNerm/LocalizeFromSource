using static System.FormattableString;

namespace LocalizeFromSourceLib
{
    public class LocalizeMethods
    {
        internal static Translator? Translator { get; set; }

        /// <summary>
        ///   Localizes either a string literal.
        /// </summary>
        /// <param name="formatString">
        ///   The string to be used - note that this must be a literal string, not an expression
        ///   that results in a string.
        /// </param>
        public static string L(string stringInSourceLocale)
        {
            return Translator!.Translate(stringInSourceLocale);
        }

        public static string LI(FormattableString s)
        {
            // TODO
            return s.ToString();
        }
    }
}
