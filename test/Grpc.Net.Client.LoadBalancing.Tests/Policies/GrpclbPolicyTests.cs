using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Lb.V1;
using Grpc.Net.Client.LoadBalancing.Policies;
using Grpc.Net.Client.LoadBalancing.Policies.Abstraction;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class GrpclbPolicyTests
    {
        [Fact]
        public async Task ForEmptyServiceName_UseGrpclbPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new GrpclbPolicy();
            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(2, 0);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "", false);
            });
            Assert.Equal("serviceName not defined", exception.Message);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, string.Empty, false);
            });
            Assert.Equal("serviceName not defined", exception.Message);
        }

        [Fact]
        public async Task ForEmptyResolutionPassed_UseGrpclbPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new GrpclbPolicy();

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(new List<GrpcNameResolutionResult>(), "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult must contain at least one blancer address", exception.Message);
        }

        [Fact]
        public async Task ForServersResolutionOnly_UseGrpclbPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new GrpclbPolicy();
            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(0, 2);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false); // non-balancers are ignored
            });
            Assert.Equal("resolutionResult must contain at least one blancer address", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResultWithBalancers_UseGrpclbPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var timerFake = new TimerFake();
            var balancerClientMock = new Mock<ILoadBalancerClient>(MockBehavior.Strict);
            var balancerStreamMock = new Mock<IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>>(MockBehavior.Strict);
            var requestStreamMock = new Mock<IClientStreamWriter<LoadBalanceRequest>>(MockBehavior.Strict);
            
            balancerClientMock.Setup(x => x.Dispose());
            balancerClientMock.Setup(x => x.BalanceLoad(null, null, It.IsAny<CancellationToken>()))
                .Returns(balancerStreamMock.Object);

            balancerStreamMock.Setup(x => x.RequestStream).Returns(requestStreamMock.Object);
            balancerStreamMock.Setup(x => x.ResponseStream).Returns(new LoadBalanceResponseFake(new List<LoadBalanceResponse>
            {
                new LoadBalanceResponse()
                {
                    InitialResponse = GetSampleInitialLoadBalanceResponse()
                },
                new LoadBalanceResponse()
                {
                    ServerList = ServerListFactory.GetSampleServerList()
                }
            }));

            requestStreamMock.Setup(x => x.CompleteAsync()).Returns(Task.CompletedTask);
            requestStreamMock.Setup(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x => x.InitialRequest != null)))
                .Returns(Task.CompletedTask).Verifiable();

            using var policy = new GrpclbPolicy();
            policy.OverrideLoadBalancerClient = balancerClientMock.Object;
            policy.OverrideTimer = timerFake;

            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(1, 0);

            // Act
            await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false);
            var subChannels = policy.SubChannels;

            // Assert
            Assert.Equal(3, subChannels.Count); // subChannels are created per results from GetSampleLoadBalanceResponse
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
            requestStreamMock.Verify(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x => x.InitialRequest != null)), Times.Once);
            Assert.Equal(TimeSpan.FromSeconds(10), timerFake.ClientStatsReportInterval ?? TimeSpan.Zero);
        }

        [Fact]
        public async Task ForLoadBalancerClient_UseGrpclbPolicy_EnsureDisposedResources()
        {
            // Arrange
            var timerFake = new TimerFake();
            var balancerClientMock = new Mock<ILoadBalancerClient>(MockBehavior.Strict);
            var balancerStreamMock = new Mock<IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>>(MockBehavior.Strict);
            var requestStreamMock = new Mock<IClientStreamWriter<LoadBalanceRequest>>(MockBehavior.Loose);

            balancerClientMock.Setup(x => x.Dispose()).Verifiable();
            balancerClientMock.Setup(x => x.BalanceLoad(null, null, It.IsAny<CancellationToken>()))
                .Returns(balancerStreamMock.Object);

            balancerStreamMock.Setup(x => x.RequestStream).Returns(requestStreamMock.Object);
            balancerStreamMock.Setup(x => x.ResponseStream).Returns(new LoadBalanceResponseFake(new List<LoadBalanceResponse>
            {
                new LoadBalanceResponse()
                {
                    InitialResponse = GetSampleInitialLoadBalanceResponse()
                },
                new LoadBalanceResponse()
                {
                    ServerList = ServerListFactory.GetSampleServerList()
                }
            }));

            using var policy = new GrpclbPolicy();
            policy.OverrideLoadBalancerClient = balancerClientMock.Object;
            policy.OverrideTimer = timerFake;

            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(1, 0);

            // Act
            await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false);

            // Assert
            policy.Dispose();
            balancerClientMock.Verify(x => x.Dispose(), Times.Once());
        }

        [Fact]
        public void ForGrpcSubChannels_UseGrpclbPolicySelectChannels_SelectChannelsInRoundRobin()
        {
            // Arrange
            using var policy = new GrpclbPolicy();
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            policy.SubChannels = subChannels;

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var subChannel = policy.GetNextSubChannel();
                Assert.Equal(subChannels[i % subChannels.Count].Address.Host, subChannel.Address.Host);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Port, subChannel.Address.Port);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Scheme, subChannel.Address.Scheme);
            }
        }

        private static InitialLoadBalanceResponse GetSampleInitialLoadBalanceResponse()
        {
            var initialResponse = new InitialLoadBalanceResponse();
            initialResponse.ClientStatsReportInterval = Duration.FromTimeSpan(TimeSpan.FromSeconds(10));
            initialResponse.LoadBalancerDelegate = string.Empty;
            return initialResponse;
        }
    }
}
