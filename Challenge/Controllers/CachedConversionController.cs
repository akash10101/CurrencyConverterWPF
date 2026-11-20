using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Caching;
using Challenge.Controllers.Interfaces;
using Challenge.Models;

namespace Challenge.Controllers
{
    /// <summary>
    /// Decorator that answers repeated historic-rate queries from a cache instead of asking the API again.
    /// The currency list and the latest rate are passed straight through: the latest rate has to be fresh,
    /// and the currency list is loaded only once per run anyway.
    ///
    /// Only successful results are cached (a failed request throws before anything is stored). A cached range
    /// that includes today can miss a rate published later; the eviction strategy bounds how long that lasts.
    /// </summary>
    public sealed class CachedConversionController : IConversionController
    {
        private readonly IConversionController _inner;
        private readonly QueryCache<HistoricRatesQuery, IReadOnlyList<DailyRate>> _cache;

        public CachedConversionController(IConversionController inner, QueryCache<HistoricRatesQuery, IReadOnlyList<DailyRate>> cache)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(CancellationToken cancellationToken)
        {
            return _inner.GetCurrenciesAsync(cancellationToken);
        }

        public Task<DailyRate> GetLatestRateAsync(string source, string target, CancellationToken cancellationToken)
        {
            return _inner.GetLatestRateAsync(source, target, cancellationToken);
        }

        public async Task<IReadOnlyList<DailyRate>> GetHistoricRatesAsync(string source, string target, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            var query = new HistoricRatesQuery(source, target, start, end);

            IReadOnlyList<DailyRate> rates;
            if (_cache.TryGet(query, out rates))
            {
                // Debug.WriteLine is removed from Release builds. Watch it in Visual Studio's Output window (Debug) when running with F5.
                Debug.WriteLine("[Cache] HIT    " + query + " (" + rates.Count + " rates, no API call)");
                return rates;
            }

            Debug.WriteLine("[Cache] MISS   " + query + " -> calling the API");
            rates = await _inner.GetHistoricRatesAsync(source, target, start, end, cancellationToken).ConfigureAwait(false);
            _cache.Set(query, rates);
            Debug.WriteLine("[Cache] STORED " + query + " (entries now in cache: " + _cache.Count + ")");
            return rates;
        }
    }
}
