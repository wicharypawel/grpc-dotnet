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

using Grpc.Net.Client.Internal;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class InterlockedBoolTests
    {
        [Fact]
        public void ForVerifyInitialization_UsingInterlockedBool()
        {
            // Arrange
            // Act
            // Assert
            Assert.False(new InterlockedBool().Get());
            Assert.True(new InterlockedBool(true).Get());
            Assert.False(new InterlockedBool(false).Get());
        }

        [Fact]
        public void ForVerifySetValue_UsingInterlockedBool()
        {
            // Arrange
            var value = new InterlockedBool(false);
            
            // Act
            value.Set(true);
            Assert.True(value.Get());
            value.Set(false);

            // Assert
            Assert.False(value.Get());
        }

        [Fact]
        public void ForVerifyGetAndSet_UsingInterlockedBool()
        {
            // Arrange
            var value = new InterlockedBool(false);

            // Act
            var previusValue = value.GetAndSet(true);

            // Assert
            Assert.False(previusValue);
            Assert.True(value.Get());
        }

        [Theory]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(true, true, false, false)]
        public void ForVerifyCompareAndSet_UsingInterlockedBool(bool expect, bool update, bool changed, bool final)
        {
            // Arrange
            var value = new InterlockedBool(false);

            // Act
            var changeSuccess = value.CompareAndSet(expect, update);

            // Assert
            Assert.Equal(changed, changeSuccess);
            Assert.Equal(final, value.Get());
        }
    }
}
