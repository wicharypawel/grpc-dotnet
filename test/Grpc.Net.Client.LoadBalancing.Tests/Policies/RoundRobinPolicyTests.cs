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
    public sealed class RoundRobinPolicyTests
    {
        [Fact]
        public void ForEmptyResolutionPassed_UseRoundRobinPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution(0)));
            });
            Assert.Equal("resolvedAddresses must contain at least one address.", exception.Message);
        }

        [Fact]
        public void ForRequestConnection_UseRoundRobinPolicy_VerifyNoOp()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.Equal(3, policy.GetInternalSubchannels().Count);
            Assert.True(policy.GetInternalSubchannels().All(x => x.RequestConnectionCount == 1)); // connection is requested on subchannel startup
            policy.RequestConnection(); // no-op
            Assert.True(policy.GetInternalSubchannels().All(x => x.RequestConnectionCount == 1));
        }

        [Fact]
        public void ForCanHandleEmptyAddressList_UseRoundRobinPolicy_VerifyFalse()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            Assert.False(policy.CanHandleEmptyAddressListFromNameResolution());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void ForResolutionResults_UseRoundRobinPolicy_CreateAmmountSubChannels(int subchannelsCount)
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            var initialSubChannelsCount = policy.GetInternalSubchannels().Count;
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution(subchannelsCount)));
            var subChannels = policy.GetInternalSubchannels();

            // Assert
            Assert.Equal(0, initialSubChannelsCount);
            Assert.Equal(subchannelsCount, subChannels.Count);
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
            Assert.Single(helper.ObservedUpdatesToBalancingState);
            Assert.Equal(GrpcConnectivityState.CONNECTING, helper.ObservedUpdatesToBalancingState[0].Item1);
            Assert.Equal(typeof(RoundRobinPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState[0].Item2.GetType());
            Assert.Equal(StatusCode.OK, helper.ObservedUpdatesToBalancingState[0].Item2.GetNextSubChannel(GrpcPickSubchannelArgs.Empty).Status.StatusCode);
        }

        [Fact]
        public void ForRepeatedResolutionResults_UseRoundRobinPolicy_VerifyCacheWorks()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            var observedUpdatesToBalancingStateCount = helper.ObservedUpdatesToBalancingState.Count;
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.1")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Equal(observedUpdatesToBalancingStateCount, helper.ObservedUpdatesToBalancingState.Count); 
        }

        [Fact]
        public void ForChangingResolvedAddresses_UseRoundRobinPolicy_RemoveAndCreateSubchannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.Equal(3, policy.GetInternalSubchannels().Count);
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Equal(2, policy.GetInternalSubchannels().Count);
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.Equal(3, policy.GetInternalSubchannels().Count);
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4", "10.1.5.5")));
            Assert.Equal(4, policy.GetInternalSubchannels().Count);
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4", "10.1.5.5")));
            Assert.Equal(2, policy.GetInternalSubchannels().Count);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UseRoundRobinPolicy_EnsureHelperIsCalledForNewSubChannelsAndAllAreStartedOnce()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(3, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(3, helper.CreateSubChannelCount);
            Assert.Equal(2, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(4, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(4, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(5, helper.CreateSubChannelCount);
            Assert.Equal(4, policy.GetInternalSubchannels().Count);
            
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(5, helper.CreateSubChannelCount);
            Assert.Equal(2, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4", "10.1.5.2", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(6, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.4", "10.1.5.5")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.StartCount == 1));
            Assert.Equal(6, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UseRoundRobinPolicy_EnsureNewSubChannelsAreRequestedConnection()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.RequestConnectionCount == 1));

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.RequestConnectionCount == 1));

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.RequestConnectionCount == 1));

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.RequestConnectionCount == 1));
        }

        [Fact]
        public void ForChangingResolvedAddresses_UseRoundRobinPolicy_EnsureRemovedChannelsAreCalledShutDown()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            var prevSubChannels = policy.GetInternalSubchannels();

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.ShutdownCount == 0));
            var removedSubChannels = prevSubChannels.Except(policy.GetInternalSubchannels()).ToList();
            Assert.Empty(removedSubChannels);
            Assert.True(removedSubChannels.All(x => x.ShutdownCount == 1));
            prevSubChannels = policy.GetInternalSubchannels();

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.ShutdownCount == 0));
            removedSubChannels = prevSubChannels.Except(policy.GetInternalSubchannels()).ToList();
            Assert.Single(removedSubChannels);
            Assert.True(removedSubChannels[0].Address.Host == "10.1.5.3");
            Assert.True(removedSubChannels.All(x => x.ShutdownCount == 1));
            prevSubChannels = policy.GetInternalSubchannels();

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.4")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.ShutdownCount == 0));
            removedSubChannels = prevSubChannels.Except(policy.GetInternalSubchannels()).ToList();
            Assert.Empty(removedSubChannels);
            Assert.True(removedSubChannels.All(x => x.ShutdownCount == 1));
            prevSubChannels = policy.GetInternalSubchannels();

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.4")));
            Assert.True(policy.GetInternalSubchannels().All(x => x.ShutdownCount == 0));
            removedSubChannels = prevSubChannels.Except(policy.GetInternalSubchannels()).ToList();
            Assert.True(removedSubChannels.Count == 2);
            Assert.True(removedSubChannels[0].Address.Host == "10.1.5.1");
            Assert.True(removedSubChannels[1].Address.Host == "10.1.5.2");
            Assert.True(removedSubChannels.All(x => x.ShutdownCount == 1));
            prevSubChannels = policy.GetInternalSubchannels();
        }

        [Fact]
        public void ForChangingResolvedAddresses_UseRoundRobinPolicy_EnsureDuplicatesDoesNotCreateSeparateSubChannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.2")));
            Assert.Equal(2, helper.CreateSubChannelCount);
            Assert.Equal(2, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Equal(2, helper.CreateSubChannelCount);
            Assert.Equal(2, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.1", "10.1.5.4", "10.1.5.4")));
            Assert.Equal(3, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);
        }

        [Fact]
        public void ForChangingResolvedAddresses_UseRoundRobinPolicy_DifferentOrderDoesNotCreateSeparateSubChannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.Equal(3, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.1", "10.1.5.3")));
            Assert.Equal(3, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);

            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.2", "10.1.5.3", "10.1.5.1")));
            Assert.Equal(3, helper.CreateSubChannelCount);
            Assert.Equal(3, policy.GetInternalSubchannels().Count);
        }

        [Fact]
        public void ForChangingSubChannelsStates_UseRoundRobinPolicy_HandleNameResolutionErrorWithReadyPreviousPickers()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
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
        public void ForChangingSubChannelsStates_UseRoundRobinPolicy_HandleNameResolutionErrorWithNoReadyPreviousPickers()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.HandleNameResolutionError(new Status(StatusCode.Internal, "test bug"));

            // Assert
            var currentState = helper.ObservedUpdatesToBalancingState.Last().Item1;
            var currentPicker = helper.ObservedUpdatesToBalancingState.Last().Item2;
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, currentState);
            Assert.Equal(typeof(RoundRobinPolicy.EmptyPicker), currentPicker.GetType());
            Assert.Equal(StatusCode.Internal, currentPicker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty).Status.StatusCode);
        }

        [Fact]
        public void ForChangingSubChannelsStates_UseRoundRobinPolicy_ForSingleSubChannelUpdateChannelState()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1")));
            Assert.Single(helper.ObservedUpdatesToBalancingState);
            Assert.Equal(GrpcConnectivityState.CONNECTING, helper.ObservedUpdatesToBalancingState[0].Item1);
            Assert.Equal(typeof(RoundRobinPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState[0].Item2.GetType());
            
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Single(helper.ObservedUpdatesToBalancingState); 
           
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(2, helper.ObservedUpdatesToBalancingState.Count); 
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState[1].Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState[1].Item2.GetType());

            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(3, helper.ObservedUpdatesToBalancingState.Count); 
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, helper.ObservedUpdatesToBalancingState[2].Item1);
            Assert.Equal(typeof(RoundRobinPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState[2].Item2.GetType());

            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            // this policy does not update state when subchannel starts connection after failure
            Assert.Equal(3, helper.ObservedUpdatesToBalancingState.Count);

            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(4, helper.ObservedUpdatesToBalancingState.Count);
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState[3].Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState[3].Item2.GetType());
        }

        [Fact]
        public void ForChangingSubChannelsStates_UseRoundRobinPolicy_ForMultipleSubChannelUpdateChannelState()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[2].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());

            policy.GetInternalSubchannels()[2].SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());

            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());

            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(RoundRobinPolicy.EmptyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
            var observedCount = helper.ObservedUpdatesToBalancingState.Count;

            // this policy does not update state when subchannel starts connection after failure
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(observedCount, helper.ObservedUpdatesToBalancingState.Count);

            // this policy does not update state when subchannel starts connection after failure
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(observedCount, helper.ObservedUpdatesToBalancingState.Count);

            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());

            // policy returns ready status and ready picker because at least one subchannel is ready
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(GrpcConnectivityState.READY, helper.ObservedUpdatesToBalancingState.Last().Item1);
            Assert.Equal(typeof(RoundRobinPolicy.ReadyPicker), helper.ObservedUpdatesToBalancingState.Last().Item2.GetType());
        }

        [Fact]
        public void ForChangingSubChannelsStates_UseRoundRobinPolicy_VerifyPreciseNumberOfChangingStates()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            Assert.Empty(helper.ObservedUpdatesToBalancingState);
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            Assert.Single(helper.ObservedUpdatesToBalancingState); // change because status is updated 
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Single(helper.ObservedUpdatesToBalancingState);
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Single(helper.ObservedUpdatesToBalancingState);
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(2, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(3, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            Assert.Equal(4, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(4, helper.ObservedUpdatesToBalancingState.Count);
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(5, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2", "10.1.5.3")));
            Assert.Equal(5, helper.ObservedUpdatesToBalancingState.Count);
            policy.GetInternalSubchannels()[2].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            Assert.Equal(5, helper.ObservedUpdatesToBalancingState.Count);
            policy.GetInternalSubchannels()[2].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            Assert.Equal(6, helper.ObservedUpdatesToBalancingState.Count); // change because picker is updated
        }

        [Fact]
        public void ForChangingSubChannelsStates_UseRoundRobinPolicy_VerifyPolicyIsNotInfluencedByRemovedSubChannels()
        {
            // Arrange
            var helper = new GrpcHelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1", "10.1.5.2")));
            policy.GetInternalSubchannels()[0].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            policy.GetInternalSubchannels()[1].SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            var soonRemovedSubChannel = policy.GetInternalSubchannels().First(x => x.Address.Host == "10.1.5.2");
            policy.HandleResolvedAddresses(NextResolved(GrpcHostAddressFactory.GetNameResolution("10.1.5.1"))); // remove 10.1.5.2
            var observedCount = helper.ObservedUpdatesToBalancingState.Count;
            // changes to removed subchannel does not influence policy anymore
            soonRemovedSubChannel.SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY)); 

            // Assert
            Assert.Equal(observedCount, helper.ObservedUpdatesToBalancingState.Count);
        }

        private static GrpcResolvedAddresses NextResolved(List<GrpcHostAddress> hostsAddresses)
        {
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("round_robin"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);
            return resolvedAddresses;
        }
    }

    public static class RoundRobinPolicyExtensions
    {
        internal static List<GrpcSubChannelFake> GetInternalSubchannels(this RoundRobinPolicy policy)
        {
            return policy.SubChannels.Values.Select(x => (GrpcSubChannelFake)x).ToList();
        }
    }
}
