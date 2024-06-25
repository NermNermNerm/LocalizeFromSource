using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NermNermNerm.Stardew.LocalizeFromSource
{
    /// <summary>
    ///   This class provides a static API to minimize the impact of localization on the code being
    ///   localized.
    /// </summary>
    public static class SdvLocalize
    {
        private static SdvTranslator? translator = null;

#if !NO_STARDEW_REFERENCES
        // This block will be visible when deployed in the NuGet, but does not compile in the LocalizeFromSource package
        //  so that we don't have to take a dependency on Stardew Valley.

        /// <summary>
        ///   Sets up Stardew Valley localization
        /// </summary>
        /// <param name="mod">The mod.</param>
        /// <param name="sourceLanguage">The language the strings in the code are written in - defaults to English.</param>
        /// <param name="doPseudoLocInDebug">If true, applies a tweak to all strings to make it clear that they are subject to translation.</param>
        public static void Initialize(StardewModdingAPI.Mod mod, string sourceLanguage = "en", bool doPseudoLocInDebug = true)
        {
            translator = new SdvTranslator(() => mod.Helper.Translation.Locale, sourceLanguage, Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "i18n"));
#if DEBUG
            DoPseudoLoc = doPseudoLocInDebug;
#endif
            OnBadTranslation += (message) => mod.Monitor.LogOnce($"Translation issue: {message}", StardewModdingAPI.LogLevel.Trace);
            OnTranslationFilesCorrupt += (message) => mod.Monitor.LogOnce($"Translation error: {message}", StardewModdingAPI.LogLevel.Error);
            OnHelpWanted += (message) => mod.Monitor.LogOnce(message, StardewModdingAPI.LogLevel.Alert);
        }
#endif

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
        ///   Marks a string literal as needing translation.
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
        public static string LF(FormattableString stringInSourceLocale)
            => EnsureTranslator().TranslateFormatted(stringInSourceLocale);

        /// <summary>
        ///   Declares that the string is invariant - just here to make it so that you can be declarative
        ///   in your code that your use of the string does not need to be localized and it was not an
        ///   oversight.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string I(string stringInSourceLocale)
            => stringInSourceLocale;

        /// <summary>
        ///   Declares that the string format is invariant - just here to make it so that you can be declarative
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
        public static event Action<string>? OnTranslationFilesCorrupt
        {
            add => EnsureTranslator().OnTranslationFilesCorrupt += value;
            remove => EnsureTranslator().OnTranslationFilesCorrupt -= value;
        }

        /// <summary>
        ///   Raised when a translation file is requested that is either missing or incomplete.  The message gives
        ///   an English description of how to do a translation.
        /// </summary>
        public static event Action<string>? OnHelpWanted
        {
            add => EnsureTranslator().OnHelpWanted += value;
            remove => EnsureTranslator().OnHelpWanted -= value;
        }

        /// <summary>
        ///   Raised when there is something wrong with the particular target language or some of the translations
        ///   within the language.
        /// </summary>
        public static event Action<string>? OnBadTranslation
        {
            add => EnsureTranslator().OnBadTranslation += value;
            remove => EnsureTranslator().OnBadTranslation -= value;
        }

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvEvent(FormattableString formattableString)
            => EnsureTranslator().SdvEvent(formattableString);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Quest code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvQuest(string questString)
            => EnsureTranslator().SdvQuest(questString);


        /// <summary>
        ///   Localizes the localizable part of a Stardew Valley mail message.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvMail(FormattableString mailFormat)
            => EnsureTranslator().SdvMail(mailFormat);

        private static SdvTranslator EnsureTranslator()
            => translator ?? throw new InvalidOperationException("The translation type has not been specified yet - ensure there is a call in ModEntry.Entry() to SdvLocalizeMethods.Initialize.");
    }
}
