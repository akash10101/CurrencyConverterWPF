using System.Collections.Generic;

namespace Challenge.Models
{
    public sealed class CurrencyList
    {
        public CurrencyList(IReadOnlyList<CurrencyInfo> all, CurrencyInfo defaultSource, CurrencyInfo defaultTarget)
        {
            All = all;
            DefaultSource = defaultSource;
            DefaultTarget = defaultTarget;
        }

        public IReadOnlyList<CurrencyInfo> All { get; }

        public CurrencyInfo DefaultSource { get; }

        public CurrencyInfo DefaultTarget { get; }
    }
}
