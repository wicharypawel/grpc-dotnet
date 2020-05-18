using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class EdsPolicyTests
    {
        [Fact]
        public async Task ForEmptyServiceName_UseEdsPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new EdsPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "", false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, string.Empty, false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
        }

        [Theory]
        [InlineData(2, 0)]
        [InlineData(0, 2)]
        public async Task ForNonEmptyResolutionPassed_UseEdsPolicy_ThrowArgumentException(int balancersCount, int serversCount)
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new EdsPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(balancersCount, serversCount);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult is expected to be empty.", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResult_UseEdsPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var edsClusterName = "eds-cluster-test-name";
            var serviceName = "sample-service.contoso.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose());
            xdsClientMock.Setup(x => x.GetEdsAsync(edsClusterName)).Returns(Task.FromResult(GetSampleEndpointUpdate()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;
            var xdsClientPool = new XdsClientObjectPool(xdsClientFactory, NullLoggerFactory.Instance);

            var helper = new HelperFake();
            using var policy = new EdsPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var attributes = new GrpcAttributes(new Dictionary<string, object> 
            { 
                { XdsAttributesConstants.XdsClientPoolInstance, xdsClientPool }, 
                { XdsAttributesConstants.EdsClusterName, edsClusterName } 
            });
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, attributes);

            // Act
            await policy.HandleResolvedAddressesAsync(resolvedAddresses, serviceName, false);
            IGrpcSubChannelPicker picker = helper?.SubChannelPicker ?? throw new ArgumentNullException();
            var subChannels = ((WeightedRandomPicker)picker)._weightedPickers
                .Select(x => (RoundRobinPicker)x.ChildPicker)
                .SelectMany(x => x.SubChannels).ToList();

            // Assert
            Assert.Equal(3, subChannels.Count);
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
            xdsClientMock.Verify(x => x.GetEdsAsync(edsClusterName), Times.Once);
            policy.Dispose();
            xdsClientMock.Verify(x => x.Dispose(), Times.Once);
        }

        private static EndpointUpdate GetSampleEndpointUpdate()
        {
            var endpoints = new List<EnvoyProtoData.LbEndpoint>()
            {
                new EnvoyProtoData.LbEndpoint(new List<GrpcHostAddress>(){ new GrpcHostAddress("10.1.5.5", 80) }, 1, true),
                new EnvoyProtoData.LbEndpoint(new List<GrpcHostAddress>(){ new GrpcHostAddress("10.1.5.6", 80) }, 1, true),
                new EnvoyProtoData.LbEndpoint(new List<GrpcHostAddress>(){ new GrpcHostAddress("10.1.5.7", 80) }, 1, true)
            };
            var locality = new EnvoyProtoData.Locality("test-cluster-region", "a", "");
            var localityEndpoints = new EnvoyProtoData.LocalityLbEndpoints(endpoints, 3, 1);
            return new EndpointUpdate("cluster-name", new Dictionary<EnvoyProtoData.Locality, EnvoyProtoData.LocalityLbEndpoints>() { { locality, localityEndpoints } }, new List<EnvoyProtoData.DropOverload>());
        }

        [Fact]
        public void ForGrpcSubChannels_UseEdsPolicySelectChannels_SelectChannelsInRoundRobin()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            var picker = new WeightedRandomPicker(new List<WeightedRandomPicker.WeightedChildPicker>()
            {
                new WeightedRandomPicker.WeightedChildPicker(1, new RoundRobinPicker(subChannels))
            });

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
    }
}
