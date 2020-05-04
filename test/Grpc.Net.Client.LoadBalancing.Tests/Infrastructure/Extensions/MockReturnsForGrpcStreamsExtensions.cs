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
