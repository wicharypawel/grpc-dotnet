using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port) and a service config.
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    public interface IGrpcResolverPlugin : IDisposable
    {
        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory { set; }

        /// <summary>
        /// Starts the resolution.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <param name="observer">Observer used to receive updates on the target.</param>
        public void Subscribe(Uri target, IGrpcNameResolutionObserver observer);

        /// <summary>
        /// Stops the resolution. Updates to the Listener will stop.
        /// </summary>
        public void Unsubscribe();

        /// <summary>
        /// Re-resolve the name. Can only be called after Start method has been called.
        /// This is only a hint. Implementation takes it as a signal but may not start resolution 
        /// immediately. It should never throw.
        /// 
        /// It is possible to leave this operation empty (no-op). 
        /// </summary>
        public void RefreshResolution();
    }
}
