using System.Reflection;
using System.Text.RegularExpressions;
using LocalizeFromSource;
using Mono.Cecil;
using NermNermNerm.Stardew.LocalizeFromSource;

namespace LocalizeFromSourceLib.Tests
{
    [TestClass]
    public class DecompilerTests
    {
        Decompiler testSubject = null!;
        AssemblyDefinition assembly = null!;
        StubReporter stubReporter = null!;
        MethodDefinition decompilerTestTarget = null!;
        MethodDefinition noStrictOverrideTestTarget = null!;
        MethodDefinition sdvCustomTestTarget = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            Config defaultConfig = new Config(true, Array.Empty<Regex>(), Array.Empty<string>());
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            this.assembly = AssemblyDefinition.ReadAssembly(thisAssemblyPath, new ReaderParameters { ReadSymbols = true });
            var combinedConfig = CombinedConfig.Create(this.assembly, Environment.CurrentDirectory, defaultConfig);
            this.testSubject = new Decompiler(combinedConfig);
            this.stubReporter = new StubReporter();

            this.decompilerTestTarget = assembly.Modules.First().Types.First(t => t.Name == nameof(DecompilerTests)).Methods.First(t => t.Name == nameof(DecompilerTestTarget));
            this.noStrictOverrideTestTarget = assembly.Modules.First().Types.First(t => t.Name == nameof(DecompilerTests)).Methods.First(t => t.Name == nameof(NoStrictOverrideTestTarget));
            this.sdvCustomTestTarget = assembly.Modules.First().Types.First(t => t.Name == nameof(DecompilerTests)).Methods.First(t => t.Name == nameof(SdvCustomStringsTestTarget));
        }

        [TestMethod]
        public void Test1()
        {
            testSubject.FindLocalizableStrings(decompilerTestTarget, this.stubReporter);
            Assert.AreEqual(2, this.stubReporter.LocalizableStrings.Count);
            Assert.IsTrue(this.stubReporter.LocalizableStrings.Any(s => s.localizedString == "should be found"));
            Assert.IsTrue(this.stubReporter.LocalizableStrings.Any(s => s.localizedString == "should be found{{arg0}}"));
            Assert.AreEqual(1, this.stubReporter.BadStrings.Count);
            Assert.IsTrue(this.stubReporter.BadStrings.Any(s => s.Contains("Should be a problem")));
        }

        [TestMethod]
        public void CustomSdvStrings()
        {
            testSubject.FindLocalizableStrings(this.sdvCustomTestTarget, this.stubReporter);
            Assert.AreEqual(2, this.stubReporter.LocalizableStrings.Count);
            Assert.IsTrue(this.stubReporter.LocalizableStrings.Any(s => s.localizedString == "Mail content"));
            Assert.IsTrue(this.stubReporter.LocalizableStrings.Any(s => s.localizedString == "Mail content with no code"));
        }
        [TestMethod]
        public void RespectsNoStrictAttribute()
        {
            testSubject.FindLocalizableStrings(this.noStrictOverrideTestTarget, this.stubReporter);
            Assert.AreEqual(1, this.stubReporter.LocalizableStrings.Count);
            Assert.IsTrue(this.stubReporter.LocalizableStrings.Any(s => s.localizedString == "should be found"));
            Assert.AreEqual(0, this.stubReporter.BadStrings.Count);
        }

        public void DecompilerTestTarget()
        {
            new StardewValley.GameLocation().playSound("ignored");
            Console.WriteLine(SdvLocalizeMethods.I("should be ignored"));
            Console.WriteLine(SdvLocalizeMethods.IF($"should be ignored{Environment.NewLine}"));
            Console.WriteLine(SdvLocalizeMethods.L("should be found"));
            Console.WriteLine(SdvLocalizeMethods.LF($"should be found{Environment.NewLine}"));
            this.InvariantByAttribute("ignored");
            Console.WriteLine("Should be a problem");
        }


        public void SdvCustomStringsTestTarget()
        {
            Console.WriteLine(SdvLocalizeMethods.SdvMail("Mail content%code"));
            Console.WriteLine(SdvLocalizeMethods.SdvMail("Mail content with no code"));
        }

        [NoStrict]
        public void NoStrictOverrideTestTarget()
        {
            Console.WriteLine("normally this would be a problem.");
            Console.WriteLine(SdvLocalizeMethods.L("should be found"));
        }
    }

    public static class TestExtensions
    {
        [ArgumentIsCultureInvariant]
        public static void InvariantByAttribute(this DecompilerTests _this, string s) { }
    }
}
