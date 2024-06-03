using System.Reflection;
using System.Runtime.CompilerServices;

namespace NermNermNerm.Stardew.LocalizeFromSource
{
    /// <summary>
    ///   Localization methods.
    /// </summary>
    public static class SdvLocalizeMethods
    {
        private static Dictionary<Assembly, SdvTranslator> translators = new Dictionary<Assembly, SdvTranslator>();

        /// <summary>
        ///   Sets up Stardew Valley localization.
        /// </summary>
        /// <param name="localeGetter">A function that will return the current locale.</param>
        /// <param name="sourceLocale">The language identifier for the language that your source code is written in.  (E.g. "en").</param>
        /// <param name="callingAssemblies">
        ///   If supplied, this is a list of all the assemblies that are accessing the same translation set.
        ///   If none are supplied, the calling assembly is used.
        /// </param>
        public static void Initialize(Func<string>localeGetter, string sourceLocale, params Assembly[] callingAssemblies)
        {
            // Right now we're only built for StardewValley, where I think it's completely nonsensical to think that
            // more than one assembly would be involved in translation, and the compiler doesn't support the idea of
            // multiple assemblies either (although it wouldn't be hard to make it so).  Still, in the interests of
            // having a future-resistant API, we'll support an array.

            var assemblies = callingAssemblies.Length == 0 ? [Assembly.GetCallingAssembly()] : callingAssemblies;

            // Someday: Make a factory pattern that infers the appropriate translation system based on looking at the calling assemblies.
            var translator = new SdvTranslator(localeGetter, sourceLocale, Path.Combine(Path.GetDirectoryName(assemblies.First().Location)!, "i18n"));

            if (assemblies.Any(a => translators.ContainsKey(a)))
            {
                throw new InvalidOperationException($"{nameof(Initialize)} should not be called twice from the same assembly.");
            }

            foreach (var assembly in assemblies)
            {
                translators[assembly] = translator;
            }
        }

        /// <summary>
        ///   Sets up Stardew Valley localization.
        /// </summary>
        public static void Initialize(Func<string> localeGetter)
            => Initialize(localeGetter, "en", Assembly.GetCallingAssembly());


        /// <summary>
        ///   Set this to true and strings will get tweaked before they are displayed so that it's
        ///   easier to spot un-translated strings and increase the chances that a thing that shouldn't 
        ///   have been translated but was will cause a visible break.
        /// </summary>
        public static bool DoPseudoLoc
        {
            get => EnsureTranslator(Assembly.GetCallingAssembly()).DoPseudoLoc;
            set => EnsureTranslator(Assembly.GetCallingAssembly()).DoPseudoLoc = value;
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
            => EnsureTranslator(Assembly.GetCallingAssembly()).Translate(stringInSourceLocale);

        /// <summary>
        ///   Same as <see cref="L"/> except for interpolated strings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string LF(FormattableString s)
            => EnsureTranslator(Assembly.GetCallingAssembly()).TranslateFormatted(s);

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
        public static event Action<string>? OnTranslationFilesCorrupt
        {
            add => EnsureTranslator(Assembly.GetCallingAssembly()).OnTranslationFilesCorrupt += value;
            remove => EnsureTranslator(Assembly.GetCallingAssembly()).OnTranslationFilesCorrupt -= value;
        }

        /// <summary>
        ///   Raised when there is something wrong with the particular target language or some of the translations
        ///   within the language.
        /// </summary>
        public static event Action<string>? OnBadTranslation
        {
            add => EnsureTranslator(Assembly.GetCallingAssembly()).OnBadTranslation += value;
            remove => EnsureTranslator(Assembly.GetCallingAssembly()).OnBadTranslation -= value;
        }

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvEvent(FormattableString formattableString)
            => EnsureTranslator(Assembly.GetCallingAssembly()).SdvEvent(formattableString);

        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvQuest(string questString)
            => EnsureTranslator(Assembly.GetCallingAssembly()).SdvQuest(questString);


        /// <summary>
        ///   Localizes the strings within Stardew Valley Event code.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string SdvMail(string questString)
            => EnsureTranslator(Assembly.GetCallingAssembly()).SdvMail(questString);

        /// <inheritDoc/>
        private static SdvTranslator EnsureTranslator(Assembly caller)
        {
            if (translators.TryGetValue(caller, out var translator))
            {
                return translator;
            }
            else
            {
                throw new InvalidOperationException("The translation type has not been specified yet - ensure there is a call in ModEntry.Entry() to SdvLocalizeMethods.Initialize.");
            }
        }
    }
}
