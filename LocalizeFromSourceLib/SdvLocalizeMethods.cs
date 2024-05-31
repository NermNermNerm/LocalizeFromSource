using System.Runtime.CompilerServices;

namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   Localization methods.
    /// </summary>
    public class SdvLocalizeMethods : LocalizeMethods
    {
        /// <summary>
        ///   Sets up Stardew Valley localization.
        /// </summary>
        public void Initialize(Func<string>localeGetter, string sourceLocale = "en-us")
        {
            Translator = new SdvTranslator(localeGetter, sourceLocale);
        }

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

        /// <inheritDoc/>
        protected new static SdvTranslator EnsureTranslator()
            => (SdvTranslator)LocalizeMethods.EnsureTranslator();
    }
}
