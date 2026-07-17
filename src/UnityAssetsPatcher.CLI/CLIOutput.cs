using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Core.Assets;

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

    public static JsonObject InspectFields(string path, long pathId, AssetsFieldInfo fieldTree)
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

    public static void WriteInspectFieldsText(TextWriter output, AssetsFieldInfo fieldTree)
    {
        WriteInspectFieldText(output, fieldTree, 0);
    }

    private static JsonObject InspectField(AssetsFieldInfo field)
    {
        return new JsonObject
        {
            ["name"] = field.Name,
            ["typeName"] = field.TypeName,
            ["value"] = field.Value?.ToInvariantString(),
            ["children"] = new JsonArray(field.Children.Select(InspectField).ToArray<JsonNode?>()),
        };
    }

    private static void WriteInspectFieldText(TextWriter output, AssetsFieldInfo field, int depth)
    {
        string value = field.Value is null ? string.Empty : $": {field.Value.ToInvariantString()}";
        output.WriteLine($"{new string(' ', depth * 2)}{field.Name} ({field.TypeName}){value}");

        foreach (AssetsFieldInfo child in field.Children)
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
            output.WriteLine($"- {issue.Code}: {issue.Message} ({issue.Path})");
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
            ["code"] = issue.Code,
            ["message"] = issue.Message,
            ["path"] = issue.Path,
        }).ToArray<JsonNode?>());

    private static void WriteRecoveryText(TextWriter output, BackupRecoveryReport recovery)
    {
        if (recovery.Status == BackupRepositoryStatus.Clean) return;
        output.WriteLine($"Backup recovery: {EnumName(recovery.Status)}");
        foreach (BackupRecoveryOperation operation in recovery.Operations)
            output.WriteLine($"- {operation.Kind} {operation.InstallId}: {operation.Action}");
        foreach (BackupRecoveryIssue issue in recovery.Issues)
            output.WriteLine($"- {issue.Code}: {issue.Message} ({issue.Path})");
    }

    private static void WriteChanges(TextWriter output, IReadOnlyList<InstallChange> changes, bool preview)
    {
        foreach (InstallChange change in changes)
        {
            output.WriteLine($"- {EnumName(change.Kind)} {change.Name}: {change.Path}");
            if (preview && change.Preview is not null)
            {
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
}
