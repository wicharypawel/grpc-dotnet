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
    public sealed class CdsPolicyTests
    {
        [Fact]
        public async Task ForEmptyServiceName_UseCdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new CdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "", false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, string.Empty, false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
        }

        [Fact]
        public async Task ForBalancersResolutionPassed_UseCdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new CdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(2, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolutionResult is expected to be empty.", exception.Message);
        }

        [Fact]
        public async Task ForServersResolutionPassed_UseCdsPolicy_ThrowArgumentException()
        {
            // Arrange
            using var policy = new CdsPolicy();
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.CreateSubChannelsAsync(resolutionResults, "sample-service.contoso.com", true);
            });
            Assert.Equal("resolutionResult is expected to be empty.", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResultWithBalancers_UseCdsPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var serviceName = "sample-service.contoso.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose());
            xdsClientMock.Setup(x => x.GetCdsAsync()).Returns(Task.FromResult(GetSampleClusters(serviceName)));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;
            var xdsClientPool = new XdsClientObjectPool(xdsClientFactory, NullLoggerFactory.Instance);
            
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var attributes = new GrpcAttributes(new Dictionary<string, object>
            {
                { XdsAttributesConstants.XdsClientPoolInstance, xdsClientPool },
                { XdsAttributesConstants.CdsClusterName, "magic-value-find-cluster-by-service-name" }
            });
            var resolutionResults = new GrpcNameResolutionResult(hostsAddresses, config, attributes);
            
            GrpcNameResolutionResult? edsResolutionResult = null;
            var edsPolicyMock = new Mock<IGrpcLoadBalancingPolicy>(MockBehavior.Loose);
            edsPolicyMock.Setup(x => x.CreateSubChannelsAsync(It.IsAny<GrpcNameResolutionResult>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<GrpcNameResolutionResult, string, bool>((resolutionResult, _, __) => { edsResolutionResult = resolutionResult; })
                .Returns(Task.CompletedTask);

            // Act
            using var policy = new CdsPolicy();
            policy.OverrideEdsPolicy = edsPolicyMock.Object;
            await policy.CreateSubChannelsAsync(resolutionResults, serviceName, false);

            // Assert
            xdsClientMock.Verify(x => x.GetCdsAsync(), Times.Once);
            policy.Dispose();
            xdsClientMock.Verify(x => x.Dispose(), Times.Once);
            Assert.NotNull(edsResolutionResult);
            Assert.NotNull(edsResolutionResult!.Attributes.Get(XdsAttributesConstants.XdsClientPoolInstance));
            Assert.NotNull(edsResolutionResult!.Attributes.Get(XdsAttributesConstants.EdsClusterName));
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
    }
}
