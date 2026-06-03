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
}
