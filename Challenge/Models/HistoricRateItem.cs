using System;

namespace Challenge.Models
{
    public sealed class HistoricRateItem
    {
        public HistoricRateItem(DateTime date, decimal value, string currency)
        {
            Date = date;
            Value = value;
            Currency = currency;
        }

        public DateTime Date { get; }

        public decimal Value { get; }

        public string Currency { get; }
    }
}
