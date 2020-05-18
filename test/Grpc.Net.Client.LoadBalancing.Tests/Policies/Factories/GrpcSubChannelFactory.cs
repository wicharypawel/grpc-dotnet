﻿using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using System.Collections.Generic;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories
{
    internal static class GrpcSubChannelFactory
    {
        public static List<IGrpcSubChannel> GetSubChannelsWithoutLoadBalanceTokens()
        {
            return new List<IGrpcSubChannel>()
            {
                new GrpcSubChannelFake(new UriBuilder("http://10.1.5.210:80").Uri, GrpcAttributes.Empty),
                new GrpcSubChannelFake(new UriBuilder("http://10.1.5.212:80").Uri, GrpcAttributes.Empty),
                new GrpcSubChannelFake(new UriBuilder("http://10.1.5.211:80").Uri, GrpcAttributes.Empty),
                new GrpcSubChannelFake(new UriBuilder("http://10.1.5.213:80").Uri, GrpcAttributes.Empty)
            };
        }
    }
}
