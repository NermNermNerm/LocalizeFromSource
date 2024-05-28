namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   The base class for translations.
    /// </summary>
    public abstract class Translator
    {
        /// <summary>
        ///   Gets a translation of <paramref name="stringInSourceLocale"/> - if none can be had,
        ///   it falls back to the source string.  It does not throw exceptions if translation files are missing or corrupt,
        ///   reporting those with <see cref="OnTranslationFilesCorrupt"/> and <see cref="OnBadTranslation"/> instead.
        /// </summary>
        public abstract string Translate(string stringInSourceLocale);

        /// <summary>
        ///   Like <see cref="Translate(string)"/> except for format strings like those passed to <code>String.Format</code>.
        /// </summary>
        public abstract string TranslateFormatted(string formatStringInSourceLocale);


        /// <summary>
        ///   Raised when there is something wrong with the translation files that will prevent it from working in
        ///   any language other than the source.  The argument is a string containing the nature of the fault.
        /// </summary>
        public event Action<string>? OnTranslationFilesCorrupt;

        /// <summary>
        ///   Raised when there is something wrong with the particular target language or some of the translations
        ///   within the language.
        /// </summary>
        public event Action<string>? OnBadTranslation;

        /// <summary>
        ///   Raises <see cref="OnTranslationFilesCorrupt"/>.
        /// </summary>
        protected void RaiseTranslationFilesCorrupt(string error)
            => this.OnTranslationFilesCorrupt?.Invoke(error);

        /// <summary>
        ///   Raises <see cref="OnBadTranslation"/>.
        /// </summary>
        protected void RaiseBadTranslation(string warning)
            => this.OnBadTranslation?.Invoke(warning);
    }
}
