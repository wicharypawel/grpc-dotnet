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
using Moq;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcHelperExtensionsTests
    {
        [Theory]
        [InlineData("http://service.googleapis.com:80", false)]
        [InlineData("http://service.googleapis.com:9000", false)]
        [InlineData("http://service.googleapis.com:443", true)]
        [InlineData("https://service.googleapis.com:9000", true)]
        [InlineData("https://service.googleapis.com:443", true)]
        [InlineData("dns://service.googleapis.com:443", true)]
        [InlineData("dns://service.googleapis.com:9000", false)]
        public void ForUrlsInHelper_UseHelperIsSecureConnection_VerifyIsSecureResult(string uriString, bool isSecure)
        {
            // Arrange
            var uri = new UriBuilder(uriString).Uri;
            var helperMock = new Mock<IGrpcHelper>();
            helperMock.Setup(x => x.GetAddress()).Returns(uri);
            var helper = helperMock.Object;

            // Act
            var result = GrpcHelperExtensions.IsSecureConnection(helper);

            // Assert
            Assert.Equal(isSecure, result);
        }
    }
}
