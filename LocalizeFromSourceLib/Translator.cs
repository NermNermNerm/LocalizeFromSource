﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalizeFromSourceLib
{
    internal abstract class Translator
    {
        public string? Locale { get; set; } = null;
        public string? SourceLocale { get; set; } = null;

        /// <summary>
        ///   Gets a translation of <paramref name="stringInSourceLocale"/> - if none can be had for the <see cref="Locale"/>,
        ///   it falls back to the source string.  It does not throw exceptions if translation files are missing or corrupt,
        ///   reporting those with <see cref="OnTranslationFilesCorrupt"/> and <see cref="OnBadTranslation"/> instead.
        /// </summary>
        public abstract string Translate(string stringInSourceLocale);

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

        protected void RaiseTranslationFilesCorrupt(string error)
            => this.OnTranslationFilesCorrupt?.Invoke(error);
        
        protected void RaiseBadTranslation(string warning)
            => this.OnBadTranslation?.Invoke(warning);
    }
}
