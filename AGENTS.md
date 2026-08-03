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
