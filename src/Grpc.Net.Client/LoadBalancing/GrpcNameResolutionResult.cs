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
        /// <param name="hostsAddresses">List of hosts addresses.</param>
        /// <param name="serviceConfig">Service config information.</param>
        /// <param name="attributes">List of metadata for name resolution.</param>
        public GrpcNameResolutionResult(List<GrpcHostAddress> hostsAddresses,
            GrpcServiceConfigOrError serviceConfig, GrpcAttributes attributes)
        {
            HostsAddresses = hostsAddresses;
            ServiceConfig = serviceConfig;
            Attributes = attributes;
        }
    }
}
