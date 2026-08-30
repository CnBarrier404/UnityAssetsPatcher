# ARCHITECTURE.md

This document describes the architecture of Unity Assets Patcher. It serves as a critical, living template designed to equip agents with a rapid and comprehensive understanding of the codebase's architecture, enabling efficient navigation and effective contribution from day one. Update this document as the codebase evolves.

## Project Structure

This section provides a high-level overview of the project's directory and file structure, categorised by architectural layer or major functional area. It is essential for quickly navigating the codebase, locating relevant files, and understanding the overall organization and separation of concerns.

[Project Root]/
├── .github/                                          # GitHub Actions or other CI/CD configurations
├── docs/                                             # User and mod author documentation
├── schema/                                           # Mod manifest schema  
├── src/          
│   ├── UnityAssetsPatcher/                           # Executable entry point and composition root
│   │   ├── Assets/
│   │   └── Logging/
│   ├── UnityAssetsPatcher.Application/               # Use cases, workflows, DTOs, and infrastructure abstractions
│   │   ├── Assets/
│   │   ├── Composition/
│   │   ├── Contracts/
│   │   ├── Features/
│   │   ├── Installation/
│   │   ├── IO/
│   │   ├── Messaging/
│   │   ├── Mods/
│   │   ├── Operations/
│   │   ├── Patching/
│   │   ├── Repository/
│   │   ├── Uninstallation/
│   │   ├── Updates/
│   │   ├── AppConfig.cs
│   │   └── AppRuntimeConfig.cs
│   ├── UnityAssetsPatcher.CLI/                        # Non-interactive command parsing and presentation
│   ├── UnityAssetsPatcher.Domain/                     # Domain models, value objects, and validation rules
│   │   ├── Assets/
│   │   ├── Integrity/
│   │   └── Json/
│   ├── UnityAssetsPatcher.Infrastructure/             # File IO, compression, persistence, packages, and AssetsTools.NET adapters
│   │   ├── AssetsTools/
│   │   ├── Installation/
│   │   ├── IO/
│   │   ├── Mods/
│   │   ├── Repository/
│   │   └── Updates/
│   ├── UnityAssetsPatcher.LocalizationGenerator/      # Roslyn source generator for localized strings
│   └── UnityAssetsPatcher.TUI/                        # Terminal.Gui interactive UI and localization
│       ├── Flows/
│       ├── Framework/
│       ├── Lifecycle/
│       ├── Localization/
│       ├── Navigation/
│       ├── Pages/
│       ├── Shell/
│       └── UnityAssetsPatcher.TUI.csproj
├── tests/                                             # Unit and integration tests
├── .editorconfig
├── .gitattributes
├── .gitignore
├── AGENTS.md
├── ARCHITECTURE.md
├── CHANGELOG.md
├── global.json
├── LICENSE
├── README.md
├── README_ZH.md
└── UnityAssetsPatcher.slnx

## Infrastructure

- Infrastructure implementations must propagate original exceptions from underlying platform APIs and third-party libraries without wrapping, translating, or replacing them.

## Application configuration

- `AppConfig` is the static source for application identity, version, and fixed application directories.
- `AppRuntimeConfig` is the singleton source for mutable process-level settings such as verbose logging.

## Terminal user interface

- Pages that initiate application work must be split into a `*View` and a corresponding `*Logic`.
- Views own Terminal.Gui controls, layout, input collection, event forwarding, and rendering only. Views must not resolve application services, dispatch requests, or orchestrate application workflows.
- Page logic owns application request dispatch, state transitions, re-entry protection, error handling, cancellation, and operation lifetime. Page logic must not reference Terminal.Gui types or access views.
- Page logic must expose immutable presentation state or results that views can render, and must remain independently testable without a running Terminal.Gui application.
- Terminal.Gui controls may only be created, mutated, focused, navigated, rendered, or disposed on the UI thread.
- Views must marshal notifications received from background threads through `View.App.Invoke` or `IApplication.Invoke` before applying them to the UI.
- Application dispatch and other work that may perform CPU-intensive or synchronous blocking operations must be started on a worker thread by page logic. A method returning `Task` must not be assumed non-blocking when it can perform work before returning an incomplete task.
- Genuinely asynchronous I/O should be awaited directly. `Task.Yield` must not be used as a substitute for moving CPU-intensive work off the UI thread.
- Terminal.Gui event handlers must remain short and non-blocking. Every asynchronous operation they start must be observed by page logic; unobserved fire-and-forget tasks are prohibited.
- Page logic must prevent unintended concurrent operations, link work to the page or session cancellation token, and prevent completed background work from updating a disposed view.
- Service scopes used by background operations must be created, used, and disposed within the operation lifetime.
- Background execution and operation lifetime must be owned explicitly by each page logic; a shared global task runner must not be reintroduced.
