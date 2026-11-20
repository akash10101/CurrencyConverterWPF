using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Utilities;

namespace Challenge.Services
{
    /// <summary>
    /// Runs one operation at a time: starting a new one cancels the previous one, and the outcome of a
    /// cancelled (superseded) operation is reported as <see cref="OperationOutcome{T}.IsSuperseded"/> so
    /// the caller can ignore it completely. Failures are turned into a message for the user.
    /// </summary>
    internal sealed class ExclusiveOperationRunner
    {
        private CancellationTokenSource _current;

        public async Task<OperationOutcome<T>> RunAsync<T>(Func<CancellationToken, Task<T>> operation)
        {
            var cts = new CancellationTokenSource();
            var previous = _current;
            _current = cts;
            if (previous != null)
            {
                previous.Cancel();
            }

            try
            {
                var value = await operation(cts.Token);
                return IsSuperseded(cts) ? OperationOutcome<T>.Superseded() : OperationOutcome<T>.Success(value);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                return OperationOutcome<T>.Superseded();
            }
            catch (Exception ex)
            {
                if (IsSuperseded(cts))
                {
                    return OperationOutcome<T>.Superseded();
                }

                if (ex is ExchangeRateException)
                {
                    return OperationOutcome<T>.Failure(ex.Message);
                }

                Trace.TraceError(ex.ToString());
                return OperationOutcome<T>.Failure(Constants.ServiceErrorMsg + ex.Message);
            }
            finally
            {
                if (ReferenceEquals(_current, cts))
                {
                    _current = null;
                }

                cts.Dispose();
            }
        }

        /// <summary>Cancels whatever is running; its outcome will be reported as superseded.</summary>
        public void Cancel()
        {
            var running = _current;
            _current = null;
            if (running != null)
            {
                running.Cancel();
            }
        }

        private bool IsSuperseded(CancellationTokenSource cts)
        {
            return cts.IsCancellationRequested || !ReferenceEquals(_current, cts);
        }
    }

    internal sealed class OperationOutcome<T>
    {
        private OperationOutcome(bool isSuperseded, T value, string error)
        {
            IsSuperseded = isSuperseded;
            Value = value;
            Error = error;
        }

        public bool IsSuperseded { get; }

        public T Value { get; }
        public string Error { get; }

        public static OperationOutcome<T> Success(T value) => new OperationOutcome<T>(false, value, null);

        public static OperationOutcome<T> Failure(string error) => new OperationOutcome<T>(false, default(T), error);

        public static OperationOutcome<T> Superseded() => new OperationOutcome<T>(true, default(T), null);
    }
}
