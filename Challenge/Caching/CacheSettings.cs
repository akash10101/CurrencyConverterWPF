using System;
using System.Collections.Specialized;
using System.Globalization;
using Challenge.Logging;

namespace Challenge.Caching
{
    /// <summary>The cache configuration, read from the &lt;appSettings&gt; section of App.config.</summary>
    public sealed class CacheSettings
    {
        public const string StrategyKey = "Cache.EvictionStrategy";
        public const string MaxAgeMinutesKey = "Cache.MaxAgeMinutes";
        public const string MaxElementsKey = "Cache.MaxElements";

        public const EvictionStrategyKind DefaultStrategy = EvictionStrategyKind.TimeBased;
        public const int DefaultMaxAgeMinutes = 10;
        public const int DefaultMaxElements = 20;

        public CacheSettings(EvictionStrategyKind strategy, int maxAgeMinutes, int maxElements)
        {
            Strategy = strategy;
            MaxAgeMinutes = maxAgeMinutes;
            MaxElements = maxElements;
        }

        public EvictionStrategyKind Strategy { get; }

        /// <summary>MAX_AGE: used by the time-based strategy.</summary>
        public int MaxAgeMinutes { get; }

        /// <summary>MAX_ELEMENTS: used by the size-based strategy.</summary>
        public int MaxElements { get; }

        /// <summary>
        /// Reads the settings. A missing or invalid value falls back to its default (and an invalid one is logged as
        /// a warning), so a typo in the config file degrades to sensible behaviour instead of preventing the
        /// application from starting.
        /// </summary>
        public static CacheSettings Load(NameValueCollection appSettings, ILogger logger = null)
        {
            logger = (logger ?? NullLogger.Instance).ForCategory(nameof(CacheSettings));

            return new CacheSettings(
                ReadStrategy(appSettings, logger),
                ReadPositiveInt(appSettings, MaxAgeMinutesKey, DefaultMaxAgeMinutes, logger),
                ReadPositiveInt(appSettings, MaxElementsKey, DefaultMaxElements, logger));
        }

        /// <summary>The settings that matter for the chosen strategy, for the log.</summary>
        public override string ToString()
        {
            return Strategy == EvictionStrategyKind.TimeBased
                ? Strategy + " (entries expire after " + MaxAgeMinutes + " min)"
                : Strategy + " (at most " + MaxElements + " entries)";
        }

        public IEvictionStrategy CreateEvictionStrategy()
        {
            switch (Strategy)
            {
                case EvictionStrategyKind.TimeBased:
                    return new TimeBasedEvictionStrategy(TimeSpan.FromMinutes(MaxAgeMinutes));
                case EvictionStrategyKind.SizeBased:
                    return new SizeBasedEvictionStrategy(MaxElements);
                default:
                    throw new InvalidOperationException("Unknown eviction strategy: " + Strategy);
            }
        }

        private static EvictionStrategyKind ReadStrategy(NameValueCollection appSettings, ILogger logger)
        {
            var text = appSettings?[StrategyKey];
            if (string.IsNullOrWhiteSpace(text))
            {
                return DefaultStrategy;
            }

            EvictionStrategyKind strategy;
            if (Enum.TryParse(text.Trim(), true, out strategy) && Enum.IsDefined(typeof(EvictionStrategyKind), strategy))
            {
                return strategy;
            }

            logger.Warning(InvalidValueMessage(text, StrategyKey, DefaultStrategy));
            return DefaultStrategy;
        }

        private static int ReadPositiveInt(NameValueCollection appSettings, string key, int defaultValue, ILogger logger)
        {
            var text = appSettings?[key];
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            int value;
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0)
            {
                return value;
            }

            logger.Warning(InvalidValueMessage(text, key, defaultValue));
            return defaultValue;
        }

        private static string InvalidValueMessage(string text, string key, object defaultValue)
        {
            return string.Format(CultureInfo.InvariantCulture, "Invalid value '{0}' for {1} in App.config; using {2}", text, key, defaultValue);
        }
    }
}
