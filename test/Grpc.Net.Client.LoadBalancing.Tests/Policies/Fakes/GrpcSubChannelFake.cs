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

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class GrpcSubChannelFake : IGrpcSubChannel
    {
        private int _requestConnectionCount = 0;
        private int _shutdownCount = 0;
        private int _startCount = 0;

        public GrpcSubChannelFake(Uri address, GrpcAttributes attributes)
        {
            Address = address;
            Attributes = attributes;
        }

        public Uri Address { get; set; }
        public GrpcAttributes Attributes { get; set; }
        public int RequestConnectionCount => _requestConnectionCount;
        public int ShutdownCount => _shutdownCount;
        public int StartCount => _startCount;

        public void RequestConnection()
        {
            Interlocked.Increment(ref _requestConnectionCount);
        }

        public void Shutdown()
        {
            Interlocked.Increment(ref _shutdownCount);
        }

        public void Start(IGrpcSubchannelStateObserver observer)
        {
            Interlocked.Increment(ref _startCount);
        }
    }
}
