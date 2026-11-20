using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Controllers;
using Challenge.Logging;
using Challenge.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class ConversionControllerTests
    {
        private const string LatestJson = "{\"date\":\"2025-01-03\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1.0299}";

        // v2 returns a flat array: one row per day and quote currency (deliberately out of order here).
        private const string SeriesJson =
            "[{\"date\":\"2025-01-03\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1.0299}," +
            "{\"date\":\"2025-01-02\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1.0321}]";

        private const string CurrenciesJson =
            "[{\"iso_code\":\"USD\",\"name\":\"United States Dollar\",\"type\":\"currency\"}," +
            "{\"iso_code\":\"XAU\",\"name\":\"Gold\",\"type\":\"metal\"}," +
            "{\"iso_code\":\"BTC\",\"name\":\"Bitcoin\",\"type\":\"crypto\"}," +
            "{\"iso_code\":\"EUR\",\"name\":\"Euro\",\"type\":\"currency\"}]";

        [TestMethod]
        public async Task GetLatestRate_ParsesRateAndDate()
        {
            var controller = Create(HttpStatusCode.OK, LatestJson, out var handler);

            var rate = await controller.GetLatestRateAsync("eur", "usd", CancellationToken.None);

            Assert.AreEqual(1.0299m, rate.Rate);
            Assert.AreEqual(new DateTime(2025, 1, 3), rate.Date);
            Assert.AreEqual("https://api.frankfurter.dev/v2/rate/EUR/USD", handler.LastRequestUri.AbsoluteUri);
        }

        [TestMethod]
        public async Task GetHistoricRates_ParsesAndSortsOldestFirst()
        {
            var controller = Create(HttpStatusCode.OK, SeriesJson, out var handler);

            var rates = await controller.GetHistoricRatesAsync("EUR", "USD", new DateTime(2025, 1, 2), new DateTime(2025, 1, 3), CancellationToken.None);

            Assert.AreEqual(2, rates.Count);
            Assert.AreEqual(new DateTime(2025, 1, 2), rates[0].Date);
            Assert.AreEqual(1.0321m, rates[0].Rate);
            Assert.AreEqual(new DateTime(2025, 1, 3), rates[1].Date);
            Assert.AreEqual("https://api.frankfurter.dev/v2/rates?from=2025-01-02&to=2025-01-03&base=EUR&quotes=USD", handler.LastRequestUri.AbsoluteUri);
        }

        [TestMethod]
        public async Task GetHistoricRates_IgnoresRowsOfOtherQuoteCurrencies()
        {
            const string json =
                "[{\"date\":\"2025-01-02\",\"base\":\"EUR\",\"quote\":\"GBP\",\"rate\":0.83}," +
                "{\"date\":\"2025-01-02\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1.0321}]";
            var controller = Create(HttpStatusCode.OK, json, out _);

            var rates = await controller.GetHistoricRatesAsync("EUR", "USD", new DateTime(2025, 1, 2), new DateTime(2025, 1, 2), CancellationToken.None);

            Assert.AreEqual(1, rates.Count);
            Assert.AreEqual(1.0321m, rates[0].Rate);
        }

        [TestMethod]
        public async Task GetHistoricRates_NotFound_ReturnsEmptyList()
        {
            var controller = Create(HttpStatusCode.NotFound, "{\"status\":404,\"message\":\"not found\"}", out _);

            var rates = await controller.GetHistoricRatesAsync("EUR", "USD", new DateTime(2025, 1, 4), new DateTime(2025, 1, 5), CancellationToken.None);

            Assert.AreEqual(0, rates.Count);
        }

        [TestMethod]
        public async Task GetHistoricRates_EmptyArray_ReturnsEmptyList()
        {
            var controller = Create(HttpStatusCode.OK, "[]", out _);

            var rates = await controller.GetHistoricRatesAsync("EUR", "USD", new DateTime(2025, 1, 4), new DateTime(2025, 1, 5), CancellationToken.None);

            Assert.AreEqual(0, rates.Count);
        }

        [TestMethod]
        public async Task GetCurrencies_ReturnsOnlyRealCurrencies_SortedByCode()
        {
            var controller = Create(HttpStatusCode.OK, CurrenciesJson, out var handler);

            var currencies = await controller.GetCurrenciesAsync(CancellationToken.None);

            Assert.AreEqual(2, currencies.Count, "metals and crypto assets are not currencies");
            Assert.AreEqual("EUR", currencies[0].Code);
            Assert.AreEqual("Euro", currencies[0].Name);
            Assert.AreEqual("USD", currencies[1].Code);
            Assert.AreEqual("https://api.frankfurter.dev/v2/currencies", handler.LastRequestUri.AbsoluteUri);
        }

        [TestMethod]
        public async Task ProviderPin_IsAppendedToEveryRequest()
        {
            var controller = Create(HttpStatusCode.OK, LatestJson, out var handler, "ecb");

            await controller.GetLatestRateAsync("EUR", "USD", CancellationToken.None);

            Assert.AreEqual("https://api.frankfurter.dev/v2/rate/EUR/USD?providers=ecb", handler.LastRequestUri.AbsoluteUri);
        }

        [TestMethod]
        public void InvalidProviderList_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => new ConversionController(new HttpClient(), "ecb&evil=1"));
        }

        [TestMethod]
        public async Task ErrorResponse_SurfacesTheServiceMessage()
        {
            var controller = Create((HttpStatusCode)422, "{\"status\":422,\"message\":\"invalid currency: ABC\"}", out _);

            var ex = await Assert.ThrowsExceptionAsync<ExchangeRateException>(
                () => controller.GetLatestRateAsync("EUR", "ABC", CancellationToken.None));

            StringAssert.Contains(ex.Message, "invalid currency: ABC");
        }

        [TestMethod]
        public async Task ServerError_WithoutJsonBody_ThrowsExchangeRateException()
        {
            var controller = Create(HttpStatusCode.InternalServerError, "oops", out _);

            await Assert.ThrowsExceptionAsync<ExchangeRateException>(
                () => controller.GetLatestRateAsync("EUR", "USD", CancellationToken.None));
        }

        [TestMethod]
        public async Task MalformedJson_ThrowsExchangeRateException()
        {
            var controller = Create(HttpStatusCode.OK, "this is not json", out _);

            await Assert.ThrowsExceptionAsync<ExchangeRateException>(
                () => controller.GetLatestRateAsync("EUR", "USD", CancellationToken.None));
        }

        [TestMethod]
        public async Task InvalidCurrencyCode_IsRejectedBeforeAnyRequest()
        {
            var controller = Create(HttpStatusCode.OK, LatestJson, out var handler);

            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => controller.GetLatestRateAsync("EUR", "US/D?", CancellationToken.None));

            Assert.IsNull(handler.LastRequestUri);
        }

        [TestMethod]
        public async Task ApiCall_LogsTheRequestAndTheResponse()
        {
            var logger = new RecordingLogger();
            var controller = Create(HttpStatusCode.OK, LatestJson, out _, logger: logger);

            await controller.GetLatestRateAsync("EUR", "USD", CancellationToken.None);

            Assert.AreEqual(2, logger.Lines.Count);
            Assert.AreEqual("Info [FrankfurterApi] Request: GET https://api.frankfurter.dev/v2/rate/EUR/USD", logger.Lines[0]);
            StringAssert.StartsWith(logger.Lines[1], "Info [FrankfurterApi] Response: 200 OK for GET /v2/rate/EUR/USD in ");
            StringAssert.EndsWith(logger.Lines[1], LatestJson.Length + " chars: " + LatestJson);
        }

        [TestMethod]
        public async Task ApiError_IsLoggedAsAWarning()
        {
            var logger = new RecordingLogger();
            var controller = Create((HttpStatusCode)422, "{\"status\":422,\"message\":\"invalid currency: ABC\"}", out _, logger: logger);

            await Assert.ThrowsExceptionAsync<ExchangeRateException>(
                () => controller.GetLatestRateAsync("EUR", "ABC", CancellationToken.None));

            Assert.IsTrue(logger.Contains("Warning [FrankfurterApi] GET /v2/rate/EUR/ABC failed:"));
            Assert.IsTrue(logger.Contains("invalid currency: ABC"));
        }

        [TestMethod]
        public async Task LongResponseBodies_AreShortenedInTheLog()
        {
            var logger = new RecordingLogger();
            var longJson = "[" + string.Join(",", Enumerable.Repeat("{\"iso_code\":\"EUR\",\"name\":\"Euro\",\"type\":\"currency\"}", 20)) + "]";
            var controller = Create(HttpStatusCode.OK, longJson, out _, logger: logger);

            await controller.GetCurrenciesAsync(CancellationToken.None);

            StringAssert.Contains(logger.Lines[1], "more chars)");
        }

        private static ConversionController Create(HttpStatusCode status, string body, out StubHandler handler, string providers = null, ILogger logger = null)
        {
            handler = new StubHandler(status, body);
            return new ConversionController(new HttpClient(handler), providers, logger);
        }
    }
}
