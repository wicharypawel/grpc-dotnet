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
using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// </summary>
    public interface IGrpcLoadBalancingPolicy : IDisposable
    {
        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory { set; }

        /// <summary>
        /// Creates a subchannel to each server address.
        /// </summary>
        /// <param name="resolvedAddresses">Resolved list of servers.</param>
        public void HandleResolvedAddresses(GrpcResolvedAddresses resolvedAddresses);

        /// <summary>
        /// Handles an error from the name resolution system.
        /// </summary>
        /// <param name="error">Error a non-OK status.</param>
        public void HandleNameResolutionError(Status error);

        /// <summary>
        /// Whether this LoadBalancer can handle empty address group list to be passed to <see cref="HandleResolvedAddresses"/>.
        /// By default implementation should returns false, meaning that if the NameResolver returns an empty list, the Channel will turn
        /// that into an error and call <see cref="HandleNameResolutionError"/>. LoadBalancers that want to
        /// accept empty lists should return true.
        /// </summary>
        /// <returns>True if policy accept empty list, false if not.</returns>
        public bool CanHandleEmptyAddressListFromNameResolution();

        /// <summary>
        /// The channel asks the LoadBalancer to establish connections now (if applicable) so that the
        /// upcoming RPC may then just pick a ready connection without waiting for connections.
        /// 
        /// If LoadBalancer doesn't override it, this is no-op.  If it infeasible to create connections
        /// given the current state, e.g. no Subchannel has been created yet, LoadBalancer can ignore this
        /// request.
        /// </summary>
        public void RequestConnection();
    }
}
