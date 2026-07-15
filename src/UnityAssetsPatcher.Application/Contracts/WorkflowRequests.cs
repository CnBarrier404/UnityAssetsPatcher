namespace UnityAssetsPatcher.Application.Contracts;

public sealed record UninstallModRequest(string InstallId, string? GameDirectory = null);

public sealed record UninstallPreviewRequest(string InstallId, string? GameDirectory = null);
