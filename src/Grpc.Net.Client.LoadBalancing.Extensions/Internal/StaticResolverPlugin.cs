using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port) and a service config.
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    internal sealed class StaticResolverPlugin : IGrpcResolverPlugin
    {
        private readonly Func<Uri, GrpcNameResolutionResult> _staticNameResolution;
        private ILogger _logger = NullLogger.Instance;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<StaticResolverPlugin>();
        }

        /// <summary>
        /// Creates a <seealso cref="StaticResolverPlugin"/> using specified <seealso cref="GrpcAttributes"/>.
        /// </summary>
        /// <param name="attributes">Attributes with options.</param>
        public StaticResolverPlugin(GrpcAttributes attributes)
        {
            var options = attributes.Get(GrpcAttributesLbConstants.StaticResolverOptions) as StaticResolverPluginOptions;
            _staticNameResolution = options?.StaticNameResolution ?? throw new ArgumentNullException(nameof(options.StaticNameResolution));
        }

        /// <summary>
        /// Creates a <seealso cref="StaticResolverPlugin"/> using specified <seealso cref="XdsResolverPluginOptions"/>.
        /// </summary>
        /// <param name="options">Options with defined behaviour.</param>
        public StaticResolverPlugin(StaticResolverPluginOptions options)
        {
            if (options?.StaticNameResolution == null)
            {
                throw new ArgumentNullException(nameof(options.StaticNameResolution));
            }
            _staticNameResolution = options.StaticNameResolution;
        }

        /// <summary>
        /// Name resolution for secified target.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <returns>List of resolved servers and/or lookaside load balancers.</returns>
        public Task<GrpcNameResolutionResult> StartNameResolutionAsync(Uri target)
        {
            _logger.LogDebug($"Using static name resolution");
            _logger.LogDebug($"Using static service config");
            return Task.FromResult(_staticNameResolution(target));
        }

        public void Dispose()
        {
        }
    }
}
