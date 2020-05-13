using Grpc.Core;
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

        /// <summary>
        /// Handles an error from the name resolution system.
        /// </summary>
        /// <param name="error">Error a non-OK status</param>
        /// <returns>Task instance.</returns>
        Task HandleNameResolutionErrorAsync(Status error);

        /// <summary>
        /// Whether this LoadBalancer can handle empty address group list to be passed to <see cref="CreateSubChannelsAsync"/>.
        /// By default implementation should returns false, meaning that if the NameResolver returns an empty list, the Channel will turn
        /// that into an error and call <see cref="HandleNameResolutionErrorAsync"/>. LoadBalancers that want to
        /// accept empty lists should return true.
        /// </summary>
        /// <returns>True if policy accept empty list, false if not.</returns>
        bool CanHandleEmptyAddressListFromNameResolution();

        /// <summary>
        /// The channel asks the LoadBalancer to establish connections now (if applicable) so that the
        /// upcoming RPC may then just pick a ready connection without waiting for connections.
        /// 
        /// If LoadBalancer doesn't override it, this is no-op.  If it infeasible to create connections
        /// given the current state, e.g. no Subchannel has been created yet, LoadBalancer can ignore this
        /// request.
        /// </summary>
        /// <returns>Task instance.</returns>
        Task RequestConnectionAsync();
    }
}
