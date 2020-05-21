using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class PickFirstPolicy : IGrpcLoadBalancingPolicy
    {
        private static readonly Status StatusEmptyOk = new Status(StatusCode.OK, "no subchannels ready");
        private readonly IGrpcHelper _helper;
        private ILogger _logger = NullLogger.Instance;
        private GrpcConnectivityState? _currentStateCache;
        private PickFirstPicker _currentPickerCache = new EmptyPicker(StatusEmptyOk);

        public PickFirstPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<PickFirstPolicy>();
        }

        private Uri[] _resolvedUris { get; set; } = Array.Empty<Uri>();
        private int _currentUriIndex = 0;
        
        internal IGrpcSubChannel? SubChannel { get; set; }

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
            if (EqualResolution(_resolvedUris, resolvedUris))
            {
                return;
            }
            _resolvedUris = resolvedUris;
            _currentUriIndex = 0;
            var previousSubChannel = SubChannel;
            var subChannel = _helper.CreateSubChannel(new CreateSubchannelArgs(_resolvedUris[_currentUriIndex], GrpcAttributes.Empty));
            subChannel.Start(new BaseSubchannelStateObserver((stateInfo) => { ProcessSubchannelState(subChannel, stateInfo); }));
            SubChannel = subChannel;
            SubChannel.RequestConnection();
            UpdateBalancingState(GrpcConnectivityState.IDLE, new EmptyPicker(Status.DefaultSuccess));
            previousSubChannel?.Shutdown();
            _logger.LogDebug($"SubChannels list created");
        }

        public void HandleNameResolutionError(Status error)
        {
            _helper.UpdateBalancingState(GrpcConnectivityState.TRANSIENT_FAILURE, _currentPickerCache is ReadyPicker ? _currentPickerCache : new EmptyPicker(error));
        }

        private void ProcessSubchannelState(IGrpcSubChannel subChannel, GrpcConnectivityStateInfo stateInfo)
        {
            if (SubChannel != subChannel)
            {
                return;
            }
            var currentState = stateInfo.State;
            PickFirstPicker picker;
            switch (currentState)
            {
                case GrpcConnectivityState.IDLE:
                    subChannel.RequestConnection();
                    picker = new EmptyPicker(StatusEmptyOk);
                    break;
                case GrpcConnectivityState.CONNECTING:
                    picker = new EmptyPicker(StatusEmptyOk);
                    break;
                case GrpcConnectivityState.READY:
                    picker = new ReadyPicker(SubChannel);
                    break;
                case GrpcConnectivityState.TRANSIENT_FAILURE:
                    if (_currentUriIndex + 1 < _resolvedUris.Length)
                    {
                        _currentUriIndex += 1;
                        var previousSubChannel = SubChannel;
                        var newSubChannel = _helper.CreateSubChannel(new CreateSubchannelArgs(_resolvedUris[_currentUriIndex], GrpcAttributes.Empty));
                        newSubChannel.Start(new BaseSubchannelStateObserver((stateInfo) => { ProcessSubchannelState(newSubChannel, stateInfo); }));
                        SubChannel = newSubChannel;
                        SubChannel.RequestConnection();
                        previousSubChannel?.Shutdown();
                    }
                    picker = new EmptyPicker(stateInfo.Status);
                    break;
                case GrpcConnectivityState.SHUTDOWN:
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported state: {currentState}.");
            }
            UpdateBalancingState(currentState, picker);
        }

        public bool CanHandleEmptyAddressListFromNameResolution()
        {
            return false;
        }

        public void RequestConnection()
        {
            SubChannel?.RequestConnection();
        }

        public void Dispose()
        {
            SubChannel?.Shutdown();
        }

        private static bool EqualResolution(Uri[] current, Uri[] other)
        {
            if (ReferenceEquals(current, other))
            {
                return true;
            }
            if (current == null || other == null)
            {
                return false;
            }
            if (current.Length != other.Length)
            {
                return false;
            }
            for (int i = 0; i < current.Length; i++)
            {
                if (!other.Contains(current[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private void UpdateBalancingState(GrpcConnectivityState newState, PickFirstPicker newPicker)
        {
            if (newState != _currentStateCache || !newPicker.IsEquivalentTo(_currentPickerCache))
            {
                _helper.UpdateBalancingState(newState, newPicker);
                _currentStateCache = newState;
                _currentPickerCache = newPicker;
            }
        }

        private abstract class PickFirstPicker : IGrpcSubChannelPicker
        {
            public abstract GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments);
            public abstract bool IsEquivalentTo(PickFirstPicker picker);
            public abstract void Dispose();
        }

        private sealed class ReadyPicker : PickFirstPicker
        {
            private readonly IGrpcSubChannel _subChannel;

            public ReadyPicker(IGrpcSubChannel subChannel)
            {
                _subChannel = subChannel ?? throw new ArgumentNullException(nameof(subChannel));
            }

            public override GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments)
            {
                return GrpcPickResult.WithSubChannel(_subChannel);
            }

            public override bool IsEquivalentTo(PickFirstPicker picker)
            {
                if(!(picker is ReadyPicker other))
                {
                    return false;
                }
                return this == other || _subChannel == other._subChannel;
            }

            public override void Dispose()
            {
            }
        }

        private sealed class EmptyPicker : PickFirstPicker
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

            public override bool IsEquivalentTo(PickFirstPicker picker)
            {
                if (picker is EmptyPicker emptyPicker)
                {
                    return _status.StatusCode == emptyPicker._status.StatusCode;
                }
                return false;
            }

            public override void Dispose()
            {
            }
        }
    }
}
