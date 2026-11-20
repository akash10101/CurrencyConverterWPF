using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.Utilities
{
    internal static class Constants
    {
        public const decimal MaxAmount = 1000000000000000m;
        public const string DefaultSourceCurrency = "EUR";
        public const string DefaultTargetCurrency = "USD";
        public const int DefaultHistoryDays = 30;

        public const string ServiceErrorMsg = "An unexpected error occurred: ";
        public const string NoExchangeRateFoundMsg = "No exchange rates were published in the selected time frame.";
        public const string ExchangeRateLoading = "Loading exchange rates...";
    }
}
