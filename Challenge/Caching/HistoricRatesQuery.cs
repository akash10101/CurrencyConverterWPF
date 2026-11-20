using System;

namespace Challenge.Caching
{
    /// <summary>
    /// Identifies one historic-rates request. Currency codes are compared case-insensitively and the dates
    /// without time of day, so equivalent requests always map to the same cache entry. The amount is
    /// deliberately not part of the key: rates do not depend on it.
    /// </summary>
    public readonly struct HistoricRatesQuery : IEquatable<HistoricRatesQuery>
    {
        public HistoricRatesQuery(string source, string target, DateTime start, DateTime end)
        {
            Source = (source ?? throw new ArgumentNullException(nameof(source))).ToUpperInvariant();
            Target = (target ?? throw new ArgumentNullException(nameof(target))).ToUpperInvariant();
            Start = start.Date;
            End = end.Date;
        }

        public string Source { get; }

        public string Target { get; }

        public DateTime Start { get; }

        public DateTime End { get; }

        public bool Equals(HistoricRatesQuery other)
        {
            return string.Equals(Source, other.Source, StringComparison.Ordinal)
                && string.Equals(Target, other.Target, StringComparison.Ordinal)
                && Start == other.Start
                && End == other.End;
        }

        public override bool Equals(object obj) => obj is HistoricRatesQuery other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Source != null ? Source.GetHashCode() : 0;
                hash = (hash * 397) ^ (Target != null ? Target.GetHashCode() : 0);
                hash = (hash * 397) ^ Start.GetHashCode();
                hash = (hash * 397) ^ End.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => Source + "->" + Target + " " + Start.ToString("yyyy-MM-dd") + ".." + End.ToString("yyyy-MM-dd");
    }
}
