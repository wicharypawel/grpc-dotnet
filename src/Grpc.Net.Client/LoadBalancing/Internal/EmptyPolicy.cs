using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class EmptyPolicy : IGrpcLoadBalancingPolicy
    {
        internal static readonly GrpcSubChannel NoResultSubChannel = new GrpcSubChannel(new Uri("not-found://magic-value"));
        private ILogger _logger = NullLogger.Instance;
        
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

        public GrpcSubChannel GetNextSubChannel()
        {
            // TODO change IGrpcLoadBalancingPolicy interface to return channels wrapped in object with special value for no-result
            return NoResultSubChannel; 
        }

        public void Dispose()
        {
        }
    }
}
