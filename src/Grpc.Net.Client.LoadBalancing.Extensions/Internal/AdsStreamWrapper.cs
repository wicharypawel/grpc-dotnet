using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class AdsStreamWrapper : IDisposable
    {
        private static readonly string ADS_TYPE_URL_LDS = "type.googleapis.com/envoy.api.v2.Listener";
        private static readonly string ADS_TYPE_URL_RDS = "type.googleapis.com/envoy.api.v2.RouteConfiguration";
        private static readonly string ADS_TYPE_URL_CDS = "type.googleapis.com/envoy.api.v2.Cluster";
        private static readonly string ADS_TYPE_URL_EDS = "type.googleapis.com/envoy.api.v2.ClusterLoadAssignment";

        private readonly XdsClient _parentXdsClient;
        private readonly Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient _adsClient;
        private readonly ILogger _logger;
        private readonly Envoy.Api.V2.Core.Node _node;
        private AsyncDuplexStreamingCall<Envoy.Api.V2.DiscoveryRequest, Envoy.Api.V2.DiscoveryResponse>? _adsStream;
        private CancellationTokenSource? _tokenSource;
        private string? _rdsResourceName;
        private bool closed = false;
        private string ldsVersion = string.Empty;
        private string rdsVersion = string.Empty;
        private string cdsVersion = string.Empty;
        private string edsVersion = string.Empty;
        private string ldsRespNonce = string.Empty;
        private string rdsRespNonce = string.Empty;
        private string cdsRespNonce = string.Empty;
        private string edsRespNonce = string.Empty;

        public AdsStreamWrapper(XdsClient xdsClient, Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient adsClient, 
            ILogger logger, Envoy.Api.V2.Core.Node node)
        {
            _parentXdsClient = xdsClient ?? throw new ArgumentNullException(nameof(xdsClient));
            _adsClient = adsClient ?? throw new ArgumentNullException(nameof(adsClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        internal bool Disposed { get; private set; }

        public void Start()
        {
            if (_adsStream != null || _tokenSource != null)
            {
                throw new InvalidOperationException("Ads stream already started");
            }
            _adsStream = _adsClient.StreamAggregatedResources();
            _tokenSource = new CancellationTokenSource();
            var streamObserver = this;
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (await _adsStream.ResponseStream.MoveNext(_tokenSource.Token).ConfigureAwait(false))
                    {
                        streamObserver.OnNext(_adsStream.ResponseStream.Current);
                    }
                    streamObserver.OnCompleted();
                }
                catch (Exception exception)
                {
                    streamObserver.OnError(exception);
                }
            }, _tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Close()
        {
            if (closed)
            {
                return;
            }
            closed = true;
            _logger.LogDebug($"ADS stream closed requested.");
            try
            {
                _adsStream?.RequestStream.CompleteAsync().Wait();
            }
            finally
            {
                Dispose();
            }
        }

        public async Task SendXdsRequestAsync(string typeUrl, IReadOnlyList<string> resourceNames)
        {
            if (_adsStream == null)
            {
                throw new InvalidOperationException("ADS stream has not been started");
            }
            string versionInfo = string.Empty;
            string nonce = string.Empty;
            if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
            {
                versionInfo = ldsVersion;
                nonce = ldsRespNonce;
                _logger.LogDebug($"Sending LDS request for resources: {string.Join(',', resourceNames)}");
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
            {
                if (resourceNames.Count != 1)
                {
                    throw new InvalidOperationException("RDS request requesting for more than one resource");
                }
                versionInfo = rdsVersion;
                nonce = rdsRespNonce;
                _rdsResourceName = resourceNames[0];
                _logger.LogDebug($"Sending RDS request for resources: {string.Join(',', resourceNames)}");
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
            {
                versionInfo = cdsVersion;
                nonce = cdsRespNonce;
                _logger.LogDebug($"Sending CDS request for resources: {string.Join(',', resourceNames)}");
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
            {
                versionInfo = edsVersion;
                nonce = edsRespNonce;
                _logger.LogDebug($"Sending EDS request for resources: {string.Join(',', resourceNames)}");
            }
            var request = new Envoy.Api.V2.DiscoveryRequest()
            {
                VersionInfo = versionInfo,
                Node = _node,
                TypeUrl = typeUrl,
                ResponseNonce = nonce
            };
            request.ResourceNames.AddRange(resourceNames);
            await _adsStream.RequestStream.WriteAsync(request).ConfigureAwait(false);
            _logger.LogDebug($"Sent DiscoveryRequest");
        }

        public async Task SendAckRequestAsync(string typeUrl, IReadOnlyList<string> resourceNames, string versionInfo)
        {
            if (_adsStream == null)
            {
                throw new InvalidOperationException("ADS stream has not been started");
            }
            string nonce = string.Empty;
            if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
            {
                ldsVersion = versionInfo;
                nonce = ldsRespNonce;
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
            {
                rdsVersion = versionInfo;
                nonce = rdsRespNonce;
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
            {
                cdsVersion = versionInfo;
                nonce = cdsRespNonce;
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
            {
                edsVersion = versionInfo;
                nonce = edsRespNonce;
            }
            var request = new Envoy.Api.V2.DiscoveryRequest()
            {
                VersionInfo = versionInfo,
                Node = _node,
                TypeUrl = typeUrl,
                ResponseNonce = nonce
            };
            request.ResourceNames.AddRange(resourceNames);
            await _adsStream.RequestStream.WriteAsync(request).ConfigureAwait(false);
            _logger.LogDebug($"Sent ACK request");
        }

        public async Task SendNackRequestAsync(string typeUrl, IReadOnlyList<string> resourceNames, string rejectVersion, string message)
        {
            if (_adsStream == null)
            {
                throw new InvalidOperationException("ADS stream has not been started");
            }
            string versionInfo = string.Empty;
            string nonce = string.Empty;
            if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
            {
                versionInfo = ldsVersion;
                nonce = ldsRespNonce;
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
            {
                versionInfo = rdsVersion;
                nonce = rdsRespNonce;
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
            {
                versionInfo = cdsVersion;
                nonce = cdsRespNonce;
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
            {
                versionInfo = edsVersion;
                nonce = edsRespNonce;
            }
            var request = new Envoy.Api.V2.DiscoveryRequest()
            {
                VersionInfo = versionInfo,
                Node = _node,
                TypeUrl = typeUrl,
                ResponseNonce = nonce,
                ErrorDetail = new Google.Rpc.Status()
                {
                    Code = (int)StatusCode.InvalidArgument,
                    Message = message
                }
            };
            request.ResourceNames.AddRange(resourceNames);
            await _adsStream.RequestStream.WriteAsync(request).ConfigureAwait(false);
            _logger.LogDebug($"Sent NACK request");
        }

        private void OnNext(Envoy.Api.V2.DiscoveryResponse response)
        {
            if (closed)
            {
                return;
            }
            string typeUrl = response.TypeUrl;
            // Nonce in each response is echoed back in the following ACK/NACK request. It is
            // used for management server to identify which response the client is ACKing/NACking.
            // To avoid confusion, client-initiated requests will always use the nonce in
            // most recently received responses of each resource type.
            if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
            {
                ldsRespNonce = response.Nonce;
                _parentXdsClient.HandleLdsResponse(response);
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
            {
                rdsRespNonce = response.Nonce;
                _parentXdsClient.HandleRdsResponse(response);
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
            {
                cdsRespNonce = response.Nonce;
                _parentXdsClient.HandleCdsResponse(response);
            }
            else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
            {
                edsRespNonce = response.Nonce;
                _parentXdsClient.HandleEdsResponse(response);
            }
            else
            {
                _logger.LogDebug("Received an unknown type of DiscoveryResponse");
            }
        }

        private void OnError(Exception exception)
        {
            HandleStreamClosed(new Status(StatusCode.Unknown, exception.Message));
        }

        private void OnCompleted()
        {
            HandleStreamClosed(new Status(StatusCode.Unavailable, "Closed by server"));
        }

        private void HandleStreamClosed(Status error)
        {
            if (error.StatusCode == StatusCode.OK)
            {
                throw new InvalidOperationException("Unexpected OK status");
            }
            if (closed)
            {
                return;
            }
            closed = true;
            _logger.LogDebug($"ADS stream closed with status {error.StatusCode}: {error.Detail}.");
            Dispose();
        }
        
        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            Disposed = true;
            try
            {
                _tokenSource?.Cancel();
            }
            finally
            {
                _tokenSource?.Dispose();
                _adsStream?.Dispose();
            }
            // _parentXdsClient should not be disposed here
        }
    }
}
