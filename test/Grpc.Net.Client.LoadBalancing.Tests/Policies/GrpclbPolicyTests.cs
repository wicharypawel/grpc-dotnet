using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Lb.V1;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal.Abstraction;
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
        public void ForEmptyServiceName_UseGrpclbPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new GrpclbPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(2, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);
            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses, "", false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
            exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses, string.Empty, false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
        }

        [Fact]
        public void ForEmptyResolutionPassed_UseGrpclbPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new GrpclbPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolvedAddresses must contain at least one blancer address.", exception.Message);
        }

        [Fact]
        public void ForServersResolutionOnly_UseGrpclbPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new GrpclbPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false); // non-balancers are ignored
            });
            Assert.Equal("resolvedAddresses must contain at least one blancer address.", exception.Message);
        }

        [Fact]
        public void ForResolutionResultWithBalancers_UseGrpclbPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var timerFake = new TimerFake();
            var balancerClientMock = new Mock<ILoadBalancerClient>(MockBehavior.Strict);
            var balancerStreamMock = new Mock<IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>>(MockBehavior.Strict);
            var requestStreamMock = new Mock<IClientStreamWriter<LoadBalanceRequest>>(MockBehavior.Strict);
            
            balancerClientMock.Setup(x => x.Dispose()).Verifiable();
            balancerClientMock.Setup(x => x.BalanceLoad(null, null, It.IsAny<CancellationToken>()))
                .Returns(balancerStreamMock.Object);

            balancerStreamMock.Setup(x => x.Dispose());
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

            var helper = new HelperFake();
            using var policy = new GrpclbPolicy(helper);
            policy.OverrideLoadBalancerClient = balancerClientMock.Object;
            policy.OverrideTimer = timerFake;

            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(1, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false);
            var subChannels = policy.SubChannels;

            // Assert
            Assert.Equal(3, subChannels.Count); // subChannels are created per results from GetSampleLoadBalanceResponse
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
            requestStreamMock.Verify(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x => x.InitialRequest != null)), Times.Once);
            Assert.Equal(TimeSpan.FromSeconds(10), timerFake.ClientStatsReportInterval ?? TimeSpan.Zero);
            policy.Dispose();
            balancerClientMock.Verify(x => x.Dispose(), Times.Once());
        }

        [Fact]
        public void ForLoadReporting_UseGrpclbPolicy_VerifySendingClientStats()
        {
            // Arrange
            var timerFake = new TimerFake();
            var balancerClientMock = new Mock<ILoadBalancerClient>(MockBehavior.Strict);
            var balancerStreamMock = new Mock<IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>>(MockBehavior.Strict);
            var requestStreamMock = new Mock<IClientStreamWriter<LoadBalanceRequest>>(MockBehavior.Strict);

            balancerClientMock.Setup(x => x.Dispose());
            balancerClientMock.Setup(x => x.BalanceLoad(null, null, It.IsAny<CancellationToken>()))
                .Returns(balancerStreamMock.Object);

            balancerStreamMock.Setup(x => x.Dispose());
            balancerStreamMock.Setup(x => x.RequestStream).Returns(requestStreamMock.Object);
            balancerStreamMock.Setup(x => x.ResponseStream).Returns(new LoadBalanceResponseFake()
                .AppendToEnd(new LoadBalanceResponse()
                {
                    InitialResponse = GetSampleInitialLoadBalanceResponse()
                })
                .AppendToEnd(new LoadBalanceResponse()
                {
                    ServerList = ServerListFactory.GetSampleServerList()
                }, 10));

            requestStreamMock.Setup(x => x.CompleteAsync()).Returns(Task.CompletedTask);
            requestStreamMock.Setup(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x => 
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.InitialRequest)))
                .Returns(Task.CompletedTask).Verifiable();
            requestStreamMock.Setup(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.ClientStats)))
                .Returns(Task.CompletedTask).Verifiable();

            var helper = new HelperFake();
            using var policy = new GrpclbPolicy(helper);
            policy.OverrideLoadBalancerClient = balancerClientMock.Object;
            policy.OverrideTimer = timerFake;

            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(1, 2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false);
            timerFake.ManualCallbackTrigger();
            timerFake.ManualCallbackTrigger();
            timerFake.ManualCallbackTrigger();
            timerFake.ManualCallbackTrigger();
            var subChannels = policy.SubChannels;
            var fallbackSubChannels = policy.FallbackSubChannels;

            // Assert
            Assert.Equal(3, subChannels.Count);
            // number of fallback subchannels depends on serversCount from name resolution
            // number of fallback subchannels should be zero for no-fallback response 
            Assert.Equal(0, fallbackSubChannels.Count); 
            requestStreamMock.Verify(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.InitialRequest)), Times.Once);
            requestStreamMock.Verify(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.ClientStats)), Times.Exactly(4));
        }

        [Fact]
        public void ForLoadReportingFallback_UseGrpclbPolicy_VerifyFallbackSubchannels()
        {
            // Arrange
            var timerFake = new TimerFake();
            var balancerClientMock = new Mock<ILoadBalancerClient>(MockBehavior.Strict);
            var balancerStreamMock = new Mock<IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>>(MockBehavior.Strict);
            var requestStreamMock = new Mock<IClientStreamWriter<LoadBalanceRequest>>(MockBehavior.Strict);

            balancerClientMock.Setup(x => x.Dispose());
            balancerClientMock.Setup(x => x.BalanceLoad(null, null, It.IsAny<CancellationToken>()))
                .Returns(balancerStreamMock.Object);

            balancerStreamMock.Setup(x => x.Dispose());
            balancerStreamMock.Setup(x => x.RequestStream).Returns(requestStreamMock.Object);
            balancerStreamMock.Setup(x => x.ResponseStream).Returns(new LoadBalanceResponseFake()
                .AppendToEnd(new LoadBalanceResponse()
                {
                    InitialResponse = GetSampleInitialLoadBalanceResponse()
                })
                .AppendToEnd(new LoadBalanceResponse()
                {
                    ServerList = ServerListFactory.GetSampleServerList()
                })
                .AppendToEnd(new LoadBalanceResponse()
                {
                    FallbackResponse = new FallbackResponse()
                }, 10));

            requestStreamMock.Setup(x => x.CompleteAsync()).Returns(Task.CompletedTask);
            requestStreamMock.Setup(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.InitialRequest)))
                .Returns(Task.CompletedTask).Verifiable();
            requestStreamMock.Setup(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.ClientStats)))
                .Returns(Task.CompletedTask).Verifiable();

            var helper = new HelperFake();
            using var policy = new GrpclbPolicy(helper);
            policy.OverrideLoadBalancerClient = balancerClientMock.Object;
            policy.OverrideTimer = timerFake;

            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(1, 2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false);
            timerFake.ManualCallbackTrigger();
            timerFake.ManualCallbackTrigger();
            var fallbackSubChannels = policy.FallbackSubChannels;

            // Assert
            // number of fallback subchannels depends on serversCount from name resolution
            // number of fallback subchannels should be zero for no-fallback response 
            Assert.Equal(2, fallbackSubChannels.Count);
            requestStreamMock.Verify(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.InitialRequest)), Times.Once);
            requestStreamMock.Verify(x => x.WriteAsync(It.Is<LoadBalanceRequest>(x =>
                x.LoadBalanceRequestTypeCase == LoadBalanceRequest.LoadBalanceRequestTypeOneofCase.ClientStats)), Times.Exactly(2));
        }

        [Fact]
        public void ForGrpcSubChannels_UseGrpclbPolicySelectChannels_SelectChannelsInRoundRobin()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            using var picker = new GrpclbPolicy.Picker(subChannels);

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);
                Assert.NotNull(pickResult);
                Assert.NotNull(pickResult!.SubChannel);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Host, pickResult!.SubChannel!.Address.Host);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Port, pickResult.SubChannel.Address.Port);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Scheme, pickResult.SubChannel.Address.Scheme);
                Assert.Equal(Status.DefaultSuccess, pickResult.Status);
            }
        }

        private static InitialLoadBalanceResponse GetSampleInitialLoadBalanceResponse()
        {
            var initialResponse = new InitialLoadBalanceResponse();
            initialResponse.ClientStatsReportInterval = Duration.FromTimeSpan(TimeSpan.FromSeconds(10));
            return initialResponse;
        }
    }
}
