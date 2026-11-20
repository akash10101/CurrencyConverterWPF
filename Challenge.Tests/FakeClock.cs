using System;

namespace Challenge.Tests
{
    /// <summary>A clock the tests move forward by hand.</summary>
    internal sealed class FakeClock
    {
        public DateTime UtcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan span)
        {
            UtcNow = UtcNow + span;
        }
    }
}
