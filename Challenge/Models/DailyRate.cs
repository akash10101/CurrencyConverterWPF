using System;

namespace Challenge.Models
{
    /// <summary>The value of one unit of the source currency, expressed in the target currency, on one day.</summary>
    public readonly struct DailyRate
    {
        public DailyRate(DateTime date, decimal rate)
        {
            Date = date;
            Rate = rate;
        }

        public DateTime Date { get; }

        public decimal Rate { get; }
    }
}
