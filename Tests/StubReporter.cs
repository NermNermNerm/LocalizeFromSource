using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LocalizeFromSource;
using Mono.Cecil.Cil;

namespace LocalizeFromSourceTests
{
    internal class StubReporter
        : Reporter
    {
        public StubReporter()
            : base(new Config(true, Array.Empty<Regex>(), Array.Empty<string>()))
        {
        }

        public List<string> BadStrings { get; } = new List<string>();

        private string MakeString(SequencePoint? sequencePoint)
            => sequencePoint is null ? "(?)" : $"({Path.GetFileName(sequencePoint.Document.Url)}:{sequencePoint.StartLine})";

        public override void ReportBadString(string s, SequencePoint? sequencePoint)
        {
            BadStrings.Add(MakeString(sequencePoint) + s);
        }
    }
}
