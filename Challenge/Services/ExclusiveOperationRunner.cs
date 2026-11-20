using System;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Logging;
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
        private readonly ILogger _logger;
        private CancellationTokenSource _current;

        public ExclusiveOperationRunner(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

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

                _logger.Error("Unexpected error while loading data", ex);
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
}
