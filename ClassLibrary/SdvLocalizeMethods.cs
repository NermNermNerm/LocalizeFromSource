using System.Runtime.CompilerServices;

namespace NermNermNerm.Stardew.LocalizeFromSource
{
    /// <summary>
    ///   Localization methods.
    /// </summary>
    public static class SdvLocalizeMethods
    {
        internal static SdvTranslator? Translator { get; set; }

        /// <summary>
        ///   Sets up Stardew Valley localization.
        /// </summary>
        public static void Initialize(Func<string>localeGetter, string sourceLocale)
        {
            Translator = new SdvTranslator(localeGetter, sourceLocale);
        }

        /// <summary>
        ///   Sets up Stardew Valley localization.
        /// </summary>
        public static void Initialize(Func<string> localeGetter)
            => Initialize(localeGetter, "en");


        /// <summary>
        ///   Set this to true and strings will get tweaked before they are displayed so that it's
        ///   easier to spot un-translated strings and increase the chances that a thing that shouldn't 
        ///   have been translated but was will cause a visible break.
        /// </summary>
        public static bool DoPseudoLoc
        {
            get => EnsureTranslator().DoPseudoLoc;
            set => EnsureTranslator().DoPseudoLoc = value;
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
            => EnsureTranslator().Translate(stringInSourceLocale);

        /// <summary>
        ///   Same as <see cref="L"/> except for interpolated strings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string LF(FormattableString s)
            => EnsureTranslator().TranslateFormatted(s);

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

        /// <summary>
        ///   Raised when there is something wrong with the translation files that will prevent it from working in
        ///   any language other than the source.  The argument is a string containing the nature of the fault.
        /// </summary>
        public static event Action<string>? OnTranslationFilesCorrupt;

        /// <summary>
        ///   Raised when there is something wrong with the particular target language or some of the translations
        ///   within the language.
        /// </summary>
        public static event Action<string>? OnBadTranslation;

        /// <summary>
        ///   Raises <see cref="OnTranslationFilesCorrupt"/>.
        /// </summary>
        /// <remarks>Test classes can override this to validate that these events are generated without having to touch a static.</remarks>
        internal static void RaiseTranslationFilesCorrupt(string error)
            => OnTranslationFilesCorrupt?.Invoke(error);

        /// <summary>
        ///   Raises <see cref="OnBadTranslation"/>.
        /// </summary>
        /// <remarks>Test classes can override this to validate that these events are generated without having to touch a static.</remarks>
        internal static void RaiseBadTranslation(string warning)
            => OnBadTranslation?.Invoke(warning);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvEvent(FormattableString formattableString)
            => EnsureTranslator().SdvEvent(formattableString);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvQuest(string questString)
            => EnsureTranslator().SdvQuest(questString);


        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvMail(string questString)
            => EnsureTranslator().SdvMail(questString);

        /// <inheritDoc/>
        private static SdvTranslator EnsureTranslator()
            => Translator ?? throw new InvalidOperationException("The translation type has not been specified yet - ensure there is a call in ModEntry.Entry() to SdvLocalizeMethods.Initialize.");
    }
}
