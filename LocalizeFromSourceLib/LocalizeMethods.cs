using System.Runtime.CompilerServices;

namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   Localization methods.
    /// </summary>
    public static class LocalizeMethods
    {
        // Architecturally, this class should simply be a static wrapper around Translator.

        // If we ever progress beyond SDV, this should be set by an initialization method and each static method should
        //  have a 'if null throw InvalidOperationException' in it.

        internal static Translator? Translator { get; set; } = new SdvTranslator();

        /// <summary>
        ///   Set this to true and strings will get tweaked before they are displayed so that it's
        ///   easier to spot un-translated strings and increase the chances that a thing that shouldn't 
        ///   have been translated but was will cause a visible break.
        /// </summary>
        public static bool DoPseudoLoc
        {
            get => Translator!.DoPseudoLoc;
            set => Translator!.DoPseudoLoc = value;
        }

        /// <summary>
        ///   Localizes either a string literal.
        /// </summary>
        /// <param name="stringInSourceLocale">
        ///   The string to be used - note that this must be a literal string, not an expression
        ///   that results in a string.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string L(string stringInSourceLocale)
            => Translator!.Translate(stringInSourceLocale);

        /// <summary>
        ///   Same as <see cref="L"/> except for interpolated strings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string LF(FormattableString s)
            => Translator!.TranslateFormatted(s);

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


        // Post sdv-is-the-only-thing, perhaps this should move into its own class?  It seems
        //  bad to ask the user to have two.  Perhaps there could be a 'SdvLocalizeMethods' that
        //  extends this class with these two, that way there'd only be the one...

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvEvent(FormattableString formattableString)
            => ((SdvTranslator)Translator!).SdvEvent(formattableString);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvQuest(string questString)
            => ((SdvTranslator)Translator!).SdvQuest(questString);
    }
}
