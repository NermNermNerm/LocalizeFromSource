using System.Runtime.CompilerServices;

namespace LocalizeFromSourceLib
{
    // It'd be great if we could somehow inherit in static methods, but we can't.  If we could,
    //  this would be a base class that is implementation-independent.  As it is, it's just a
    //  place to copy-paste from.

    ///// <summary>
    /////   Localization methods.
    ///// </summary>
    //public class LocalizeMethods
    //{

    //internal static Translator? Translator { get; set; }

    ///// <summary>Gets <see cref="Translator"/> and throws if it's null.</summary>
    //protected static Translator EnsureTranslator()
    //    => Translator ?? throw new InvalidOperationException("The translation type has not been specified yet - it looks like translations are being requested before localization setup has happened.");

    ///// <summary>
    /////   Set this to true and strings will get tweaked before they are displayed so that it's
    /////   easier to spot un-translated strings and increase the chances that a thing that shouldn't 
    /////   have been translated but was will cause a visible break.
    ///// </summary>
    //public static bool DoPseudoLoc
    //{
    //    get => EnsureTranslator().DoPseudoLoc;
    //    set => EnsureTranslator().DoPseudoLoc = value;
    //}

    ///// <summary>
    /////   Localizes either a string literal.
    ///// </summary>
    ///// <param name="stringInSourceLocale">
    /////   The string to be used - note that this must be a literal string, not an expression
    /////   that results in a string.
    ///// </param>
    //[MethodImpl(MethodImplOptions.NoInlining)]
    //public static string L(string stringInSourceLocale)
    //    => EnsureTranslator().Translate(stringInSourceLocale);

    ///// <summary>
    /////   Same as <see cref="L"/> except for interpolated strings.
    ///// </summary>
    //[MethodImpl(MethodImplOptions.NoInlining)]
    //public static string LF(FormattableString s)
    //    => EnsureTranslator().TranslateFormatted(s);

    ///// <summary>
    /////   Declares that the string is invariant - just here to make it so that you can be declarative
    /////   in your code that your use of the string does not need to be localized and it was not an
    /////   oversight.
    ///// </summary>
    //[MethodImpl(MethodImplOptions.NoInlining)]
    //public static string I(string stringInSourceLocale)
    //    => stringInSourceLocale;

    ///// <summary>
    /////   Declares that the string is invariant - just here to make it so that you can be declarative
    /////   in your code that your use of the string does not need to be localized and it was not an
    /////   oversight.
    ///// </summary>
    //[MethodImpl(MethodImplOptions.NoInlining)]
    //public static string IF(FormattableString stringInSourceLocale)
    //    => FormattableString.Invariant(stringInSourceLocale);

    ///// <summary>
    /////   Raised when there is something wrong with the translation files that will prevent it from working in
    /////   any language other than the source.  The argument is a string containing the nature of the fault.
    ///// </summary>
    //public static event Action<string>? OnTranslationFilesCorrupt;

    ///// <summary>
    /////   Raised when there is something wrong with the particular target language or some of the translations
    /////   within the language.
    ///// </summary>
    //public static event Action<string>? OnBadTranslation;

    ///// <summary>
    /////   Raises <see cref="OnTranslationFilesCorrupt"/>.
    ///// </summary>
    ///// <remarks>Test classes can override this to validate that these events are generated without having to touch a static.</remarks>
    //internal static void RaiseTranslationFilesCorrupt(string error)
    //    => OnTranslationFilesCorrupt?.Invoke(error);

    ///// <summary>
    /////   Raises <see cref="OnBadTranslation"/>.
    ///// </summary>
    ///// <remarks>Test classes can override this to validate that these events are generated without having to touch a static.</remarks>
    //internal static void RaiseBadTranslation(string warning)
    //    => OnBadTranslation?.Invoke(warning);
    //}
}
