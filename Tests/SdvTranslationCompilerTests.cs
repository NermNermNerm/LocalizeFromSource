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

            string projectPath = Path.Combine(Environment.CurrentDirectory, TestFolderName);
            this.i18nFolder = Path.Combine(projectPath, "i18n");

            Config defaultConfig = new Config(true, Array.Empty<Regex>(), Array.Empty<string>());
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var assembly = AssemblyDefinition.ReadAssembly(thisAssemblyPath, new ReaderParameters { ReadSymbols = true });
            var combinedConfig = new CombinedConfig(projectPath, defaultConfig);
            this.testSubject = new TestableSdvTranslationCompiler(combinedConfig, projectPath);
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
            string deJsonPath = Path.Combine(i18nFolder, "de.json");
            string deEditsJsonPath = Path.Combine(i18nFolder, "de.edits.json");

            // Add some source language stuff - do it in two passes to guarantee key order for testing purposes
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test", 57),
                ]);
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);
            var defaultJson = this.ReadDefaultJson();
            Assert.AreEqual(2, defaultJson!.Count);
            Assert.AreEqual("one two three", defaultJson["000001"]);
            Assert.AreEqual("count me in", defaultJson["000002"]);

            // Initial translation - translator copies default.json and updates the translation file.
            defaultJson["000001"] = "eins zwei drei";
            defaultJson["000002"] = "ich bin dabei";
            string unformattedDeJson = JsonSerializer.Serialize(defaultJson);
            File.WriteAllText(
                Path.Combine(i18nFolder, "de.json"),
                unformattedDeJson);

            Assert.AreEqual(0, testSubject.Errors.Count);
            // Run in verify mode
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);
            Assert.AreEqual(0, testSubject.Errors.Count); // It shouldn't do anything.
            Assert.AreEqual(unformattedDeJson, File.ReadAllText(deJsonPath)); // It shouldn't even reformat the language file.
            Assert.IsFalse(File.Exists(deEditsJsonPath));

            // Add a new translation and edit another
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thang", false, "test", 61)
                ]);
            var edits = ReadTranslationEditsFile(deEditsJsonPath);
            Assert.AreEqual(2, edits.Count);
            Assert.AreEqual(edits["000002"].oldSource, "count me in");
            Assert.AreEqual(edits["000002"].oldTarget, "ich bin dabei");
            Assert.AreEqual(edits["000002"].newSource, "count me in!");
            Assert.AreEqual(edits["000002"].newTarget, null);
            Assert.AreEqual(edits["000003"].oldSource, null);
            Assert.AreEqual(edits["000003"].oldTarget, null);
            Assert.AreEqual(edits["000003"].newSource, "I added a new thang");
            Assert.AreEqual(edits["000003"].newTarget, null);
            var deTranslation = this.ReadDeJson();
            Assert.AreEqual(1, deTranslation!.Count);
            Assert.AreEqual("eins zwei drei", deTranslation["000001"]);

            // German translator updates the 'count me in' translation but sees the 'thang' spelling error and ignores that one.
            File.WriteAllText(deEditsJsonPath, JsonSerializer.Serialize(new Dictionary<string, TranslationEdit>()
            {
                { "000002", edits["000002"] with { newTarget = "ich bin dabei!" } },
                { "000003", edits["000003"] }
            }));
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thang", false, "test", 61)
                ]);
            // Then it removes the edit we took care of and leaves the one we didn't
            edits = ReadTranslationEditsFile(deEditsJsonPath);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(edits["000003"].oldSource, null);
            Assert.AreEqual(edits["000003"].oldTarget, null);
            Assert.AreEqual(edits["000003"].newSource, "I added a new thang");
            Assert.AreEqual(edits["000003"].newTarget, null);
            deTranslation = this.ReadDeJson();
            Assert.AreEqual(2, deTranslation!.Count);
            Assert.AreEqual("eins zwei drei", deTranslation["000001"]);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);

            // German translator gets creative and fixes that thang.
            File.WriteAllText(deEditsJsonPath, JsonSerializer.Serialize(new Dictionary<string, TranslationEdit>()
            {
                { "000003", edits["000003"] with { newSource = "I added a new thing", newTarget = "Ich habe etwas Neues hinzugefügt" } }
            }));
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thing", false, "test", 61)
                ]);
            deTranslation = this.ReadDeJson();
            Assert.IsFalse(File.Exists(deEditsJsonPath));
            Assert.AreEqual(3, deTranslation!.Count);
            Assert.AreEqual("eins zwei drei", deTranslation["000001"]);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);
            Assert.AreEqual("Ich habe etwas Neues hinzugefügt", deTranslation["000003"]);

            // Now things get out of sync again
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three five", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thing", false, "test", 61)
                ]);
            edits = ReadTranslationEditsFile(deEditsJsonPath);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(edits["000001"].oldSource, "one two three");
            Assert.AreEqual(edits["000001"].oldTarget, "eins zwei drei");
            Assert.AreEqual(edits["000001"].newSource, "one two three five");
            Assert.AreEqual(edits["000001"].newTarget, null);
            deTranslation = this.ReadDeJson();
            Assert.AreEqual(2, deTranslation!.Count);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);
            Assert.AreEqual("Ich habe etwas Neues hinzugefügt", deTranslation["000003"]);


            // Now things get further out of sync
            testSubject.GenerateI18nFiles([
                new DiscoveredString("one two three four", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thing", false, "test", 61)
                ]);
            edits = ReadTranslationEditsFile(deEditsJsonPath);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(edits["000001"].oldSource, "one two three");
            Assert.AreEqual(edits["000001"].oldTarget, "eins zwei drei");
            Assert.AreEqual(edits["000001"].newSource, "one two three four");
            Assert.AreEqual(edits["000001"].newTarget, null);
            deTranslation = this.ReadDeJson();
            Assert.AreEqual(2, deTranslation!.Count);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);
            Assert.AreEqual("Ich habe etwas Neues hinzugefügt", deTranslation["000003"]);
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