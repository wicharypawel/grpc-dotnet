using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

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
        ILoggerFactory LoggerFactory { set; }

        /// <summary>
        /// Creates a subchannel to each server address. Depending on policy this may require additional 
        /// steps eg. reaching out to lookaside loadbalancer.
        /// </summary>
        /// <param name="resolutionResult">Resolved list of servers and/or lookaside load balancers.</param>
        /// <param name="serviceName">The name of the load balanced service (e.g., service.googleapis.com).</param>
        /// <param name="isSecureConnection">Flag if connection between client and destination server should be secured.</param>
        /// <returns>List of subchannels.</returns>
        Task CreateSubChannelsAsync(GrpcNameResolutionResult resolutionResult, string serviceName, bool isSecureConnection);
    }
}
