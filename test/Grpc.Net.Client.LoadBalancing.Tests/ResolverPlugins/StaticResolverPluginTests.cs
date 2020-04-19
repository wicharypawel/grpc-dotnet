using Grpc.Net.Client.LoadBalancing.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class StaticResolverPluginTests
    {
        [Fact]
        public async Task ForStaticResolutionFunction_UseStaticResolverPlugin_ReturnPredefinedValues()
        {
            // Arrange
            Func<Uri, GrpcNameResolutionResult> resolveFunction = (uri) =>
            {
                var hosts = new List<GrpcHostAddress>()
                {
                    new GrpcHostAddress("10.1.5.212", 8080),
                    new GrpcHostAddress("10.1.5.213", 8080)
                };
                var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
                return new GrpcNameResolutionResult(hosts, config, GrpcAttributes.Empty);
            };
            var resolverPlugin = new StaticResolverPlugin(resolveFunction);

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));

            // Assert
            Assert.Equal(2, resolutionResult.HostsAddresses.Count);
            Assert.Equal("10.1.5.212", resolutionResult.HostsAddresses[0].Host);
            Assert.Equal("10.1.5.213", resolutionResult.HostsAddresses[1].Host);
            Assert.Equal(8080, resolutionResult.HostsAddresses[0].Port);
            Assert.Equal(8080, resolutionResult.HostsAddresses[1].Port);
        }
    }
}
