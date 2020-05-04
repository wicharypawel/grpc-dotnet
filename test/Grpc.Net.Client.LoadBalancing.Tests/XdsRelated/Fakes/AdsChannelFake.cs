using Envoy.Api.V2;
using Grpc.Core;
using Moq;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated.Fakes
{
    internal sealed class AdsChannelFake : ChannelBase
    {
        private readonly Mock<ChannelBase> _adsChannelMock;
        private readonly Mock<CallInvoker> _callInvokerMock;

        public AdsChannelFake(string target, AsyncDuplexStreamingCall<DiscoveryRequest, DiscoveryResponse> adsStream) : base(target)
        {
            _callInvokerMock = new Mock<CallInvoker>(MockBehavior.Strict);
            _callInvokerMock.Setup(x => x.AsyncDuplexStreamingCall(It.IsAny<Method<DiscoveryRequest, DiscoveryResponse>>(), It.IsAny<string>(), It.IsAny<CallOptions>())).Returns(adsStream);
            _adsChannelMock = new Mock<ChannelBase>(MockBehavior.Loose, target);
            _adsChannelMock.Setup(x => x.CreateCallInvoker()).Returns(_callInvokerMock.Object);
        }

        private ChannelBase MockDelegate => _adsChannelMock.Object;

        public override CallInvoker CreateCallInvoker()
        {
            return MockDelegate.CreateCallInvoker();
        }
    }
}
