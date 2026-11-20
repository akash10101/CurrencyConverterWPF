using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Controllers.Interfaces;
using Challenge.Models;

namespace Challenge.Tests
{
    /// <summary>In-memory IConversionController that records how often it was called.</summary>
    internal sealed class FakeConversionController : IConversionController
    {
        public int CurrencyCalls;
        public int LatestCalls;
        public int HistoricCalls;

        public Exception LatestException;
        public Exception HistoricException;

        public DailyRate Latest = new DailyRate(new DateTime(2025, 1, 3), 2m);

        public IReadOnlyList<DailyRate> History = new[]
        {
            new DailyRate(new DateTime(2025, 1, 2), 1.5m),
            new DailyRate(new DateTime(2025, 1, 3), 2m),
        };

        public Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(CancellationToken cancellationToken)
        {
            CurrencyCalls++;
            IReadOnlyList<CurrencyInfo> currencies = new[]
            {
                new CurrencyInfo("EUR", "Euro"),
                new CurrencyInfo("GBP", "British Pound"),
                new CurrencyInfo("USD", "US Dollar"),
            };
            return Task.FromResult(currencies);
        }

        public Task<DailyRate> GetLatestRateAsync(string source, string target, CancellationToken cancellationToken)
        {
            LatestCalls++;
            return LatestException != null
                ? Task.FromException<DailyRate>(LatestException)
                : Task.FromResult(Latest);
        }

        public Task<IReadOnlyList<DailyRate>> GetHistoricRatesAsync(string source, string target, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            HistoricCalls++;
            return HistoricException != null
                ? Task.FromException<IReadOnlyList<DailyRate>>(HistoricException)
                : Task.FromResult(History);
        }
    }
}
