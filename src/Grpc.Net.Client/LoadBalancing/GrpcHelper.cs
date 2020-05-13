using Microsoft.Extensions.Logging;
using System;

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

        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs args)
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            return new GrpcSubChannel(args.Address, args.Attributes);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            if (newPicker == null)
            {
                throw new ArgumentNullException(nameof(newPicker));
            }
            _channel.SyncContext.Execute(() => 
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
            return _channel.SyncContext;
        }

        public void RefreshNameResolution()
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            _channel.SyncContext.Execute(() => { _channel.RefreshNameResolution(); });
        }
    }
}
