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
