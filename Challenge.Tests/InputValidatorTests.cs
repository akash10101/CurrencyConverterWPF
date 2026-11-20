using System;
using System.Globalization;
using System.Threading;
using Challenge.Models;
using Challenge.Utilities;
using Challenge.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Challenge.Tests
{
    [TestClass]
    public class InputValidatorTests
    {
        private static readonly DateTime Today = new DateTime(2025, 1, 10);

        private static readonly CurrencyInfo[] Currencies = { new CurrencyInfo("EUR", "Euro"), new CurrencyInfo("USD", "US Dollar") };

        [TestInitialize]
        public void SetUp()
        {
            // The amount is parsed with the current culture; pin it so "1,234.50" means the same everywhere.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        // ----- amount -----

        [DataTestMethod]
        [DataRow("1")]
        [DataRow("0.01")]
        [DataRow("1,234.50")]
        [DataRow("  12  ")]
        [DataRow("1000000000000000")]
        public void Amount_ValidValues_AreAccepted(string text)
        {
            Assert.IsNull(InputValidator.ValidateAmount(text));
        }

        [DataTestMethod]
        [DataRow(null, "Enter an amount.")]
        [DataRow("", "Enter an amount.")]
        [DataRow("   ", "Enter an amount.")]
        [DataRow("abc", "The amount must be a number.")]
        [DataRow("1.2.3", "The amount must be a number.")]
        [DataRow("0", "The amount must be greater than zero.")]
        [DataRow("-3", "The amount must be greater than zero.")]
        [DataRow("1000000000000001", "The amount is too large.")]
        public void Amount_InvalidValues_ReturnTheMessage(string text, string expected)
        {
            Assert.AreEqual(expected, InputValidator.ValidateAmount(text));
        }

        [TestMethod]
        public void TryParseAmount_ReturnsTheParsedValue()
        {
            decimal amount;
            string error;

            Assert.IsTrue(InputValidator.TryParseAmount("12.5", out amount, out error));
            Assert.AreEqual(12.5m, amount);
            Assert.IsNull(error);
        }

        [TestMethod]
        public void TryParseAmount_OnFailure_ReturnsZeroAndTheMessage()
        {
            decimal amount;
            string error;

            Assert.IsFalse(InputValidator.TryParseAmount("-1", out amount, out error));
            Assert.AreEqual(0m, amount);
            Assert.AreEqual("The amount must be greater than zero.", error);
        }

        // ----- currencies -----

        [TestMethod]
        public void Currencies_WhileTheListIsStillLoading_AreNotReported()
        {
            Assert.IsNull(InputValidator.ValidateSourceCurrency(new CurrencyInfo[0], null));
            Assert.IsNull(InputValidator.ValidateTargetCurrency(new CurrencyInfo[0], null));
            Assert.IsNull(InputValidator.ValidateSourceCurrency(null, null));
        }

        [TestMethod]
        public void Currencies_MissingSelection_IsReported()
        {
            Assert.AreEqual("Select a source currency.", InputValidator.ValidateSourceCurrency(Currencies, null));
            Assert.AreEqual("Select a target currency.", InputValidator.ValidateTargetCurrency(Currencies, null));
        }

        [TestMethod]
        public void Currencies_WithSelection_AreValid_EvenIfIdentical()
        {
            Assert.IsNull(InputValidator.ValidateSourceCurrency(Currencies, Currencies[0]));
            Assert.IsNull(InputValidator.ValidateTargetCurrency(Currencies, Currencies[0]));
        }

        // ----- dates -----

        [TestMethod]
        public void Dates_ValidRange_IsAccepted()
        {
            var start = new DateTime(2025, 1, 1);
            var end = new DateTime(2025, 1, 10);

            Assert.IsNull(InputValidator.ValidateStartDate(start, end, Today));
            Assert.IsNull(InputValidator.ValidateEndDate(start, end, Today));
        }

        [TestMethod]
        public void Dates_SingleDay_IsAccepted_AndTimeOfDayIsIgnored()
        {
            var start = new DateTime(2025, 1, 10, 15, 0, 0);
            var end = new DateTime(2025, 1, 10, 9, 0, 0);

            Assert.IsNull(InputValidator.ValidateStartDate(start, end, Today));
            Assert.IsNull(InputValidator.ValidateEndDate(start, end, Today));
        }

        [TestMethod]
        public void Dates_Missing_AreReported()
        {
            Assert.AreEqual("Select a start date.", InputValidator.ValidateStartDate(null, Today, Today));
            Assert.AreEqual("Select an end date.", InputValidator.ValidateEndDate(Today, null, Today));
        }

        [TestMethod]
        public void Dates_InTheFuture_AreReported()
        {
            var tomorrow = Today.AddDays(1);

            Assert.AreEqual("The start date can't be in the future.", InputValidator.ValidateStartDate(tomorrow, null, Today));
            Assert.AreEqual("The end date can't be in the future.", InputValidator.ValidateEndDate(null, tomorrow, Today));
        }

        [TestMethod]
        public void Dates_StartAfterEnd_IsReportedOnBothFields()
        {
            var start = new DateTime(2025, 1, 5);
            var end = new DateTime(2025, 1, 4);

            Assert.AreEqual("The start date must not be after the end date.", InputValidator.ValidateStartDate(start, end, Today));
            Assert.AreEqual("The end date must not be before the start date.", InputValidator.ValidateEndDate(start, end, Today));
        }

        [TestMethod]
        public void Dates_OneSideMissing_DoesNotTriggerTheOrderCheck()
        {
            Assert.IsNull(InputValidator.ValidateStartDate(new DateTime(2025, 1, 5), null, Today));
            Assert.IsNull(InputValidator.ValidateEndDate(null, new DateTime(2025, 1, 5), Today));
        }
    }
}
