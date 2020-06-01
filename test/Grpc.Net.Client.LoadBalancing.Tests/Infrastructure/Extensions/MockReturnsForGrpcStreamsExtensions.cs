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

using Moq.Language.Flow;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grpc.Net.Client.LoadBalancing.Tests.Infrastructure.Extensions
{
    public static class MockReturnsForGrpcStreamsExtensions
    {
        /// <summary>
        /// This extension method allows Moq to setup list of results. Mock will return those values sequentially.
        /// </summary>
        public static IReturnsResult<TMock> Returns<TMock, TResult>(this ISetup<TMock, TResult> setup, IEnumerable<TResult> valueEnumerable) where TMock : class
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            if (valueEnumerable == null)
            {
                throw new ArgumentNullException(nameof(valueEnumerable));
            }
            return setup.Returns(valueEnumerable.ToArray());
        }

        /// <summary>
        /// This extension method allows Moq to setup list of results. Mock will return those values sequentially.
        /// </summary>
        public static IReturnsResult<TMock> Returns<TMock, TResult>(this ISetup<TMock, TResult> setup, params TResult[] values) where TMock : class
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            var i = 0;
            return setup.Returns(() =>
            {
                if (i == values.Length)
                {
                    throw new InvalidOperationException("Mock reached end of the stream");
                }
                return values[i++];
            });
        }
    }
}
