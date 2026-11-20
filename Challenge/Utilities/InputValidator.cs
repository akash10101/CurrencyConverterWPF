using System;
using System.Collections.Generic;
using System.Globalization;
using Challenge.Models;
using Challenge.Utilities;

namespace Challenge.Validators
{
    /// <summary>
    /// All validation rules for the user's input, in one place. Every method is a pure function of its
    /// arguments (the current date is passed in, so nothing depends on the clock) and returns the message to
    /// show the user, or null when the input is valid.
    /// </summary>
    public static class InputValidator
    {
        public static string ValidateAmount(string text)
        {
            decimal ignored;
            string error;
            TryParseAmount(text, out ignored, out error);
            return error;
        }

        /// <summary>
        /// Parses the amount the user typed (using the current culture). On failure <paramref name="error"/>
        /// holds the message to show and <paramref name="amount"/> is zero.
        /// </summary>
        public static bool TryParseAmount(string text, out decimal amount, out string error)
        {
            amount = 0m;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Enter an amount.";
                return false;
            }

            // Parse into a local so that a value rejected by the range checks never leaks out.
            decimal parsed;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                error = "The amount must be a number.";
                return false;
            }

            if (parsed <= 0m)
            {
                error = "The amount must be greater than zero.";
                return false;
            }

            if (parsed > Constants.MaxAmount)
            {
                error = "The amount is too large.";
                return false;
            }

            amount = parsed;
            error = null;
            return true;
        }

        public static string ValidateSourceCurrency(IReadOnlyCollection<CurrencyInfo> availableCurrencies, CurrencyInfo source)
        {
            // While the currency list is still loading there is nothing to select yet.
            return IsLoading(availableCurrencies) || source != null ? null : "Select a source currency.";
        }

        public static string ValidateTargetCurrency(IReadOnlyCollection<CurrencyInfo> availableCurrencies, CurrencyInfo target)
        {
            return IsLoading(availableCurrencies) || target != null ? null : "Select a target currency.";
        }

        public static string ValidateStartDate(DateTime? start, DateTime? end, DateTime today)
        {
            if (start == null)
            {
                return "Select a start date.";
            }

            if (start.Value.Date > today.Date)
            {
                return "The start date can't be in the future.";
            }

            if (end != null && start.Value.Date > end.Value.Date)
            {
                return "The start date must not be after the end date.";
            }

            return null;
        }

        public static string ValidateEndDate(DateTime? start, DateTime? end, DateTime today)
        {
            if (end == null)
            {
                return "Select an end date.";
            }

            if (end.Value.Date > today.Date)
            {
                return "The end date can't be in the future.";
            }

            if (start != null && start.Value.Date > end.Value.Date)
            {
                return "The end date must not be before the start date.";
            }

            return null;
        }

        private static bool IsLoading(IReadOnlyCollection<CurrencyInfo> availableCurrencies)
        {
            return availableCurrencies == null || availableCurrencies.Count == 0;
        }
    }
}
