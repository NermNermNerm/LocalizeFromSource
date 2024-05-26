using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalizeFromSource;
using Mono.Cecil.Cil;

namespace LocalizeFromSourceLib.Tests
{
    internal class StubReporter
        : Reporter
    {
        public StubReporter()
            : base(isStrict: true)
        {
        }

        public List<string> BadStrings { get; } = new List<string>();

        private string MakeString(SequencePoint? sequencePoint)
            => sequencePoint is null ? "(?)" : $"({Path.GetFileName(sequencePoint.Document.Url)}:{sequencePoint.StartLine})";

        public override void ReportBadFormatString(string s, SequencePoint? sequencePoint)
        {
            BadStrings.Add(MakeString(sequencePoint) + s);
        }

        public override void ReportBadString(string s, SequencePoint? sequencePoint)
        {
            BadStrings.Add(MakeString(sequencePoint) + s);
        }
    }
}
