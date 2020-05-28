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
using System.Collections.Concurrent;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes
{
    internal sealed class ExecutorFake : IGrpcExecutor
    {
        public ConcurrentQueue<Action> Actions { get; } = new ConcurrentQueue<Action>();

        public void Execute(Action action)
        {
            Actions.Enqueue(action);
        }

        public void DrainSingleAction()
        {
            if (Actions.TryDequeue(out var action))
            {
                action();
                return;
            }
            throw new InvalidOperationException("Can not dequeue action");
        }
    }
}
