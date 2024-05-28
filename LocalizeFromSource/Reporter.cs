using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Cil;

namespace LocalizeFromSource
{
    public class Reporter(Config config)
    {
        private List<DiscoveredString> discoveredStrings = new List<DiscoveredString>();
        private SequencePoint? lastReportSequencePoint;

        public bool AnyUnmarkedStringsEncountered { get; private set; }
        public bool AnyUsageErrorsEncountered { get; private set; }

        public IReadOnlyCollection<DiscoveredString> LocalizableStrings => this.discoveredStrings;

        private string GetPositionString(SequencePoint? sequencePoint)
            => sequencePoint is null ? "" : $"{sequencePoint.Document.Url}({sequencePoint.StartLine},{sequencePoint.StartColumn})";

        public virtual void ReportBadString(string s, SequencePoint? sequencePoint)
        {
            if (config.InvariantStringPatterns.Any(r => r.IsMatch(s)))
            {
                return;
            }

            this.AnyUnmarkedStringsEncountered = true;

            // Format strings can get broken into bits - minimize the spam by only reporting one error per line.
            if (sequencePoint is null
                || lastReportSequencePoint is null
                || lastReportSequencePoint.Document.Url != sequencePoint.Document.Url
                || lastReportSequencePoint.StartLine != sequencePoint.StartLine)
            {
                Console.Error.WriteLine(
                    $"{GetPositionString(sequencePoint)} : {(config.IsStrict ? "error" : "warning")} {TranslationCompiler.ErrorPrefix}{TranslationCompiler.StringNotMarked:d4}: "
                    + $"String is not marked as invariant or localized - it should be surrounded with I(), IF(), L() or LF() to indicate which it is: \"{s}\"");
            }

            lastReportSequencePoint = sequencePoint;
        }

        public virtual void ReportLocalizedString(string s, SequencePoint? sequencePoint)
        {
            if (s == "")
            {
                this.ReportBadUsage(sequencePoint, TranslationCompiler.LocalizingEmpty, "The empty string should not be localized - it's empty in all locales.");
            }
            else
            {
                discoveredStrings.Add(new DiscoveredString(s, false, sequencePoint?.Document.Url, sequencePoint?.StartLine));
            }
        }

        public virtual void ReportLocalizedFormatString(string s, SequencePoint? sequencePoint)
        {
            if (s == "")
            {
                this.ReportBadUsage(sequencePoint, TranslationCompiler.LocalizingEmpty, "The empty string should not be localized - it's empty in all locales.");
            }
            else
            {
                discoveredStrings.Add(new DiscoveredString(s, true, sequencePoint?.Document.Url, sequencePoint?.StartLine));
            }
        }

        public virtual void ReportBadUsage(SequencePoint? sequencePoint, int errorCode, string errorMessage)
        {
            this.AnyUsageErrorsEncountered = true;
            Console.Error.WriteLine(
                $"{GetPositionString(sequencePoint)} : error {TranslationCompiler.ErrorPrefix}{errorCode:d4}: {errorMessage}");
        }
    }
}
