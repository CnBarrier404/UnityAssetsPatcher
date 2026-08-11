# AGENTS.md

UnityAssetsPatcher is a .NET 10 console app for inspecting Unity assets files and installing or uninstalling mods.

It provides a Terminal.Gui-based interactive UI and a non-interactive CLI.

## Commands

| Action | Command                                |
| :----- | :------------------------------------- |
| Build  | `dotnet build UnityAssetsPatcher.slnx` |
| Test   | `dotnet test UnityAssetsPatcher.slnx`  |

## Repository Structure

| Directory                                               | Responsibility                                                                                       |
| :------------------------------------------------------ | :--------------------------------------------------------------------------------------------------- |
| `src/UnityAssetsPatcher/`                               | Executable entry point and composition root; owns dependency registration and concrete logging setup |
| `src/UnityAssetsPatcher.Application/`                   | Use cases, workflow orchestration, DTOs, and infrastructure abstractions                             |
| `src/UnityAssetsPatcher.CLI/`                           | Non-interactive command parsing and text/JSON presentation                                           |
| `src/UnityAssetsPatcher.Domain/`                        | Domain models, value objects, validation rules, and domain errors                                    |
| `src/UnityAssetsPatcher.Infrastructure/`                | File system, compression, persistence, backup, package, and AssetsTools.NET implementations          |
| `src/UnityAssetsPatcher.LocalizationGenerator/`         | Roslyn source generator for strongly typed localized strings                                         |
| `src/UnityAssetsPatcher.TUI/`                           | Interactive terminal UI and localization inputs                                                      |
| `tests/UnityAssetsPatcher.Application.Tests/`           | Unit tests for application workflows with test doubles at infrastructure boundaries                  |
| `tests/UnityAssetsPatcher.CLI.Tests/`                   | Command parsing, text presentation, exit-code, and CLI composition tests                             |
| `tests/UnityAssetsPatcher.Domain.Tests/`                | Unit tests for domain models and validation rules                                                    |
| `tests/UnityAssetsPatcher.Infrastructure.Tests/`        | Integration tests for file IO, compression, persistence, and asset editing                           |
| `tests/UnityAssetsPatcher.LocalizationGenerator.Tests/` | Source-generator and localization-format tests                                                       |
| `tests/UnityAssetsPatcher.TUI.Tests/`                   | Terminal UI component, localization, and navigation tests                                            |

## Code Style

Follow [`.agents/code-style.md`](.agents/code-style.md) for repository-specific C# and test style. `.editorconfig` is authoritative for text encoding and line endings.

## Workflows

### 1. Implementing a Feature

1. Read the user's requirements and inspect the relevant existing code, configuration, content, and documentation.
2. Before editing implementation files, present a concrete plan that describes the intended behavior, affected areas, and validation steps.
3. Do not modify source code or other implementation files, and do not run commands that rewrite or generate files, until the user explicitly approves the plan and authorizes implementation. Read-only inspection is allowed.
4. After approval, implement only the approved scope. If a materially different approach or broader scope becomes necessary, explain the change and request approval again before proceeding.
5. Validate the implementation with the commands required by this document and report the result.

### 2. Fixing a Bug

1. Start from the user's problem description. Inspect the relevant code and, when practical, reproduce the issue using read-only or non-mutating diagnostics.
2. Identify the root cause and support the diagnosis with concrete evidence from the code or reproduction. Do not present an unverified guess as the cause.
3. Explain the root cause, affected behavior, proposed fix, and validation plan to the user. Wait for explicit approval before modifying source code or other implementation files.
4. After approval, implement the fix within the agreed scope, add or update regression coverage when appropriate, and run the required validation commands.

## Error Handling

* Use `OperationResult<T>` / `OperationError` for expected application failures.
* Do not throw exceptions for ordinary validation or domain failures when the API already returns `OperationResult<T>`.
* Exceptions are for unexpected technical failures, programming errors, and violated invariants.
* Translate exceptions into `OperationError` only where their application meaning is known.
* Do not convert unknown exceptions into generic error codes; let them propagate.
* Do not use `Exception.Message`, string matching, or file extensions to determine error semantics.
* Prefer typed exceptions with structured reasons when infrastructure must communicate domain-specific technical failures.
* Cancellation is not a failure: propagate `OperationCanceledException` and do not convert or log normal cancellation as an error.

## Application Error Codes

* Error codes must be stable, machine-readable application semantics.
* Preserve the same error code across different handlers and entry points.
* Do not collapse distinct actionable failures into generic codes such as `*.invalid` or `*.failed`.
* Store structured data in `OperationError.Parameters`; do not store exceptions, stack traces, or raw exception messages.
* User-facing text belongs to presentation/formatting code, not application error objects.

## Logging

* Logging must not be used for failure propagation.
* Expected `OperationError` failures should normally not be logged at `Error`.
* Log unexpected exceptions once, at the boundary that finally handles or terminates the operation.
* Avoid log-and-rethrow when a higher layer will log the same exception.
* Use:

  * `Debug` for internal details and timings.
  * `Information` for meaningful lifecycle events.
  * `Warning` for expected but noteworthy/recoverable conditions.
  * `Error` for unexpected failures requiring investigation.
* Cleanup-only `catch` blocks may rethrow without translating or logging.
