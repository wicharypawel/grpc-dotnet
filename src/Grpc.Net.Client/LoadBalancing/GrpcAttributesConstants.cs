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
        /// Key used to get synchronization context.
        /// </summary>
        public static readonly GrpcAttributes.Key<GrpcSynchronizationContext> ChannelSynchronizationContext = GrpcAttributes.Key<GrpcSynchronizationContext>.Create("synchronization-context");

        /// <summary>
        /// Used to override the default DNS cache duration (TTL). TTL equal zero turns off caching.
        /// </summary>
        public static readonly GrpcAttributes.Key<double> DnsResolverNetworkTtlSeconds = GrpcAttributes.Key<double>.Create("dns-resolver-network-ttl-seconds");

        /// <summary>
        /// Used to enable an optional periodic DNS resolution. Refreshes occurring more frequently than DNS cache duration (TTL) are skipped. 
        /// </summary>
        public static readonly GrpcAttributes.Key<double> DnsResolverPeriodicResolutionSeconds = GrpcAttributes.Key<double>.Create("dns-resolver-periodic-resolution-seconds");
    }
}
