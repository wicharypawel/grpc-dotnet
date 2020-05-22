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

using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes
{
    internal sealed class GrpcNameResolutionObserverFake : IGrpcNameResolutionObserver
    {
        private readonly ConcurrentQueue<GrpcNameResolutionResult> _results = new ConcurrentQueue<GrpcNameResolutionResult>();
        private readonly ConcurrentQueue<Status> _errors = new ConcurrentQueue<Status>();

        public void OnNext(GrpcNameResolutionResult value)
        {
            _results.Enqueue(value);
        }

        public void OnError(Status error)
        {
            _errors.Enqueue(error);
        }

        internal async Task<GrpcNameResolutionResult?> GetFirstValueOrDefaultAsync(TimeSpan? timeout = null)
        {
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(2));
            while (!timeoutTask.IsCompleted)
            {
                if (!_results.IsEmpty && _results.TryPeek(out var result))
                {
                    return result;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
            return null;
        }

        internal async Task<Status?> GetFirstErrorOrDefaultAsync(TimeSpan? timeout = null)
        {
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(2));
            while (!timeoutTask.IsCompleted)
            {
                if (!_errors.IsEmpty && _errors.TryPeek(out var result))
                {
                    return result;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
            return null;
        }
    }
}
