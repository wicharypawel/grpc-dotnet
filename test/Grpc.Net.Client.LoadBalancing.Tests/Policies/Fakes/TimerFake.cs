using Grpc.Net.Client.LoadBalancing.Extensions.Internal.Abstraction;
using System;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class TimerFake : ITimer
    {
        public TimeSpan? FirstReportInterval { get; private set; }
        public TimeSpan? ClientStatsReportInterval { get; private set; }
        public TimerCallback? Callback { get; private set; }
        public object? State { get; private set; }
        
        public void Start(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Callback = callback;
            State = state;
            FirstReportInterval = dueTime;
            ClientStatsReportInterval = period;
        }

        public void ManualCallbackTrigger()
        {
            if(Callback == null)
            {
                throw new InvalidOperationException("TimerFake Started");
            }
            Callback.Invoke(State);
        }

        public bool Change(int dueTime, int period)
        {
            return true;
        }

        public void Dispose()
        {
        }
    }
}
