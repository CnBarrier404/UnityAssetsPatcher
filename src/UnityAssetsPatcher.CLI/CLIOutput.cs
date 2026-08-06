using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;
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
        string code = error.Code.Value;
        string message = OperationErrorText.Format(error);

        if (parseResult.GetValue(options.Format) == CLIOutputFormat.Json)
        {
            JsonObject envelope = ErrorEnvelope(command, code, message, []);
            envelope["error"]!["parameters"] = new JsonObject(
                error.Parameters.Select(parameter =>
                    KeyValuePair.Create<string, JsonNode?>(parameter.Key, ToJsonNode(parameter.Value))));
            if (error.Recovery is not null)
            {
                envelope["recovery"] = Recovery(error.Recovery);
            }

            WriteJson(output, envelope);
        }
        else
        {
            OperationErrorText.WriteTextFailure(output, error, includeParameters: false);
        }

        return 1;
    }

    public static void WriteFailure(TextWriter error, OperationError operationError)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(operationError);

        OperationErrorText.WriteTextFailure(error, operationError, includeParameters: true);
    }

    public static void WriteUnexpectedFailure(TextWriter error, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(exception);

        error.WriteLine($"Error [application.unexpected_failure]: {exception.Message}");
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
            RepositoryRecoveryException? recovery = EnumerateExceptions(exception).OfType<RepositoryRecoveryException>()
                .FirstOrDefault();
            if (recovery is not null) envelope["recovery"] = Recovery(recovery.Recovery);
            WriteJson(error, envelope);
        }
        else
        {
            RepositoryRecoveryException? recovery = EnumerateExceptions(exception).OfType<RepositoryRecoveryException>()
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
            ["dependencyFailures"] = new JsonArray(result.DependencyFailures.Select(failure => new JsonObject
            {
                ["name"] = failure.ModName,
                ["version"] = failure.ModVersion,
                ["relativePath"] = failure.RelativePath,
                ["diagnostic"] = new JsonObject
                {
                    ["code"] = EnumName(failure.Diagnostic.Code),
                    ["assetsFilePath"] = failure.Diagnostic.AssetsFilePath,
                    ["pathId"] = failure.Diagnostic.PathId,
                    ["fieldPath"] = failure.Diagnostic.FieldPath,
                    ["expected"] = failure.Diagnostic.Expected,
                    ["actual"] = failure.Diagnostic.Actual,
                    ["detail"] = failure.Diagnostic.Detail,
                },
            }).ToArray<JsonNode?>()),
            ["changedFiles"] = new JsonArray(result.ChangedFiles.Select(file => new JsonObject
            {
                ["relativePath"] = file.RelativePath,
                ["action"] = EnumName(file.Action),
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
            ["changedFiles"] = new JsonArray(result.ChangedFiles.Select(file => new JsonObject
            {
                ["relativePath"] = file.RelativePath,
                ["action"] = EnumName(file.Action),
                ["status"] = EnumName(file.Status),
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
        if (result.CanUninstall)
        {
            output.WriteLine(
                $"Can uninstall: yes; {result.ChangedFiles.Count} file(s) will be recomposed or restored.");
        }
        else if (result.DependencyFailures.Count > 0)
        {
            output.WriteLine("Can uninstall: no; real patch dependencies were found.");
        }
        else
        {
            output.WriteLine("Can uninstall: no; current game files do not match the active composition.");
        }

        foreach (UninstallChangedFileResult file in result.ChangedFiles)
        {
            output.WriteLine($"- {EnumName(file.Action)} {file.RelativePath}: {EnumName(file.Status)}");
        }

        if (result.DependencyFailures.Count > 0)
        {
            output.WriteLine("Real patch dependencies:");
            foreach (UninstallDependencyFailureResult failure in result.DependencyFailures)
            {
                PatchDiagnostic diagnostic = failure.Diagnostic;
                output.WriteLine(
                    $"- {failure.ModName} {failure.ModVersion} at {failure.RelativePath}: " +
                    $"{EnumName(diagnostic.Code)}{FormatDiagnosticValues(diagnostic)}");
            }
        }
    }

    public static void WriteUninstallResultText(TextWriter output, UninstallModResult result)
    {
        WriteRecoveryText(output, result.Recovery);
        output.WriteLine($"Uninstalled: {result.ModName} {result.ModVersion}");
        output.WriteLine($"Install ID: {result.InstallId}");
        output.WriteLine($"Changed files: {result.ChangedFiles.Count}");
        foreach (UninstallChangedFileResult file in result.ChangedFiles)
        {
            output.WriteLine($"- {EnumName(file.Action)} {file.RelativePath}: {EnumName(file.Status)}");
        }
    }

    private static string FormatDiagnosticValues(PatchDiagnostic diagnostic)
    {
        var values = new List<string>();
        if (diagnostic.AssetsFilePath is not null)
        {
            values.Add($"file={diagnostic.AssetsFilePath}");
        }

        if (diagnostic.PathId is not null)
        {
            values.Add($"pathId={diagnostic.PathId}");
        }

        if (diagnostic.FieldPath is not null)
        {
            values.Add($"field={diagnostic.FieldPath}");
        }

        if (diagnostic.Expected is not null)
        {
            values.Add($"expected={diagnostic.Expected}");
        }

        if (diagnostic.Actual is not null)
        {
            values.Add($"actual={diagnostic.Actual}");
        }

        if (diagnostic.Detail is not null)
        {
            values.Add($"detail={diagnostic.Detail}");
        }

        return values.Count == 0 ? string.Empty : $" ({string.Join(", ", values)})";
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        JsonNode? node = value switch
        {
            null => null,
            string text => JsonValue.Create(text),
            bool boolean => JsonValue.Create(boolean),
            byte number => JsonValue.Create(number),
            sbyte number => JsonValue.Create(number),
            short number => JsonValue.Create(number),
            ushort number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            uint number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            ulong number => JsonValue.Create(number),
            float number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            char character => JsonValue.Create(character.ToString()),
            _ => JsonValue.Create(value.ToString()),
        };

        return node;
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
                    ["pathId"] = asset.Asset.PathId.Value,
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

    public static JsonObject RecoveryPreview(RepositoryRecoveryPreview preview)
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

    public static JsonObject RecoveryReport(RepositoryRecoveryReport recovery) => Recovery(recovery);

    public static void WriteRecoveryPreviewText(TextWriter output, RepositoryRecoveryPreview preview)
    {
        output.WriteLine($"Recovery preview: {EnumName(preview.Status)}");
        if (preview.GameDirectory is not null) output.WriteLine($"Game directory: {preview.GameDirectory}");
        if (preview.Kind is not null) output.WriteLine($"Transaction: {preview.Kind} {preview.InstallId}");
        if (preview.Action is not null) output.WriteLine($"Action: {EnumName(preview.Action.Value)}");
        foreach (RepositoryRecoveryFileChange file in preview.Files)
            output.WriteLine($"- {EnumName(file.Action)}: {file.RelativePath}");
        foreach (RepositoryRecoveryIssue issue in preview.Issues)
            output.WriteLine($"- {RecoveryIssueCode(issue.Code)}: {RecoveryIssueText(issue)} ({issue.Path})");
    }

    public static void WriteRecoveryReportText(TextWriter output, RepositoryRecoveryReport recovery) =>
        WriteRecoveryText(output, recovery);

    private static JsonObject Recovery(RepositoryRecoveryReport recovery)
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

    private static JsonArray RecoveryIssues(IEnumerable<RepositoryRecoveryIssue> issues) =>
        new(issues.Select(issue => new JsonObject
        {
            ["code"] = RecoveryIssueCode(issue.Code),
            ["message"] = RecoveryIssueText(issue),
            ["path"] = issue.Path,
            ["parameters"] = new JsonObject(issue.Parameters.Select(parameter =>
                KeyValuePair.Create<string, JsonNode?>(parameter.Key, parameter.Value))),
        }).ToArray<JsonNode?>());

    internal static void WriteRecoveryText(TextWriter output, RepositoryRecoveryReport recovery)
    {
        if (recovery.Status == RepositoryRecoveryStatus.Clean) return;
        output.WriteLine($"Backup recovery: {EnumName(recovery.Status)}");
        foreach (RepositoryRecoveryOperation operation in recovery.Operations)
            output.WriteLine($"- {operation.Kind} {operation.InstallId}: {operation.Action}");
        foreach (RepositoryRecoveryIssue issue in recovery.Issues)
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

    private static string RecoveryIssueCode(RepositoryRecoveryIssueCode code)
    {
        return code switch
        {
            RepositoryRecoveryIssueCode.RepositoryUnsafe => "repository_unsafe",
            RepositoryRecoveryIssueCode.RecoveryUnsafe => "recovery_unsafe",
            RepositoryRecoveryIssueCode.OperationFailed => "operation_failed",
            RepositoryRecoveryIssueCode.UnexpectedFailure => "unexpected_failure",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
        };
    }

    private static string RecoveryIssueText(RepositoryRecoveryIssue issue)
    {
        if (issue.Parameters.GetValueOrDefault("detail") is { Length: > 0 } detail)
        {
            return detail;
        }

        return issue.Code switch
        {
            RepositoryRecoveryIssueCode.RepositoryUnsafe => "The backup repository is damaged or unsafe.",
            RepositoryRecoveryIssueCode.RecoveryUnsafe => "Recovery cannot continue safely.",
            RepositoryRecoveryIssueCode.OperationFailed => "The recovery operation failed.",
            RepositoryRecoveryIssueCode.UnexpectedFailure => "An unexpected recovery failure occurred.",
            _ => "Recovery failed.",
        };
    }
}

internal static class OperationErrorText
{
    public static string Format(OperationError error)
    {
        string context = ParameterText(error, "path") ?? string.Empty;
        string? detail = ParameterText(error, "detail");

        if (!string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        if (error.Code == FileErrorCodes.NotFound)
        {
            return $"The file was not found: {context}";
        }

        if (error.Code == FileErrorCodes.DirectoryNotFound)
        {
            return $"The directory was not found: {context}";
        }

        if (error.Code == FileErrorCodes.AccessDenied)
        {
            return $"Access was denied: {context}";
        }

        if (error.Code == FileErrorCodes.ReadFailed || error.Code == FileErrorCodes.SystemFailure)
        {
            return $"The file operation failed: {context}";
        }

        if (error.Code == ManifestErrorCodes.MissingProperty)
        {
            string? property = ParameterText(error, "property");

            return property is null
                ? "A required manifest property is missing."
                : $"Required manifest property '{property}' is missing.";
        }

        if (error.Code == ManifestErrorCodes.UnsupportedSchema)
        {
            return "The manifest schema version is not supported.";
        }

        if (error.Code.Value.StartsWith("manifest.", StringComparison.Ordinal))
        {
            return "The mod manifest is invalid.";
        }

        if (error.Code.Value.StartsWith("mod_package.", StringComparison.Ordinal))
        {
            return "The mod package is invalid.";
        }

        return error.Code switch
        {
            _ when error.Code == WorkflowErrorCodes.GameDirectoryRequired => "Select the game directory to continue.",
            _ when error.Code == WorkflowErrorCodes.GameDirectoryNotFound =>
                $"The game directory was not found: {context}",
            _ when error.Code == WorkflowErrorCodes.AssetNotFound => "The requested asset was not found.",
            _ when error.Code == WorkflowErrorCodes.PatchPlanningFailed =>
                "The patch cannot be applied to the selected game files.",
            _ when error.Code == WorkflowErrorCodes.InstallRecordNotFound =>
                "The selected install record was not found.",
            _ when error.Code == WorkflowErrorCodes.FileIntegrityMismatch =>
                "An installed file or backup no longer matches its record.",
            _ when error.Code == WorkflowErrorCodes.InstallPreviewStale =>
                "The install preview is stale. Preview the installation again.",
            _ when error.Code == WorkflowErrorCodes.OperationAlreadyRunning =>
                "Another mutating operation is already running.",
            _ when error.Code == WorkflowErrorCodes.RecoveryRequired =>
                "An interrupted operation must be recovered first.",
            _ when error.Code == WorkflowErrorCodes.RepositoryUnsafe =>
                "The backup repository is damaged or unsafe.",
            _ when error.Code == WorkflowErrorCodes.UnsupportedRepositoryVersion =>
                "The backup repository version is not supported.",
            _ => "The operation failed.",
        };
    }

    private static IEnumerable<KeyValuePair<string, object?>> VisibleParameters(OperationError error)
    {
        if (error.Code == ManifestErrorCodes.MissingProperty)
        {
            return [];
        }

        return error.Parameters.Where(parameter =>
            parameter.Key is not "instance_path" and not "schema_path" and not "keyword");
    }

    private static string? ParameterText(OperationError error, string key)
    {
        return error.Parameters.TryGetValue(key, out object? value) ? value?.ToString() : null;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            string text => text,
            IEnumerable values => string.Join(", ", values.Cast<object?>().Select(FormatValue)),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    public static void WriteTextFailure(TextWriter error, OperationError operationError, bool includeParameters)
    {
        error.WriteLine($"Error [{operationError.Code.Value}]: {Format(operationError)}");

        if (includeParameters)
        {
            foreach ((string key, object? value) in VisibleParameters(operationError)
                         .OrderBy(parameter => parameter.Key))
            {
                error.WriteLine($"  {key}: {FormatValue(value)}");
            }
        }

        if (operationError.Recovery is not null)
        {
            CLIOutput.WriteRecoveryText(error, operationError.Recovery);
        }
    }
}
