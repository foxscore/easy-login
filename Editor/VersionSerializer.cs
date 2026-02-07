using System;
using Newtonsoft.Json;
using Version = Foxscore.EasyLogin.SemanticVersioning.Version;

namespace Foxscore.EasyLogin
{
    public class VersionSerializer : JsonConverter<Version>
    {
        public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                return Version.Parse((string) reader.Value);
            }
            catch (Exception e)
            {
                Log.Error<VersionSerializer>($"Failed to parse Version ({reader.Value ?? "<null>"})", e);
                throw;
            }
        }
    }
}