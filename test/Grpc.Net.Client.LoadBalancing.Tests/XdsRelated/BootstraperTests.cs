using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated
{
    public sealed class BootstraperTests
    {
        [Fact]
        public void ForSampleBootstrapFile_UseXdsBootstrapper_VerifyCorrectParse()
        {
            // Arrange
            var bootstrapper = XdsBootstrapper.Instance;
            var bootstrapFile = XdsBootstrapFileFactory.GetSampleFile("XdsBootstrapFile1.json");
            
            // Act
            var bootstrapInfo = bootstrapper.ReadBootstrap(bootstrapFile);

            // Assert
            Assert.NotNull(bootstrapInfo);
            Assert.NotNull(bootstrapInfo.Servers);
            Assert.NotNull(bootstrapInfo.Node);
            
            Assert.Single(bootstrapInfo.Servers);
            Assert.Equal("server_uri_test_value", bootstrapInfo.Servers[0].ServerUri);
            Assert.Equal("channel_creds_type_value", bootstrapInfo.Servers[0].ChannelCredsList[0].Type);
            Assert.Equal("channel_creds_config_value1", bootstrapInfo.Servers[0].ChannelCredsList[0].Config["cnfKey"].ToString());
            Assert.Equal("2", bootstrapInfo.Servers[0].ChannelCredsList[0].Config["cnfKey2"].ToString());

            Assert.Equal("node_id_value_adasd123123123", bootstrapInfo.Node.Id);
            Assert.Equal("node_cluster_value_djsdj319", bootstrapInfo.Node.Cluster);

            Assert.Equal("testvalue", bootstrapInfo.Node.Metadata.Fields["testkey"].StringValue);
            Assert.Equal(102, bootstrapInfo.Node.Metadata.Fields["testkey2"].NumberValue);

            Assert.Equal("region_value_123838", bootstrapInfo.Node.Locality.Region);
            Assert.Equal("zone_value_382u140", bootstrapInfo.Node.Locality.Zone);
            Assert.Equal("subzone_value_284719", bootstrapInfo.Node.Locality.SubZone);

            Assert.Equal("grpc-dotnet", bootstrapInfo.Node.UserAgentName);
            Assert.NotEmpty(bootstrapInfo.Node.UserAgentVersion);
            Assert.Contains("envoy.lb.does_not_support_overprovisioning", bootstrapInfo.Node.ClientFeatures);
        }

        [Fact]
        public void ForMissingElementBootstrapFile_UseXdsBootstrapper_ThrowsInvalidOperationException()
        {
            // Arrange
            var bootstrapper = XdsBootstrapper.Instance;
            var bootstrapFile = XdsBootstrapFileFactory.GetSampleFile("XdsBootstrapFile2.json");

            // Act
            var bootstrapInfo = bootstrapper.ReadBootstrap(bootstrapFile);
            
            // Assert
            Assert.Empty(bootstrapInfo.Servers);
        }
    }
}
