using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Controllers.Interfaces;
using Challenge.Models;
using Challenge.Models.Api.FrankfurterAPI;
using Challenge.Utilities;
using Newtonsoft.Json;

namespace Challenge.Controllers
{
    /// <summary>Talks to the Frankfurter API (v2) and turns its JSON into domain objects.</summary>
    public class ConversionController : IConversionController
    {
        private const string Host = "api.frankfurter.dev";
        private const string DateFormat = "yyyy-MM-dd";
        private const string ApiVersion = "v2";

        private static readonly Regex CurrencyCodePattern = new Regex("^[A-Za-z]{3}$", RegexOptions.Compiled);
        private static readonly Regex ProvidersPattern = new Regex("^[A-Za-z0-9._,]+$", RegexOptions.Compiled);

        // HttpClient is designed to be shared; creating one per request exhausts sockets.
        private static readonly HttpClient SharedHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private readonly HttpClient _httpClient;
        private readonly string _providers;

        public ConversionController() : this(SharedHttpClient)
        {
        }

        /// <param name="httpClient">The client to use (a stub handler in tests).</param>
        /// <param name="providers">
        /// null (default) uses Frankfurter's blend across all providers. "ecb" pins the European Central Bank's
        /// reference rates, which is what the v1 API served.
        /// </param>
        public ConversionController(HttpClient httpClient, string providers = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (!string.IsNullOrWhiteSpace(providers) && !ProvidersPattern.IsMatch(providers))
            {
                throw new ArgumentException("'" + providers + "' is not a valid provider list.", nameof(providers));
            }

            _providers = string.IsNullOrWhiteSpace(providers) ? null : providers.Trim();
        }

        public async Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(CancellationToken cancellationToken)
        {
            var response = await GetAsync<List<CurrencyDto>>("currencies", BuildQuery(), false, cancellationToken).ConfigureAwait(false);

            // The catalogue also lists precious metals and crypto assets; a currency converter wants real currencies only.
            var currencies = (response ?? new List<CurrencyDto>())
                .Where(currency => currency != null
                    && !string.IsNullOrWhiteSpace(currency.IsoCode)
                    && string.Equals(currency.Type, "currency", StringComparison.OrdinalIgnoreCase))
                .Select(currency => new CurrencyInfo(currency.IsoCode.ToUpperInvariant(), currency.Name))
                .OrderBy(currency => currency.Code, StringComparer.Ordinal)
                .ToList();

            if (currencies.Count == 0)
            {
                throw new ExchangeRateException("The exchange rate service returned no currencies.");
            }

            return currencies;
        }

        public async Task<DailyRate> GetLatestRateAsync(string source, string target, CancellationToken cancellationToken)
        {
            var path = "rate/" + NormalizeCode(source) + "/" + NormalizeCode(target);
            var response = await GetAsync<RateDto>(path, BuildQuery(), false, cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                throw new ExchangeRateException("The exchange rate service returned an empty response.");
            }

            return ToDailyRate(response);
        }

        public async Task<IReadOnlyList<DailyRate>> GetHistoricRatesAsync(string source, string target, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            if (start.Date > end.Date)
            {
                throw new ArgumentException("The start date must not be after the end date.");
            }

            var sourceCode = NormalizeCode(source);
            var targetCode = NormalizeCode(target);
            var query = BuildQuery(
                "from=" + start.ToString(DateFormat, CultureInfo.InvariantCulture),
                "to=" + end.ToString(DateFormat, CultureInfo.InvariantCulture),
                "base=" + sourceCode,
                "quotes=" + targetCode);

            // A 404 means the range contains no rates: that is an empty result, not a failure.
            var rows = await GetAsync<List<RateDto>>("rates", query, true, cancellationToken).ConfigureAwait(false);
            if (rows == null)
            {
                return new DailyRate[0];
            }

            // One row per day and quote currency. We asked for a single quote, but filter anyway so an
            // unexpected extra row can never be mistaken for the target currency.
            return rows
                .Where(row => row != null && string.Equals(row.Quote, targetCode, StringComparison.OrdinalIgnoreCase))
                .Select(ToDailyRate)
                .OrderBy(rate => rate.Date)
                .ToList();
        }

        private async Task<T> GetAsync<T>(string path, string query, bool notFoundMeansEmpty, CancellationToken cancellationToken) where T : class
        {
            var uri = new Uri("https://" + Host + "/" + ApiVersion + "/" + path + (query == null ? string.Empty : "?" + query));

            try
            {
                using (var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    if (notFoundMeansEmpty && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var detail = await ReadErrorMessageAsync(response).ConfigureAwait(false);
                        throw new ExchangeRateException(
                            "The exchange rate service returned an error (" + (int)response.StatusCode + " " + response.ReasonPhrase + ")"
                            + (string.IsNullOrWhiteSpace(detail) ? "." : ": " + detail));
                    }

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new ExchangeRateException("The exchange rate service could not be reached. Please check your internet connection.", ex);
            }
            catch (JsonException ex)
            {
                throw new ExchangeRateException("The exchange rate service returned an unexpected response.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient reports its own timeout as a cancellation; the caller did not cancel anything.
                throw new ExchangeRateException("The exchange rate service did not respond in time.", ex);
            }
        }

        /// <summary>v2 error bodies look like {"status":422,"message":"invalid currency: ABC"}. Returns null if there is none.</summary>
        private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var error = JsonConvert.DeserializeObject<ErrorDto>(body);
                return error == null ? null : error.Message;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Builds the query string; the provider pin (if any) is appended to every request.</summary>
        private string BuildQuery(params string[] parameters)
        {
            var all = parameters.ToList();
            if (_providers != null)
            {
                all.Add("providers=" + Uri.EscapeDataString(_providers));
            }

            return all.Count == 0 ? null : string.Join("&", all);
        }

        private static DailyRate ToDailyRate(RateDto dto)
        {
            if (dto.Rate <= 0m)
            {
                throw new ExchangeRateException("The exchange rate service returned an invalid rate.");
            }

            return new DailyRate(ParseDate(dto.Date), dto.Rate);
        }

        private static string NormalizeCode(string currencyCode)
        {
            if (currencyCode == null || !CurrencyCodePattern.IsMatch(currencyCode))
            {
                throw new ArgumentException("'" + currencyCode + "' is not a valid three-letter currency code.", nameof(currencyCode));
            }

            return currencyCode.ToUpperInvariant();
        }

        private static DateTime ParseDate(string value)
        {
            DateTime date;
            if (!DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                throw new ExchangeRateException("The exchange rate service returned a date in an unexpected format.");
            }

            return date;
        }
    }
}
