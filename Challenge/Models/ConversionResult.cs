using System;
using System.Collections.Generic;

namespace Challenge.Models
{
    public sealed class ConversionResult
    {
        public ConversionResult(
            decimal latestAmount, decimal latestRate, DateTime latestDate, string targetCode, IReadOnlyList<HistoricRateItem> history)
        {
            LatestAmount = latestAmount;
            LatestRate = latestRate;
            LatestDate = latestDate;
            TargetCode = targetCode;
            History = history;
        }

        public decimal LatestAmount { get; }

        public decimal LatestRate { get; }

        public DateTime LatestDate { get; }

        public string TargetCode { get; }

        public IReadOnlyList<HistoricRateItem> History { get; }
    }
}
