using System;
using System.Threading;

namespace Grpc.Net.Client.Internal
{
    internal sealed class SystemTimer : ITimer
    {
        private Timer? _timer;

        public void Start(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (_timer != null)
            {
                throw new InvalidOperationException("Timer already started.");
            }
            _timer = new Timer(callback, state, dueTime, period);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return _timer?.Change(dueTime, period) ?? false;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
