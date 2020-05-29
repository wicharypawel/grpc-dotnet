using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes;
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
            var options = new StaticResolverPluginOptions(resolveFunction);
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { GrpcAttributesConstants.StaticResolverOptions, options } });
            var resolverPlugin = new StaticResolverPlugin(attributes);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri("https://sample.host.com"), nameResolutionObserver);
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Equal(2, resolutionResult!.HostsAddresses.Count);
            Assert.Equal("10.1.5.212", resolutionResult.HostsAddresses[0].Host);
            Assert.Equal("10.1.5.213", resolutionResult.HostsAddresses[1].Host);
            Assert.Equal(8080, resolutionResult.HostsAddresses[0].Port);
            Assert.Equal(8080, resolutionResult.HostsAddresses[1].Port);
        }
    }
}
