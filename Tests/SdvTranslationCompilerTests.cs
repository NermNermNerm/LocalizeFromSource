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
        private string i18nSourceFolder = null!;
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
            this.i18nSourceFolder = Path.Combine(this.projectFolder, "i18nSource");

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
            // Start with English-only
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);

            // Now do a translation by reading the built default.json and replacing English with German
            string translatedContent = this.ReadJsonRaw("default.json")
                .Replace("one two three four five", "eins zwei drei vier fünf")
                .Replace("count me in", "ich bin dabei");
            string translatedDePath = Path.Combine(this.projectFolder, "de.json");
            File.WriteAllText(translatedDePath, translatedContent);

            // Rebuild with our German translation
            testSubject.IngestTranslatedFile(translatedDePath, "nexus:TestyMcTester");
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);

            // Validate that it translates
            this.locale = "de-de";
            Assert.AreEqual("ich bin dabei", this.translator.Translate("count me in"));

            // But then the source changes...
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five six", false, "test", 57), // close to the other one
                new DiscoveredString("count me in", false, "test", 59),
                new DiscoveredString("I'm new here", false, "test", 61),
                ]);
            // ...and the built German translation has complaints in it...
            string newDeJson = this.ReadJsonRaw("de.json");
            Assert.IsTrue(newDeJson.Contains("// >>>SOURCE STRING CHANGED"));
            Assert.IsTrue(newDeJson.Contains("four five\""));
            Assert.IsTrue(newDeJson.Contains("four five six\""));

            Assert.IsTrue(newDeJson.Contains("// >>>MISSING TRANSLATION"));
            Assert.IsTrue(newDeJson.Contains("new here\""));
            this.translator = new SdvTranslator(() => this.locale, "en", this.i18nFolder);
            // ...this source didn't change - no drama here
            Assert.AreEqual("ich bin dabei", this.translator.Translate("count me in"));
            // ...this source changed only slightly, so it uses the old translation
            Assert.AreEqual("eins zwei drei vier fünf", this.translator.Translate("one two three four five six"));
            // ...new translations get dumped as-is
            Assert.AreEqual("I'm new here", this.translator.Translate("I'm new here"));

            // Now a new translator comes to our rescue
            translatedContent = newDeJson
                .Replace("\"eins zwei drei vier fünf\"", "\"eins zwei drei vier fünf sechs\"")
                .Replace("// \"", "\"")
                .Replace("\"\"", "\"Ich bin neu hier\"");
            File.WriteAllText(translatedDePath, translatedContent);
            testSubject.IngestTranslatedFile(translatedDePath, "nexus:NoobyMcNewb");
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five six", false, "test", 57), // close to the other one
                new DiscoveredString("count me in", false, "test", 59),
                new DiscoveredString("I'm new here", false, "test", 61),
                ]);
            newDeJson = this.ReadJsonRaw("de.json");
            // The updated built de.json is now clean...
            Assert.IsFalse(newDeJson.Contains(">>>"));
            // ...and credits both translators
            Assert.IsTrue(newDeJson.Contains("NoobyMcNewb"));
            Assert.IsTrue(newDeJson.Contains("TestyMcTester"));

            // ...and all the translations work again
            this.translator = new SdvTranslator(() => this.locale, "en", this.i18nFolder);
            Assert.AreEqual("ich bin dabei", this.translator.Translate("count me in"));
            Assert.AreEqual("eins zwei drei vier fünf sechs", this.translator.Translate("one two three four five six"));
            Assert.AreEqual("Ich bin neu hier", this.translator.Translate("I'm new here"));
        }

        // TODO Test when sources differ between translated file and built file.


        [TestMethod]
        public void TestLegacyTranslations()
        {
            // Start with English-only
            Directory.CreateDirectory(this.i18nSourceFolder);
            File.WriteAllText(Path.Combine(i18nSourceFolder, "default.json"), @"{
    ""key1"": ""old smapi-based value""
}");
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);

            // Now do a translation by reading the built default.json and replacing English with German
            string translatedContent = this.ReadJsonRaw("default.json")
                .Replace("one two three four five", "eins zwei drei vier fünf")
                .Replace("count me in", "ich bin dabei")
                .Replace("old smapi-based value", "alter Smapi-basierter Wert");
            string translatedDePath = Path.Combine(this.projectFolder, "de.json");
            File.WriteAllText(translatedDePath, translatedContent);

            // Rebuild with our German translation
            testSubject.IngestTranslatedFile(translatedDePath, "nexus:TestyMcTester");
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four five", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);

            // Validate that it translates
            this.locale = "de-de";
            Assert.AreEqual("ich bin dabei", this.translator.Translate("count me in"));
            Assert.AreEqual("alter Smapi-basierter Wert", this.translator.Translate("old smapi-based value"));
            var translations = this.ReadDeJson();

            // And retains the key from the legacy system
            Assert.IsTrue(translations.ContainsKey("key1"));
        }
    }
}