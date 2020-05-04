using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Infrastructure.Extensions;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.XdsRelated.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated
{
    public sealed class XdsClientTests
    {
        [Fact]
        public async Task ForLdsHavingRouteConfigInline_UseXdsClient_ReturnConfigUpdate()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = $"{serviceHostName}:80";
            var clusterName = "cluster-foo.googleapis.com";

            var ldsResponse = XdsClientTestFactory.BuildLdsResponseForCluster("0", authority, clusterName, "0000");
            var responseStreamMock = new Mock<IAsyncStreamReader<Envoy.Api.V2.DiscoveryResponse>>();
            responseStreamMock.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true), Task.FromResult(false));
            responseStreamMock.Setup(x => x.Current).Returns(ldsResponse);

            var bootstrapperFake = new XdsBootstrapperFake();
            var adsStream = AsyncDuplexStreamingCallBuilder.InitializeBuilderWithFakeData().OverrideResponseStream(responseStreamMock.Object).Build();
            var channelFactory = new XdsChannelFactory();
            channelFactory.OverrideChannel = new AdsChannelFake(authority, adsStream);

            // Act
            using var client = new XdsClient(bootstrapperFake, NullLoggerFactory.Instance, channelFactory);
            var configUpdate = await client.GetLdsRdsAsync($"{serviceHostName}:80");

            // Assert
            Assert.NotNull(configUpdate);
            Assert.NotNull(configUpdate.Routes);
            Assert.Equal(2, configUpdate.Routes.Count);
            Assert.Equal(string.Empty, configUpdate.Routes[0].RouteMatch.Prefix);
            Assert.Equal(string.Empty, configUpdate.Routes[1].RouteMatch.Prefix);
            Assert.NotEmpty(configUpdate.Routes[0]?.RouteAction?.Cluster);
            Assert.Equal(clusterName, configUpdate.Routes[1]?.RouteAction?.Cluster);
        }

        [Fact]
        public async Task ForLdsPointingToRds_UseXdsClient_ReturnConfigUpdate()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = $"{serviceHostName}:80";
            var routeConfigName = "route-foo.googleapis.com";
            var clusterName = "cluster-foo.googleapis.com";

            var ldsResponse = XdsClientTestFactory.BuildLdsResponseForRdsResource("0", authority, routeConfigName, "0000");
            var rdsResponse = XdsClientTestFactory.BuildRdsResponseForCluster("0", routeConfigName, authority, clusterName, "0000");
            var responseStreamMock = new Mock<IAsyncStreamReader<Envoy.Api.V2.DiscoveryResponse>>();
            responseStreamMock.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true), Task.FromResult(true), Task.FromResult(false));
            responseStreamMock.Setup(x => x.Current).Returns(ldsResponse, rdsResponse);

            var bootstrapperFake = new XdsBootstrapperFake();
            var adsStream = AsyncDuplexStreamingCallBuilder.InitializeBuilderWithFakeData().OverrideResponseStream(responseStreamMock.Object).Build();
            var channelFactory = new XdsChannelFactory();
            channelFactory.OverrideChannel = new AdsChannelFake(authority, adsStream);
            
            // Act
            using var client = new XdsClient(bootstrapperFake, NullLoggerFactory.Instance, channelFactory);
            var configUpdate = await client.GetLdsRdsAsync($"{serviceHostName}:80");

            // Assert
            Assert.NotNull(configUpdate);
            Assert.NotNull(configUpdate.Routes);
            Assert.Equal(2, configUpdate.Routes.Count);
            Assert.Equal(string.Empty, configUpdate.Routes[0].RouteMatch.Prefix);
            Assert.Equal(string.Empty, configUpdate.Routes[1].RouteMatch.Prefix);
            Assert.NotEmpty(configUpdate.Routes[0]?.RouteAction?.Cluster);
            Assert.Equal(clusterName, configUpdate.Routes[1]?.RouteAction?.Cluster);
        }

        [Fact]
        public void ForLdsNotFoundListener_UseXdsClient_ThrowInvalidOperation()
        {
            // according to gRFC documentation XdsClient should throw error for not found listener
            // current implementation pretends to find listener for cluster "magic-value-find-cluster-by-service-name"
            // it is implemented that way because currently used control-plane does not support LDS
            // in the future simply throw an error if not found and verify that in tests
        }

        [Fact]
        public async Task ForCds_UseXdsClient_ReturnClusterUpdate()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = $"{serviceHostName}:80";
            var clusterName = "cluster-foo.googleapis.com";
            var edsServiceName = "eds-cluster-foo.googleapis.com";

            var cdsResponse = XdsClientTestFactory.BuildCdsResponseForCluster("0", clusterName, edsServiceName, "0000");
            var responseStreamMock = new Mock<IAsyncStreamReader<Envoy.Api.V2.DiscoveryResponse>>();
            responseStreamMock.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true), Task.FromResult(false));
            responseStreamMock.Setup(x => x.Current).Returns(cdsResponse);

            var bootstrapperFake = new XdsBootstrapperFake();
            var adsStream = AsyncDuplexStreamingCallBuilder.InitializeBuilderWithFakeData().OverrideResponseStream(responseStreamMock.Object).Build();
            var channelFactory = new XdsChannelFactory();
            channelFactory.OverrideChannel = new AdsChannelFake(authority, adsStream);

            // Act
            using var client = new XdsClient(bootstrapperFake, NullLoggerFactory.Instance, channelFactory);
            var clusterUpdate = await client.GetCdsAsync(clusterName, serviceHostName);

            // Assert
            Assert.NotNull(clusterUpdate);
            Assert.Equal(clusterName, clusterUpdate.ClusterName);
            Assert.Equal(edsServiceName, clusterUpdate.EdsServiceName);
            Assert.Equal("eds_experimental", clusterUpdate.LbPolicy);
            Assert.Null(clusterUpdate.LrsServerName);
        }

        [Fact]
        public async Task ForEds_UseXdsClient_ReturnEndpointUpdate()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = $"{serviceHostName}:80";
            var clusterName = "cluster-foo.googleapis.com";

            var edsResponse = XdsClientTestFactory.BuildEdsResponseForCluster("0", clusterName, "0000");
            var responseStreamMock = new Mock<IAsyncStreamReader<Envoy.Api.V2.DiscoveryResponse>>();
            responseStreamMock.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true), Task.FromResult(false));
            responseStreamMock.Setup(x => x.Current).Returns(edsResponse);

            var bootstrapperFake = new XdsBootstrapperFake();
            var adsStream = AsyncDuplexStreamingCallBuilder.InitializeBuilderWithFakeData().OverrideResponseStream(responseStreamMock.Object).Build();
            var channelFactory = new XdsChannelFactory();
            channelFactory.OverrideChannel = new AdsChannelFake(authority, adsStream);

            // Act
            using var client = new XdsClient(bootstrapperFake, NullLoggerFactory.Instance, channelFactory);
            var endpointUpdate = await client.GetEdsAsync(clusterName);

            // Assert
            Assert.NotNull(endpointUpdate);
            Assert.Equal(clusterName, endpointUpdate.ClusterName);
            var keys = endpointUpdate.LocalityLbEndpoints.Keys.ToArray();
            Assert.Single(keys);
            var key = keys[0];
            Assert.Equal("test-locality", key.Region);
            Assert.Equal("a", key.Zone);
            Assert.Equal(string.Empty, key.SubZone);
            Assert.Equal(1, endpointUpdate.LocalityLbEndpoints[key].Priority);
            Assert.Equal(3, endpointUpdate.LocalityLbEndpoints[key].LocalityWeight);
            Assert.Equal(3, endpointUpdate.LocalityLbEndpoints[key].Endpoints.Count);
            Assert.True(endpointUpdate.LocalityLbEndpoints[key].Endpoints.All(x => x.IsHealthy));
            Assert.True(endpointUpdate.LocalityLbEndpoints[key].Endpoints.All(x => x.LoadBalancingWeight == 1));
            Assert.True(endpointUpdate.LocalityLbEndpoints[key].Endpoints.All(x => x.HostsAddresses != null));
            Assert.Equal(0, endpointUpdate.DropPolicies.Count);
        }
    }
}
