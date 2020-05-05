using static Grpc.Net.Client.LoadBalancing.Extensions.Internal.EnvoyProtoData;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// An IXdsClient interface encapsulates all of the logic for communicating with 
    /// the xDS server. It may create multiple RPC streams (or a single ADS stream) for 
    /// a series of xDS protocols (e.g., LDS, RDS, VHDS, CDS and EDS) over a single channel.
    /// </summary>
    internal interface IXdsClient : IDisposable
    {
        Task<ClusterUpdate> GetCdsAsync(string clusterName, string serviceName); // service name is required only because of legacy reasons, remove in the near future
        Task<EndpointUpdate> GetEdsAsync(string clusterName);

        void Subscribe(string targetAuthority, IConfigUpdateObserver observer);
        void Subscribe(string clusterName, IClusterUpdateObserver observer);
        void Unsubscribe(string clusterName, IClusterUpdateObserver observer);
        void Subscribe(string clusterName, IEndpointUpdateObserver observer);
        void Unsubscribe(string clusterName, IEndpointUpdateObserver observer);
    }

    /// <summary>
    /// Data class containing the results of performing a series of resource discovery RPCs via 
    /// LDS/RDS/VHDS protocols. The results may include configurations for path/host rewriting, 
    /// traffic mirroring, retry or hedging, default timeouts and load balancing policy that will 
    /// be used to generate a service config.
    /// </summary>
    internal sealed class ConfigUpdate
    {
        public IReadOnlyList<Route> Routes { get; }

        public ConfigUpdate(List<Route> routes)
        {
            Routes = routes;
        }
    }

    /// <summary>
    /// Data class containing the results of performing a resource discovery RPC via CDS protocol.
    /// The results include configurations for a single upstream cluster, such as endpoint discovery
    /// type, load balancing policy, connection timeout and etc.
    /// </summary>
    internal sealed class ClusterUpdate
    {
        public string ClusterName { get; }
        public string? EdsServiceName { get; }
        public string LbPolicy { get; }
        public string? LrsServerName { get; }

        public ClusterUpdate(string clusterName, string? edsServiceName, string lbPolicy,
            string? lrsServerName)
        {
            ClusterName = clusterName;
            EdsServiceName = edsServiceName;
            LbPolicy = lbPolicy;
            LrsServerName = lrsServerName;
        }
    }

    /// <summary>
    /// Data class containing the results of performing a resource discovery RPC via EDS protocol.
    /// The results include endpoint addresses running the requested service, as well as
    /// configurations for traffic control such as drop overloads, inter-cluster load balancing
    /// policy and etc.
    /// </summary>
    internal sealed class EndpointUpdate
    {
        public string ClusterName { get; }
        public IReadOnlyDictionary<Locality, LocalityLbEndpoints> LocalityLbEndpoints { get; }
        public IReadOnlyList<DropOverload> DropPolicies { get; }

        public EndpointUpdate(string clusterName,
            Dictionary<Locality, LocalityLbEndpoints> localityLbEndpoints,
            List<DropOverload> dropPolicies)
        {
            ClusterName = clusterName;
            LocalityLbEndpoints = localityLbEndpoints;
            DropPolicies = dropPolicies;
        }
    }

    /// <summary>
    /// An interface for objects that react to changes of <seealso cref="ConfigUpdate"/>.
    /// </summary>
    internal interface IConfigUpdateObserver
    {
        void OnNext(ConfigUpdate value);
        void OnError(Status error);
    }

    /// <summary>
    /// An interface for objects that react to changes of <seealso cref="ClusterUpdate"/>.
    /// </summary>
    internal interface IClusterUpdateObserver
    {
        void OnNext(ClusterUpdate value);
        void OnError(Status error);
    }

    /// <summary>
    /// An interface for objects that react to changes of <seealso cref="EndpointUpdate"/>.
    /// </summary>
    internal interface IEndpointUpdateObserver
    {
        void OnNext(EndpointUpdate value);
        void OnError(Status error);
    }
}
