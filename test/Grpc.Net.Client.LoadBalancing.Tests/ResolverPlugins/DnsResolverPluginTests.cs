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

using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
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
        [Fact]
        public void ForMultipleSubscriptions_UseDnsResolverPlugin_ThrowInvalidOperation()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions);
            Assert.Throws<InvalidOperationException>(() =>
            {
                resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            });
            Assert.Single(executor.Actions);
        }

        [Fact]
        public async Task ForEmptyDnsResults_UseDnsResolverPlugin_ReturnNoFinidings()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithEmpty(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Empty(executor.Actions);
            Assert.NotNull(resolutionResult.Attributes);
            Assert.Empty(resolutionResult.HostsAddresses);
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForTargetAndARecordsDnsResults_UseDnsResolverPlugin_ReturnServers()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Empty(executor.Actions);
            Assert.NotNull(resolutionResult.Attributes);
            Assert.Equal(3, resolutionResult.HostsAddresses.Count);
            Assert.All(resolutionResult.HostsAddresses, x => Assert.Equal(80, x.Port));
            Assert.All(resolutionResult.HostsAddresses, x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseDnsResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin").Build(); // overwrite default policy
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:443"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("round_robin", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }

        [Fact]
        public void ForRefreshingResolution_UseDnsResolverPlugin_ReturnNextValues()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public void ForPendingResolution_UseDnsResolverPlugin_ResolutionDoesNotOverlap()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions); // pending resolution
            resolverPlugin.RefreshResolution();
            resolverPlugin.RefreshResolution();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public async Task ForExceptionDuringDnsSearch_UseDnsResolverPlugin_ReturnError()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithError(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.NotNull(error);
            Assert.Equal(StatusCode.Unavailable, error.Value.StatusCode);
        }

        [Theory]
        [InlineData("http")]
        [InlineData("https")]
        [InlineData("unknown")]
        public async Task ForTargetWithNonDnsScheme_UseDnsResolverPluginTests_ReturnsArgumentException(string scheme)
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"{scheme}://{serviceHostName}"), nameResolutionObserver);
            executor.DrainSingleAction();
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();
            
            // Assert
            Assert.Empty(executor.Actions);
            Assert.NotNull(error);
            Assert.Equal(StatusCode.Unavailable, error.Value.StatusCode);
            Assert.Contains("require dns:// scheme to set as target address", error.Value.Detail);
        }

        [Fact]
        public void ForShutdownAndFollowingRefresh_UseDnsResolverPlugin_VerifyNotReturnResultsOnRefresh()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions); // pending resolution does not influence this scenario
            resolverPlugin.Shutdown();

            // Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                resolverPlugin.RefreshResolution();
            });
            Assert.Single(executor.Actions);
        }

        [Fact]
        public void ForDisposeAndFollowingRefresh_UseDnsResolverPlugin_VerifyNotReturnResultsOnRefresh()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            using var resolverPlugin = new DnsResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            Assert.Single(executor.Actions); // pending resolution does not influence this scenario
            resolverPlugin.Dispose();

            // Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                resolverPlugin.RefreshResolution();
            });
            Assert.Single(executor.Actions);
        }

        [Fact]
        public void ForDnsCacheNegativeTtlValue_UseDnsResolverPlugin_Throws()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds, -5).Build();

            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            });
        }

        [Fact]
        public void ForDnsCacheBeingOff_UseDnsResolverPlugin_AlwaysSkipCache()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.FromSeconds(1)); // cached values are fairly fresh
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds, 0).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            resolverPlugin.RefreshResolution();
            Assert.Single(executor.Actions);
            executor.DrainSingleAction();
            resolverPlugin.RefreshResolution();

            // Assert
            Assert.Single(executor.Actions);
        }

        [Fact]
        public void ForDnsCacheOnCachedValue_UseDnsResolverPlugin_UseCachedValue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.FromSeconds(1)); // cached values are fairly fresh
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds, 30).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction(); // initial resolution that populates cache
            resolverPlugin.RefreshResolution();
            resolverPlugin.RefreshResolution();
            resolverPlugin.RefreshResolution();

            // Assert
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public void ForDnsCacheOnStaleValue_UseDnsResolverPlugin_SkipCache()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.FromSeconds(31)); // stale value
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds, 30).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction(); // initial resolution that populates cache
            resolverPlugin.RefreshResolution();

            // Assert
            Assert.Single(executor.Actions);
        }

        [Fact]
        public void ForDnsCacheNeverPopulated_UseDnsResolverPlugin_SkipCache()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds, 30).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithError(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction(); // drains an error from dns resolution (does not populate cache)
            resolverPlugin.RefreshResolution();

            // Assert
            Assert.Single(executor.Actions);
        }

        [Theory]
        [InlineData(-5)]
        [InlineData(-1)]
        [InlineData(0)]
        public void ForPeriodicResolutionAndNegativePeriod_UseDnsResolverPlugin_Throws(double periodicSeconds)
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverPeriodicResolutionSeconds, periodicSeconds).Build();

            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            });
        }

        [Fact]
        public void ForPeriodicResolutionAndResolverShutdown_UseDnsResolverPlugin_VerifyPeiodicIsOff()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverPeriodicResolutionSeconds, 40).Build(); // set periodic for non-zero value
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            resolverPlugin.Shutdown();
            timer.ManualCallbackTrigger();

            // Assert
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public void ForPeriodicResolutionWithPendingResolution_UseDnsResolverPlugin_VerifyNoOverlapping()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverPeriodicResolutionSeconds, 40).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            timer.ManualCallbackTrigger();

            // Assert
            Assert.Single(executor.Actions);
        }

        [Fact]
        public void ForPeriodicResolution_UseDnsResolverPlugin_VerifyPeriodicCallsResolve()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.MaxValue);
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverPeriodicResolutionSeconds, 40).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction(); // we have to ensure periodic resolution will not overlap with the initial resolution
            timer.ManualCallbackTrigger();

            // Assert
            Assert.Single(executor.Actions);
        }

        [Fact]
        public void ForPeriodicResolutionWithCachedValue_UseDnsResolverPlugin_RefreshIsSkipped()
        {
            // Arrange
            var executor = new ExecutorFake();
            var timer = new TimerFake();
            var stopwatch = new StopwatchFake(() => TimeSpan.FromSeconds(1)); // this lines ensure our cache is fresh
            var serviceHostName = "service.googleapis.com";
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DnsResolverPeriodicResolutionSeconds, 40)
                .Add(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds, 30).Build();
            using var resolverPlugin = new DnsResolverPlugin(attributes, executor, timer, stopwatch);
            OverrideDnsWithResults(resolverPlugin);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri($"dns://{serviceHostName}:80"), nameResolutionObserver);
            executor.DrainSingleAction();
            timer.ManualCallbackTrigger();

            // Assert
            Assert.Empty(executor.Actions);
        }

        private static void OverrideDnsWithResults(DnsResolverPlugin plugin)
        {
            plugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"),
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });
        }

        private static void OverrideDnsWithEmpty(DnsResolverPlugin plugin)
        {
            plugin.OverrideDnsResults = Task.FromResult(Array.Empty<IPAddress>());
        }

        private static void OverrideDnsWithError(DnsResolverPlugin plugin)
        {
            plugin.OverrideDnsResults = Task.FromException<IPAddress[]>(new InvalidOperationException());
        }
    }
}
