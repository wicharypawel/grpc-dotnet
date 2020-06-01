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

using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Represents the results from a Name Resolver.
    /// </summary>
    public sealed class GrpcNameResolutionResult
    {
        /// <summary>
        /// Found list of addresses.
        /// </summary>
        public IReadOnlyList<GrpcHostAddress> HostsAddresses { get; }

        /// <summary>
        /// Service config information. 
        /// </summary>
        public GrpcServiceConfigOrError ServiceConfig { get; }

        /// <summary>
        /// List of metadata for name resolution.
        /// </summary>
        public GrpcAttributes Attributes { get; }

        /// <summary>
        /// Creates new instance of <seealso cref="GrpcNameResolutionResult"/>.
        /// </summary>
        /// <param name="hostsAddresses">Read-only list of hosts addresses.</param>
        /// <param name="serviceConfig">Service config information.</param>
        /// <param name="attributes">List of metadata for name resolution.</param>
        public GrpcNameResolutionResult(IReadOnlyList<GrpcHostAddress> hostsAddresses,
            GrpcServiceConfigOrError serviceConfig, GrpcAttributes attributes)
        {
            HostsAddresses = hostsAddresses;
            ServiceConfig = serviceConfig;
            Attributes = attributes;
        }
    }
}
