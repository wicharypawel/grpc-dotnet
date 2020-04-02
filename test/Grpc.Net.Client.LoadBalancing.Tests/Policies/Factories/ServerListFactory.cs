using Google.Protobuf;
using Grpc.Lb.V1;
using System.Net;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories
{
    internal static class ServerListFactory
    {
        public static ServerList GetSampleServerList()
        {
            var serverList = new ServerList();
            serverList.Servers.Add(new Server()
            {
                IpAddress = ByteString.CopyFrom(IPAddress.Parse("10.1.5.210").GetAddressBytes()),
                Port = 80
            });
            serverList.Servers.Add(new Server()
            {
                IpAddress = ByteString.CopyFrom(IPAddress.Parse("10.1.5.211").GetAddressBytes()),
                Port = 80
            });
            serverList.Servers.Add(new Server()
            {
                IpAddress = ByteString.CopyFrom(IPAddress.Parse("10.1.5.212").GetAddressBytes()),
                Port = 80
            });
            return serverList;
        }
    }
}
