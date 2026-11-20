using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.Models.Api.FrankfurterAPI
{
    /// <summary>Body of 4xx/5xx responses, e.g. {"status":422,"message":"invalid currency: ABC"}.</summary>
    internal sealed class ErrorDto
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
