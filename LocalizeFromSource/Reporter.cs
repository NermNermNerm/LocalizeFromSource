using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Cil;

namespace LocalizeFromSource
{
    internal class Reporter(bool isStrict)
    {
        private List<DiscoveredString> discoveredStrings = new List<DiscoveredString>();

        public bool AnyUnmarkedStringsEncountered { get; private set; }

        public IReadOnlyCollection<DiscoveredString> LocalizableStrings => this.discoveredStrings;

        private string GetPositionString(SequencePoint? sequencePoint)
            => sequencePoint is null ? "" : $"{sequencePoint.Document.Url}({sequencePoint.StartLine},{sequencePoint.StartColumn})";

        public virtual void ReportBadString(string s, SequencePoint? sequencePoint)
        {
            this.AnyUnmarkedStringsEncountered = true;
            Console.Error.WriteLine(
                $"{GetPositionString(sequencePoint)} : {(isStrict ? "error" : "warning")} {TranslationCompiler.ErrorPrefix}{TranslationCompiler.StringNotMarked:d4}: "
                + $"String is not marked as invariant or localized - it should be surrounded with I() or L() to indicate which it is: \"{s}\"");
        }

        public virtual void ReportBadFormatString(string s, SequencePoint? sequencePoint)
        {
            this.AnyUnmarkedStringsEncountered = true;
            Console.Error.WriteLine(
                $"{GetPositionString(sequencePoint)} : {(isStrict ? "error" : "warning")} {TranslationCompiler.ErrorPrefix}{TranslationCompiler.StringNotMarked:d4}: "
                + $"Formatted string is not marked as invariant or localized - it should be surrounded with IF() or LF() to indicate which it is: \"{s}\"");
        }

        public virtual void ReportLocalizedString(string s, SequencePoint? sequencePoint)
        {
            discoveredStrings.Add(new DiscoveredString(s, false, sequencePoint?.Document.Url, sequencePoint?.StartLine));
        }

        public virtual void ReportLocalizedFormatString(string s, SequencePoint? sequencePoint)
        {
            discoveredStrings.Add(new DiscoveredString(s, true, sequencePoint?.Document.Url, sequencePoint?.StartLine));
        }
    }
}
