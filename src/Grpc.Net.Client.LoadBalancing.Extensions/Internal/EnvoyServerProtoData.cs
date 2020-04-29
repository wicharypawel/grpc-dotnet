using System;
using System.Collections.Generic;
using System.Linq;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// This class wraps re-implementation of envoy types. Types are reimplemented 
    /// to avoid building implementation about elements that can change. Moreover 
    /// reimplemented types are much smaller in size as they do not store unused fields.
    /// </summary>
    internal static class EnvoyServerProtoData
    {
        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Core.CidrRange"/>
        /// </summary>
        public sealed class CidrRange
        {
            public string AddressPrefix { get; }
            public int PrefixLen { get; }
            
            public CidrRange(string addressPrefix, int prefixLen)
            {
                AddressPrefix = addressPrefix;
                PrefixLen = prefixLen;
            }

            public static CidrRange FromEnvoyProtoCidrRange(Envoy.Api.V2.Core.CidrRange cidrRange)
            {
                return new CidrRange(cidrRange.AddressPrefix, Convert.ToInt32(cidrRange.PrefixLen.GetValueOrDefault()));
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.ListenerNS.FilterChainMatch"/>
        /// </summary>
        public sealed class FilterChainMatch
        {
            public int DestinationPort { get; }
            public IReadOnlyList<CidrRange> PrefixRanges { get; }
            public IReadOnlyList<string> ApplicationProtocols { get; }
            
            public FilterChainMatch(int destinationPort, List<CidrRange> prefixRanges, List<string> applicationProtocols)
            {
                DestinationPort = destinationPort;
                PrefixRanges = prefixRanges;
                ApplicationProtocols = applicationProtocols;
            }

            public static FilterChainMatch FromEnvoyProtoFilterChainMatch(Envoy.Api.V2.ListenerNS.FilterChainMatch filterChainMatch)
            {
                var prefixRanges = filterChainMatch.PrefixRanges.Select(CidrRange.FromEnvoyProtoCidrRange).ToList();
                var applicationProtocols = filterChainMatch.ApplicationProtocols.ToList();
                return new FilterChainMatch(Convert.ToInt32(filterChainMatch.DestinationPort.GetValueOrDefault()), prefixRanges, applicationProtocols);
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.ListenerNS.FilterChain"/>
        /// </summary>
        public sealed class FilterChain
        {
            public FilterChainMatch FilterChainMatch { get; }

            public FilterChain(FilterChainMatch filterChainMatch)
            {
                FilterChainMatch = filterChainMatch;
            }

            public static FilterChain FromEnvoyProtoFilterChain(Envoy.Api.V2.ListenerNS.FilterChain filterChain)
            {
                return new FilterChain(FilterChainMatch.FromEnvoyProtoFilterChainMatch(filterChain.FilterChainMatch));
            }
        }

        /// <summary>
        /// See corresponding Envoy proto message <seealso cref="Envoy.Api.V2.Listener"/>
        /// </summary>
        public sealed class Listener
        {
            public string Name { get; }
            public string? Address { get; }
            public IReadOnlyList<FilterChain> FilterChains { get; }

            public Listener(string name, string? address, List<FilterChain> filterChains)
            {
                Name = name;
                Address = address;
                FilterChains = filterChains;
            }

            private static string? ConvertEnvoyAddressToString(Envoy.Api.V2.Core.Address proto)
            {
                if (proto.SocketAddress != null)
                {
                    var socketAddress = proto.SocketAddress;
                    string address = socketAddress.Address;
                    switch (socketAddress.PortSpecifierCase)
                    {
                        case Envoy.Api.V2.Core.SocketAddress.PortSpecifierOneofCase.NamedPort:
                            return address + ":" + socketAddress.NamedPort;
                        case Envoy.Api.V2.Core.SocketAddress.PortSpecifierOneofCase.PortValue:
                            return address + ":" + socketAddress.PortValue;
                        default:
                            return address;
                    }

                }
                return null;
            }

            public static Listener FromEnvoyProtoListener(Envoy.Api.V2.Listener listener)
            {
                return new Listener(listener.Name, ConvertEnvoyAddressToString(listener.Address), 
                    listener.FilterChains.Select(FilterChain.FromEnvoyProtoFilterChain).ToList());
            }
        }
    }
}
