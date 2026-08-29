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
