using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcSubChannel : IGrpcSubChannel
    {
        private readonly IGrpcBackoffPolicyProvider _backoffPolicyProvider;
        private readonly GrpcSynchronizationContext _synchronizationContext;
        private readonly ILogger _logger;
        private GrpcConnectivityStateInfo _stateInfo = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.IDLE);
        private IGrpcSubchannelStateObserver? _observer = null;
        private bool _started = false;
        private bool _shutdown = false;
        private IGrpcBackoffPolicy? _backoffPolicy = null;

        public Uri Address { get; private set; }

        public GrpcAttributes Attributes { get; private set; }

        public GrpcSubChannel(GrpcChannel channel, CreateSubchannelArgs arguments, IGrpcHelper grpcHelper)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            if (grpcHelper == null) throw new ArgumentNullException(nameof(grpcHelper));
            Address = arguments.Address;
            Attributes = arguments.Attributes;
            _backoffPolicyProvider = channel.BackoffPolicyProvider ?? throw new ArgumentNullException(nameof(channel.BackoffPolicyProvider));
            _synchronizationContext = channel.SyncContext ?? throw new ArgumentNullException(nameof(channel.SyncContext));
            _logger = channel.LoggerFactory.CreateLogger(nameof(GrpcSubChannel) + arguments.Address.ToString());
        }

        public void Start(IGrpcSubchannelStateObserver observer)
        {
            if (_started) throw new InvalidOperationException("Already started.");
            if (_shutdown) throw new InvalidOperationException("Already shutdown.");
            _started = true;
            _logger.LogDebug("Start GrpcSubChannel.");
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            _logger.LogDebug("Shutdown GrpcSubChannel.");
            SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.SHUTDOWN));
        }

        public void RequestConnection()
        {
            if (!_started) throw new InvalidOperationException("Not started.");
            _logger.LogDebug("RequestConnection GrpcSubChannel.");
            // This part is a workaround missing monitoring connectivity state in HttpClient  
            if (_stateInfo.State == GrpcConnectivityState.IDLE || _stateInfo.State == GrpcConnectivityState.TRANSIENT_FAILURE)
            {
                SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
                SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            }
        }

        public void UpdateAddress(Uri address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        private void SetState(GrpcConnectivityStateInfo newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }
            if (_stateInfo.State != newState.State)
            {
                _logger.LogDebug($"Change state from {_stateInfo.State} to {newState.State}");
                if (_stateInfo.State == GrpcConnectivityState.SHUTDOWN)
                {
                    throw new InvalidOperationException("Cannot transition out of SHUTDOWN state.");
                }
                _stateInfo = newState;
                _observer?.OnNext(newState);
            }
        }

        internal void TriggerSubChannelFailure(Status status)
        {
            if (_backoffPolicy == null)
            {
                _backoffPolicy = _backoffPolicyProvider.CreateBackoffPolicy();
            }
            SetState(GrpcConnectivityStateInfo.ForTransientFailure(status));
            _synchronizationContext.Schedule(() => RequestConnection(), _backoffPolicy.NextBackoff());
        }

        internal void TriggerSubChannelSuccess()
        {
            _backoffPolicy = null;
        }
    }
}
