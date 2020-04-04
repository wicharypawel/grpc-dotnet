using System;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal.Abstraction
{
    /// <summary>
    /// This abstraction was added to the code base to make policies easy to mock in testing scenarios.
    /// </summary>
    interface ITimer : IDisposable
    {
        public void Start(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period);
        public bool Change(int dueTime, int period);
    }
}
