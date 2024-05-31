namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   The base class for translations.
    /// </summary>
    public abstract class Translator
    {
        /// <summary>
        ///   Set this to true and strings will get tweaked before they are displayed so that it's
        ///   easier to spot un-translated strings and increase the chances that a thing that shouldn't 
        ///   have been translated but was will cause a visible break.
        /// </summary>
        public bool DoPseudoLoc { get; set; } = false;

        /// <summary>
        ///   Gets a translation of <paramref name="stringInSourceLocale"/> - if none can be had,
        ///   it falls back to the source string.  It does not throw exceptions if translation files are missing or corrupt,
        ///   reporting those with <see cref="OnTranslationFilesCorrupt"/> and <see cref="OnBadTranslation"/> instead.
        /// </summary>
        public string Translate(string stringInSourceLocale)
            => this.ApplyPseudo(this.GetTranslation(stringInSourceLocale));

        /// <summary>
        ///   Uses the translation for the source language <paramref name="formattableString"/> as the format string
        ///   and applies the given arguments to it.
        /// </summary>
        public string TranslateFormatted(FormattableString formattableString)
            => this.ApplyPseudo(string.Format(this.GetTranslationOfFormatString(formattableString.Format), formattableString.GetArguments()));

        /// <summary>
        ///   Like <see cref="Translate(string)"/> except for format strings like those passed to <code>String.Format</code>.
        /// </summary>
        protected abstract string GetTranslationOfFormatString(string formatStringInSourceLocale);

        /// <summary>
        ///   Gets a translation of <paramref name="stringInSourceLocale"/> - if none can be had,
        ///   it falls back to the source string.  It does not throw exceptions if translation files are missing or corrupt,
        ///   reporting those with <see cref="OnTranslationFilesCorrupt"/> and <see cref="OnBadTranslation"/> instead.
        /// </summary>
        protected abstract string GetTranslation(string stringInSourceLocale);

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
        protected virtual void RaiseTranslationFilesCorrupt(string error)
            => OnTranslationFilesCorrupt?.Invoke(error);

        /// <summary>
        ///   Raises <see cref="OnBadTranslation"/>.
        /// </summary>
        /// <remarks>Test classes can override this to validate that these events are generated without having to touch a static.</remarks>
        protected virtual void RaiseBadTranslation(string warning)
            => OnBadTranslation?.Invoke(warning);

        private string ApplyPseudo(string s)
        {
            return this.DoPseudoLoc ? s.Replace('e', 'ê').Replace('E', 'É').Replace('a', 'ã').Replace('o', 'ö').Replace('B', 'ß') : s;
        }
    }
}
