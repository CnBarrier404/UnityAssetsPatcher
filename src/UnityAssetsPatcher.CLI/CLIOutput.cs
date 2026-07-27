using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.CLI;

internal static class CLIOutput
{
    public static int WriteSuccess(
        ParseResult parseResult,
        CLIOptions options,
        string command,
        JsonObject data,
        Action<TextWriter> writeText)
    {
        if (parseResult.GetValue(options.Format) == CLIOutputFormat.Json)
        {
            WriteJson(parseResult.InvocationConfiguration.Output, new JsonObject
            {
                ["schemaVersion"] = 1,
                ["success"] = true,
                ["command"] = command,
                ["data"] = data,
            });
        }
        else
        {
            writeText(parseResult.InvocationConfiguration.Output);
        }

        return 0;
    }

    public static int WriteResult<T>(
        ParseResult parseResult,
        CLIOptions options,
        string command,
        OperationResult<T> result,
        Func<T, JsonObject> createJson,
        Action<TextWriter, T> writeText)
    {
        return result switch
        {
            OperationSucceeded<T> succeeded => WriteSuccess(
                parseResult,
                options,
                command,
                createJson(succeeded.Value),
                output => writeText(output, succeeded.Value)),
            OperationFailed<T> failed => WriteFailure(parseResult, options, command, failed.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    public static int WriteFailure(
        ParseResult parseResult,
        CLIOptions options,
        string command,
        OperationError error)
    {
        TextWriter output = parseResult.InvocationConfiguration.Error;
        string code = ErrorCode(error.Code);
        string message = OperationErrorText.Format(error);

        if (parseResult.GetValue(options.Format) == CLIOutputFormat.Json)
        {
            JsonObject envelope = ErrorEnvelope(command, code, message, []);
            envelope["error"]!["parameters"] = new JsonObject(
                error.Parameters.Select(parameter =>
                    KeyValuePair.Create<string, JsonNode?>(parameter.Key, parameter.Value)));
            if (error.Recovery is not null)
            {
                envelope["recovery"] = Recovery(error.Recovery);
            }

            WriteJson(output, envelope);
        }
        else
        {
            output.WriteLine($"Error [{code}]: {message}");
            if (error.Recovery is not null)
            {
                WriteRecoveryText(output, error.Recovery);
            }
        }

        return 1;
    }

    public static int WriteFailure(
        ParseResult parseResult,
        CLIOptions options,
        string command,
        Exception exception)
    {
        TextWriter error = parseResult.InvocationConfiguration.Error;

        if (parseResult.GetValue(options.Format) == CLIOutputFormat.Json)
        {
            JsonObject envelope = ErrorEnvelope(command, "command_failed", exception.Message, Flatten(exception));
            BackupRecoveryException? recovery = EnumerateExceptions(exception).OfType<BackupRecoveryException>()
                .FirstOrDefault();
            if (recovery is not null) envelope["recovery"] = Recovery(recovery.Recovery);
            WriteJson(error, envelope);
        }
        else
        {
            BackupRecoveryException? recovery = EnumerateExceptions(exception).OfType<BackupRecoveryException>()
                .FirstOrDefault();
            if (recovery is not null) WriteRecoveryText(error, recovery.Recovery);
            WriteException(error, exception);
        }

        return 1;
    }

    public static void WriteUsageFailure(TextWriter error, string command, IEnumerable<string> messages)
    {
        string[] details = messages.ToArray();
        WriteJson(error, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["success"] = false,
            ["command"] = command,
            ["error"] = new JsonObject
            {
                ["code"] = "usage_error",
                ["message"] = details.FirstOrDefault() ?? "Invalid command-line arguments.",
                ["causes"] =
                    new JsonArray(details.Skip(1).Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            },
        });
    }

    public static JsonObject ManifestSummary(string path, ModManifest manifest)
    {
        return new JsonObject
        {
            ["configPath"] = Path.GetFullPath(path),
            ["schemaVersion"] = manifest.SchemaVersion,
            ["name"] = manifest.Name,
            ["author"] = manifest.Author,
            ["version"] = manifest.Version,
            ["description"] = manifest.Description,
            ["game"] = manifest.Game,
            ["optionalGroups"] = new JsonArray(manifest.Optional.Select(group => new JsonObject
            {
                ["name"] = group.Name,
                ["description"] = group.Description,
            }).ToArray<JsonNode?>()),
        };
    }

    public static JsonObject InspectList(string path, InspectListResult result)
    {
        return new JsonObject
        {
            ["assetsFilePath"] = path,
            ["totalCount"] = result.TotalCount,
            ["returnedCount"] = result.Assets.Count,
            ["assets"] = new JsonArray(result.Assets.Select(asset => new JsonObject
            {
                ["pathId"] = asset.PathId,
                ["typeName"] = asset.TypeName,
                ["name"] = asset.Name,
            }).ToArray<JsonNode?>()),
        };
    }

    public static JsonObject InspectFields(string path, long pathId, AssetField fieldTree)
    {
        return new JsonObject
        {
            ["assetsFilePath"] = path,
            ["pathId"] = pathId,
            ["fieldTree"] = InspectField(fieldTree),
        };
    }

    public static void WriteInspectListText(TextWriter output, InspectListResult result)
    {
        output.WriteLine("Path ID\tType Name\tName");
        foreach (InspectAssetSummary asset in result.Assets)
        {
            output.WriteLine(
                $"{asset.PathId.ToString(System.Globalization.CultureInfo.InvariantCulture)}\t{asset.TypeName}\t{asset.Name}");
        }

        if (result.Assets.Count < result.TotalCount)
        {
            output.WriteLine();
            output.WriteLine($"Showing {result.Assets.Count} of {result.TotalCount} assets.");
        }
    }

    public static void WriteInspectFieldsText(TextWriter output, AssetField fieldTree)
    {
        WriteInspectFieldText(output, fieldTree, 0);
    }

    private static JsonObject InspectField(AssetField field)
    {
        return new JsonObject
        {
            ["name"] = field.Name,
            ["typeName"] = field.TypeName,
            ["value"] = field.Value?.ToInvariantString(),
            ["children"] = new JsonArray(field.Children.Select(InspectField).ToArray<JsonNode?>()),
        };
    }

    private static void WriteInspectFieldText(TextWriter output, AssetField field, int depth)
    {
        string value = field.Value is null ? string.Empty : $": {field.Value.ToInvariantString()}";
        output.WriteLine($"{new string(' ', depth * 2)}{field.Name} ({field.TypeName}){value}");

        foreach (AssetField child in field.Children)
        {
            WriteInspectFieldText(output, child, depth + 1);
        }
    }

    public static JsonObject InstallPreview(InstallPreviewResult result)
    {
        return new JsonObject
        {
            ["mod"] = Mod(result.ModName, result.ModVersion, result.ModAuthor),
            ["changes"] = Changes(result.Changes),
            ["optionalGroups"] = new JsonArray(result.OptionalGroups.Select(group => new JsonObject
            {
                ["name"] = group.Name,
                ["description"] = group.Description,
            }).ToArray<JsonNode?>()),
            ["timing"] = Timing(result.Timing),
        };
    }

    public static JsonObject InstallResult(InstallModResult result)
    {
        return new JsonObject
        {
            ["installId"] = result.InstallId,
            ["mod"] = Mod(result.ModName, result.ModVersion),
            ["changes"] = Changes(result.Changes),
            ["optionalGroups"] =
                new JsonArray(result.OptionalGroups.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["timing"] = Timing(result.Timing),
            ["recovery"] = Recovery(result.Recovery),
        };
    }

    public static JsonObject InstalledMods(IReadOnlyList<InstallRecordSummary> installed)
    {
        return new JsonObject
        {
            ["mods"] = new JsonArray(installed.Select(record => new JsonObject
            {
                ["installId"] = record.InstallId,
                ["name"] = record.ModName,
                ["version"] = record.ModVersion,
                ["game"] = record.GameName,
                ["installedAt"] = record.InstalledAt.ToString("O"),
            }).ToArray<JsonNode?>()),
        };
    }

    public static JsonObject UninstallPreview(UninstallPreviewResult result)
    {
        return new JsonObject
        {
            ["installId"] = result.InstallId,
            ["mod"] = Mod(result.ModName, result.ModVersion),
            ["installedAt"] = result.InstalledAt.ToString("O"),
            ["gameDirectory"] = result.GameDirectory,
            ["canUninstall"] = result.CanUninstall,
            ["blockingMods"] = new JsonArray(result.BlockingMods.Select(mod => new JsonObject
            {
                ["name"] = mod.ModName,
                ["version"] = mod.ModVersion,
                ["installedAt"] = mod.InstalledAt.ToString("O"),
                ["overlappingAssetsFiles"] = new JsonArray(
                    mod.OverlappingAssetsFiles.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            }).ToArray<JsonNode?>()),
            ["restoredFiles"] = new JsonArray(result.RestoredFiles.Select(file => new JsonObject
            {
                ["target"] = file.Target,
                ["targetStatus"] = EnumName(file.TargetStatus),
                ["backupStatus"] = EnumName(file.BackupStatus),
            }).ToArray<JsonNode?>()),
            ["deletedFiles"] = new JsonArray(result.DeletedFiles.Select(file => new JsonObject
            {
                ["destinationPath"] = file.DestinationPath,
                ["status"] = EnumName(file.Status),
            }).ToArray<JsonNode?>()),
        };
    }

    public static JsonObject UninstallResult(UninstallModResult result)
    {
        return new JsonObject
        {
            ["installId"] = result.InstallId,
            ["mod"] = Mod(result.ModName, result.ModVersion),
            ["restoredFiles"] = new JsonArray(result.RestoredFiles.Select(file => new JsonObject
            {
                ["target"] = file.Target,
                ["assetsFilePath"] = file.AssetsFilePath,
            }).ToArray<JsonNode?>()),
            ["deletedFiles"] = new JsonArray(result.DeletedFiles.Select(file => new JsonObject
            {
                ["destinationPath"] = file.DestinationPath,
                ["deleted"] = file.Deleted,
            }).ToArray<JsonNode?>()),
            ["recovery"] = Recovery(result.Recovery),
        };
    }

    public static void WriteInstallPreviewText(TextWriter output, InstallPreviewResult result)
    {
        output.WriteLine($"Preview: {result.ModName} {result.ModVersion} by {result.ModAuthor}");
        WriteChanges(output, result.Changes, preview: true);

        if (result.OptionalGroups.Count > 0)
        {
            output.WriteLine("Optional groups:");
            foreach ((string name, string? description) in result.OptionalGroups)
            {
                output.WriteLine(description is null ? $"- {name}" : $"- {name}: {description}");
            }
        }
    }

    public static void WriteInstallResultText(TextWriter output, InstallModResult result)
    {
        WriteRecoveryText(output, result.Recovery);
        output.WriteLine($"Installed: {result.ModName} {result.ModVersion}");
        output.WriteLine($"Install ID: {result.InstallId}");
        WriteChanges(output, result.Changes, preview: false);
    }

    public static void WriteInstalledModsText(TextWriter output, IReadOnlyList<InstallRecordSummary> installed)
    {
        if (installed.Count == 0)
        {
            output.WriteLine("No installed mods.");
            return;
        }

        foreach (InstallRecordSummary record in installed)
        {
            string game = record.GameName is null ? string.Empty : $" | {record.GameName}";
            output.WriteLine(
                $"{record.InstallId} | {record.ModName} {record.ModVersion}{game} | {record.InstalledAt:O}");
        }
    }

    public static void WriteUninstallPreviewText(TextWriter output, UninstallPreviewResult result)
    {
        output.WriteLine($"Preview uninstall: {result.ModName} {result.ModVersion}");
        output.WriteLine($"Install ID: {result.InstallId}");
        output.WriteLine($"Game directory: {result.GameDirectory}");
        output.WriteLine($"Can uninstall: {result.CanUninstall}");

        foreach (UninstallPreviewRestoredFileResult file in result.RestoredFiles)
        {
            output.WriteLine(
                $"- restore {file.Target}: target={EnumName(file.TargetStatus)}, backup={EnumName(file.BackupStatus)}");
        }

        foreach (UninstallPreviewDeletedFileResult file in result.DeletedFiles)
        {
            output.WriteLine($"- delete {file.DestinationPath}: {EnumName(file.Status)}");
        }

        foreach (UninstallBlockingModResult blocker in result.BlockingMods)
        {
            output.WriteLine($"- blocked by {blocker.ModName} {blocker.ModVersion}: " +
                             string.Join(", ", blocker.OverlappingAssetsFiles));
        }
    }

    public static void WriteUninstallResultText(TextWriter output, UninstallModResult result)
    {
        WriteRecoveryText(output, result.Recovery);
        output.WriteLine($"Uninstalled: {result.ModName} {result.ModVersion}");
        output.WriteLine($"Install ID: {result.InstallId}");
        output.WriteLine($"Restored files: {result.RestoredFiles.Count}");
        output.WriteLine($"Deleted files: {result.DeletedFiles.Count(file => file.Deleted)}");
    }

    private static JsonObject ErrorEnvelope(
        string command,
        string code,
        string message,
        IEnumerable<JsonNode?> causes)
    {
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["success"] = false,
            ["command"] = command,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
                ["causes"] = new JsonArray(causes.ToArray()),
            },
        };
    }

    private static IEnumerable<JsonNode?> Flatten(Exception exception)
    {
        foreach (Exception current in EnumerateExceptions(exception).Skip(1))
        {
            yield return new JsonObject
            {
                ["type"] = current.GetType().Name,
                ["message"] = current.Message,
            };
        }
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        yield return exception;

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                foreach (Exception nested in EnumerateExceptions(inner))
                {
                    yield return nested;
                }
            }
        }
        else if (exception.InnerException is { } inner)
        {
            foreach (Exception nested in EnumerateExceptions(inner))
            {
                yield return nested;
            }
        }
    }

    private static void WriteException(TextWriter error, Exception exception)
    {
        bool first = true;
        foreach (Exception current in EnumerateExceptions(exception))
        {
            error.WriteLine($"{(first ? string.Empty : "Caused by ")}{current.GetType().Name}: {current.Message}");
            first = false;
        }
    }

    private static void WriteJson(TextWriter output, JsonObject value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            value.WriteTo(writer);
        }

        output.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static JsonObject Mod(string name, string version, string? author = null)
    {
        var result = new JsonObject
        {
            ["name"] = name,
            ["version"] = version,
        };

        if (author is not null)
        {
            result["author"] = author;
        }

        return result;
    }

    private static JsonArray Changes(IReadOnlyList<InstallChange> changes)
    {
        return new JsonArray(changes.Select(change =>
        {
            var json = new JsonObject
            {
                ["kind"] = EnumName(change.Kind),
                ["name"] = change.Name,
                ["path"] = change.Path,
                ["backupPath"] = change.BackupPath,
                ["assetCount"] = change.AssetCount,
                ["operationCount"] = change.OperationCount,
            };

            if (change.Preview is not null)
            {
                if (change.Preview.Diagnostic is { } diagnostic)
                {
                    json["diagnostic"] = new JsonObject
                    {
                        ["code"] = EnumName(diagnostic.Code),
                        ["assetsFilePath"] = diagnostic.AssetsFilePath,
                        ["pathId"] = diagnostic.PathId,
                        ["fieldPath"] = diagnostic.FieldPath,
                        ["expected"] = diagnostic.Expected,
                        ["actual"] = diagnostic.Actual,
                        ["detail"] = diagnostic.Detail,
                    };
                }

                json["assets"] = new JsonArray(change.Preview.Assets.Select(asset => new JsonObject
                {
                    ["pathId"] = asset.Asset.PathId,
                    ["typeName"] = asset.Asset.TypeName,
                    ["operations"] = new JsonArray(asset.Operations.Select(operation => new JsonObject
                    {
                        ["path"] = operation.Path,
                        ["oldValue"] = operation.OldValue,
                        ["from"] = operation.FromText,
                        ["to"] = operation.ToText,
                        ["willChange"] = operation.WillChange,
                    }).ToArray<JsonNode?>()),
                }).ToArray<JsonNode?>());
            }

            return json;
        }).ToArray<JsonNode?>());
    }

    private static JsonObject Timing(TimingSnapshot timing)
    {
        return new JsonObject
        {
            ["elapsedMilliseconds"] = timing.Elapsed.TotalMilliseconds,
            ["steps"] = new JsonArray(timing.Steps.Select(step => new JsonObject
            {
                ["name"] = step.Name,
                ["elapsedMilliseconds"] = step.Elapsed.TotalMilliseconds,
            }).ToArray<JsonNode?>()),
        };
    }

    public static JsonObject RecoveryPreview(BackupRecoveryPreview preview)
    {
        return new JsonObject
        {
            ["status"] = EnumName(preview.Status),
            ["gameDirectory"] = preview.GameDirectory,
            ["kind"] = preview.Kind,
            ["installId"] = preview.InstallId,
            ["action"] = preview.Action is null ? null : EnumName(preview.Action.Value),
            ["canRecover"] = preview.CanRecover,
            ["files"] = new JsonArray(preview.Files.Select(file => new JsonObject
            {
                ["relativePath"] = file.RelativePath,
                ["action"] = EnumName(file.Action),
            }).ToArray<JsonNode?>()),
            ["issues"] = RecoveryIssues(preview.Issues),
        };
    }

    public static JsonObject RecoveryReport(BackupRecoveryReport recovery) => Recovery(recovery);

    public static void WriteRecoveryPreviewText(TextWriter output, BackupRecoveryPreview preview)
    {
        output.WriteLine($"Recovery preview: {EnumName(preview.Status)}");
        if (preview.GameDirectory is not null) output.WriteLine($"Game directory: {preview.GameDirectory}");
        if (preview.Kind is not null) output.WriteLine($"Transaction: {preview.Kind} {preview.InstallId}");
        if (preview.Action is not null) output.WriteLine($"Action: {EnumName(preview.Action.Value)}");
        foreach (BackupRecoveryFileChange file in preview.Files)
            output.WriteLine($"- {EnumName(file.Action)}: {file.RelativePath}");
        foreach (BackupRecoveryIssue issue in preview.Issues)
            output.WriteLine($"- {RecoveryIssueCode(issue.Code)}: {RecoveryIssueText(issue)} ({issue.Path})");
    }

    public static void WriteRecoveryReportText(TextWriter output, BackupRecoveryReport recovery) =>
        WriteRecoveryText(output, recovery);

    private static JsonObject Recovery(BackupRecoveryReport recovery)
    {
        return new JsonObject
        {
            ["status"] = EnumName(recovery.Status),
            ["operations"] = new JsonArray(recovery.Operations.Select(operation => new JsonObject
            {
                ["kind"] = operation.Kind,
                ["installId"] = operation.InstallId,
                ["action"] = operation.Action,
            }).ToArray<JsonNode?>()),
            ["issues"] = RecoveryIssues(recovery.Issues),
        };
    }

    private static JsonArray RecoveryIssues(IEnumerable<BackupRecoveryIssue> issues) =>
        new(issues.Select(issue => new JsonObject
        {
            ["code"] = RecoveryIssueCode(issue.Code),
            ["message"] = RecoveryIssueText(issue),
            ["path"] = issue.Path,
            ["parameters"] = new JsonObject(issue.Parameters.Select(parameter =>
                KeyValuePair.Create<string, JsonNode?>(parameter.Key, parameter.Value))),
        }).ToArray<JsonNode?>());

    private static void WriteRecoveryText(TextWriter output, BackupRecoveryReport recovery)
    {
        if (recovery.Status == BackupRepositoryStatus.Clean) return;
        output.WriteLine($"Backup recovery: {EnumName(recovery.Status)}");
        foreach (BackupRecoveryOperation operation in recovery.Operations)
            output.WriteLine($"- {operation.Kind} {operation.InstallId}: {operation.Action}");
        foreach (BackupRecoveryIssue issue in recovery.Issues)
            output.WriteLine($"- {RecoveryIssueCode(issue.Code)}: {RecoveryIssueText(issue)} ({issue.Path})");
    }

    private static void WriteChanges(TextWriter output, IReadOnlyList<InstallChange> changes, bool preview)
    {
        foreach (InstallChange change in changes)
        {
            output.WriteLine($"- {EnumName(change.Kind)} {change.Name}: {change.Path}");
            if (preview && change.Preview is not null)
            {
                if (change.Preview.Diagnostic is { } diagnostic)
                {
                    output.WriteLine($"  - planning failed [{EnumName(diagnostic.Code)}]: {diagnostic.Detail}");
                }

                foreach (PatchPreviewAssetResult asset in change.Preview.Assets)
                {
                    output.WriteLine($"  Path ID {asset.Asset.PathId} ({asset.Asset.TypeName})");
                    foreach (PatchPreviewOperationResult operation in asset.Operations)
                    {
                        string result = operation.WillChange
                            ? $"{operation.OldValue} -> {operation.ToText}"
                            : $"skipped; expected {operation.FromText}, found {operation.OldValue}";
                        output.WriteLine($"  - {operation.Path}: {result}");
                    }
                }
            }
        }
    }

    private static string EnumName<T>(T value) where T : struct, Enum
    {
        string name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string ErrorCode(OperationErrorCode code)
    {
        return code switch
        {
            OperationErrorCode.FileNotFound => "file_not_found",
            OperationErrorCode.DirectoryNotFound => "directory_not_found",
            OperationErrorCode.AccessDenied => "access_denied",
            OperationErrorCode.FileSystemFailure => "file_system_failure",
            OperationErrorCode.InvalidManifest => "invalid_manifest",
            OperationErrorCode.UnsupportedManifestVersion => "unsupported_manifest_version",
            OperationErrorCode.InvalidModPackage => "invalid_mod_package",
            OperationErrorCode.GameDirectoryRequired => "game_directory_required",
            OperationErrorCode.GameDirectoryNotFound => "game_directory_not_found",
            OperationErrorCode.AssetNotFound => "asset_not_found",
            OperationErrorCode.PatchPlanningFailed => "patch_planning_failed",
            OperationErrorCode.InstallRecordNotFound => "install_record_not_found",
            OperationErrorCode.FileIntegrityMismatch => "file_integrity_mismatch",
            OperationErrorCode.OperationAlreadyRunning => "operation_already_running",
            OperationErrorCode.RecoveryRequired => "recovery_required",
            OperationErrorCode.BackupRepositoryUnsafe => "backup_repository_unsafe",
            OperationErrorCode.UnsupportedBackupRepositoryVersion => "unsupported_backup_repository_version",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
        };
    }

    private static string RecoveryIssueCode(BackupRecoveryIssueCode code)
    {
        return code switch
        {
            BackupRecoveryIssueCode.RepositoryUnsafe => "repository_unsafe",
            BackupRecoveryIssueCode.RecoveryUnsafe => "recovery_unsafe",
            BackupRecoveryIssueCode.OperationFailed => "operation_failed",
            BackupRecoveryIssueCode.UnexpectedFailure => "unexpected_failure",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
        };
    }

    private static string RecoveryIssueText(BackupRecoveryIssue issue)
    {
        if (issue.Parameters.GetValueOrDefault("detail") is { Length: > 0 } detail)
        {
            return detail;
        }

        return issue.Code switch
        {
            BackupRecoveryIssueCode.RepositoryUnsafe => "The backup repository is damaged or unsafe.",
            BackupRecoveryIssueCode.RecoveryUnsafe => "Recovery cannot continue safely.",
            BackupRecoveryIssueCode.OperationFailed => "The recovery operation failed.",
            BackupRecoveryIssueCode.UnexpectedFailure => "An unexpected recovery failure occurred.",
            _ => "Recovery failed.",
        };
    }
}

internal static class OperationErrorText
{
    public static string Format(OperationError error)
    {
        string context = error.Parameters.GetValueOrDefault("path", string.Empty);
        string? detail = error.Parameters.GetValueOrDefault("detail");

        if (!string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        return error.Code switch
        {
            OperationErrorCode.FileNotFound => $"The file was not found: {context}",
            OperationErrorCode.DirectoryNotFound => $"The directory was not found: {context}",
            OperationErrorCode.AccessDenied => $"Access was denied: {context}",
            OperationErrorCode.FileSystemFailure => $"The file operation failed: {context}",
            OperationErrorCode.InvalidManifest => detail ?? "The mod manifest is invalid.",
            OperationErrorCode.UnsupportedManifestVersion => "The manifest schema version is not supported.",
            OperationErrorCode.InvalidModPackage => detail ?? "The mod package is invalid.",
            OperationErrorCode.GameDirectoryRequired => "Select the game directory to continue.",
            OperationErrorCode.GameDirectoryNotFound => $"The game directory was not found: {context}",
            OperationErrorCode.AssetNotFound => "The requested asset was not found.",
            OperationErrorCode.PatchPlanningFailed => "The patch cannot be applied to the selected game files.",
            OperationErrorCode.InstallRecordNotFound => "The selected install record was not found.",
            OperationErrorCode.FileIntegrityMismatch => "An installed file or backup no longer matches its record.",
            OperationErrorCode.OperationAlreadyRunning => "Another mutating operation is already running.",
            OperationErrorCode.RecoveryRequired => "An interrupted operation must be recovered first.",
            OperationErrorCode.BackupRepositoryUnsafe => "The backup repository is damaged or unsafe.",
            OperationErrorCode.UnsupportedBackupRepositoryVersion => "The backup repository version is not supported.",
            _ => "The operation failed.",
        };
    }
}
