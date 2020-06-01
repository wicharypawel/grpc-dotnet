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

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class HelperFake : IGrpcHelper
    {
        public IGrpcSubChannelPicker? SubChannelPicker { get; private set; }

        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs args)
        {
            return new GrpcSubChannelFake(args.Address, args.Attributes);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            SubChannelPicker = newPicker;
        }

        public void RefreshNameResolution()
        {
            throw new System.NotImplementedException();
        }

        public GrpcSynchronizationContext GetSynchronizationContext()
        {
            throw new System.NotImplementedException();
        }
    }
}
