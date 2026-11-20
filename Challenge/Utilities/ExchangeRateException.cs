using System;

namespace Challenge.Utilities
{
    /// <summary>
    /// The exchange rate service could not deliver usable data. The message is written for the
    /// end user and is shown in the status line.
    /// </summary>
    public class ExchangeRateException : Exception
    {
        public ExchangeRateException(string message) : base(message)
        {
        }

        public ExchangeRateException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
