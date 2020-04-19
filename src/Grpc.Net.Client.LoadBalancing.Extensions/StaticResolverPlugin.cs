using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port) and a service config.
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    public sealed class StaticResolverPlugin : IGrpcResolverPlugin
    {
        private readonly Func<Uri, List<GrpcHostAddress>> _staticNameResolution;
        private readonly Func<GrpcServiceConfig> _staticServiceConfig;
        private ILogger _logger = NullLogger.Instance;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<StaticResolverPlugin>();
        }

        /// <summary>
        /// Creates a <seealso cref="StaticResolverPlugin"/> with configation passed as function parameter.  
        /// </summary>
        /// <param name="staticNameResolution">Define name resolution using lambda</param>
        /// <param name="staticServiceConfig">Define service config using lambda</param>
        public StaticResolverPlugin(Func<Uri, List<GrpcHostAddress>> staticNameResolution,
            Func<GrpcServiceConfig> staticServiceConfig)
        {
            if(staticNameResolution == null)
            {
                throw new ArgumentNullException(nameof(staticNameResolution));
            }
            if (staticServiceConfig == null)
            {
                throw new ArgumentNullException(nameof(staticServiceConfig));
            }
            _staticNameResolution = staticNameResolution;
            _staticServiceConfig = staticServiceConfig;
        }

        /// <summary>
        /// Name resolution for secified target.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <returns>List of resolved servers and/or lookaside load balancers.</returns>
        public Task<List<GrpcHostAddress>> StartNameResolutionAsync(Uri target)
        {
            _logger.LogDebug($"Using static name resolution");
            return Task.FromResult(_staticNameResolution(target));
        }

        /// <summary>
        /// Returns load balancing configuration discovered during name resolution.
        /// </summary>
        /// <returns>Load balancing configuration.</returns>
        public Task<GrpcServiceConfig> GetServiceConfigAsync()
        {
            _logger.LogDebug($"Using static service config");
            return Task.FromResult(_staticServiceConfig());
        }
    }
}
