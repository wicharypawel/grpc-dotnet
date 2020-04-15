using Grpc.Net.Client.LoadBalancing.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class XdsResolverPluginTests
    {
        [Fact]
        public async Task ForTargetWithNonXdsScheme_UseXdsResolverPlugin_ThrowArgumentException()
        {
            // Arrange
            var resolverPlugin = new XdsResolverPlugin();

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("http://sample.host.com"));
            });
            Assert.Contains("require xds://", exception.Message, StringComparison.Ordinal);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));
            });
            Assert.Contains("require xds://", exception.Message, StringComparison.Ordinal);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("unknown://sample.host.com"));
            });
            Assert.Contains("require xds://", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ForTarget_UseXdsResolverPlugin_ReturnNoFinidingsAndServiceConfigWithXdsPolicy()
        {
            // Arrange
            var serviceHostName = "my-service";

            var resolverPlugin = new XdsResolverPlugin();

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            var serviceConfig = await resolverPlugin.GetServiceConfigAsync();

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Empty(resolutionResult);
            Assert.NotEmpty(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "xds");
        }
    }
}
