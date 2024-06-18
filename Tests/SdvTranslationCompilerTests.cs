using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalizeFromSource;
using Mono.Cecil;
using NermNermNerm.Stardew.LocalizeFromSource;

namespace LocalizeFromSourceTests
{
    [TestClass]
    public class SdvTranslationCompilerTests
    {
        private const string TestFolderName = "ut1";
        private string projectFolder = null!;
        private string i18nFolder = null!;
        private string locale = "en";

        private TestableSdvTranslationCompiler testSubject = null!;
        private SdvTranslator translator = null!;

        [TestInitialize]
        public void Initialize()
        {
            if (Directory.Exists(TestFolderName))
            {
                Directory.Delete(TestFolderName, recursive: true);
            }
            Directory.CreateDirectory(TestFolderName);

            this.projectFolder = Path.Combine(Environment.CurrentDirectory, TestFolderName);
            this.i18nFolder = Path.Combine(this.projectFolder, "i18n");

            Config defaultConfig = new Config(true, Array.Empty<Regex>(), Array.Empty<string>());
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var assembly = AssemblyDefinition.ReadAssembly(thisAssemblyPath, new ReaderParameters { ReadSymbols = true });
            var combinedConfig = new CombinedConfig(projectFolder, defaultConfig);
            this.testSubject = new TestableSdvTranslationCompiler(combinedConfig, projectFolder);
            this.translator = new SdvTranslator(() => this.locale,  "en", this.i18nFolder);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Directory.Delete(TestFolderName, true);
        }

        private T ReadJson<T>(string filename)
        {
            var result = JsonSerializer.Deserialize<T>(
                this.ReadJsonRaw(filename),
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
            Assert.IsNotNull(result);
            return result!;
        }

        private void WriteJson<T>(string filename, T jsonData)
        {
            var json = JsonSerializer.Serialize<T>(jsonData);
            this.WriteJsonRaw(filename, json);
        }

        private string ReadJsonRaw(string filename)
                => File.ReadAllText(Path.Combine(i18nFolder, filename));

        private void WriteJsonRaw(string filename, string contents)
                => File.WriteAllText(Path.Combine(i18nFolder, filename), contents);

        private Dictionary<string, string> ReadDefaultJson()
            => this.ReadJson<Dictionary<string, string>>("default.json");

        private Dictionary<string, string> ReadDeJson()
            => this.ReadJson<Dictionary<string, string>>("de.json");

        private void WriteDeJson(Dictionary<string, string> translations)
            => this.WriteJson("de.json", translations);

        private Dictionary<string, string> ReadDeEditsJson()
            => this.ReadJson<Dictionary<string, string>>("de.edits.json");

        [TestMethod]
        public void BasicDefaultJsonTests()
        {
            // Starts from nothing
            testSubject.GenerateI18nFiles([new DiscoveredString("one two three", false, "test.cs", 57)]);
            var defaultJson = this.ReadDefaultJson();
            Assert.AreEqual(1, defaultJson!.Count);
            Assert.IsTrue(defaultJson.Values.Contains("one two three"));

            // check that the generated content has links
            var raw = this.ReadJsonRaw("default.json");
            Assert.IsTrue(raw.Contains("github"));
            Assert.IsTrue(raw.Contains("test.cs"));
            Assert.IsTrue(raw.Contains("57"));

            // Starts adds new thing, recognizes old thing and leaves it alone.
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test.cs", 57),
                new DiscoveredString("a b c", false, "test.cs", 67),
                ]);
            defaultJson = this.ReadJson<Dictionary<string, string>>(Path.Combine(i18nFolder, "default.json"));
            Assert.AreEqual(2, defaultJson!.Count);
            Assert.IsTrue(defaultJson.Values.Contains("one two three"));
            Assert.IsTrue(defaultJson.Values.Contains("a b c"));
        }

        [TestMethod]
        public void FormatTest1()
        {
            testSubject.GenerateI18nFiles([new DiscoveredString("one {0}, {1}, and {2}", true, "test", 57)]);
            var defaultJson = this.ReadDefaultJson();
            Assert.AreEqual(1, defaultJson!.Count);
            Assert.IsTrue(defaultJson.Values.Contains("one {{arg0}}, {{arg1}}, and {{arg2}}"));

            WriteDeJson(new Dictionary<string, string>() { { defaultJson.Keys.First(), "ein {{arg2}}, {{arg1}}, {{arg0}} fin" } });

            this.locale = "de-de";
            string translation = this.translator.TranslateFormatted($"one {"a"}, {"b"}, and {"c"}");
            Assert.AreEqual("ein c, b, a fin", translation);
            this.locale = "en-us";
            translation = this.translator.TranslateFormatted($"one {"a"}, {"b"}, and {"c"}");
            Assert.AreEqual("one a, b, and c", translation);
            this.locale = "es-es";
            translation = this.translator.TranslateFormatted($"one {"a"}, {"b"}, and {"c"}");
            Assert.AreEqual("one a, b, and c", translation);
        }


        [TestMethod]
        public void FormatTest2()
        {
            // Ensure names and format specifiers are respected
            testSubject.GenerateI18nFiles(
                [new DiscoveredString("one {0:d4}|one|, {1:d3}|two|.", true, "test", 57)]);
            var defaultJson = this.ReadDefaultJson();
            Assert.AreEqual(1, defaultJson!.Count);
            Assert.IsTrue(defaultJson.Values.Contains("one {{one:d4}}, {{two:d3}}."));

            WriteDeJson(new Dictionary<string, string>() { { defaultJson.Keys.First(), "ein {{one:x2}}, {{two:x3}} fin." } });

            this.locale = "de-de";
            var translation = this.translator.TranslateFormatted($"one {123:d4}|one|, {234:d3}|two|.");
            Assert.AreEqual("ein 7b, 0ea fin.", translation);
            this.locale = "en-us";
            translation = this.translator.TranslateFormatted($"one {123:d4}|one|, {234:d3}|two|.");
            Assert.AreEqual("one 0123, 234.", translation);
            this.locale = "es-es";
            translation = this.translator.TranslateFormatted($"one {123:d4}|one|, {234:d3}|two|.");
            Assert.AreEqual("one 0123, 234.", translation);
        }

        [TestMethod]
        public void TranslationTests()
        {
            string deIncomingPath = Path.Combine(this.projectFolder, "de.json");

            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);

            string translatedDePath = Path.Combine(this.projectFolder, "de.json");
            string translatedContent = this.ReadJsonRaw("default.json")
                .Replace("one two three four five", "eins zwei drei vier geben")
                .Replace("count me in", "ich bin dabei");
            File.WriteAllText(translatedDePath, translatedContent);

            testSubject.IngestTranslatedFile(translatedDePath, "nexus:TestyMcTester");
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);

            this.locale = "de-de";
            Assert.AreEqual("ich bin dabei", this.translator.Translate("count me in"));

            // Now there are source changes without updates to the translation
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five six", false, "test", 57), // close to the other one
                new DiscoveredString("count me in", false, "test", 59),
                new DiscoveredString("I'm new here", false, "test", 61),
                ]);
            string newDeJson = this.ReadJsonRaw("de.json");
            Assert.IsTrue(newDeJson.Contains("// >>>SOURCE STRING CHANGED"));
            Assert.IsTrue(newDeJson.Contains("four five\""));
            Assert.IsTrue(newDeJson.Contains("four five six\""));

            Assert.IsTrue(newDeJson.Contains("// >>>MISSING TRANSLATION"));
            Assert.IsTrue(newDeJson.Contains("new here\""));

            this.translator = new SdvTranslator(() => this.locale, "en", this.i18nFolder);
            Assert.AreEqual("ich bin dabei", this.translator.Translate("count me in"));
            Assert.AreEqual("eins zwei drei vier geben", this.translator.Translate("one two three four five six"));
            Assert.AreEqual("I'm new here", this.translator.Translate("I'm new here"));
        }

        private static Dictionary<string, TranslationEdit> ReadTranslationEditsFile(string path)
        {
            if (File.Exists(path))
            {
                var result = JsonSerializer.Deserialize<Dictionary<string, TranslationEdit>>(File.ReadAllText(path), new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })!;
                if (result is null)
                {
                    throw new JsonException($"{path} contains just 'null'.");
                }
                return result;
            }
            else
            {
                return new Dictionary<string, TranslationEdit>();
            }
        }
    }
}