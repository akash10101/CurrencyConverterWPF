namespace Challenge.Services
{
    /// <summary>What <see cref="ExclusiveOperationRunner"/> reports back: a value, an error message for the user, or "superseded".</summary>
    internal sealed class OperationOutcome<T>
    {
        private OperationOutcome(bool isSuperseded, T value, string error)
        {
            IsSuperseded = isSuperseded;
            Value = value;
            Error = error;
        }

        /// <summary>A newer operation replaced this one (or it was cancelled); ignore the outcome.</summary>
        public bool IsSuperseded { get; }

        public T Value { get; }

        /// <summary>A message for the user, or <c>null</c> if the operation succeeded.</summary>
        public string Error { get; }

        public static OperationOutcome<T> Success(T value) => new OperationOutcome<T>(false, value, null);

        public static OperationOutcome<T> Failure(string error) => new OperationOutcome<T>(false, default(T), error);

        public static OperationOutcome<T> Superseded() => new OperationOutcome<T>(true, default(T), null);
    }
}
