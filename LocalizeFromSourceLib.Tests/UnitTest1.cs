using System.Text.Json;

namespace LocalizeFromSourceLib.Tests
{
    [TestClass]
    public class UnitTest1
    {
        private const string TestFolderName = "ut1";
        private string i18nFolder = null!;

        [TestInitialize]
        public void Initialize()
        {
            if (Directory.Exists(TestFolderName))
            {
                Directory.Delete(TestFolderName, recursive: true);
            }
            Directory.CreateDirectory(TestFolderName);

            this.i18nFolder = Path.Combine(Environment.CurrentDirectory, TestFolderName, "i18n");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Directory.Delete(TestFolderName, true);
        }

        [TestMethod]
        public void BasicDefaultJsonTests()
        {
            SdvTranslationCompiler testSubject = new SdvTranslationCompiler();

            // Starts from nothing
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [new DiscoveredString("one two three", false, "test", 57)]);
            var defaultJson = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nFolder, "default.json")));
            Assert.AreEqual(1, defaultJson!.Count);
            Assert.AreEqual("one two three", defaultJson["000001"]);

            // Starts adds new thing, recognizes old thing and leaves it alone.
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("a b c", false, "test", 67),
                ]);
            defaultJson = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nFolder, "default.json")));
            Assert.AreEqual(2, defaultJson!.Count);
            Assert.AreEqual("one two three", defaultJson["000001"]);
            Assert.AreEqual("a b c", defaultJson["000002"]);

            // recognizes near match
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two four", false, "test", 57),
                new DiscoveredString("a b c", false, "test", 67),
                ]);
            defaultJson = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nFolder, "default.json")));
            Assert.AreEqual(2, defaultJson!.Count);
            Assert.AreEqual("one two four", defaultJson["000001"]);
            Assert.AreEqual("a b c", defaultJson["000002"]);
        }


        [TestMethod]
        public void TranslationTests()
        {
            string defaultJsonPath = Path.Combine(i18nFolder, "default.json");
            string deJsonPath = Path.Combine(i18nFolder, "de.json");
            string deEditsJsonPath = TranslationEdit.MakePath(i18nFolder, "de");

            var testSubject = new TestableSdvTranslationCompiler();

            // Add some source language stuff - do it in two passes to guarantee key order for testing purposes
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three", false, "test", 57),
                ]);
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);
            var defaultJson = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(defaultJsonPath));
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
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), true, [
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in", false, "test", 59)
                ]);
            Assert.AreEqual(0, testSubject.Errors.Count); // It shouldn't do anything.
            Assert.AreEqual(unformattedDeJson, File.ReadAllText(deJsonPath)); // It shouldn't even reformat the language file.
            Assert.IsFalse(File.Exists(deEditsJsonPath));

            // Add a new translation and edit another
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thang", false, "test", 61)
                ]);
            var edits = TranslationEdit.Read(deEditsJsonPath);
            Assert.AreEqual(2, edits.Count);
            Assert.AreEqual(edits["000002"].oldSource, "count me in");
            Assert.AreEqual(edits["000002"].oldTarget, "ich bin dabei");
            Assert.AreEqual(edits["000002"].newSource, "count me in!");
            Assert.AreEqual(edits["000002"].newTarget, null);
            Assert.AreEqual(edits["000003"].oldSource, null);
            Assert.AreEqual(edits["000003"].oldTarget, null);
            Assert.AreEqual(edits["000003"].newSource, "I added a new thang");
            Assert.AreEqual(edits["000003"].newTarget, null);
            var deTranslation = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(deJsonPath));
            Assert.AreEqual(1, deTranslation!.Count);
            Assert.AreEqual("eins zwei drei", deTranslation["000001"]);

            // German translator updates the 'count me in' translation but sees the 'thang' spelling error and ignores that one.
            File.WriteAllText(deEditsJsonPath, JsonSerializer.Serialize(new Dictionary<string, TranslationEdit>()
            {
                { "000002", new TranslationEdit(oldSource: "count me in", oldTarget: "ich bin dabei", newSource: "count me in!", newTarget: "ich bin dabei!") },
                { "000003", new TranslationEdit(oldSource: null, oldTarget: null, newSource: "I added a new thang", newTarget: null) }
            }));
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thang", false, "test", 61)
                ]);
            // Then it removes the edit we took care of and leaves the one we didn't
            edits = TranslationEdit.Read(deEditsJsonPath);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(edits["000003"].oldSource, null);
            Assert.AreEqual(edits["000003"].oldTarget, null);
            Assert.AreEqual(edits["000003"].newSource, "I added a new thang");
            Assert.AreEqual(edits["000003"].newTarget, null);
            deTranslation = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(deJsonPath));
            Assert.AreEqual(2, deTranslation!.Count);
            Assert.AreEqual("eins zwei drei", deTranslation["000001"]);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);

            // German translator gets creative and fixes that thang.
            File.WriteAllText(deEditsJsonPath, JsonSerializer.Serialize(new Dictionary<string, TranslationEdit>()
            {
                { "000003", new TranslationEdit(oldSource: null, oldTarget: null, newSource: "I added a new thing", newTarget: "Ich habe etwas Neues hinzugefügt") }
            }));
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thing", false, "test", 61)
                ]);
            deTranslation = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(deJsonPath));
            Assert.IsFalse(File.Exists(deEditsJsonPath));
            Assert.AreEqual(3, deTranslation!.Count);
            Assert.AreEqual("eins zwei drei", deTranslation["000001"]);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);
            Assert.AreEqual("Ich habe etwas Neues hinzugefügt", deTranslation["000003"]);

            // Now things get out of sync again
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three five", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thing", false, "test", 61)
                ]);
            edits = TranslationEdit.Read(deEditsJsonPath);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(edits["000001"].oldSource, "one two three");
            Assert.AreEqual(edits["000001"].oldTarget, "eins zwei drei");
            Assert.AreEqual(edits["000001"].newSource, "one two three five");
            Assert.AreEqual(edits["000001"].newTarget, null);
            deTranslation = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(deJsonPath));
            Assert.AreEqual(2, deTranslation!.Count);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);
            Assert.AreEqual("Ich habe etwas Neues hinzugefügt", deTranslation["000003"]);

            // Now things get further out of sync
            testSubject.Compiled(Path.Combine(Environment.CurrentDirectory, TestFolderName), false, [
                new DiscoveredString("one two three four", false, "test", 57),
                new DiscoveredString("count me in!", false, "test", 59),
                new DiscoveredString("I added a new thing", false, "test", 61)
                ]);
            edits = TranslationEdit.Read(deEditsJsonPath);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(edits["000001"].oldSource, "one two three");
            Assert.AreEqual(edits["000001"].oldTarget, "eins zwei drei");
            Assert.AreEqual(edits["000001"].newSource, "one two three four");
            Assert.AreEqual(edits["000001"].newTarget, null);
            deTranslation = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(deJsonPath));
            Assert.AreEqual(2, deTranslation!.Count);
            Assert.AreEqual("ich bin dabei!", deTranslation["000002"]);
            Assert.AreEqual("Ich habe etwas Neues hinzugefügt", deTranslation["000003"]);
        }
    }
}