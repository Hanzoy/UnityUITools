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

### Open Editor Request File

Use this when Unity is already open or BatchMode is blocked by licensing/project lock issues. Create a request file under:

```text
Assets/UITreeJsonRequests/
```

The file name must end with:

```text
.uitree-export.json
```

Example request:

```json
{
  "version": 1,
  "prefabPath": "Assets/UI/LoginPanel.prefab",
  "outputPath": "Assets/UITreeJson/LoginPanel.ui-tree.json",
  "status": "pending"
}
```

When the open Unity editor sees this file, `UITreeJsonExportRequestProcessor` exports the UI tree and rewrites the request with:

- `status`: `completed` or `failed`.
- `completedAt`: completion timestamp.
- `resultPath`: generated JSON path.
- `errorMessage`: failure message, if any.

If `prefabPath` is unknown, `prefabName` and `outputFolder` are also supported:

```json
{
  "version": 1,
  "prefabName": "LoginPanel",
  "outputFolder": "Assets/UITreeJson",
  "status": "pending"
}
```

Prefer `prefabPath` when possible. Request files are detected by a lightweight editor poller, so this does not require Unity Auto Refresh to be enabled. If Unity is compiling, paused, or not running editor updates, wait for the editor to become responsive.

If the request does not complete while Unity is in the background, focus the Unity editor and keep waiting. On Windows, use:

```powershell
powershell -ExecutionPolicy Bypass -File "Packages/com.hanzoy.uitools/Tools/FocusUnityEditor.ps1" -ProjectPath "D:\path\to\UnityProject" -TimeoutSeconds 10
```

When the package is resolved in `Library/PackageCache`, call the script from that resolved package path instead:

```powershell
powershell -ExecutionPolicy Bypass -File "Library/PackageCache/com.hanzoy.uitools@<hash>/Tools/FocusUnityEditor.ps1" -ProjectPath "D:\path\to\UnityProject" -TimeoutSeconds 10
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
- If BatchMode cannot start, create an open-editor request file instead of writing a temporary editor script.
- If an open-editor request stays pending, focus Unity with `Tools/FocusUnityEditor.ps1` and continue waiting.
- Prefer `-uitreePrefabPath` over `-uitreePrefabName` when the path is known.
- If `-uitreePrefabName` reports multiple matches, rerun with `-uitreePrefabPath`.
- Do not edit generated `*.ui-tree.json` as a source of truth; regenerate it from the prefab.
- Use `*.ui-tree.json` to understand UI hierarchy, then create or update `*.uibind.json` for binding intent.
