using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
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
            var helper = new HelperFake();
            using var policy = new CdsPolicy(helper);
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
        public async Task ForNonEmptyResolutionPassed_UseCdsPolicy_ThrowArgumentException(int balancersCount, int serversCount)
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new CdsPolicy(helper);
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
        public async Task ForEmptyXdsClientPool_UseCdsPolicy_Throw()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new CdsPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", false);
            });
            Assert.Equal("Can not find xds client pool.", exception.Message);
        }

        [Fact]
        public async Task ForEmptyCdsClusterName_UseCdsPolicy_Throw()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new CdsPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose());
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;
            var xdsClientPool = new XdsClientObjectPool(xdsClientFactory, NullLoggerFactory.Instance);
            var attributes = new GrpcAttributes(new Dictionary<string, object> { { XdsAttributesConstants.XdsClientPoolInstance, xdsClientPool } });
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, attributes);

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await policy.HandleResolvedAddressesAsync(resolvedAddresses, "sample-service.contoso.com", false);
            });
            Assert.Equal("Can not find CDS cluster name.", exception.Message);
        }

        [Fact]
        public async Task ForResolutionResultWithBalancers_UseCdsPolicy_CreateSubchannelsForFoundServers()
        {
            // Arrange
            var serviceName = "sample-service.contoso.com";
            var clusterName = "cluster-test-name";
            
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.Dispose());
            xdsClientMock.Setup(x => x.GetCdsAsync(clusterName, serviceName)).Returns(Task.FromResult(GetSampleClusterUpdate(serviceName)));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;
            var xdsClientPool = new XdsClientObjectPool(xdsClientFactory, NullLoggerFactory.Instance);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0, 0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var attributes = new GrpcAttributes(new Dictionary<string, object>
            {
                { XdsAttributesConstants.XdsClientPoolInstance, xdsClientPool },
                { XdsAttributesConstants.CdsClusterName, clusterName }
            });
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, attributes);
            
            GrpcNameResolutionResult? edsResolutionResult = null;
            var edsPolicyMock = new Mock<IGrpcLoadBalancingPolicy>(MockBehavior.Loose);
            edsPolicyMock.Setup(x => x.HandleResolvedAddressesAsync(It.IsAny<GrpcResolvedAddresses>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<GrpcNameResolutionResult, string, bool>((resolutionResult, _, __) => { edsResolutionResult = resolutionResult; })
                .Returns(Task.CompletedTask);

            // Act
            var helper = new HelperFake();
            using var policy = new CdsPolicy(helper);
            policy.OverrideEdsPolicy = edsPolicyMock.Object;
            await policy.HandleResolvedAddressesAsync(resolvedAddresses, serviceName, false);

            // Assert
            xdsClientMock.Verify(x => x.GetCdsAsync(clusterName, serviceName), Times.Once);
            policy.Dispose();
            xdsClientMock.Verify(x => x.Dispose(), Times.Once);
            Assert.NotNull(edsResolutionResult);
            Assert.NotNull(edsResolutionResult!.Attributes.Get(XdsAttributesConstants.XdsClientPoolInstance));
            Assert.NotNull(edsResolutionResult!.Attributes.Get(XdsAttributesConstants.EdsClusterName));
            edsPolicyMock.Verify(x => x.HandleResolvedAddressesAsync(It.IsAny<GrpcResolvedAddresses>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        }

        private static ClusterUpdate GetSampleClusterUpdate(string serviceName)
        {
            return new ClusterUpdate($"outbound|8000||{serviceName}", $"outbound|8000||{serviceName}", "eds_experimental", null);

        }
    }
}
