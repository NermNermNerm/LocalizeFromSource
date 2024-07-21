using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Cil;
using Spectre.Console;

namespace LocalizeFromSource
{
    public class Reporter(Config config)
    {
        private Dictionary<string,DiscoveredString> discoveredStrings = new();
        private SequencePoint? lastReportSequencePoint;

        public bool AnyUnmarkedStringsEncountered { get; private set; }
        public bool AnyUsageErrorsEncountered { get; private set; }

        public IReadOnlyCollection<DiscoveredString> LocalizableStrings => this.discoveredStrings.Values;

        public virtual void ReportUnmarkedString(string s, SequencePoint? sequencePoint)
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
                this.WriteMessage(
                    sequencePoint,
                    config.IsStrict ? WarningOrError.Error : WarningOrError.Warning,
                    TranslationCompiler.StringNotMarked,
                    $"String is not marked as invariant or localized - it should be surrounded with I(), IF(), L() or LF() to indicate which it is: \"{s}\"");
            }

            lastReportSequencePoint = sequencePoint;
        }

        public virtual void ReportLocalizedString(string s, SequencePoint? sequencePoint)
        {
            if (s == "")
            {
                this.ReportEmptyStringShouldNotBeLocalized(sequencePoint);
            }
            else
            {
                // Consider - if it's already there, add a new line&file.
                discoveredStrings[s] = new DiscoveredString(s, false, sequencePoint?.Document.Url, sequencePoint?.StartLine);
            }
        }

        public virtual void ReportLocalizedFormatString(string s, SequencePoint? sequencePoint)
        {
            if (s == "")
            {
                this.ReportEmptyStringShouldNotBeLocalized(sequencePoint);
            }
            else
            {
                // Consider - if it's already there, add a new line&file.
                discoveredStrings[s] = new DiscoveredString(s, true, sequencePoint?.Document.Url, sequencePoint?.StartLine);
            }
        }

        public virtual void ReportImproperUseOfMethod(SequencePoint? sequencePoint, string message)
        {
            this.WriteMessage(sequencePoint, WarningOrError.Error, TranslationCompiler.ImproperUseOfMethod, message);
        }

        public virtual void ReportEmptyStringShouldNotBeLocalized(SequencePoint? sequencePoint)
        {
            this.WriteMessage(sequencePoint, WarningOrError.Error, TranslationCompiler.LocalizingEmpty, "The empty string should not be localized - it's empty in all locales.");
        }

        public virtual void ReportGitRepoError(string errorMessage)
        {
            this.WriteMessage(null, WarningOrError.Warning, TranslationCompiler.LocalizingEmpty, errorMessage);
        }

        protected enum WarningOrError
        {
            Error,
            Warning,
        }

        protected virtual void WriteMessage(SequencePoint? sequencePoint, WarningOrError kind, int errorCode, string errorMessage)
        {
            // Definition for how to construct msbuild task error messages:
            // https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks?view=vs-2022

            StringBuilder message = new StringBuilder();
            if (sequencePoint != null)
            {
                message.Append($"{sequencePoint.Document.Url}({sequencePoint.StartLine},{sequencePoint.StartColumn})");
            }
            else
            {
                message.Append("LfsCompiler");
            }

            message.Append(" : ");
            message.Append(kind == WarningOrError.Error ? "error" : "warning");
            message.Append(" ");
            message.Append(TranslationCompiler.ErrorPrefix);
            message.Append(errorCode.ToString("d4"));
            message.Append(": ");
            message.Append(errorMessage);

            Console.Error.WriteLine(message.ToString());
        }
    }
}
