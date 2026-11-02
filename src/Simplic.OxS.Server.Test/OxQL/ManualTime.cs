namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// A clock a test moves by hand. Its timers fire on the thread that moves it, once the clock
    /// reaches their due time, and never on their own: a time bound runs out exactly when the
    /// test says so, however fast or slow the machine is.
    /// </summary>
    internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset now = start;

        /// <summary>The time now; setting it later fires the timers that fall due on the way.</summary>
        public DateTimeOffset Now
        {
            get
            {
                lock (gate)
                    return now;
            }
            set => Advance(value - Now);
        }

        public override DateTimeOffset GetUtcNow() => Now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Now.UtcTicks;

        /// <summary>Moves the clock on by <paramref name="by"/>, firing every timer due by then in the order they fall due.</summary>
        public void Advance(TimeSpan by)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);

            DateTimeOffset target;

            lock (gate)
                target = now + by;

            while (true)
            {
                ManualTimer? next;

                lock (gate)
                {
                    next = timers.Where(timer => timer.Due is { } due && due <= target).MinBy(timer => timer.Due);

                    if (next is null)
                    {
                        now = target;
                        return;
                    }

                    now = next.Due!.Value;
                    next.Due = next.Period is { } period ? now + period : null;
                }

                next.Fire();
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);

            lock (gate)
                timers.Add(timer);

            timer.Change(dueTime, period);

            return timer;
        }

        private sealed class ManualTimer(ManualTime clock, TimerCallback callback, object? state) : ITimer
        {
            /// <summary>When it fires next; null while it is stopped.</summary>
            public DateTimeOffset? Due { get; set; }

            /// <summary>The time between two firings; null when it fires once.</summary>
            public TimeSpan? Period { get; private set; }

            public void Fire() => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (clock.gate)
                {
                    if (!clock.timers.Contains(this))
                        return false;

                    Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
                    Period = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? null : period;
                }

                return true;
            }

            public void Dispose()
            {
                lock (clock.gate)
                    clock.timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();

                return ValueTask.CompletedTask;
            }
        }
    }
}
