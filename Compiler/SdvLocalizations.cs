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


        private static readonly Regex sdvLocalizableParts = new Regex(
            @"""(?<localizablePart>[^""]+)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex paths = new Regex(
            @"^(\([A-Z]+\))?\w+[\./\\][\w\./\\]*\w$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IEnumerable<string> SdvEvent(string eventCodeInSourceLanguage)
            => sdvLocalizableParts
                .Matches(eventCodeInSourceLanguage)
                .Select(m => m.Groups["localizablePart"].Value)
                .Where(s => !paths.IsMatch(s));

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

        public static IEnumerable<string> DomainSpecificInvariantMethodNames => [
            "StardewModdingAPI.IAssetName.IsEquivalentTo",
            "StardewModdingAPI.IAssetName.IsEquivalentTo",
            "StardewModdingAPI.IAssetName.StartsWith",
            "StardewModdingAPI.IAssetName.IsDirectlyUnderPath",
            "StardewValley.Farmer.getFriendshipHeartLevelForNPC",
            "StardewValley.Game1.playSound",
            "StardewValley.GameLocation.playSound",
            "Netcode.NetFields.AddField",
            "StardewModdingAPI.Events.AssetRequestedEventArgs.LoadFromModFile"
];

    }
}
