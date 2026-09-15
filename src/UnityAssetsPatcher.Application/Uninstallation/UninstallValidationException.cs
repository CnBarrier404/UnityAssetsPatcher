namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed class UninstallValidationException(string message) : InvalidOperationException(message);
