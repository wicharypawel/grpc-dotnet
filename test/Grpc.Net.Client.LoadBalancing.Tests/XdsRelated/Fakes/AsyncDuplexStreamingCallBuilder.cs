using Envoy.Api.V2;
using Grpc.Core;
using Moq;
using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated.Fakes
{
    internal sealed class AsyncDuplexStreamingCallBuilder
    {
        private IClientStreamWriter<DiscoveryRequest>? _requestStream;
        private IAsyncStreamReader<DiscoveryResponse>? _responseStream;
        private Task<Metadata>? _responseHeadersAsync;
        private Func<Status>? _getStatusFunc;
        private Func<Metadata>? _getTrailersFunc;
        private Action? _disposeAction;

        private AsyncDuplexStreamingCallBuilder()
        {
        }

        public static AsyncDuplexStreamingCallBuilder InitializeBuilderWithFakeData()
        {
            return new AsyncDuplexStreamingCallBuilder()
            {
                _requestStream = new Mock<IClientStreamWriter<DiscoveryRequest>>(MockBehavior.Loose).Object,
                _responseStream = new Mock<IAsyncStreamReader<DiscoveryResponse>>(MockBehavior.Loose).Object,
                _responseHeadersAsync = Task.FromResult(new Metadata()),
                _getStatusFunc = new Func<Status>(() => Status.DefaultSuccess),
                _getTrailersFunc = new Func<Metadata>(() => new Metadata()),
                _disposeAction = new Action(() => { }),
            };
        }

        public AsyncDuplexStreamingCallBuilder OverrideRequestStream(IClientStreamWriter<DiscoveryRequest> requestStream)
        {
            _requestStream = requestStream ?? throw new ArgumentNullException(nameof(requestStream));
            return this;
        }

        public AsyncDuplexStreamingCallBuilder OverrideResponseStream(IAsyncStreamReader<DiscoveryResponse> responseStream)
        {
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            return this;
        }

        public AsyncDuplexStreamingCallBuilder OverrideResponseHeaders(Task<Metadata> responseHeadersAsync)
        {
            _responseHeadersAsync = responseHeadersAsync;
            return this;
        }

        public AsyncDuplexStreamingCallBuilder OverrideGetStatusFunc(Func<Status> getStatusFunc)
        {
            _getStatusFunc = getStatusFunc;
            return this;
        }

        public AsyncDuplexStreamingCallBuilder OverrideGetTrailersFunc(Func<Metadata> getTrailersFunc)
        {
            _getTrailersFunc = getTrailersFunc;
            return this;
        }

        public AsyncDuplexStreamingCallBuilder OverrideDisposeAction(Action disposeAction)
        {
            _disposeAction = disposeAction;
            return this;
        }

        public AsyncDuplexStreamingCall<DiscoveryRequest, DiscoveryResponse> Build()
        {
            return new AsyncDuplexStreamingCall<DiscoveryRequest, DiscoveryResponse>(_requestStream, _responseStream, _responseHeadersAsync,
                _getStatusFunc, _getTrailersFunc, _disposeAction);
        }
    }
}
