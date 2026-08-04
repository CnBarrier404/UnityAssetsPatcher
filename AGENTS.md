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