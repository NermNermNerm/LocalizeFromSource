using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LocalizeFromSource;
using Mono.Cecil;
using static LocalizeFromSource.DecompileCommand;

namespace LocalizeFromSourceLib.Tests
{
    [TestClass]
    public class DecompilerTests
    {
        [TestMethod]
        public void Test1()
        {
            Decompiler testSubject = new Decompiler();
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(thisAssemblyPath, new ReaderParameters { ReadSymbols = true });
            var decompilerTestTarget = assembly.Modules.First().Types.First(t => t.Name == nameof(DecompilerTests)).Methods.First(t => t.Name == nameof(DecompilerTestTarget));
            var reporter = new StubReporter();
            var compiler = new SdvTranslationCompiler();

            testSubject.FindLocalizableStrings(decompilerTestTarget, reporter, compiler.GetInvariantMethodNames(assembly));
            Assert.AreEqual(2, reporter.LocalizableStrings.Count);
            Assert.IsTrue(reporter.LocalizableStrings.Any(s => s.localizedString == "should be found"));
            Assert.IsTrue(reporter.LocalizableStrings.Any(s => s.localizedString == "should be found{0}"));
            Assert.AreEqual(1, reporter.BadStrings.Count);
            Assert.IsTrue(reporter.BadStrings.Any(s => s.Contains("Should be a problem")));
        }

        public void DecompilerTestTarget()
        {
            new StardewValley.GameLocation().playSound("ignored");
            Console.WriteLine(LocalizeMethods.I("should be ignored"));
            Console.WriteLine(LocalizeMethods.IF($"should be ignored{Environment.NewLine}"));
            Console.WriteLine(LocalizeMethods.L("should be found"));
            Console.WriteLine(LocalizeMethods.LF($"should be found{Environment.NewLine}"));
            this.InvariantByAttribute("ignored");
            Console.WriteLine("Should be a problem");
        }
    }

    public static class TestExtensions
    {
        [ArgumentIsCultureInvariant]
        public static void InvariantByAttribute(this DecompilerTests _this, string s) { }
    }
}
