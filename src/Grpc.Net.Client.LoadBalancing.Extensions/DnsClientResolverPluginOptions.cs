using System;
using System.Net;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    /// <summary>
    /// An options class for configuring a <see cref="DnsClientResolverPlugin"/>.
    /// </summary>
    public sealed class DnsClientResolverPluginOptions
    {
        /// <summary>
        /// Allows override dns nameservers used during lookup. Default value is an empty list.
        /// If an empty list is specified client defaults to machine list of nameservers.
        /// </summary>
        public IPEndPoint[] NameServers { get; set; }
        
        /// <summary>
        /// Allows enabling TXT records lookup for service config. Default value false.
        /// </summary>
        public bool EnableTxtServiceConfig { get; set; }

        /// <summary>
        /// Allows enabling SRV records lookup for grpclb. Default value false.
        /// </summary>
        public bool EnableSrvGrpclb { get; set; }

        /// <summary>
        /// Creates a <seealso cref="DnsClientResolverPluginOptions"/> options class with default values.
        /// </summary>
        public DnsClientResolverPluginOptions()
        {
            NameServers = Array.Empty<IPEndPoint>();
            EnableTxtServiceConfig = false;
            EnableSrvGrpclb = false;
        }
    }
}
