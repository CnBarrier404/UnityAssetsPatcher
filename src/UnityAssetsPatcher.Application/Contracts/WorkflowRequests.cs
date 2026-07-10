namespace UnityAssetsPatcher.Application.Contracts;

public sealed record UninstallModRequest(string InstallDirectory, string GameDirectory);

public sealed record UninstallPreviewRequest(string InstallDirectory, string? GameDirectory = null);
