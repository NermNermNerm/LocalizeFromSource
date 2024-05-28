using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocalizeFromSource
{
    /// <summary>
    ///   Methods that are the compiler versions of the SDV-specific localization methods in LocalizeFromSourceLib.LocalizeMethods
    /// </summary>
    public static class SdvLocalizations
    {

        private static readonly Regex sdvLocalizableParts = new Regex(
            @"""(?<localizablePart>[^""]+)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IEnumerable<string> SdvEvent(string eventCodeInSourceLanguage)
            => sdvLocalizableParts
                .Matches(eventCodeInSourceLanguage)
                .Select(m => m.Groups["localizablePart"].Value);
    }
}
