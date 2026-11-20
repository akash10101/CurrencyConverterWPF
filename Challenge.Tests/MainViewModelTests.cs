using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Services;
using Challenge.Utilities;
using Challenge.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class MainViewModelTests
    {
        private static readonly DateTime Today = new DateTime(2025, 1, 10);

        private FakeConversionController _controller;
        private MainViewModel _viewModel;

        [TestInitialize]
        public async Task SetUp()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            _controller = new FakeConversionController();
            _viewModel = new MainViewModel(new ExchangeRateService(_controller), () => Today);
            await _viewModel.InitializeAsync();
        }

        [TestMethod]
        public void Initialize_SelectsDefaultsAndShowsLatestAndHistoricRates()
        {
            Assert.AreEqual("EUR", _viewModel.SelectedSourceCurrency.Code);
            Assert.AreEqual("USD", _viewModel.SelectedTargetCurrency.Code);

            Assert.AreEqual("2.00", _viewModel.LatestAmountText);
            Assert.AreEqual(2, _viewModel.HistoricRates.Count);
            Assert.AreEqual(new DateTime(2025, 1, 3), _viewModel.HistoricRates[0].Date, "newest first");
            Assert.AreEqual("USD", _viewModel.HistoricRates[0].Currency);
            Assert.IsFalse(_viewModel.IsStatusError);
            Assert.IsFalse(_viewModel.IsBusy);
        }

        [TestMethod]
        public void ChangingTheAmount_RecalculatesWithoutNewRequests()
        {
            _viewModel.AmountText = "10";

            Assert.AreEqual("20.00", _viewModel.LatestAmountText);
            Assert.AreEqual(15m, _viewModel.HistoricRates.Single(r => r.Date == new DateTime(2025, 1, 2)).Value);
            Assert.AreEqual(1, _controller.LatestCalls);
            Assert.AreEqual(1, _controller.HistoricCalls);
        }

        [TestMethod]
        public void InvalidAmount_ShowsErrorAndClearsResults_ThenRecovers()
        {
            _viewModel.AmountText = "abc";

            Assert.IsTrue(_viewModel.IsStatusError);
            Assert.AreEqual("The amount must be a number.", _viewModel.StatusMessage);
            Assert.AreEqual("The amount must be a number.", _viewModel["AmountText"]);
            Assert.AreEqual(string.Empty, _viewModel.LatestAmountText);
            Assert.AreEqual(0, _viewModel.HistoricRates.Count);

            _viewModel.AmountText = "5";

            Assert.IsFalse(_viewModel.IsStatusError);
            Assert.AreEqual("10.00", _viewModel.LatestAmountText);
            Assert.AreEqual(1, _controller.LatestCalls, "fixing the amount must not need the network");
        }

        [TestMethod]
        public void ZeroNegativeAndEmptyAmounts_AreRejected()
        {
            foreach (var text in new[] { "0", "-3", "", "   " })
            {
                _viewModel.AmountText = text;
                Assert.IsTrue(_viewModel.IsStatusError, "'" + text + "' should be invalid");
            }
        }

        [TestMethod]
        public void ChangingTheTargetCurrency_RequestsNewRates()
        {
            _viewModel.SelectedTargetCurrency = _viewModel.Currencies.Single(c => c.Code == "GBP");

            Assert.AreEqual(2, _controller.LatestCalls);
            Assert.AreEqual(2, _controller.HistoricCalls);
            Assert.AreEqual("GBP", _viewModel.HistoricRates[0].Currency);
        }

        [TestMethod]
        public void SameSourceAndTarget_IsAllowed_BecauseTheApiAnswersWithAnIdentityRate()
        {
            _viewModel.SelectedTargetCurrency = _viewModel.SelectedSourceCurrency;

            Assert.IsNull(_viewModel["SelectedTargetCurrency"]);
            Assert.IsFalse(_viewModel.IsStatusError);
            Assert.AreEqual(2, _controller.LatestCalls);
        }

        [TestMethod]
        public void EndDateBeforeStartDate_IsReportedAndNothingIsRequested()
        {
            _viewModel.EndDate = _viewModel.StartDate.Value.AddDays(-1);

            Assert.AreEqual("The end date must not be before the start date.", _viewModel["EndDate"]);
            Assert.AreEqual("The start date must not be after the end date.", _viewModel["StartDate"]);
            Assert.AreEqual(1, _controller.HistoricCalls);
        }

        [TestMethod]
        public void FutureEndDate_IsRejected()
        {
            _viewModel.EndDate = Today.AddDays(1);

            Assert.AreEqual("The end date can't be in the future.", _viewModel["EndDate"]);
            Assert.IsTrue(_viewModel.IsStatusError);
        }

        [TestMethod]
        public void ClearedDate_IsRejected()
        {
            _viewModel.StartDate = null;

            Assert.AreEqual("Select a start date.", _viewModel["StartDate"]);
        }

        [TestMethod]
        public async Task ServiceFailure_IsShownInTheStatusLine()
        {
            _controller.LatestException = new ExchangeRateException("The exchange rate service could not be reached.");

            await _viewModel.RefreshAsync();

            Assert.IsTrue(_viewModel.IsStatusError);
            Assert.AreEqual("The exchange rate service could not be reached.", _viewModel.StatusMessage);
            Assert.AreEqual(0, _viewModel.HistoricRates.Count);
            Assert.IsFalse(_viewModel.IsBusy);
        }

        [TestMethod]
        public void HistoricalData_IsHiddenAtStart_AndTheButtonTogglesIt()
        {
            Assert.IsFalse(_viewModel.IsHistoryVisible);
            Assert.AreEqual("Show Historical data", _viewModel.HistoryButtonText);

            _viewModel.ToggleHistoryCommand.Execute(null);

            Assert.IsTrue(_viewModel.IsHistoryVisible);
            Assert.AreEqual("Hide Historical data", _viewModel.HistoryButtonText);

            _viewModel.ToggleHistoryCommand.Execute(null);

            Assert.IsFalse(_viewModel.IsHistoryVisible);
            Assert.AreEqual("Show Historical data", _viewModel.HistoryButtonText);
            Assert.AreEqual(1, _controller.HistoricCalls, "toggling only changes what is shown");
        }

        [TestMethod]
        public void TogglingTheList_RaisesChangeNotificationForTheButtonText()
        {
            var changed = new System.Collections.Generic.List<string>();
            _viewModel.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            _viewModel.ToggleHistoryCommand.Execute(null);

            CollectionAssert.Contains(changed, "IsHistoryVisible");
            CollectionAssert.Contains(changed, "HistoryButtonText");
        }

        [TestMethod]
        public void Initialize_ShowsTheRateForOneUnitAndItsDate()
        {
            Assert.AreEqual("1 EUR = 2.00 USD", _viewModel.LatestRateText);
            StringAssert.StartsWith(_viewModel.LatestDateText, "rate of ");
        }

        [TestMethod]
        public void SwapCurrencies_ExchangesSourceAndTarget_WithASingleRefresh()
        {
            _viewModel.SwapCurrenciesCommand.Execute(null);

            Assert.AreEqual("USD", _viewModel.SelectedSourceCurrency.Code);
            Assert.AreEqual("EUR", _viewModel.SelectedTargetCurrency.Code);
            Assert.AreEqual(2, _controller.LatestCalls, "one request for the swapped pair, not one per currency");
            Assert.AreEqual(2, _controller.HistoricCalls);
            Assert.AreEqual("EUR", _viewModel.HistoricRates[0].Currency);
        }

        [TestMethod]
        public void InvalidInput_ClearsTheRateLine()
        {
            _viewModel.AmountText = "abc";

            Assert.AreEqual(string.Empty, _viewModel.LatestRateText);
            Assert.AreEqual(string.Empty, _viewModel.LatestDateText);
        }

        [TestMethod]
        public async Task EveryButton_IsLogged()
        {
            var logger = new RecordingLogger();
            var viewModel = new MainViewModel(new ExchangeRateService(_controller), () => Today, logger);
            await viewModel.InitializeAsync();

            viewModel.RefreshCommand.Execute(null);
            viewModel.ToggleHistoryCommand.Execute(null);
            viewModel.ToggleHistoryCommand.Execute(null);
            viewModel.SwapCurrenciesCommand.Execute(null);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Info [MainViewModel] Button clicked: Refresh (amount '1', EUR -> USD, 2024-12-11 to 2025-01-10)",
                    "Info [MainViewModel] Button clicked: Show Historical data",
                    "Info [MainViewModel] Button clicked: Hide Historical data",
                    "Info [MainViewModel] Button clicked: Swap currencies (EUR -> USD becomes USD -> EUR)"
                },
                logger.Lines.ToArray());
        }

        [TestMethod]
        public async Task EmptyTimeFrame_ShowsAnExplanation()
        {
            _controller.History = new Challenge.Models.DailyRate[0];

            await _viewModel.RefreshAsync();

            Assert.AreEqual(0, _viewModel.HistoricRates.Count);
            Assert.IsFalse(_viewModel.IsStatusError);
            Assert.AreEqual("No exchange rates were published in the selected time frame.", _viewModel.StatusMessage);
        }
    }
}
