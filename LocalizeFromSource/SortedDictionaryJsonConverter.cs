using System.Text.Json.Serialization;
using System.Text.Json;

namespace LocalizeFromSource
{
    public class SortedDictionaryJsonConverter<TValue> : JsonConverter<Dictionary<string, TValue>>
    {
        private readonly Func<string, string> _keyRank;

        public SortedDictionaryJsonConverter(Func<string, string> keyRank)
        {
            _keyRank = keyRank;
        }

        public override Dictionary<string, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, TValue> dictionary, JsonSerializerOptions options)
        {
            var sortedDictionary = dictionary.OrderBy(pair => _keyRank(pair.Key)).ToList();
            writer.WriteStartObject();
            foreach (var pair in sortedDictionary)
            {
                writer.WritePropertyName(pair.Key);
                JsonSerializer.Serialize(writer, pair.Value, options);
            }
            writer.WriteEndObject();
        }
    }
}
