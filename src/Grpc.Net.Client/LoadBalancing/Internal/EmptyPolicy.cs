using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class EmptyPolicy : IGrpcLoadBalancingPolicy
    {
        private ILogger _logger = NullLogger.Instance;
        private readonly IGrpcHelper _helper;

        public EmptyPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<EmptyPolicy>();
        }

        public Task CreateSubChannelsAsync(GrpcNameResolutionResult resolutionResult, string serviceName, bool isSecureConnection)
        {
            _logger.LogDebug($"Start empty policy");
            _logger.LogDebug($"Empty policy will return no result when asked for next subchannel.");
            return Task.CompletedTask;
        }

        public GrpcPickResult GetNextSubChannel()
        {
            return GrpcPickResult.WithNoResult(); 
        }

        public void Dispose()
        {
        }
    }
}
