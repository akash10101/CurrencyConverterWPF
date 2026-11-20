using System;
using System.Collections.Specialized;
using System.Linq;
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
        public void InvalidValues_AreLoggedAsWarnings()
        {
            var logger = new RecordingLogger();

            CacheSettings.Load(
                new NameValueCollection
                {
                    { CacheSettings.StrategyKey, "Fastest" },
                    { CacheSettings.MaxAgeMinutesKey, "abc" },
                    { CacheSettings.MaxElementsKey, "5" },
                },
                logger);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Warning [CacheSettings] Invalid value 'Fastest' for Cache.EvictionStrategy in App.config; using TimeBased",
                    "Warning [CacheSettings] Invalid value 'abc' for Cache.MaxAgeMinutes in App.config; using 10"
                },
                logger.Lines.ToArray());
        }

        [TestMethod]
        public void ValidOrMissingValues_LogNothing()
        {
            var logger = new RecordingLogger();

            CacheSettings.Load(new NameValueCollection { { CacheSettings.MaxElementsKey, "5" } }, logger);

            Assert.AreEqual(0, logger.Lines.Count);
        }

        [TestMethod]
        public void ToString_DescribesTheSettingsOfTheChosenStrategy()
        {
            Assert.AreEqual("TimeBased (entries expire after 10 min)", new CacheSettings(EvictionStrategyKind.TimeBased, 10, 20).ToString());
            Assert.AreEqual("SizeBased (at most 20 entries)", new CacheSettings(EvictionStrategyKind.SizeBased, 10, 20).ToString());
        }

        [TestMethod]
        public void EmptyConfiguration_UsesDefaults()
        {
            var settings = CacheSettings.Load(new NameValueCollection());

            Assert.AreEqual(EvictionStrategyKind.TimeBased, settings.Strategy);
        }
    }
}
