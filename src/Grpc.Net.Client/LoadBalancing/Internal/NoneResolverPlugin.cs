using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// Assume name was already resolved or pass through to HttpClient to handle
    /// </summary>
    internal sealed class NoneResolverPlugin : IGrpcResolverPlugin
    {
        private readonly string[] WellKnownSchemes = new string[] { "dns", "xds" }; 
        private ILogger _logger = NullLogger.Instance;
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<NoneResolverPlugin>();
        }

        public Task<List<GrpcHostAddress>> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (WellKnownSchemes.Contains(target.Scheme.ToLowerInvariant()))
            {
                throw new ArgumentException($"{target.Scheme}:// scheme require non-default name resolver in channelOptions.ResolverPlugin");
            }
            _logger.LogDebug($"Name resolver using defined target as name resolution");
            return Task.FromResult(new List<GrpcHostAddress>()
            {
               new GrpcHostAddress(target.Host, target.Port)
            });
        }

        public Task<GrpcServiceConfig> GetServiceConfigAsync()
        {
            _logger.LogDebug($"Name resolver returns default pick_first policy");
            return Task.FromResult(GrpcServiceConfig.Create("pick_first"));
        }
    }
}
