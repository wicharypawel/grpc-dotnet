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

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Receives state changes for one <see cref="IGrpcSubChannel"/>. All methods are run under 
    /// <see cref="IGrpcHelper.GetSynchronizationContext"/>.
    /// </summary>
    public interface IGrpcSubchannelStateObserver
    {
        /// <summary>
        /// Handles a state change on a Subchannel.
        /// 
        /// The initial state of a Subchannel is IDLE. You won't get a notification for the initial
        /// IDLE state.
        /// 
        /// If the new state is not SHUTDOWN, this method should create a new picker and call
        /// <see cref="IGrpcHelper.UpdateBalancingState"/>. Failing to do so may result in
        /// unnecessary delays of RPCs. Please refer to <see cref="GrpcPickResult.WithSubChannel"/>
        /// for more information.
        /// 
        /// LoadBalancer usually don't need to react to a SHUTDOWN state.
        /// </summary>
        /// <param name="value">NewState the new state.</param>
        public void OnNext(GrpcConnectivityStateInfo value);
    }
}
