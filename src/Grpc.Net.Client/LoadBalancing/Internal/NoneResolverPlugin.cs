using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// Assume name was already resolved or pass through to HttpClient to handle
    /// </summary>
    internal sealed class NoneResolverPlugin : IGrpcResolverPlugin
    {
        private ILogger _logger = NullLogger.Instance;
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<NoneResolverPlugin>();
        }

        public Task<List<GrpcNameResolutionResult>> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("dns:// scheme require non-default name resolver in channelOptions.ResolverPlugin");
            }
            _logger.LogDebug($"Name resolver using defined target as name resolution");
            return Task.FromResult(new List<GrpcNameResolutionResult>()
            {
               new GrpcNameResolutionResult(target.Host, target.Port)
            });
        }

        public Task<GrpcServiceConfig> GetServiceConfigAsync()
        {
            _logger.LogDebug($"Name resolver returns default pick_first policy");
            return Task.FromResult(GrpcServiceConfig.Create("pick_first"));
        }
    }
}
