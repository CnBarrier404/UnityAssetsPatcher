# AGENTS.md

UnityAssetsPatcher is a .NET 10 and Avalonia desktop application for managing Unity assets mods.

## Commands

| Action | Command                                |
| :----- | :------------------------------------- |
| Build  | `dotnet build UnityAssetsPatcher.slnx` |
| Test   | `dotnet test UnityAssetsPatcher.slnx`  |

Existing tests may be updated to reflect authorized implementation changes. Do not add test files or test cases unless the user explicitly requests them.

## Repository Structure

| Directory                                | Responsibility                                                                                                                                 |
| :--------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------- |
| `docs/`                                  | User and mod author documentation                                                                                                              |
| `schema/`                                | Mod manifest schema                                                                                                                            |
| `src/UnityAssetsPatcher/`                | Executable entry point, dependency composition, concrete logging setup, and Avalonia UI; owns presentation, localization, and user interaction |
| `src/UnityAssetsPatcher.Application/`    | Use cases, business workflow orchestration, transaction and recovery policies, DTOs, and infrastructure interfaces                             |
| `src/UnityAssetsPatcher.Domain/`         | Business models, value objects, validation rules, and domain errors independent of infrastructure                                              |
| `src/UnityAssetsPatcher.Infrastructure/` | File system, compression, persistence, package parsing, and external integrations implementing Application interfaces                          |
| `tests/`                                 | Unit and integration tests                                                                                                                     |

## Architecture

### Application Configuration

- `AppConfig` is the static source for application identity, version, and fixed application directories.
- `AppRuntimeConfig` is the singleton source for mutable process-level settings such as verbose logging.

### Application Boundaries

- Prefer direct, use-case-oriented application APIs.
- Return business data directly by default. Use use-case-specific results when callers need to distinguish multiple normal business outcomes; allow null or empty collections when their meaning is unambiguous. Do not require a common result wrapper.
- Infrastructure propagates original platform and third-party exceptions without wrapping, translating, or replacing them. Application interprets known failures according to the use-case contract and may expose specific application exceptions. UI owns their presentation.

### Exception Policy

- `Program.Main` is the single process-level unexpected-exception boundary, covering startup, dependency composition, GUI execution, and asynchronous disposal. It logs full diagnostics once, shows generic output without internal details, and terminates with a non-zero exit code.
- Boundary logging must outlive the dependency container. Unexpected failures from UI and background work must reach this boundary promptly, rather than waiting for disposal; lower layers must not log or convert them into expected failures.
- Only the creator of a `CancellationTokenSource`, or the lifecycle boundary explicitly given control of it, may consume cancellation after verifying a cancellation request and matching exception token identity. Integrations that cannot preserve identity require a narrowly scoped, documented adapter. An unmatched cancellation escaping to `Program.Main` is treated as unexpected.

