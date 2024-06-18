using System.Text.Json.Serialization;

namespace LocalizeFromSource
{
    public record TranslationEntry(string source, string translation, string author, DateTime ingestionDate)
    {
        [JsonIgnore]
        public bool IsMachineGenerated => author.StartsWith("automation:");
    }
}
