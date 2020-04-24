using Envoy.Api.V2;
using Envoy.Api.V2.Core;
using Envoy.Api.V2.Endpoint;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
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
            using var policy = new EdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, GrpcAttributes.Empty);

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
        public async Task ForBalancersResolutionPassed_UseEdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new EdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(2, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult is expected to be empty", exception.Message);
        }

        [Fact]
        public async Task ForServersResolutionPassed_UseEdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new EdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", true);
            });
            Assert.Equal("resolutionResult is expected to be empty", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResult_UseEdsPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var serviceName = "sample-service.contoso.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose());
            xdsClientMock.Setup(x => x.GetEdsAsync(It.IsAny<string>())).Returns(Task.FromResult(GetSampleClusterLoadAssignments()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;
            var xdsClientPool = new XdsClientObjectPool(xdsClientFactory, NullLoggerFactory.Instance);

            using var policy = new EdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var attributes = new GrpcAttributes(new Dictionary<string, object> 
            { 
                { XdsAttributesConstants.XdsClientPoolInstance, xdsClientPool }, 
                { XdsAttributesConstants.EdsClusterName, "cluster-name" } 
            });
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, attributes);

            // Act
            await policy.CreateSubChannelsAsync(resolutionResults, serviceName, false);
            var subChannels = ((WeightedRandomPicker)policy._subchannelPicker)._weightedPickers
                .Select(x => (RoundRobinPicker)x.ChildPicker)
                .SelectMany(x => x.SubChannels).ToList();

            // Assert
            Assert.Equal(3, subChannels.Count);
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
            xdsClientMock.Verify(x => x.GetEdsAsync(It.IsAny<string>()), Times.Once);
            policy.Dispose();
            xdsClientMock.Verify(x => x.Dispose(), Times.Once);
        }

        private static List<Cluster> GetSampleClusters(string serviceName)
        {
            var cluster = new Cluster();
            cluster.Name = $"outbound|8000||{serviceName}";
            cluster.Type = Cluster.Types.DiscoveryType.Eds;
            cluster.LbPolicy = Cluster.Types.LbPolicy.RoundRobin;
            cluster.EdsClusterConfig = new Cluster.Types.EdsClusterConfig()
            {
                ServiceName = $"outbound|8000||{serviceName}",
                EdsConfig = new ConfigSource()
                {
                    Ads = new AggregatedConfigSource()
                }
            };
            return new List<Cluster>() { cluster };
        }

        private static List<ClusterLoadAssignment> GetSampleClusterLoadAssignments()
        {
            var localityLbEndpoints = new LocalityLbEndpoints();
            localityLbEndpoints.LbEndpoints.Add(GetLbEndpoint("10.1.5.210", 80));
            localityLbEndpoints.LbEndpoints.Add(GetLbEndpoint("10.1.5.211", 80));
            localityLbEndpoints.LbEndpoints.Add(GetLbEndpoint("10.1.5.212", 80));
            var clusterLoadAssignment = new ClusterLoadAssignment();
            clusterLoadAssignment.Endpoints.Add(localityLbEndpoints);
            return new List<ClusterLoadAssignment>() { clusterLoadAssignment };
        }

        private static LbEndpoint GetLbEndpoint(string address, int port)
        {
            var endpoint = new LbEndpoint();
            endpoint.Endpoint = new Endpoint();
            endpoint.Endpoint.Address = new Address();
            endpoint.Endpoint.Address.SocketAddress = new SocketAddress();
            endpoint.Endpoint.Address.SocketAddress.Address = address;
            endpoint.Endpoint.Address.SocketAddress.PortValue = Convert.ToUInt32(port);
            return endpoint;
        }

        [Fact]
        public void ForGrpcSubChannels_UseEdsPolicySelectChannels_SelectChannelsInRoundRobin()
        {
            // Arrange
            using var policy = new EdsPolicy();
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            policy._subchannelPicker = new WeightedRandomPicker(new List<WeightedRandomPicker.WeightedChildPicker>()
            {
                new WeightedRandomPicker.WeightedChildPicker(1, new RoundRobinPicker(subChannels))
            });

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
    }
}
