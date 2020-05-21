using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class RoundRobinPolicy : IGrpcLoadBalancingPolicy
    {
        private static readonly Status StatusEmptyOk = new Status(StatusCode.OK, "no subchannels ready");
        private static readonly string StateInfoKey = "state-info";
        private readonly IGrpcHelper _helper;
        private ILogger _logger = NullLogger.Instance;
        private GrpcConnectivityState? _currentStateCache;
        private RoundRobinPicker _currentPickerCache = new EmptyPicker(StatusEmptyOk);

        public RoundRobinPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<PickFirstPolicy>();
        }

        internal Dictionary<Uri, IGrpcSubChannel> SubChannels { get; set; } = new Dictionary<Uri, IGrpcSubChannel>();

        public void HandleResolvedAddresses(GrpcResolvedAddresses resolvedAddresses, string serviceName, bool isSecureConnection)
        {
            if (resolvedAddresses == null)
            {
                throw new ArgumentNullException(nameof(resolvedAddresses));
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException($"{nameof(serviceName)} not defined.");
            }
            var hostsAddresses = resolvedAddresses.HostsAddresses;
            hostsAddresses = hostsAddresses.Where(x => !x.IsLoadBalancer).ToList();
            if (hostsAddresses.Count == 0)
            {
                throw new ArgumentException($"{nameof(resolvedAddresses)} must contain at least one non-blancer address.");
            }
            _logger.LogDebug($"Start pick_first policy");
            var resolvedUris = hostsAddresses.Select(hostsAddress =>
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = hostsAddress.Host;
                uriBuilder.Port = hostsAddress.Port ?? (isSecureConnection ? 443 : 80);
                uriBuilder.Scheme = isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                _logger.LogDebug($"Found a server {uri}");
                return uri;
            }).ToArray();
            var removedSubChannels = new List<IGrpcSubChannel>();
            foreach (var uri in SubChannels.Keys)
            {
                if (!resolvedUris.Contains(uri)) // uri is no longer available mark subChannel for deletion
                {
                    removedSubChannels.Add(SubChannels[uri]);
                    SubChannels.Remove(uri);
                }
            }
            foreach (var uri in resolvedUris)
            {
                if (SubChannels.GetValueOrDefault(uri) != null)
                {
                    continue; // subChannel for this address already exist
                }
                var initialStateInfo = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.IDLE);
                var attributes = new GrpcAttributes(new Dictionary<string, object>() { { StateInfoKey, new Ref<GrpcConnectivityStateInfo>(initialStateInfo) } });
                var subChannel = _helper.CreateSubChannel(new CreateSubchannelArgs(uri, attributes));
                subChannel.Start(new BaseSubchannelStateObserver((stateInfo) => { ProcessSubchannelState(subChannel, stateInfo); }));
                SubChannels[uri] = subChannel;
                subChannel.RequestConnection();
            }
            UpdateBalancingState();
            foreach (var subChannel in removedSubChannels)
            {
                ShutdownSubchannel(subChannel);
            }
            _logger.LogDebug($"SubChannels list created");
        }

        public void HandleNameResolutionError(Status error)
        {
            UpdateBalancingState(GrpcConnectivityState.TRANSIENT_FAILURE, _currentPickerCache is ReadyPicker ? _currentPickerCache : new EmptyPicker(error));
        }

        private void ProcessSubchannelState(IGrpcSubChannel subChannel, GrpcConnectivityStateInfo stateInfo)
        {
            if (SubChannels.GetValueOrDefault(subChannel.Address) != subChannel)
            {
                return;
            }
            if (stateInfo.State == GrpcConnectivityState.IDLE)
            {
                subChannel.RequestConnection();
            }
            Ref<GrpcConnectivityStateInfo> subchannelStateRef = GetSubchannelStateInfoRef(subChannel);
            if (subchannelStateRef.Value.State == GrpcConnectivityState.TRANSIENT_FAILURE)
            {
                if (stateInfo.State == GrpcConnectivityState.CONNECTING || stateInfo.State == GrpcConnectivityState.IDLE)
                {
                    return;
                }
            }
            subchannelStateRef.Value = stateInfo;
            UpdateBalancingState();
        }

        public bool CanHandleEmptyAddressListFromNameResolution()
        {
            return false;
        }

        public void RequestConnection()
        {
        }

        public void Dispose()
        {
            foreach (var subChannel in SubChannels.Values.ToArray())
            {
                ShutdownSubchannel(subChannel);
            }
        }

        private static void ShutdownSubchannel(IGrpcSubChannel subChannel)
        {
            subChannel.Shutdown();
            GetSubchannelStateInfoRef(subChannel).Value = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.SHUTDOWN);
        }

        private void UpdateBalancingState()
        {
            var activeList = FilterNonFailingSubchannels(SubChannels.Values.ToArray());
            if (activeList.Count == 0)
            {
                // No READY subchannels, determine aggregate state and error status
                var isConnecting = false;
                var aggStatus = StatusEmptyOk;
                foreach (var subchannel in SubChannels.Values.ToArray())
                {
                    var stateInfo = GetSubchannelStateInfoRef(subchannel).Value;
                    // This subchannel IDLE is not because of channel IDLE_TIMEOUT,
                    // in which case LB is already shutdown.
                    // RRLB will request connection immediately on subchannel IDLE.
                    if (stateInfo.State == GrpcConnectivityState.CONNECTING || stateInfo.State == GrpcConnectivityState.IDLE)
                    {
                        isConnecting = true;
                    }
                    if (aggStatus.StatusCode == StatusEmptyOk.StatusCode || aggStatus.StatusCode != StatusCode.OK)
                    {
                        aggStatus = stateInfo.Status;
                    }
                }
                UpdateBalancingState(isConnecting ? GrpcConnectivityState.CONNECTING : GrpcConnectivityState.TRANSIENT_FAILURE, new EmptyPicker(aggStatus));
            }
            else
            {
                UpdateBalancingState(GrpcConnectivityState.READY, new ReadyPicker(activeList));
            }
        }

        private void UpdateBalancingState(GrpcConnectivityState newState, RoundRobinPicker newPicker)
        {
            if (newState != _currentStateCache || !newPicker.IsEquivalentTo(_currentPickerCache))
            {
                _helper.UpdateBalancingState(newState, newPicker);
                _currentStateCache = newState;
                _currentPickerCache = newPicker;
            }
        }

        private static IReadOnlyList<IGrpcSubChannel> FilterNonFailingSubchannels(IReadOnlyList<IGrpcSubChannel> subChannels)
        {
            var readySubChannels = new List<IGrpcSubChannel>();
            foreach (var subChannel in subChannels)
            {
                if (IsReady(subChannel))
                {
                    readySubChannels.Add(subChannel);
                }
            }
            return readySubChannels;
        }

        private static Ref<GrpcConnectivityStateInfo> GetSubchannelStateInfoRef(IGrpcSubChannel subChannel)
        {
            var result = subChannel.Attributes.Get(StateInfoKey) as Ref<GrpcConnectivityStateInfo>;
            return result ?? throw new InvalidOperationException("SubChannel state not found.");
        }

        private static bool IsReady(IGrpcSubChannel subChannel)
        {
            return GetSubchannelStateInfoRef(subChannel).Value.State == GrpcConnectivityState.READY;
        }

        internal abstract class RoundRobinPicker : IGrpcSubChannelPicker
        {
            public abstract GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments);
            public abstract bool IsEquivalentTo(RoundRobinPicker picker);
        }

        internal sealed class ReadyPicker : RoundRobinPicker
        {
            private readonly IReadOnlyList<IGrpcSubChannel> _subChannels;
            private int index;

            public ReadyPicker(IReadOnlyList<IGrpcSubChannel> subChannels)
            {
                if (subChannels == null) throw new ArgumentNullException(nameof(subChannels));
                if (subChannels.Count == 0) throw new ArgumentException($"Empty {nameof(subChannels)}.");
                _subChannels = subChannels;
                index = -1;
            }

            public override GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments)
            {
                var nextSubChannel = _subChannels[Interlocked.Increment(ref index) % _subChannels.Count];
                return GrpcPickResult.WithSubChannel(nextSubChannel);
            }

            public override bool IsEquivalentTo(RoundRobinPicker picker)
            {
                if (!(picker is ReadyPicker other))
                {
                    return false;
                }
                return this == other || (_subChannels.Count == other._subChannels.Count); //TODO HERE
            }
        }

        internal sealed class EmptyPicker : RoundRobinPicker
        {
            private readonly Status _status;

            public EmptyPicker(Status status)
            {
                _status = status;
            }

            public override GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments)
            {
                return _status.StatusCode == StatusCode.OK ? GrpcPickResult.WithNoResult() : GrpcPickResult.WithError(_status);
            }

            public override bool IsEquivalentTo(RoundRobinPicker picker)
            {
                if (picker is EmptyPicker emptyPicker)
                {
                    return _status.StatusCode == emptyPicker._status.StatusCode;
                }
                return false;
            }
        }

        private sealed class Ref<T>
        {
            public T Value { get; set; }

            public Ref(T value)
            {
                Value = value;
            }
        }
    }
}
