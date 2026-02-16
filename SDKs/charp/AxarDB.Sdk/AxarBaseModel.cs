using System;
using Newtonsoft.Json;

namespace AxarDB.Sdk
{
    public abstract class AxarBaseModel
    {
        [JsonProperty("_id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }
}
