using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class DnsResolverPluginTests
    {
        [Theory]
        [InlineData("http")]
        [InlineData("https")]
        [InlineData("unknown")]
        public async Task ForTargetWithNonDnsScheme_UseDnsResolverPluginTests_ThrowArgumentException(string scheme)
        {
            // Arrange
            var resolverPlugin = new DnsResolverPlugin();
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"{scheme}://sample.host.com"), nameResolutionObserver);
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();
            Assert.NotNull(error);
            Assert.Contains("require dns:// scheme to set as target address", error.Value.Detail);
        }

        [Fact]
        public async Task ForTargetAndEmptyDnsResults_UseDnsResolverPlugin_ReturnNoFinidings()
        {
            // Arrange
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin();
            resolverPlugin.OverrideDnsResults = Task.FromResult(Array.Empty<IPAddress>());
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Empty(resolutionResult.HostsAddresses);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseDnsResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var serviceHostName = "my-service";
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin" } });
            var timerFake = new TimerFake();
            var resolverPlugin = new DnsResolverPlugin(attributes, timerFake);
            resolverPlugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"),
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:443"), nameResolutionObserver);
            timerFake.ManualCallbackTrigger();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("round_robin", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }

        [Fact]
        public async Task ForTargetAndARecordsDnsResults_UseDnsResolverPlugin_ReturnServers()
        {
            // Arrange
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin();
            resolverPlugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"), 
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Equal(3, resolutionResult.HostsAddresses.Count);
            Assert.Empty(resolutionResult.HostsAddresses.Where(x => x.IsLoadBalancer));
            Assert.Equal(3, resolutionResult.HostsAddresses.Where(x => !x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.HostsAddresses.Where(x => !x.IsLoadBalancer), x => Assert.Equal(80, x.Port));
            Assert.All(resolutionResult.HostsAddresses.Where(x => !x.IsLoadBalancer), x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForExceptionDuringDnsSearch_UseDnsResolverPlugin_ReturnError()
        {
            // Arrange
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin();
            resolverPlugin.OverrideDnsResults = Task.FromException<IPAddress[]>(new InvalidOperationException());
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();

            // Assert
            Assert.NotNull(error);
        }
    }
}
