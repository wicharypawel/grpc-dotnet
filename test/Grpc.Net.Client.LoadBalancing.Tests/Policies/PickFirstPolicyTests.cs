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
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class PickFirstPolicyTests
    {
        [Fact]
        public void ForEmptyResolutionPassed_UsePickFirstPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution(0)));
            });
            Assert.Equal("resolvedAddresses must contain at least one address.", exception.Message);
        }

        [Fact]
        public void ForRequestConnection_UsePickFirstPolicy_VerifyNoOp()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannel().RequestConnectionCount == 1); // connection is requested on subchannel startup
            policy.RequestConnection(); // no-op
            Assert.True(policy.GetInternalSubchannel().RequestConnectionCount == 1);
        }

        [Fact]
        public void ForCanHandleEmptyAddressList_UsePickFirstPolicy_VerifyFalse()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            Assert.False(policy.CanHandleEmptyAddressListFromNameResolution());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void ForResolutionResults_UsePickFirstPolicy_CreateAmmountSubChannels(int subchannelsCount)
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution(subchannelsCount)));
            var subChannel = policy.GetInternalSubchannel();

            // Assert
            Assert.Equal("http", subChannel.Address.Scheme);
            Assert.Equal(80, subChannel.Address.Port);
            Assert.StartsWith("10.1.5.", subChannel.Address.Host);
            Assert.Single(helper.ObservedUpdatesToBalancingState);
            Assert.Equal(GrpcConnectivityState.CONNECTING, helper.ObservedUpdatesToBalancingState[0].Item1);
            Assert.Equal(typeof(PickFirstPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState[0].Item2.GetType());
            Assert.Equal(StatusCode.OK, helper.ObservedUpdatesToBalancingState[0].Item2.GetNextSubChannel(GrpcPickSubchannelArgs.Empty).Status.StatusCode);
        }

        [Theory]
        [InlineData("https", 9000)]
        [InlineData("dns", 443)]
        public void ForResolutionResultsWithSecure_UsePickFirstPolicy_CreateAmmountSubChannels(string scheme, int port)
        {
            // Arrange
            var helper = new GrpcHelperFake(new UriBuilder($"{scheme}://google.apis.com:{port}").Uri);
            using var policy = new PickFirstPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution(3, port)));
            var subChannel = policy.GetInternalSubchannel();

            // Assert
            Assert.Equal("https", subChannel.Address.Scheme);
            Assert.Equal(port, subChannel.Address.Port);
            Assert.StartsWith("10.1.5.", subChannel.Address.Host);
        }

        [Fact]
        public void ForRepeatedResolutionResults_UsePickFirstPolicy_VerifyCacheWorks()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            var observedUpdatesToBalancingStateCount = helper.ObservedUpdatesToBalancingState.Count;
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.1")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Equal(observedUpdatesToBalancingStateCount, helper.ObservedUpdatesToBalancingState.Count);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UsePickFirstPolicy_RemoveAndCreateSubchannel()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            var prevSubChannel = policy.GetInternalSubchannel();
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.NotEqual(prevSubChannel, policy.GetInternalSubchannel());
            prevSubChannel = policy.GetInternalSubchannel();
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.NotEqual(prevSubChannel, policy.GetInternalSubchannel());
            prevSubChannel = policy.GetInternalSubchannel();
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4", "10.1.5.5")));
            Assert.NotEqual(prevSubChannel, policy.GetInternalSubchannel());
            prevSubChannel = policy.GetInternalSubchannel();
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4", "10.1.5.5")));
            Assert.NotEqual(prevSubChannel, policy.GetInternalSubchannel());
            prevSubChannel = policy.GetInternalSubchannel();
        }

        [Fact]
        public void ForChangingResolvedAddresses_UsePickFirstPolicy_EnsureHelperIsCalledForNewSubChannelsAndAllAreStartedOnce()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(1, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(2, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(3, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(3, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(4, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(5, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4", "10.1.5.2", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(6, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.4", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannel().StartCount == 1);
            Assert.Equal(6, helper.CreateSubChannelCount);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UsePickFirstPolicy_EnsureNewSubChannelsAreRequestedConnection()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannel().RequestConnectionCount == 1);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.True(policy.GetInternalSubchannel().RequestConnectionCount == 1);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannel().RequestConnectionCount == 1);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannel().RequestConnectionCount == 1);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UsePickFirstPolicy_EnsureRemovedChannelsAreCalledShutDown()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannel().ShutdownCount == 0);
            var prevSubChannel = policy.GetInternalSubchannel();

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.True(policy.GetInternalSubchannel().ShutdownCount == 0);
            Assert.NotEqual(prevSubChannel, policy.GetInternalSubchannel());
            Assert.True(prevSubChannel.ShutdownCount == 1);
            prevSubChannel = policy.GetInternalSubchannel();

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannel().ShutdownCount == 0);
            Assert.NotEqual(prevSubChannel, policy.GetInternalSubchannel());
            Assert.True(prevSubChannel.ShutdownCount == 1);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UsePickFirstPolicy_EnsureDuplicatesDoesNotCreateSeparateSubChannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.2")));
            Assert.Equal(1, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Equal(1, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.1", "10.1.5.2")));
            Assert.Equal(1, helper.CreateSubChannelCount);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UsePickFirstPolicy_DifferentOrderDoesNotCreateSeparateSubChannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.Equal(1, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.1", "10.1.5.3")));
            Assert.Equal(1, helper.CreateSubChannelCount);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.3", "10.1.5.1")));
            Assert.Equal(1, helper.CreateSubChannelCount);
        }

        [Fact]
        public void ForChangingSubChannelState_UsePickFirstPolicy_HandleNameResolutionErrorWithReadyPreviousPickers()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            var lastPicker = helper.ObservedUpdatesToBalancingState.Last().Item2;
            policy.HandleNameResolutionError(new Status(StatusCode.Internal, "test bug"));

            // Assert
            var currentState = helper.ObservedUpdatesToBalancingState.Last().Item1;
            var currentPicker = helper.ObservedUpdatesToBalancingState.Last().Item2;
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, currentState);
            Assert.Equal(lastPicker, currentPicker);
        }

        [Fact]
        public void ForChangingSubChannelState_UsePickFirstPolicy_HandleNameResolutionErrorWithNoReadyPreviousPickers()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.HandleNameResolutionError(new Status(StatusCode.Internal, "test bug"));

            // Assert
            var currentState = helper.ObservedUpdatesToBalancingState.Last().Item1;
            var currentPicker = helper.ObservedUpdatesToBalancingState.Last().Item2;
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, currentState);
            Assert.Equal(typeof(PickFirstPolicy.EmptyPicker), currentPicker.GetType());
            Assert.Equal(StatusCode.Internal, currentPicker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty).Status.StatusCode);
        }

        [Fact]
        public void ForChangingSubChannelState_UsePickFirstPolicy_ForSubChannelUpdateChannelState()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
            var observedCount = helper.ObservedUpdatesToBalancingState.Count;

            // this policy does not update state when subchannel starts connection after failure
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(observedCount, helper.ObservedUpdatesToBalancingState.Count);

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
            observedCount = helper.ObservedUpdatesToBalancingState.Count;

            // this policy does not update state when subchannel starts connection after failure
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(observedCount, helper.ObservedUpdatesToBalancingState.Count);

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
        }

        [Fact]
        public void ForChangingSubChannelState_UsePickFirstPolicy_AllTransportsFail()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
            Assert.Equal("10.1.5.1", policy.GetInternalSubchannel().Address.Host);

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, helper.ObservedUpdatesToBalancingState.Last().Item1);
            var currentPicker = helper.ObservedUpdatesToBalancingState.Last().Item2;
            Assert.Equal(typeof(PickFirstPolicy.EmptyPicker), currentPicker.GetType());
            Assert.Equal(StatusCode.Internal, currentPicker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty).Status.StatusCode);

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
            Assert.Equal("10.1.5.2", policy.GetInternalSubchannel().Address.Host);

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, helper.ObservedUpdatesToBalancingState.Last().Item1);
            currentPicker = helper.ObservedUpdatesToBalancingState.Last().Item2;
            Assert.Equal(typeof(PickFirstPolicy.EmptyPicker), currentPicker.GetType());
            Assert.Equal(StatusCode.Internal, currentPicker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty).Status.StatusCode);

            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(PickFirstPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
            Assert.Equal("10.1.5.3", policy.GetInternalSubchannel().Address.Host);
        }

        [Fact]
        public void ForChangingSubChannelState_UsePickFirstPolicy_VerifyPreciseNumberOfChangingStates()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            // Assert
            Assert.Empty(helper.ObservedUpdatesToBalancingState);
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Single(helper.ObservedUpdatesToBalancingState); // change because status is updated 
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Single(helper.ObservedUpdatesToBalancingState);
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(2, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(3, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(3, helper.ObservedUpdatesToBalancingState.Count);
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(4, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.Equal(5, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(5, helper.ObservedUpdatesToBalancingState.Count);
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(6, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
        }

        [Fact]
        public void ForChangingSubChannelState_UsePickFirstPolicy_VerifyPolicyIsNotInfluencedByRemovedSubChannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new PickFirstPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannel().SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            var soonRemovedSubChannel = policy.GetInternalSubchannel();
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2")));
            var observedCount = helper.ObservedUpdatesToBalancingState.Count;
            // changes to removed subchannel does not influence policy anymore
            soonRemovedSubChannel.SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));

            // Assert
            Assert.Equal(observedCount, helper.ObservedUpdatesToBalancingState.Count);
        }

        private static GrpcResolvedAddresses NextResolved(List<GrpcHostAddress> hostsAddresses)
        {
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);
            return resolvedAddresses;
        }
    }

    public static class PickFirstPolicyExtensions
    {
        internal static GrpcSubChannelFake GetInternalSubchannel(this PickFirstPolicy policy)
        {
            if (policy.SubChannel == null)
            {
                throw new ArgumentException("subchannel not found");
            }
            return (GrpcSubChannelFake)policy.SubChannel;
        }
    }
}
