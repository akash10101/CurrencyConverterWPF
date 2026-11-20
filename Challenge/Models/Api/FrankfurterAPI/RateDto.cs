using Newtonsoft.Json;

namespace Challenge.Models.Api.FrankfurterAPI
{
    internal sealed class RateDto
    {
        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("base")]
        public string Base { get; set; }

        [JsonProperty("quote")]
        public string Quote { get; set; }

        [JsonProperty("rate")]
        public decimal Rate { get; set; }
    }
}
