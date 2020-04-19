using System;
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories
{
    internal static class GrpcHostAddressFactory
    {
        public static List<GrpcHostAddress> GetNameResolution(int balancersCount, int serversCount)
        {
            if (balancersCount > 9 || serversCount > 9)
            {
                throw new ArgumentException("max count is 9");
            }
            var result = new List<GrpcHostAddress>();
            for (int i = 0; i < balancersCount; i++)
            {
                result.Add(new GrpcHostAddress($"10.1.6.12{i}", 80) { IsLoadBalancer = true });
            }
            for (int i = 0; i < serversCount; i++)
            {
                result.Add(new GrpcHostAddress($"10.1.5.21{i}", 80) { IsLoadBalancer = false });
            }
            return result;
        }
    }
}
