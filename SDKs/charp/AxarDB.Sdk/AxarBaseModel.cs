using System;
using System.Text.Json.Serialization;

namespace AxarDB.Sdk
{
    public abstract class AxarBaseModel
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }
}
