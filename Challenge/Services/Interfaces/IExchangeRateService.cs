using System;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Models;

namespace Challenge.Services.Interfaces
{
    /// <summary>
    /// The data the main screen needs, in the shape the view model works with.
    /// </summary>
    public interface IExchangeRateService
    {
        /// <summary>Loads all currencies and picks the default source and target currency from them.</summary>
        Task<CurrencyList> GetCurrenciesAsync(CancellationToken cancellationToken);

        /// <summary>Fetches the latest and the historic rates for one selection.</summary>
        Task<RateSnapshot> GetRatesAsync(string source, string target, DateTime start, DateTime end, CancellationToken cancellationToken);
    }
}
