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
    /// GrpcAttributes well-known keys.
    /// </summary>
    public sealed class GrpcAttributesConstants
    {
        /// <summary>
        /// Key used to set options for static resolver.
        /// </summary>
        public static readonly GrpcAttributes.Key<StaticResolverPluginOptions> StaticResolverOptions = GrpcAttributes.Key<StaticResolverPluginOptions>.Create("static-resolver-options");

        /// <summary>
        /// Key used to set default load balancing policy.
        /// </summary>
        public static readonly GrpcAttributes.Key<string> DefaultLoadBalancingPolicy = GrpcAttributes.Key<string>.Create("default-loadbalancing-policy");

        /// <summary>
        /// Used to override default DNS cache duration (TTL).
        /// </summary>
        public static readonly GrpcAttributes.Key<string> DnsResolverNetworkTtlSeconds = GrpcAttributes.Key<string>.Create("dns-resolver-network-ttl-seconds");
    }
}
