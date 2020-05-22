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

using System.Threading;

namespace Grpc.Net.Client.Internal
{
    /// <summary>
    /// A bool value that may be updated atomically.
    /// 
    /// This class was created because dotnet does not support <see cref="Interlocked"/> for bool.
    /// See more: https://github.com/dotnet/runtime/issues/4282
    /// </summary>
    internal sealed class InterlockedBool
    {
        private const int FALSE = 0;
        private const int TRUE = 1;
        private int _value;

        /// <summary>
        /// Creates a new <see cref="InterlockedBool"/> with initial value false.
        /// </summary>
        public InterlockedBool()
        {
            _value = FALSE;
        }

        /// <summary>
        /// Creates a new <see cref="InterlockedBool"/> with the given initial value.
        /// </summary>
        /// <param name="initialValue">Initial value.</param>
        public InterlockedBool(bool initialValue)
        {
            _value = initialValue ? TRUE : FALSE;
        }

        /// <summary>
        /// Atomically sets the value to the given updated value if the current value == the expected value.
        /// Returns true if successful. False return indicates that the actual value was not equal to the expected value.
        /// </summary>
        public bool CompareAndSet(bool expect, bool update)
        {
            var originalValue = Interlocked.CompareExchange(ref _value, update ? TRUE : FALSE, expect ? TRUE : FALSE) == TRUE;
            return originalValue == expect;
        }

        /// <summary>
        /// Returns the current value.
        /// </summary>
        public bool Get()
        {
            return _value == TRUE;
        }

        /// <summary>
        /// Atomically sets to the given value and returns the previous value.
        /// </summary>
        public bool GetAndSet(bool newValue)
        {
            return Interlocked.Exchange(ref _value, newValue ? TRUE : FALSE) == TRUE;
        }

        /// <summary>
        /// Unconditionally sets to the given value.
        /// </summary>
        public void Set(bool newValue)
        {
            GetAndSet(newValue);
        }
    }
}
