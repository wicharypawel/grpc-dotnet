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
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Configuration discovered during name resolution.
    /// </summary>
    public sealed class GrpcServiceConfig
    {
        /// <summary>
        /// Returns a list of supported policy names eg. [xds, grpclb, round_robin, pick_first]
        /// Multiple LB policies can be specified; clients will iterate through the list in order and stop at the first policy that they support. 
        /// If none are supported, the service config is considered invalid.
        /// </summary>
        public IReadOnlyList<string> RequestedLoadBalancingPolicies { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Creates GrpcServiceConfig using specified policies 
        /// </summary>
        /// <param name="policies">Policies for service config</param>
        /// <returns>New instance of GrpcServiceConfig</returns>
        public static GrpcServiceConfig Create(params string[] policies)
        {
            return new GrpcServiceConfig() { RequestedLoadBalancingPolicies = policies };
        }
    }
}
