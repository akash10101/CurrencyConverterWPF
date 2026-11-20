using Newtonsoft.Json;

namespace Challenge.Models.Api.FrankfurterAPI
{
    internal sealed class CurrencyDto
    {
        [JsonProperty("iso_code")]
        public string IsoCode { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// "currency", "metal" or "crypto".
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
