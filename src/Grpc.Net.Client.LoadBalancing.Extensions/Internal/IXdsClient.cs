using Envoy.Api.V2;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal interface IXdsClient : IDisposable
    {
        /// <param name="resourceName">Resource name needs to be in host or host:port syntax.</param>
        Task<List<Listener>> GetLdsAsync(string resourceName);
        Task<List<RouteConfiguration>> GetRdsAsync(string listenerName);
        Task<List<Cluster>> GetCdsAsync();
        Task<List<ClusterLoadAssignment>> GetEdsAsync(string clusterName);
    }
}
