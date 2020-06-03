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
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcSubChannelTests
    {
        [Fact]
        public void ForNewSubChannel_UseGrpcSubChannel_VerifyReturnValuesUsedInCtor()
        {
            // Arrange
            var channel = GrpcChannelForSubChannelFake.Get();
            var attributes = GrpcAttributes.Builder.NewBuilder().Add(GrpcAttributes.Key<string>.Create("test-key"), "testValue").Build();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, attributes);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            // Assert
            Assert.Equal(new UriBuilder("http://10.1.5.210:80").Uri, subchannel.Address);
            Assert.Equal(attributes, subchannel.Attributes);
            Assert.Empty(observedStateInfos);
        }

        [Fact]
        public void ForStarting_UseGrpcSubChannel_DoesNotPublishIdleState()
        {
            // Arrange
            var channel = GrpcChannelForSubChannelFake.Get();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });

            // Assert
            Assert.Empty(observedStateInfos);
        }

        [Fact]
        public async Task ForStartedSubchannel_UseGrpcSubChannel_StartThrows()
        {
            // Arrange
            Exception? actualException = null;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => actualException = ex);
            var channel = GrpcChannelForSubChannelFake.Get(synchronizationContext);
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });

            // Assert
            Assert.Empty(observedStateInfos);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            while (!timeoutTask.IsCompleted)
            {
                if (actualException != null)
                {
                    Assert.Equal(typeof(InvalidOperationException), actualException.GetType());
                    Assert.Equal("Already started.", actualException.Message);
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
            Assert.True(false);
        }

        [Fact]
        public void ForShutingdown_UseGrpcSubChannel_PublishesShutdownState()
        {
            // Arrange
            var channel = GrpcChannelForSubChannelFake.Get();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            channel.SyncContext.Execute(() => { subchannel.Shutdown(); });

            // Assert
            Assert.Single(observedStateInfos);
            Assert.Equal(GrpcConnectivityState.SHUTDOWN, observedStateInfos[0].State);
        }

        [Fact]
        public void ForDoubleShutingdown_UseGrpcSubChannel_DoesNotRepeatShutdownState()
        {
            // Arrange
            var channel = GrpcChannelForSubChannelFake.Get();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            channel.SyncContext.Execute(() => { subchannel.Shutdown(); });
            channel.SyncContext.Execute(() => { subchannel.Shutdown(); });

            // Assert
            Assert.Single(observedStateInfos);
        }

        [Fact]
        public void ForRequestConnectionIdle_UseGrpcSubChannel_MoveToConnectingState()
        {
            // Arrange
            var channel = GrpcChannelForSubChannelFake.Get();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            channel.SyncContext.Execute(() => { subchannel.RequestConnection(); });

            // Assert
            Assert.True(observedStateInfos.Count == 2);
            Assert.Equal(GrpcConnectivityState.CONNECTING, observedStateInfos[0].State);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[1].State);
        }

        [Fact]
        public async Task ForNotStarted_UseGrpcSubChannel_RequestConnectionThrows()
        {
            // Arrange
            Exception? actualException = null;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => actualException = ex);
            var channel = GrpcChannelForSubChannelFake.Get(synchronizationContext);
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.RequestConnection(); });

            // Assert
            Assert.Empty(observedStateInfos);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            while (!timeoutTask.IsCompleted)
            {
                if (actualException != null)
                {
                    Assert.Equal(typeof(InvalidOperationException), actualException.GetType());
                    Assert.Equal("Not started.", actualException.Message);
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
            Assert.True(false);
        }

        #region HTTP_CLIENT_MISSING_MONITORING_WORKAROUND
        [Fact]
        public void ForRequestConnectionOnConnecting_UseGrpcSubChannel_ChangeToReadyState()
        {
            // Arrange
            var channel = GrpcChannelForSubChannelFake.Get();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            
            var prop = subchannel.GetType().GetField("_stateInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop!.SetValue(subchannel, GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            
            channel.SyncContext.Execute(() => { subchannel.RequestConnection(); });

            // Assert
            Assert.Single(observedStateInfos);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[0].State);
        }

        [Fact]
        public async Task ForTriggerFailureOnReady_UseGrpcSubChannel_ChangeToTransientFailureState()
        {
            // Arrange
            var executor = new ExecutorFake();
            var backoffPolicyProvider = new GrpcBackoffPolicyProviderFake(new GrpcBackoffPolicyFake(TimeSpan.Zero));
            var channel = GrpcChannelForSubChannelFake.Get(backoffPolicyProvider);
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            subchannel.Executor = executor;
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            channel.SyncContext.Execute(() => { subchannel.RequestConnection(); });
            subchannel.TriggerSubChannelFailure(new Status(StatusCode.Internal, "test bug"));
            executor.DrainSingleAction();

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            do
            {
                if (observedStateInfos.Count == 5)
                {
                    break;
                }
                await Task.Delay(25); // TriggerSubChannelFailure auto reconnection with a delay
            } while (!timeoutTask.IsCompleted);

            // Assert
            Assert.True(observedStateInfos.Count == 5);
            Assert.Equal(GrpcConnectivityState.CONNECTING, observedStateInfos[0].State);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[1].State);
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, observedStateInfos[2].State);
            Assert.Equal(GrpcConnectivityState.CONNECTING, observedStateInfos[3].State);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[4].State);
            Assert.Empty(executor.Actions);
        }

        [Fact]
        public void ForTriggerSuccessOnReady_UseGrpcSubChannel_NoChangeOccur()
        {
            // Arrange
            var executor = new ExecutorFake();
            var channel = GrpcChannelForSubChannelFake.Get();
            var subChannelArgs = new CreateSubchannelArgs(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty);
            var helper = new HelperFake();
            var subchannel = new GrpcSubChannel(channel, subChannelArgs, helper);
            subchannel.Executor = executor;
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var observer = new BaseSubchannelStateObserver((stateInfo) => observedStateInfos.Add(stateInfo));

            // Act
            channel.SyncContext.Execute(() => { subchannel.Start(observer); });
            channel.SyncContext.Execute(() => { subchannel.RequestConnection(); });
            subchannel.TriggerSubChannelSuccess();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.True(observedStateInfos.Count == 2);
            Assert.Equal(GrpcConnectivityState.CONNECTING, observedStateInfos[0].State);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[1].State);
        }
        #endregion
    }
}
