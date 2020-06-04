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
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcHelperTests
    {
        [Fact]
        public void ForAddressAndAlwaysIncludePort_UseGrpcHelperGetAuthorityCore_VerifyReturnHostNameAndPort()
        {
            // Arrange
            // Act
            // Assert
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com:80").Uri, true));
            Assert.Equal("google.apis.com:8080", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com:8080").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("http://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("https://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("https://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:8080", GrpcHelper.GetAuthorityCore(new UriBuilder("https://google.apis.com:8080").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com:80").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:9000", GrpcHelper.GetAuthorityCore(new UriBuilder("dns://google.apis.com:9000").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com").Uri, true));
            Assert.Equal("google.apis.com:80", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com:80").Uri, true));
            Assert.Equal("google.apis.com:443", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com:443").Uri, true));
            Assert.Equal("google.apis.com:9000", GrpcHelper.GetAuthorityCore(new UriBuilder("xds://google.apis.com:9000").Uri, true));
        }
    }
}
