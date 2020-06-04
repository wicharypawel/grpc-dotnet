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
using System;
using System.Collections.Generic;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class BaseSubchannelStateObserverTests
    {
        [Fact]
        public void ForOnNextValues_UseBaseSubchannelStateObserver_VerifyCallingActionWithValues()
        {
            // Arrange
            var observedStateInfos = new List<GrpcConnectivityStateInfo>();
            var action = new Action<GrpcConnectivityStateInfo>((stateInfo) => observedStateInfos.Add(stateInfo));
            var observer = new BaseSubchannelStateObserver(action);

            // Act
            observer.OnNext(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            observer.OnNext(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            observer.OnNext(GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Internal, "test bug")));
            observer.OnNext(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
            observer.OnNext(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            observer.OnNext(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.SHUTDOWN));

            // Assert
            Assert.NotEmpty(observedStateInfos);
            Assert.True(observedStateInfos.Count == 6);
            Assert.Equal(GrpcConnectivityState.CONNECTING, observedStateInfos[0].State);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[1].State);
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, observedStateInfos[2].State);
            Assert.Equal(GrpcConnectivityState.CONNECTING, observedStateInfos[3].State);
            Assert.Equal(GrpcConnectivityState.READY, observedStateInfos[4].State);
            Assert.Equal(GrpcConnectivityState.SHUTDOWN, observedStateInfos[5].State);
        }
    }
}
