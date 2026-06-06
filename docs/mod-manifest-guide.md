# Mod Manifest 编写指南

本文档说明 Unity Assets Patcher 当前支持的 `manifest.json` 格式。Manifest 用来描述 mod 元数据、目标游戏、需要复制的 payload 文件，以及要安装到 Unity `.assets` 文件中的变更。

Manifest 主要被两个交互功能使用：

- `Install Mod`：读取 mod zip，预览并安装 manifest 中声明的变更。
- `Find assets`：使用 manifest 中的 `type` 和 `match` 条件查找目标 asset。

## Mod 包结构

mod 包是一个 zip 文件，内部必须包含且只能包含一个 `manifest.json`。`manifest.json` 可以放在 zip 根目录，也可以放在子目录中。

示例：

```text
Mod.zip
  manifest.json
  resources/
    modassets.assets
    modassets.resource
```

如果包内包含需要复制到游戏目录的文件，例如 `.resource`，必须在 `copyFiles` 中显式声明。工具不会根据 `replaceAsset.fromFile` 自动推断要复制哪些附带文件。

## 完整示例

```json
{
  "name": "Camera Tweak",
  "author": "Example",
  "version": "1.0.0",
  "description": "Adjusts camera and installs mod resources.",
  "game": "GameName",
  "copyFiles": [
    {
      "source": "resources/modassets.resource"
    }
  ],
  "targets": [
    {
      "file": "resources.assets",
      "patches": [
        {
          "type": "Camera",
          "match": {
            "field of view": 60.0
          },
          "set": {
            "field of view": {
              "from": 60.0,
              "to": 90.0
            }
          }
        },
        {
          "type": "Material",
          "match": {
            "m_Name": "TargetMaterial"
          },
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
          "match": {
            "m_Name": "CrazySound"
          },
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

## 顶层字段

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `name` | 是 | mod 名称。 |
| `author` | 是 | mod 作者。 |
| `version` | 是 | mod 版本，建议使用语义化版本。 |
| `description` | 否 | mod 简短说明。 |
| `game` | 否 | 游戏名。未手动选择游戏目录时，工具会尝试用它从 Steam 安装信息中解析游戏目录。 |
| `copyFiles` | 否 | 安装后需要复制到目标 assets 文件所在目录的 payload 文件。 |
| `targets` | 是 | 要处理的目标 `.assets` 文件分组。 |

`name`、`author`、`version` 必须是非空字符串。`description` 如果存在，必须是字符串。`game` 如果存在，必须是非空字符串。

## copyFiles

`copyFiles` 用来声明安装完成后要复制到游戏目录的 payload 文件。

```json
"copyFiles": [
  {
    "source": "resources/modassets.resource"
  }
]
```

规则：

- `source` 是 mod zip 内部的相对路径。
- `source` 不能是绝对路径，不能以 `/` 开头，不能包含空路径段、`.` 或 `..`。
- 安装时只使用 `source` 的文件名作为目标文件名，例如 `resources/modassets.resource` 会复制为 `modassets.resource`。
- payload 文件会复制到目标 assets 文件所在目录。
- 如果声明了 `copyFiles`，所有目标 assets 文件必须位于同一个目录。
- payload 目标文件已存在时，安装会拒绝继续；预览会标记该文件不会复制。

## targets

`targets` 是目标 assets 文件列表。每个 target 按目标文件名分组。

```json
"targets": [
  {
    "file": "sharedassets0.assets",
    "patches": []
  }
]
```

### file

`file` 是目标 assets 文件名，只能写文件名，不能包含目录。

正确：

```json
"file": "sharedassets0.assets"
```

错误：

```json
"file": "Game_Data/sharedassets0.assets"
```

安装时，工具会在游戏目录下递归查找该文件名。必须刚好匹配一个文件；找不到或匹配多个文件都会停止安装。

### patches

`patches` 是当前 target 文件中的变更规则列表。每个 patch 先用 `type` 和 `match` 定位 asset，再执行 `set`、`add` 或 `replaceAsset`。

```json
"patches": [
  {
    "type": "Camera",
    "match": {
      "field of view": 90.0
    },
    "set": {
      "field of view": {
        "from": 90.0,
        "to": 75.0
      }
    }
  }
]
```

用于安装的 patch 必须至少包含 `set`、`add` 或 `replaceAsset` 之一。只包含 `type` 和 `match` 的 manifest 可以用于 `Find assets`，不能用于安装。

## 定位目标 asset

### type

`type` 是 Unity asset 类型名，例如 `Camera`、`Material`、`AudioClip`、`GameObject`。

```json
"type": "Camera"
```

工具会先按类型过滤 assets，再检查 `match`。

### match

`match` 是字段匹配条件，key 是字段路径，value 是期望值。一个 `match` 中的多个字段是 AND 关系，必须全部匹配。

```json
"match": {
  "m_Name": "Main Camera",
  "field of view": 90.0
}
```

如果需要 OR 关系，写多条 patch：

```json
"patches": [
  {
    "type": "Camera",
    "match": {
      "field of view": 60.0
    },
    "set": {
      "field of view": {
        "from": 60.0,
        "to": 75.0
      }
    }
  },
  {
    "type": "Camera",
    "match": {
      "field of view": 90.0
    },
    "set": {
      "field of view": {
        "from": 90.0,
        "to": 75.0
      }
    }
  }
]
```

匹配值支持字符串、数字、布尔值、对象和数组。数字按数值比较；字符串区分大小写；数组要求长度和元素都匹配。

### component

`component` 用于通过 `GameObject` 定位挂载组件，再修改组件字段。

```json
{
  "type": "GameObject",
  "match": {
    "m_Name": "_Equipment_Items"
  },
  "component": "Transform",
  "set": {
    "m_LocalPosition.x": {
      "from": 0,
      "to": 12.5
    }
  }
}
```

规则：

- `component` 只能在 `type` 为 `GameObject` 时使用。
- `match` 匹配的是 `GameObject` 的字段。
- `set` 和 `add` 中的字段路径属于组件，不属于 `GameObject`。
- 同一个 `GameObject` 上如果找到多个同类型组件，安装会停止，避免写错目标。
- `component` 不能和 `replaceAsset` 组合。

## 安装变更

### set

`set` 用于替换字段值。

```json
"set": {
  "field of view": {
    "from": 90.0,
    "to": 75.0
  }
}
```

规则：

- `set` 的 key 是要写入的字段路径。
- 每个字段必须包含 `from` 和 `to`。
- 写入前，当前字段值必须匹配 `from`；不匹配时预览会标记为 skipped，安装会拒绝继续。
- `to` 支持字符串、数字、布尔值。
- `to` 可以是标量数组，用于写入数组字段。
- `to` 可以是对象，此时会写入目标字段的直接子字段。

同时修改多个字段：

```json
"set": {
  "field of view": {
    "from": 90.0,
    "to": 75.0
  },
  "near clip plane": {
    "from": 0.3,
    "to": 0.1
  }
}
```

修改复合字段的直接子字段：

```json
"set": {
  "m_Color": {
    "from": {
      "r": 1.0,
      "g": 1.0,
      "b": 1.0,
      "a": 1.0
    },
    "to": {
      "r": 0.8,
      "g": 0.6,
      "b": 0.2,
      "a": 1.0
    }
  }
}
```

写入数组字段：

```json
"set": {
  "m_ValidKeywords.Array": {
    "from": ["_NORMALMAP"],
    "to": ["_NORMALMAP", "_EMISSION"]
  }
}
```

### add

`add` 用于向数组字段追加标量值。

```json
"add": {
  "m_ValidKeywords.Array": ["_EMISSION"]
}
```

规则：

- `add` 的 key 是数组字段路径。
- value 必须是数组。
- 数组元素支持字符串、数字、布尔值。
- 如果目标数组中已经存在相同值，工具不会重复追加。

`set` 和 `add` 可以放在同一个 patch 中：

```json
{
  "type": "Material",
  "match": {
    "m_Name": "TargetMaterial"
  },
  "set": {
    "m_CustomRenderQueue": {
      "from": -1,
      "to": 3000
    }
  },
  "add": {
    "m_ValidKeywords.Array": ["_EMISSION"]
  }
}
```

### replaceAsset

`replaceAsset` 用 mod 包中的源 asset 全量替换游戏中的目标 asset。

```json
"replaceAsset": {
  "fromFile": "resources/modassets.assets",
  "matchField": "m_Name"
}
```

规则：

- 目标 asset 由当前 patch 的 `type` 和 `match` 定位。
- `fromFile` 是源 `.assets` 文件路径。mod zip 中建议使用相对路径。
- `matchField` 是用于把目标 asset 和源 asset 对应起来的字段路径。
- 源 asset 必须和目标 asset 类型相同。
- 对每个目标 asset，工具会读取目标 asset 的 `matchField` 值，再在 `fromFile` 中查找同类型、同字段值的唯一源 asset。
- 找不到源 asset、源 asset 不唯一、目标匹配值不唯一都会停止安装。

示例：

```json
{
  "type": "AudioClip",
  "match": {
    "m_Name": "CrazySound"
  },
  "replaceAsset": {
    "fromFile": "resources/modassets.assets",
    "matchField": "m_Name"
  }
}
```

组合限制：

- `replaceAsset` 不能和 `set` 或 `add` 放在同一个 patch 中。
- `replaceAsset` 不能和 `component` 放在同一个 patch 中。
- 同一个目标 assets 文件中，如果存在 `replaceAsset`，就不能混入字段级 `set` 或 `add`。

## 字段路径

字段路径使用 Unity asset 字段树中的字段名。

简单字段：

```json
"m_Name"
```

嵌套字段：

```json
"m_CullingMask.m_Bits"
```

数组字段：

```json
"m_ValidKeywords.Array"
```

带选择器的数组元素路径：

```json
"m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID"
```

选择器格式是 `[子字段名=值]`。上面的路径表示：在 `m_TexEnvs.Array` 中找到 `data.first` 等于 `_EmissionMap` 的元素，再访问它的 `second.m_Texture.m_PathID`。

注意：

- 字段路径不能是空字符串。
- 点号分隔的每个路径段都不能为空。
- 简单字段名会在字段树中查找第一个同名后代；多段路径会按层级逐段查找。
- 选择器值按字符串比较，不支持复杂表达式。

## Path ID 引用

某些字段需要写入另一个 asset 的 Path ID。可以在 `set.to` 中使用 `$pathId`。

```json
"set": {
  "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID": {
    "from": 0,
    "to": {
      "$pathId": {
        "type": "Texture2D",
        "match": {
          "m_Name": "NewEmission"
        }
      }
    }
  }
}
```

规则：

- `$pathId.type` 是要查找的 asset 类型。
- `$pathId.match` 是用于识别该 asset 的字段匹配条件。
- 查找范围是同一个目标 assets 文件。
- 必须刚好匹配一个 asset；找不到或匹配多个都会停止安装。
- `$pathId` 只能作为某个 `set` 字段的 `to` 值使用。

## 安装行为

安装 mod 时，工具按以下顺序处理：

1. 读取 zip 中唯一的 `manifest.json`。
2. 如果没有手动选择游戏目录，尝试使用 `game` 从 Steam 安装信息中解析游戏目录。
3. 根据 `targets[].file` 在游戏目录下定位目标 assets 文件。
4. 生成 dry run 预览，展示目标文件、命中的 asset、将执行的变更和 payload 文件状态。
5. 用户确认后，先检查 payload 目标文件是否可用，再写入 assets 文件。
6. 覆盖原始 assets 文件前，在程序目录的 `backup` 文件夹创建备份。
7. 写入 assets 文件成功后，复制 `copyFiles` 中声明的 payload 文件。

预览不会写入 assets 文件，也不会复制 payload 文件。

## 编写建议

### 使用 UABEA 确认字段

推荐使用 UABEA（Unity Asset Bundle Extractor Avalonia）打开目标游戏的 `.assets` 文件，查看实际 asset 类型、Path ID、字段树、字段路径和当前字段值。

编写 manifest 前，至少确认：

- `targets[].file` 对应的 assets 文件名。
- `type` 是否是目标 asset 的真实类型名。
- `match` 使用的字段能否稳定、唯一地定位目标 asset。
- `set.from` 是否等于目标游戏版本中的实际旧值。
- `set`、`add`、`matchField` 和 `$pathId.match` 使用的字段路径是否和字段树一致。

Unity 不同版本、不同游戏版本、不同导出方式下的字段名和值都可能变化。不要只根据示例或旧版本经验编写字段路径。

### 优先使用稳定匹配字段

优先使用 `m_Name`、稳定 ID 或其他不容易随版本变化的字段。不要只依赖容易变化的数值字段，除非它确实能唯一定位目标 asset。

### 保留 from 校验

`set.from` 是写入前的安全检查，不只是说明文字。即使 `match` 已经使用了同一个字段，也建议保留准确的 `from` 值。

### 用多条 patch 表达 OR

如果多个目标 asset 需要同样修改，写多条 patch。这样预览输出和错误定位更清楚。

### 显式声明 payload

`replaceAsset.fromFile` 只告诉工具从哪里读取源 asset，不会把相关 `.resource` 文件复制到游戏目录。需要安装的文件必须写在 `copyFiles`。

### 不要使用旧格式字段

当前格式使用：

- `targets[].file`，不是顶层 `target`。
- `targets[].patches[].match`，不是 `include`。
- `copyFiles`，不是 `files`。
- `replaceAsset.fromFile` / `replaceAsset.matchField`，不是 `replaceFrom.assets` / `replaceFrom.match`。
- `set` 对象属性名作为字段路径，不是 `set` 数组里的 `field`。

## 发布前检查清单

- `name`、`author`、`version` 已填写。
- zip 中只有一个 `manifest.json`。
- `targets[].file` 只写文件名，不包含目录。
- 每个用于安装的 patch 都有 `set`、`add` 或 `replaceAsset`。
- `match` 能唯一定位预期 asset。
- `set.from` 等于目标游戏版本中的实际旧值。
- `add` 指向数组字段，且追加值是字符串、数字或布尔值。
- 使用 `component` 时，`type` 是 `GameObject`，字段路径属于组件。
- 使用 `replaceAsset` 时，源 `.assets` 文件存在于 zip 中，`matchField` 能唯一对应源 asset。
- 需要复制的 `.resource` 或其他 payload 文件已写入 `copyFiles`。
- 预览结果只命中预期 asset，且没有意外 skipped 操作。
