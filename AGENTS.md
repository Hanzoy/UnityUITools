# Agent Usage Guide

This package provides Unity editor tools for AI-assisted UI binding. Agents should use the stable APIs and batch entry points in this file instead of creating temporary editor scripts in the consumer project.

## Export UI Tree JSON

Use this when the user asks for a prefab UI tree, node JSON, UI hierarchy JSON, or a `*.ui-tree.json` file.

### Preferred Entry Point

```csharp
UITreeJsonExporter.ExportPrefabToJson(
    "Assets/UI/LoginPanel.prefab",
    "Assets/UITreeJson/LoginPanel.ui-tree.json");
```

### Unity BatchMode

Use `UITreeJsonExporter.ExportFromCommandLine` with either a prefab path or an exact prefab name.

Windows example:

```powershell
Unity.exe -batchmode -quit -projectPath "D:\path\to\UnityProject" -executeMethod UITreeJsonExporter.ExportFromCommandLine -uitreePrefabPath "Assets/UI/LoginPanel.prefab" -uitreeOutputPath "Assets/UITreeJson/LoginPanel.ui-tree.json"
```

Prefab name example:

```powershell
Unity.exe -batchmode -quit -projectPath "D:\path\to\UnityProject" -executeMethod UITreeJsonExporter.ExportFromCommandLine -uitreePrefabName "LoginPanel" -uitreeOutputFolder "Assets/UITreeJson"
```

Arguments:

- `-uitreePrefabPath`: Project-relative prefab path. Prefer this when known.
- `-uitreePrefabName`: Exact prefab asset name without requiring a path. Fails if multiple prefabs share the name.
- `-uitreeOutputPath`: Project-relative JSON output path.
- `-uitreeOutputFolder`: Project-relative output folder. Used only when `-uitreeOutputPath` is omitted.

Default output:

```text
Assets/UITreeJson/<PrefabName>.ui-tree.json
```

## Output Schema

The exported JSON is intentionally compact. It preserves hierarchy and component type names, not component internals.

```json
{
  "version": 1,
  "prefab": "Assets/UI/LoginPanel.prefab",
  "prefabGuid": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "generatedAt": "2026-05-14T19:30:00.0000000+08:00",
  "root": {
    "name": "LoginPanel",
    "path": "[ROOT]",
    "fileId": 10001,
    "active": true,
    "components": [
      "UnityEngine.RectTransform",
      "UnityEngine.CanvasGroup",
      "Game.UI.LoginPanel"
    ],
    "children": []
  }
}
```

Node fields:

- `name`: GameObject name.
- `path`: Path relative to prefab root. Root is `[ROOT]`.
- `fileId`: Unity local file id for stable matching.
- `active`: `GameObject.activeSelf`.
- `components`: All mounted component type names. Missing scripts are emitted as `MissingScript`.
- `children`: Child nodes in transform order.

## Agent Rules

- Do not parse raw prefab YAML unless Unity is unavailable and the user accepts a partial fallback.
- Do not create temporary editor scripts just to export UI tree JSON.
- Prefer `-uitreePrefabPath` over `-uitreePrefabName` when the path is known.
- If `-uitreePrefabName` reports multiple matches, rerun with `-uitreePrefabPath`.
- Do not edit generated `*.ui-tree.json` as a source of truth; regenerate it from the prefab.
- Use `*.ui-tree.json` to understand UI hierarchy, then create or update `*.uibind.json` for binding intent.
