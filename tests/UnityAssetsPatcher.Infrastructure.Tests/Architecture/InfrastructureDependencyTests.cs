using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using UnityAssetsPatcher.Infrastructure.Mods;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Architecture;

public sealed class InfrastructureDependencyTests
{
    [Fact]
    public void InfrastructureAssembly_WhenInspected_DoesNotReferenceApplicationOperationOrModErrorTypes()
    {
        string assemblyPath = typeof(ZipModArchiveReader).Assembly.Location;
        using FileStream assemblyStream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(assemblyStream);
        MetadataReader metadata = peReader.GetMetadataReader();
        HashSet<string> referencedTypes = metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(reference => $"{metadata.GetString(reference.Namespace)}.{metadata.GetString(reference.Name)}")
            .ToHashSet(StringComparer.Ordinal);
        string[] forbiddenTypes = referencedTypes
            .Where(type =>
                type is "UnityAssetsPatcher.Application.Operations.OperationResult`1" or
                    "UnityAssetsPatcher.Application.Operations.OperationError" ||
                type.StartsWith("UnityAssetsPatcher.Application.Mods.", StringComparison.Ordinal) &&
                type.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(forbiddenTypes);
    }
}
