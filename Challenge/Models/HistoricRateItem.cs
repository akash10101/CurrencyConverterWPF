using System;

namespace Challenge.Models
{
    /// <summary>One row of the historic conversion list.</summary>
    public sealed class HistoricRateItem
    {
        public HistoricRateItem(DateTime date, decimal value, string currency)
        {
            Date = date;
            Value = value;
            Currency = currency;
        }

        public DateTime Date { get; }

        /// <summary>The user's amount converted with this day's rate.</summary>
        public decimal Value { get; }

        public string Currency { get; }
    }
}
