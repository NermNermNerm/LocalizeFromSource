using System.Text.RegularExpressions;
using NermNermNerm.Stardew.LocalizeFromSource;

namespace LocalizeFromSource
{
    /// <summary>
    ///   This is the compiler-side version of <see cref="SdvLocalize"/>.
    /// </summary>
    public static class SdvLocalizeCompiler
    {
        public static IEnumerable<string> L(string sourceString) => [sourceString];
        public static IEnumerable<string> LF(string sourceString) => [SdvTranslator.TransformCSharpFormatStringToSdvFormatString(sourceString)]; // ? Shouldn't this do the SDV formatting conversion?
        public static IEnumerable<string> I(string _) => [];
        public static IEnumerable<string> IF(string _) => [];

        // SdvEvent just pulls out all the stuff in double-quotes in the event text, then filters out things that look
        //   like paths to assets (which is the only thing I know of that's commonly put in quotes).  There's a finite
        //   number of commands that actually do anything localizable:
        //
        // speak grandpa
        // spritetext 4
        // textabovehead linus
        // message
        // end dialogue <NPC>
        // end dialogueWarpOut <NPC>
        // question
        // splitspeak
        //
        // However, there's the risk that somebody could add a custom command that does something localizable.  The risk
        // of overlocalizing is pretty small - hopefully localizers will be able to spot the thing that shouldn't be
        // localized and just copy it verbatim.

        public static IEnumerable<string> SdvQuest(string questString)
        {
            var splits = questString.Split('/', 5);
            return splits.Skip(1).Take(3).Where(s => s != "");
        }

        public static IEnumerable<string> SdvMail(string mailString)
        {
            int percentIndex = mailString.IndexOf('%');
            yield return (percentIndex < 0) ? mailString : mailString.Substring(0, percentIndex);
        }

        private static readonly Regex sdvLocalizableParts = new Regex(
            @"""(?<localizablePart>[^""]+)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex paths = new Regex(
            @"^(\([A-Z]+\))?\w+[\./\\][\w\./\\]*\w$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        ///   Gets the localizable parts out of the event code.
        /// </summary>
        /// <remarks>
        ///  The run-time version of this is method takes a formattable string, and we're not putting
        ///   any of these strings through <see cref="SdvTranslator.TransformCSharpFormatStringToSdvFormatString"/>.
        ///   That's because the formatting should only apply to the code, not to the content.
        /// </remarks>
        public static IEnumerable<string> SdvEvent(string eventCodeInSourceLanguage)
            => sdvLocalizableParts
                .Matches(eventCodeInSourceLanguage)
                .Select(m => m.Groups["localizablePart"].Value)
                .Where(s => !paths.IsMatch(s));

    }
}
