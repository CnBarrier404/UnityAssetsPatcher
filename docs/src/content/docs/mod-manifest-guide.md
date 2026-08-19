---
title: Mod manifest guide
description: The manifest.json format, matching rules, and examples supported by Unity Assets Patcher.
sidebar:
  order: 3
---

A mod package is a ZIP archive containing exactly one `manifest.json`. The manifest describes mod metadata, the target game, payload files to copy, and changes to Unity `.assets` files.

## Quick start

### Editor validation

Every manifest must contain the project’s JSON Schema URL at the top level:

```json
{
  "$schema": "https://uap.cnbarrier.com/schema-v1.json"
}
```

Editors with JSON Schema support can then provide completion and structural validation. Runtime validation still enforces path safety, operation combinations, file existence, unique asset matches, and optional-content conflicts.

### Package layout

```text
Mod.zip
  manifest.json
  resources/
    modassets.assets
    modassets.resource
```

The manifest may be at the ZIP root or in a subdirectory, although the root is recommended. Payloads that must be installed, including `.resource` files used by replacement assets, must be declared explicitly in `copyFiles`.

### Complete example

```json
{
  "$schema": "https://uap.cnbarrier.com/schema-v1.json",
  "name": "Camera Tweak",
  "author": "Example",
  "version": "1.0.0",
  "description": "Adjusts the camera and installs mod resources.",
  "game": "GameName",
  "copyFiles": [
    { "source": "resources/modassets.resource" }
  ],
  "targets": [
    {
      "file": "resources.assets",
      "patches": [
        {
          "type": "Camera",
          "match": { "field of view": 60.0 },
          "set": {
            "field of view": { "from": 60.0, "to": 90.0 }
          }
        },
        {
          "type": "Material",
          "match": { "m_Name": "TargetMaterial" },
          "add": {
            "m_ValidKeywords.Array": ["_EMISSION"]
          }
        }
      ]
    },
    {
      "file": "sharedassets4.assets",
      "patches": [
        {
          "type": "AudioClip",
          "match": { "m_Name": "CrazySound" },
          "replaceAsset": {
            "fromFile": "resources/modassets.assets",
            "matchField": "m_Name"
          }
        }
      ]
    }
  ]
}
```

Validate the JSON file or complete package before publishing:

```powershell
.\UnityAssetsPatcher.exe check --config .\manifest.json
.\UnityAssetsPatcher.exe check --config .\Mod.zip
.\UnityAssetsPatcher.exe install preview --package .\Mod.zip --game-directory "C:\Games\Game"
```

## Top-level fields

| Field | Required | Description |
| --- | --- | --- |
| `$schema` | Yes | Must be `https://uap.cnbarrier.com/schema-v1.json`. |
| `name` | Yes | Mod name. |
| `author` | Yes | Mod author. |
| `version` | Yes | Mod version; semantic versioning is recommended. |
| `description` | No | Short description. |
| `game` | No | Game name used when resolving a Steam installation. |
| `copyFiles` | No | Payload files copied beside the target assets files. |
| `targets` | Yes | Groups of target `.assets` files and patches. |
| `optional` | No | Independently selectable optional-content groups. |

The old top-level `schemaVersion: 1` may remain in existing manifests, but the current runtime does not use it. The `schemaVersion` in CLI JSON responses belongs to the output protocol and is unrelated.

## Payload files

```json
"copyFiles": [
  { "source": "resources/modassets.resource" }
]
```

`source` must be a safe relative path inside the mod ZIP. Only its file name is used at the destination, and the file is copied beside the target assets files. If `copyFiles` is present, all targets must reside in one directory. Existing destination files are never overwritten.

## Targets and patches

Each target identifies an assets file by file name and contains at least one patch:

```json
{
  "file": "sharedassets0.assets",
  "patches": [
    {
      "type": "Camera",
      "match": { "m_Name": "Main Camera" },
      "set": {
        "field of view": { "from": 60.0, "to": 75.0 }
      }
    }
  ]
}
```

`file` cannot contain a directory. The installer searches the game directory recursively and stops if the file is missing or ambiguous.

`type` is a Unity asset type such as `Camera`, `Material`, `AudioClip`, or `GameObject`. All fields in `match` use AND semantics and must match. Use multiple patches to express OR conditions. Field-level changes can affect all matching assets; operations that copy or replace whole assets impose additional uniqueness requirements.

### Component patches

Set `type` to `GameObject` and use `componentType` to modify an attached component:

```json
{
  "type": "GameObject",
  "match": { "m_Name": "_Equipment_Items" },
  "componentType": "Transform",
  "set": {
    "m_LocalPosition.x": { "from": 0, "to": 12.5 }
  }
}
```

The match applies to the `GameObject`, while `set` and `add` paths apply to the component. The installer stops if more than one component of the requested type is attached. `componentType` cannot be combined with `replaceAsset` or `copyAsset`.

## Change operations

### `set`

`set` replaces field values. Every entry requires both the expected current value and the new value:

```json
"set": {
  "field of view": { "from": 90.0, "to": 75.0 },
  "near clip plane": { "from": 0.3, "to": 0.1 }
}
```

The current field must equal `from`. Values are not implicitly converted between strings, numbers, and booleans. `to` may also contain a scalar array or an object representing the target field’s direct children.

### `add`

`add` appends scalar values to an array without creating duplicates:

```json
"add": {
  "m_ValidKeywords.Array": ["_EMISSION"]
}
```

`set` and `add` may appear in the same patch.

### `replaceAsset`

`replaceAsset` replaces a complete target asset with an asset from a packaged source file:

```json
"replaceAsset": {
  "fromFile": "resources/modassets.assets",
  "matchField": "m_Name"
}
```

The source and target types must match. For each target, `matchField` must uniquely identify one source asset of the same type. It cannot be combined with `set`, `add`, `componentType`, or `copyAsset`. If a target assets file contains a replacement, that file cannot also contain field-level changes.

### `copyAsset`

`copyAsset` copies another asset in the same target file after ordinary field patches have run:

```json
{
  "type": "Material",
  "match": { "m_Name": "DiningChair_mtl" },
  "copyAsset": {
    "from": {
      "type": "Material",
      "match": { "m_Name": "DiningTable_mtl" }
    }
  }
}
```

The source and target must each be unique, distinct assets of the same type. The target keeps its Path ID and scalar string `m_Name`; the remaining field tree comes from the patched source. Copy chains, cycles, and combinations with other operations in the same patch are rejected.

## Field paths

Paths follow names in the Unity asset field tree:

```text
m_Name
m_CullingMask.m_Bits
m_ValidKeywords.Array
m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID
```

The selector syntax `[child=value]` selects an array element by a child field. Path segments cannot be empty. Selector values use string comparison and do not support expressions.

### Path ID references

Use `$pathId` as a `set.to` value to look up exactly one asset in the same target file:

```json
"set": {
  "m_Texture.m_PathID": {
    "from": 0,
    "to": {
      "$pathId": {
        "type": "Texture2D",
        "match": { "m_Name": "NewEmission" }
      }
    }
  }
}
```

## Optional content

Each optional group has a unique, non-empty `name`, an optional `description`, and at least one of `targets` or `copyFiles`:

```json
"optional": [
  {
    "name": "High-resolution textures",
    "description": "Replaces textures with 4K versions.",
    "copyFiles": [
      { "source": "extras/skin_4k.resource" }
    ]
  }
]
```

Selected groups are merged with the main content before preview and installation. Payload destination names must remain unique, and merged operations must satisfy the same combination restrictions as ordinary targets.

## Installation and uninstall behavior

Installation first validates the package, resolves the game and target files, and produces a dry-run preview. After confirmation, it prepares patched outputs, payloads, rollback snapshots, and hashes in the backup repository’s `.temp` directory. It then replaces assets atomically, creates payloads without overwriting existing files, verifies the results, and commits an immutable layer record.

Uninstall reconstructs modified assets by replaying remaining layers over base snapshots. It also restores or removes payloads according to their snapshots. Missing or damaged layer records, original packages, or snapshots stop the operation instead of risking an incorrect result.

## Safety limits

- `manifest.json` cannot exceed 10 MB.
- Total uncompressed package size cannot exceed 10 GB.
- ZIP entries and manifest file paths must be safe relative paths.
- Duplicate entries after path normalization are rejected.
- Preview does not modify assets files or copy payloads.

## Authoring recommendations

Use UABEA to inspect the target game’s actual asset types, Path IDs, field trees, paths, and values. Prefer stable identifiers such as `m_Name`; confirm uniqueness wherever an operation requires it; keep accurate `set.from` checks; use separate patches for OR conditions; and declare every payload explicitly in `copyFiles`.
