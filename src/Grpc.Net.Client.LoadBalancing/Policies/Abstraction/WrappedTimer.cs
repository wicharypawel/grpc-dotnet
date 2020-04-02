using System;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Policies.Abstraction
{
    /// <summary>
    /// This class wrap and delegate Timer.
    /// The reason why it was added described here <seealso cref="ITimer"/>  
    /// </summary>
    internal sealed class WrappedTimer : ITimer
    {
        private Timer? _timer;

        public void Start(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new Timer(callback, state, dueTime, period);
        }

        public bool Change(int dueTime, int period)
        {
            return _timer?.Change(dueTime, period) ?? false;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
