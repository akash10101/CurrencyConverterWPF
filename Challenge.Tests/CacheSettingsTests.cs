using System;
using System.Collections.Specialized;
using Challenge.Caching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class CacheSettingsTests
    {
        [TestMethod]
        public void ReadsConfiguredValues()
        {
            var settings = CacheSettings.Load(new NameValueCollection
            {
                { CacheSettings.StrategyKey, "sizebased" },
                { CacheSettings.MaxAgeMinutesKey, "3" },
                { CacheSettings.MaxElementsKey, "7" },
            });

            Assert.AreEqual(EvictionStrategyKind.SizeBased, settings.Strategy);
            Assert.AreEqual(3, settings.MaxAgeMinutes);
            Assert.AreEqual(7, settings.MaxElements);
            Assert.IsInstanceOfType(settings.CreateEvictionStrategy(), typeof(SizeBasedEvictionStrategy));
        }

        [TestMethod]
        public void MissingOrInvalidValues_FallBackToDefaults()
        {
            var settings = CacheSettings.Load(new NameValueCollection
            {
                { CacheSettings.StrategyKey, "7" },
                { CacheSettings.MaxAgeMinutesKey, "abc" },
                { CacheSettings.MaxElementsKey, "-4" },
            });

            Assert.AreEqual(CacheSettings.DefaultStrategy, settings.Strategy);
            Assert.AreEqual(CacheSettings.DefaultMaxAgeMinutes, settings.MaxAgeMinutes);
            Assert.AreEqual(CacheSettings.DefaultMaxElements, settings.MaxElements);
            Assert.IsInstanceOfType(settings.CreateEvictionStrategy(), typeof(TimeBasedEvictionStrategy));
        }

        [TestMethod]
        public void EmptyConfiguration_UsesDefaults()
        {
            var settings = CacheSettings.Load(new NameValueCollection());

            Assert.AreEqual(EvictionStrategyKind.TimeBased, settings.Strategy);
        }
    }
}
