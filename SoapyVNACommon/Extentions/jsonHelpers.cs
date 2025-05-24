using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SoapyVNACommon.Extentions
{
    public class ForceIntConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            objectType == typeof(object); // We only want to intercept ambiguous object values

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Integer)
            {
                long longVal = token.Value<long>();
                if (longVal >= int.MinValue && longVal <= int.MaxValue)
                    return (int)longVal;
                return longVal; // return as long if it's too big for int
            }

            // Use default deserialization for other types
            return token.ToObject<object>();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}