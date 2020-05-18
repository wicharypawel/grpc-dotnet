using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Represents a combination of the resolved server address, associated attributes and a load
    /// balancing policy config.
    /// </summary>
    public sealed class GrpcResolvedAddresses
    {
        /// <summary>
        /// Found list of addresses.
        /// </summary>
        public IReadOnlyList<GrpcHostAddress> HostsAddresses { get; }

        /// <summary>
        /// Service config information. 
        /// </summary>
        public object ServiceConfig { get; }

        /// <summary>
        /// List of metadata for name resolution.
        /// </summary>
        public GrpcAttributes Attributes { get; }

        /// <summary>
        /// Creates new instance of <seealso cref="GrpcResolvedAddresses"/>.
        /// </summary>
        /// <param name="hostsAddresses">Read-only list of hosts addresses.</param>
        /// <param name="serviceConfig">Service config information.</param>
        /// <param name="attributes">List of metadata for name resolution.</param>
        public GrpcResolvedAddresses(IReadOnlyList<GrpcHostAddress> hostsAddresses,
            object serviceConfig, GrpcAttributes attributes)
        {
            HostsAddresses = hostsAddresses;
            ServiceConfig = serviceConfig;
            Attributes = attributes;
        }
    }
}
