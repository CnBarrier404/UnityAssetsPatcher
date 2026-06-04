using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Tests;

internal static class TestManifest
{
    public static void Write(string configPath, string patchJson)
    {
        string patchBody = patchJson.Trim()[1..^1].Trim();
        string manifestPatchBody = patchBody.Length == 0
            ? string.Empty
            : $",\n  {patchBody.Replace("\r\n", "\n").Replace("\n", "\n  ")}";

        File.WriteAllText(
            configPath,
            $$"""
              {
                "name": "Test Mod",
                "author": "UnityAssetsPatcher.Tests",
                "version": "1.0.0"{{manifestPatchBody}}
              }
              """);
    }

    public static void WriteZip(string zipPath, string manifestBody, string entryName = "Mod/manifest.json")
    {
        using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        using StreamWriter writer = new(entry.Open());
        writer.Write(
            $$"""
              {
                "name": "Test Mod",
                "author": "UnityAssetsPatcher.Tests",
                "version": "1.0.0",
                {{manifestBody.Trim()[1..^1].Trim().Replace("\r\n", "\n").Replace("\n", "\n  ")}}
              }
              """);
    }
}
