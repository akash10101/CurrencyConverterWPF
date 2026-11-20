using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Models;

namespace Challenge.Controllers.Interfaces
{
    /// <summary>
    /// Source of currencies and exchange rates.
    /// </summary>
    public interface IConversionController
    {
        /// <summary>All currencies that can be converted.</summary>
        Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// The most recent rate for one unit of <paramref name="source"/> in <paramref name="target"/>.
        /// </summary>
        Task<DailyRate> GetLatestRateAsync(string source, string target, CancellationToken cancellationToken);

        /// <summary>
        /// Daily rates (oldest first) for one unit of <paramref name="source"/> in <paramref name="target"/>
        /// between <paramref name="start"/> and <paramref name="end"/> (inclusive).
        /// </summary>
        Task<IReadOnlyList<DailyRate>> GetHistoricRatesAsync(string source, string target, DateTime start, DateTime end, CancellationToken cancellationToken);
    }
}
