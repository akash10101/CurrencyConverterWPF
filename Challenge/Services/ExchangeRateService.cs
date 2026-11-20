using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Controllers.Interfaces;
using Challenge.Models;
using Challenge.Services.Interfaces;
using Challenge.Utilities;

namespace Challenge.Services
{
    /// <summary>
    /// Fetches the data the main screen needs from the conversion controller and puts it into the
    /// shape the view model works with. Knows nothing about UI state.
    /// </summary>
    public sealed class ExchangeRateService : IExchangeRateService
    {
        private readonly IConversionController _controller;

        public ExchangeRateService(IConversionController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>Loads all currencies and picks the default source and target currency from them.</summary>
        public async Task<CurrencyList> GetCurrenciesAsync(CancellationToken cancellationToken)
        {
            var currencies = await _controller.GetCurrenciesAsync(cancellationToken);

            var source = FindCurrency(currencies, Constants.DefaultSourceCurrency) ?? currencies.FirstOrDefault();
            var target = FindCurrency(currencies, Constants.DefaultTargetCurrency) ?? currencies.FirstOrDefault(c => c != source);

            return new CurrencyList(currencies, source, target);
        }

        /// <summary>Fetches the latest and the historic rates for one selection, in parallel.</summary>
        public async Task<RateSnapshot> GetRatesAsync(
            string source, string target, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            var latestTask = _controller.GetLatestRateAsync(source, target, cancellationToken);
            var historyTask = _controller.GetHistoricRatesAsync(source, target, start, end, cancellationToken);

            await Task.WhenAll(latestTask, historyTask);

            return new RateSnapshot(target, latestTask.Result, historyTask.Result);
        }

        private static CurrencyInfo FindCurrency(IEnumerable<CurrencyInfo> currencies, string code)
        {
            return currencies.FirstOrDefault(currency => string.Equals(currency.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>The available currencies together with the ones to preselect.</summary>
    public sealed class CurrencyList
    {
        public CurrencyList(IReadOnlyList<CurrencyInfo> all, CurrencyInfo defaultSource, CurrencyInfo defaultTarget)
        {
            All = all;
            DefaultSource = defaultSource;
            DefaultTarget = defaultTarget;
        }

        public IReadOnlyList<CurrencyInfo> All { get; }

        public CurrencyInfo DefaultSource { get; }

        public CurrencyInfo DefaultTarget { get; }
    }
}
