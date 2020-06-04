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

using Grpc.Net.Client.LoadBalancing.Internal;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class GrpcHelperFake : IGrpcHelper
    {
        private readonly Uri _address;
        private int _refreshNameResolutionCount = 0;
        private int _createSubChannelCount = 0;

        public GrpcHelperFake()
        {
            _address = new UriBuilder("http://google.apis.com:80").Uri;
        }

        public List<ValueTuple<GrpcConnectivityState, IGrpcSubChannelPicker>> ObservedUpdatesToBalancingState { get; } = new List<ValueTuple<GrpcConnectivityState, IGrpcSubChannelPicker>>();
        public int CreateSubChannelCount => _createSubChannelCount;
        public int RefreshNameResolutionCount => _refreshNameResolutionCount;

        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs args)
        {
            Interlocked.Increment(ref _createSubChannelCount);
            return new GrpcSubChannelFake(args.Address, args.Attributes);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            ObservedUpdatesToBalancingState.Add(new ValueTuple<GrpcConnectivityState, IGrpcSubChannelPicker>(newState, newPicker));
        }

        public void RefreshNameResolution()
        {
            Interlocked.Increment(ref _refreshNameResolutionCount);
        }

        public GrpcSynchronizationContext GetSynchronizationContext()
        {
            throw new NotImplementedException();
        }

        public string GetAuthority()
        {
            return GrpcHelper.GetAuthorityCore(_address, true);
        }

        public Uri GetAddress()
        {
            return _address;
        }
    }
}
