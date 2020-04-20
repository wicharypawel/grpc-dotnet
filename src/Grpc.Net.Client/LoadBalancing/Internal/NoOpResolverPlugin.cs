using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// No-Op resolver will pass uri address to HttpClient to handle.
    /// </summary>
    internal sealed class NoOpResolverPlugin : IGrpcResolverPlugin
    {
        private readonly string[] WellKnownSchemes = new string[] { "dns", "xds", "xds-experimental" }; 
        private ILogger _logger = NullLogger.Instance;
        private readonly string _defaultLoadBalancingPolicy; 
        
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<NoOpResolverPlugin>();
        }

        public NoOpResolverPlugin()
        {
            _defaultLoadBalancingPolicy = "pick_first";
        }

        public NoOpResolverPlugin(GrpcAttributes attributes)
        {
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) as string
                ?? "pick_first";
        }

        public Task<GrpcNameResolutionResult> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (WellKnownSchemes.Contains(target.Scheme.ToLowerInvariant()))
            {
                throw new ArgumentException($"{target.Scheme}:// scheme require non-default name resolver");
            }
            _logger.LogDebug("NoOpResolverPlugin using defined target as name resolution");
            var hosts = new List<GrpcHostAddress>()
            {
               new GrpcHostAddress(target.Host, target.Port)
            };
            _logger.LogDebug($"NoOpResolverPlugin returns {_defaultLoadBalancingPolicy} policy");
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create(_defaultLoadBalancingPolicy));
            return Task.FromResult(new GrpcNameResolutionResult(hosts, config, GrpcAttributes.Empty));
        }
    }
}
