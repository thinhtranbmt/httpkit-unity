using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpKit
{
    // =====================================================================
    // OPTIONAL helper (not part of the core transport).
    // For backends whose JSON body is a flat { key: value } object that you want
    // captured as Dictionary<string, string> (each value kept as its compact JSON string).
    //
    // Usage A — let your DTO implement IKeyValueEnvelope, then:
    //     var res = await http.GetAsync<MyEnvelope>(url, headers, KeyValueEnvelope.Deserialize<MyEnvelope>);
    //
    // Usage B — when T is only known at runtime (reflection / typeof dispatch):
    //     object env = KeyValueEnvelope.Deserialize(type, json);
    //
    // Usage C — just the raw flatten:
    //     Dictionary<string,string> map = KeyValueEnvelope.Flatten(json);
    // =====================================================================

    /// <summary>Marks a DTO whose JSON body is a flat { key: value } object.</summary>
    public interface IKeyValueEnvelope
    {
        void SetData(Dictionary<string, string> data);
    }

    public static class KeyValueEnvelope
    {
        /// <summary>Flatten a { key: value } JSON object. Each value is stored as its compact JSON string.</summary>
        public static Dictionary<string, string> Flatten(string json)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json))
            {
                return dict;
            }

            var raw = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
            if (raw != null)
            {
                foreach (var kv in raw)
                {
                    dict[kv.Key] = kv.Value?.ToString(Formatting.None);
                }
            }
            return dict;
        }

        /// <summary>Build a T : IKeyValueEnvelope from a flat { key: value } JSON object.</summary>
        public static T Deserialize<T>(string json) where T : IKeyValueEnvelope, new()
        {
            var env = new T();
            env.SetData(Flatten(json));
            return env;
        }

        /// <summary>Non-generic variant for when the type is only known at runtime.</summary>
        public static object Deserialize(Type type, string json)
        {
            var env = (IKeyValueEnvelope)Activator.CreateInstance(type);
            env.SetData(Flatten(json));
            return env;
        }
    }
}
