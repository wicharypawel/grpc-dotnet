using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcSubChannel : IGrpcSubChannel
    {
        private readonly GrpcChannel _channel;
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
            _channel = channel;
            _synchronizationContext = channel.SyncContext ?? throw new ArgumentNullException(nameof(channel.SyncContext));
            _logger = channel.LoggerFactory.CreateLogger(nameof(GrpcSubChannel) + arguments.Address.ToString());
        }

        public void Start(IGrpcSubchannelStateObserver observer)
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (_started) throw new InvalidOperationException("Already started.");
            if (_shutdown) throw new InvalidOperationException("Already shutdown.");
            _started = true;
            _logger.LogDebug("Start GrpcSubChannel.");
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public void Shutdown()
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (_shutdown) return;
            _shutdown = true;
            _logger.LogDebug("Shutdown GrpcSubChannel.");
            SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.SHUTDOWN));
        }

        public void RequestConnection()
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (!_started) throw new InvalidOperationException("Not started.");
            _logger.LogDebug("RequestConnection GrpcSubChannel.");
            #region HTTP_CLIENT_MISSING_MONITORING_WORKAROUND
            _synchronizationContext.Execute(() => 
            {
                if (_stateInfo.State == GrpcConnectivityState.IDLE || _stateInfo.State == GrpcConnectivityState.TRANSIENT_FAILURE)
                {
                    SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
                }
            });
            _synchronizationContext.Execute(() =>
            {
                if (_stateInfo.State == GrpcConnectivityState.CONNECTING)
                {
                    SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
                }
            });
            #endregion
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
                _channel.HandleInternalSubchannelState(newState);
                _observer?.OnNext(newState);
            }
        }

        #region HTTP_CLIENT_MISSING_MONITORING_WORKAROUND

        internal void TriggerSubChannelFailure(Status status)
        {
            Task.Factory.StartNew(() => _synchronizationContext.Execute(() => TriggerSubChannelFailureCore(status)));
        }

        internal void TriggerSubChannelSuccess()
        {
            Task.Factory.StartNew(() => _synchronizationContext.Execute(() => TriggerSubChannelSuccessCore()));
        }

        private void TriggerSubChannelFailureCore(Status status)
        {
            if (_backoffPolicy == null)
            {
                _backoffPolicy = _channel.BackoffPolicyProvider.CreateBackoffPolicy();
            }
            SetState(GrpcConnectivityStateInfo.ForTransientFailure(status));
            var delay = _backoffPolicy.NextBackoff();
            _logger.LogDebug($"Scheduling subChannel reconnect backoff for {delay}.");
            _synchronizationContext.Schedule(() => RequestConnection(), delay); // ScheduledHandle ignored 
        }

        private void TriggerSubChannelSuccessCore()
        {
            _backoffPolicy = null;
        }

        #endregion
    }
}
