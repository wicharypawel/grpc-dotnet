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
