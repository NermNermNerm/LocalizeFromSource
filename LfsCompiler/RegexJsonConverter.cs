using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocalizeFromSource
{
    internal class RegexJsonConverter : JsonConverter<Regex>
    {
        public override Regex? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? s = reader.GetString();
            if (s == null)
            {
                throw new JsonException("Null regular expressions are not allowed"); /* Seems like it should be able to supply line and file, but how?? */
            }

            return new Regex(s, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        public override void Write(Utf8JsonWriter writer, Regex value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
