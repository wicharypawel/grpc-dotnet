#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using Grpc.Net.Client.Internal;
using System;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes
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

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return true;
        }

        public void Dispose()
        {
        }
    }
}
