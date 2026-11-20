namespace Challenge.Caching
{
    public enum EvictionStrategyKind
    {
        /// <summary>Remove entries older than MAX_AGE minutes.</summary>
        TimeBased,

        /// <summary>Keep at most MAX_ELEMENTS entries, dropping the oldest.</summary>
        SizeBased
    }
}
