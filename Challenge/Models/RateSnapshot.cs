using System.Collections.Generic;

namespace Challenge.Models
{
    /// <summary>The rates fetched for one selection of currencies and time frame.</summary>
    public sealed class RateSnapshot
    {
        public RateSnapshot(string targetCode, DailyRate latest, IReadOnlyList<DailyRate> history)
        {
            TargetCode = targetCode;
            Latest = latest;
            History = history;
        }

        public string TargetCode { get; }

        public DailyRate Latest { get; }

        public IReadOnlyList<DailyRate> History { get; }
    }
}
