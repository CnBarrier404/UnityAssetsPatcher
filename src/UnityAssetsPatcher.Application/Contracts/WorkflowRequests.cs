namespace UnityAssetsPatcher.Application.Contracts;

public sealed record UninstallModRequest(string InstallDirectory);

public sealed record UninstallPreviewRequest(string InstallDirectory);
