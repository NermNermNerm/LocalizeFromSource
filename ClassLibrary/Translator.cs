namespace NermNermNerm.Stardew.LocalizeFromSource
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
        ///   reporting those with <see cref="SdvLocalize.OnTranslationFilesCorrupt"/> and <see cref="SdvLocalize.OnBadTranslation"/> instead.
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
        ///   reporting those with <see cref="SdvLocalize.OnTranslationFilesCorrupt"/> and <see cref="SdvLocalize.OnBadTranslation"/> instead.
        /// </summary>
        protected abstract string GetTranslation(string stringInSourceLocale);

        /// <summary>
        ///   If Pseudo-localization is enabled, it sprinkles accent marks on its input, else it returns it as-is.
        /// </summary>
        protected string ApplyPseudo(string s)
        {
            return this.DoPseudoLoc ? s.Replace('e', 'ê').Replace('E', 'É').Replace('a', 'ã').Replace('o', 'ö').Replace('B', 'ß') : s;
        }

        /// <summary>
        ///   Raised when there is something wrong with the translation files that will prevent it from working in
        ///   any language other than the source.  The argument is a string containing the nature of the fault.
        /// </summary>
        public event Action<string>? OnTranslationFilesCorrupt;

        /// <summary>
        ///   Raises <see cref="OnTranslationFilesCorrupt"/>.
        /// </summary>
        protected virtual void RaiseTranslationFilesCorrupt(string error)
            => this.OnTranslationFilesCorrupt?.Invoke(error);

        /// <summary>
        ///   Raised when there is something wrong with the particular target language or some of the translations
        ///   within the language.
        /// </summary>
        public event Action<string>? OnBadTranslation;


        /// <summary>
        ///   Raises <see cref="OnBadTranslation"/>.
        /// </summary>
        internal virtual void RaiseBadTranslation(string warning)
            => this.OnBadTranslation?.Invoke(warning);
    }
}
