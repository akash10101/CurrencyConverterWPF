using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Controllers.Interfaces;
using Challenge.Logging;
using Challenge.Models;
using Challenge.Models.Api.FrankfurterAPI;
using Challenge.Utilities;
using Newtonsoft.Json;

namespace Challenge.Controllers
{
    public class ConversionController : IConversionController
    {
        private const string Host = "api.frankfurter.dev";
        private const string DateFormat = "yyyy-MM-dd";
        private const string ApiVersion = "v2";

        private static readonly Regex CurrencyCodePattern = new Regex("^[A-Za-z]{3}$", RegexOptions.Compiled);
        private static readonly Regex ProvidersPattern = new Regex("^[A-Za-z0-9._,]+$", RegexOptions.Compiled);

        
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        /// <summary>How much of a response body goes into the log.</summary>
        private const int LoggedBodyLength = 300;

        private readonly HttpClient _httpClient;
        private readonly string _providers;
        private readonly ILogger _logger;

        public ConversionController(ILogger logger = null) : this(HttpClient, null, logger)
        {
        }

        /// <param name="httpClient">The client to use (a stub handler in tests).</param>
        /// <param name="providers">
        /// null (default) uses Frankfurter's blend across all providers. "ecb" pins the European Central Bank's
        /// reference rates, which is what the v1 API served.
        /// </param>
        /// <param name="logger">Receives every request and response. Nothing is logged when null.</param>
        public ConversionController(HttpClient httpClient, string providers = null, ILogger logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = (logger ?? NullLogger.Instance).ForCategory("FrankfurterApi");

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

            return rows
                .Where(row => row != null && string.Equals(row.Quote, targetCode, StringComparison.OrdinalIgnoreCase))
                .Select(ToDailyRate)
                .OrderBy(rate => rate.Date)
                .ToList();
        }

        private async Task<T> GetAsync<T>(string path, string query, bool notFoundMeansEmpty, CancellationToken cancellationToken) where T : class
        {
            var uri = new Uri("https://" + Host + "/" + ApiVersion + "/" + path + (query == null ? string.Empty : "?" + query));
            var stopwatch = Stopwatch.StartNew();
            _logger.Info("Request: GET " + uri);

            try
            {
                using (var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? string.Empty;
                    _logger.Info(string.Format(
                        CultureInfo.InvariantCulture,
                        "Response: {0} {1} for GET {2} in {3} ms, {4} chars: {5}",
                        (int)response.StatusCode, response.ReasonPhrase, uri.PathAndQuery,
                        stopwatch.ElapsedMilliseconds, body.Length, Shorten(body)));

                    if (notFoundMeansEmpty && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.Info("No data for GET " + uri.PathAndQuery + "; treating it as an empty result");
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var detail = ParseErrorMessage(body);
                        var error = new ExchangeRateException(
                            "The exchange rate service returned an error (" + (int)response.StatusCode + " " + response.ReasonPhrase + ")"
                            + (string.IsNullOrWhiteSpace(detail) ? "." : ": " + detail));
                        _logger.Warning("GET " + uri.PathAndQuery + " failed: " + error.Message);
                        throw error;
                    }

                    return JsonConvert.DeserializeObject<T>(body);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error("GET " + uri + " could not reach the server after " + stopwatch.ElapsedMilliseconds + " ms", ex);
                throw new ExchangeRateException("The exchange rate service could not be reached. Please check your internet connection.", ex);
            }
            catch (JsonException ex)
            {
                _logger.Error("GET " + uri.PathAndQuery + " returned JSON that could not be read", ex);
                throw new ExchangeRateException("The exchange rate service returned an unexpected response.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {           
                _logger.Error("GET " + uri + " timed out after " + stopwatch.ElapsedMilliseconds + " ms", ex);
                throw new ExchangeRateException("The exchange rate service did not respond in time.", ex);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("GET " + uri.PathAndQuery + " cancelled after " + stopwatch.ElapsedMilliseconds + " ms (superseded by a newer request)");
                throw;
            }
        }

        private static string ParseErrorMessage(string body)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<ErrorDto>(body);
                return error == null ? null : error.Message;
            }
            catch (JsonException)
            {
                return null;
            }
        }
        
        private static string Shorten(string body)
        {
            var oneLine = body.Replace("\r", " ").Replace("\n", " ");
            return oneLine.Length <= LoggedBodyLength
                ? oneLine
                : oneLine.Substring(0, LoggedBodyLength) + "... (" + (oneLine.Length - LoggedBodyLength) + " more chars)";
        }

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
