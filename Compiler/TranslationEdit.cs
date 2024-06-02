using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalizeFromSource
{
    public record TranslationEdit(string? oldSource, string newSource, string? oldTarget, string? newTarget, Uri? link)
    {
        public static string MakePath(string folder, string localeId)
            => Path.Combine(folder, localeId + ".edits.json");

        public static Dictionary<string,TranslationEdit> Read(string path)
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
