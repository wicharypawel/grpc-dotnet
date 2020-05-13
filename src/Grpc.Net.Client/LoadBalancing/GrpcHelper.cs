using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcHelper : IGrpcHelper
    {
        private readonly GrpcChannel _channel;
        private readonly ILogger _logger;

        public GrpcHelper(GrpcChannel channel)
        {
            _channel = channel;
            _logger = channel.LoggerFactory.CreateLogger<GrpcHelper>();
        }

        public GrpcSubChannel CreateSubChannel(CreateSubchannelArgs args)
        {
            return new GrpcSubChannel(args.Address, args.Attributes);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            if (newPicker == null)
            {
                throw new ArgumentNullException(nameof(newPicker));
            }
            Task.Factory.StartNew(() => 
            { 
                _channel.UpdateSubchannelPicker(newPicker);
                // It's not appropriate to report SHUTDOWN state from lb.
                // Ignore the case of newState == SHUTDOWN for now.
                if (newState != GrpcConnectivityState.SHUTDOWN)
                {
                    _logger.LogDebug($"Entering {newState} state with picker: {newPicker.GetType().Name}");
                    _channel.ChannelStateManager.SetState(newState);
                }
            });
        }

        public GrpcSynchronizationContext GetSynchronizationContext()
        {
            throw new NotImplementedException();
        }

        public void RefreshNameResolution()
        {
            throw new NotImplementedException();
        }
    }
}
