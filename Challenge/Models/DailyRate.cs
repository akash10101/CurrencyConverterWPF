using System;

namespace Challenge.Models
{
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
