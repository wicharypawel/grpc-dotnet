using System;
using System.IO;
using System.Reflection;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories
{
    internal static class XdsBootstrapFileFactory
    {
        private const string BootstrapPathEnvironmentVariable = "GRPC_XDS_BOOTSTRAP";

        public static void SetBootstrapFileEnv(string? fileName = null)
        {
            fileName ??= "XdsBootstrapFile.json";
            var assemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(XdsBootstrapFileFactory))!.Location)
                ?? throw new InvalidOperationException("Assembly location not found");
            var bootstrapFilePath = Path.Combine(assemblyPath, "XdsRelated", "Factories", fileName);
            Environment.SetEnvironmentVariable(BootstrapPathEnvironmentVariable, bootstrapFilePath);
        }

        public static string GetSampleFile(string? fileName = null)
        {
            fileName ??= "XdsBootstrapFile.json";
            var assemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(XdsBootstrapFileFactory))!.Location)
                ?? throw new InvalidOperationException("Assembly location not found");
            var bootstrapFilePath = Path.Combine(assemblyPath, "XdsRelated", "Factories", fileName);
            return File.ReadAllText(bootstrapFilePath, System.Text.Encoding.UTF8);
        }
    }
}
