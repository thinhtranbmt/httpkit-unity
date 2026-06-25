using Newtonsoft.Json;

namespace HttpKit
{
    /// <summary>
    /// Default IJsonSerializer. Uses the non-generic JsonConvert.DeserializeObject(json, type)
    /// path which is IL2CPP-safe (avoids problematic generic instantiation).
    /// </summary>
    public sealed class NewtonsoftJsonSerializer : IJsonSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public NewtonsoftJsonSerializer(JsonSerializerSettings settings = null)
        {
            _settings = settings ?? new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
        }

        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return (T)JsonConvert.DeserializeObject(json, typeof(T), _settings);
        }

        public string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, _settings);
        }
    }
}
