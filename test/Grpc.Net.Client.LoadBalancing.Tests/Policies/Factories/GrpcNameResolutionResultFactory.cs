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

using System;
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories
{
    internal static class GrpcHostAddressFactory
    {
        public static List<GrpcHostAddress> GetNameResolution(int serversCount, int port)
        {
            if (serversCount > 9)
            {
                throw new ArgumentException("max count is 9");
            }
            var result = new List<GrpcHostAddress>();
            for (int i = 0; i < serversCount; i++)
            {
                result.Add(new GrpcHostAddress($"10.1.5.21{i}", port));
            }
            return result;
        }

        public static List<GrpcHostAddress> GetNameResolution(int serversCount)
        {
            return GetNameResolution(serversCount, 80);
        }

        public static List<GrpcHostAddress> GetNameResolution(params string[] serverAddresses)
        {
            var result = new List<GrpcHostAddress>();
            foreach (var serverAddress in serverAddresses)
            {
                result.Add(new GrpcHostAddress(serverAddress, 80));
            }
            return result;
        }
    }
}
