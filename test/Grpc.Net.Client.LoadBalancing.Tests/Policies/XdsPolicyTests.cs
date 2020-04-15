using Envoy.Api.V2;
using Envoy.Api.V2.Core;
using Envoy.Api.V2.Endpoint;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class XdsPolicyTests
    {
        [Fact]
        public async Task ForEmptyServiceName_UseXdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new XdsPolicy();
            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(0, 0);

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
        public async Task ForBalancersResolutionPassed_UseXdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new XdsPolicy();
            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(2, 0);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult is expected to be empty", exception.Message);
        }

        [Fact]
        public async Task ForServersResolutionPassed_UseXdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new XdsPolicy();
            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(0, 2);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", true);
            });
            Assert.Equal("resolutionResult is expected to be empty", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResultWithBalancers_UseXdsPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var serviceName = "sample-service.contoso.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose());
            xdsClientMock.Setup(x => x.GetCdsAsync()).Returns(Task.FromResult(GetSampleClusters(serviceName)));
            xdsClientMock.Setup(x => x.GetEdsAsync(It.IsAny<string>())).Returns(Task.FromResult(GetSampleClusterLoadAssignments()));

            XdsClientFactory.OverrideXdsClient = xdsClientMock.Object; 

            using var policy = new XdsPolicy();
            var resolutionResults = GrpcNameResolutionResultFactory.GetNameResolution(0, 0);

            // Act
            await policy.CreateSubChannelsAsync(resolutionResults, serviceName, false);
            var subChannels = policy.SubChannels;

            // Assert
            Assert.Equal(3, subChannels.Count);
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
            xdsClientMock.Verify(x => x.GetCdsAsync(), Times.Once);
            xdsClientMock.Verify(x => x.GetEdsAsync(It.IsAny<string>()), Times.Once);
            policy.Dispose();
            xdsClientMock.Verify(x => x.Dispose(), Times.Once);
        }

        private static List<Cluster> GetSampleClusters(string serviceName)
        {
            var cluster = new Cluster();
            cluster.Type = Cluster.Types.DiscoveryType.Eds;
            cluster.EdsClusterConfig = new Cluster.Types.EdsClusterConfig()
            {
                ServiceName = $"outbound|8000||{serviceName}"
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
        public void ForGrpcSubChannels_UseXdsPolicySelectChannels_SelectChannelsInRoundRobin()
        {
            // Arrange
            using var policy = new XdsPolicy();
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
    }
}
