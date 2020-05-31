#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes;
using System;
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
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var resolverPlugin = new DnsResolverPlugin(GrpcAttributes.Empty, executor, timer);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"{scheme}://sample.host.com"), nameResolutionObserver);
            timer.ManualCallbackTrigger();
            executor.DrainSingleAction();
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();
            Assert.NotNull(error);
            Assert.Contains("require dns:// scheme to set as target address", error.Value.Detail);
        }

        [Fact]
        public async Task ForTargetAndEmptyDnsResults_UseDnsResolverPlugin_ReturnNoFinidings()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin(GrpcAttributes.Empty, executor, timer);
            resolverPlugin.OverrideDnsResults = Task.FromResult(Array.Empty<IPAddress>());
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            timer.ManualCallbackTrigger();
            executor.DrainSingleAction();
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
            var executor = new ExecutorFake();
            var serviceHostName = "my-service";
            var attributes = GrpcAttributes.Builder.NewBuilder().Add(GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin").Build();
            var timerFake = new TimerFake();
            var resolverPlugin = new DnsResolverPlugin(attributes, executor, timerFake);
            resolverPlugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"),
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:443"), nameResolutionObserver);
            timerFake.ManualCallbackTrigger();
            executor.DrainSingleAction();
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
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin(GrpcAttributes.Empty, executor, timer);
            resolverPlugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"), 
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            timer.ManualCallbackTrigger();
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Equal(3, resolutionResult.HostsAddresses.Count);
            Assert.All(resolutionResult.HostsAddresses, x => Assert.Equal(80, x.Port));
            Assert.All(resolutionResult.HostsAddresses, x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForExceptionDuringDnsSearch_UseDnsResolverPlugin_ReturnError()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin(GrpcAttributes.Empty, executor, timer);
            resolverPlugin.OverrideDnsResults = Task.FromException<IPAddress[]>(new InvalidOperationException());
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            timer.ManualCallbackTrigger();
            executor.DrainSingleAction();
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();

            // Assert
            Assert.NotNull(error);
        }
    }
}
