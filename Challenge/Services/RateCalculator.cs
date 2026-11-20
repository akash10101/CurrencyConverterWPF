using System;
using System.Linq;
using Challenge.Models;

namespace Challenge.Services
{
    public static class RateCalculator
    {
        public static ConversionResult Convert(RateSnapshot rates, decimal amount)
        {
            if (rates == null)
            {
                throw new ArgumentNullException(nameof(rates));
            }

            var history = (rates.History ?? Enumerable.Empty<DailyRate>())
                .OrderByDescending(day => day.Date)
                .Select(day => new HistoricRateItem(day.Date, amount * day.Rate, rates.TargetCode))
                .ToList();

            return new ConversionResult(
                amount * rates.Latest.Rate,
                rates.Latest.Rate,
                rates.Latest.Date,
                rates.TargetCode,
                history);
        }
    }
}
