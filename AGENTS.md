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
Assets/UIBindRequests/
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

- `status`: `processing`, `completed`, or `failed`.
- `startedAt`: set when Unity has seen and started the request.
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

Prefer `prefabPath` when possible. Request files are detected by a lightweight editor poller, so this does not require Unity Auto Refresh to be enabled. After creating a request, wait about 5 seconds for `status` to become `processing`. If it is still `pending`, focus Unity and continue waiting. If Unity is compiling, paused, or not running editor updates, wait for the editor to become responsive.

If the request stays `pending` while Unity is in the background, focus the Unity editor and keep waiting. On Windows, use the fixed-purpose helper executable:

```powershell
Packages/com.hanzoy.uitools/Tools/UnityFocus/bin/win-x64/UnityFocus.exe --projectPath "D:\path\to\UnityProject" --timeoutSeconds 10
```

When the package is resolved in `Library/PackageCache`, call the executable from that resolved package path instead:

```powershell
Library/PackageCache/com.hanzoy.uitools@<hash>/Tools/UnityFocus/bin/win-x64/UnityFocus.exe --projectPath "D:\path\to\UnityProject" --timeoutSeconds 10
```

The executable is fixed-purpose and only focuses a Unity editor window; it does not execute arbitrary commands. The PowerShell helper remains available as a fallback:

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

## Convert UI Bind Specs

Use `*.uibind.json` as the editable binding intent file. Use `UIPanelBindings.asset` as the Unity editor asset consumed by the existing binding generator.

### Spec Schema

```json
{
  "version": 1,
  "prefab": "Assets/UI/LoginPanel.prefab",
  "prefabGuid": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "panelName": "LoginPanel",
  "className": "LoginPanel",
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "bindings": [
    {
      "path": "Root/BtnLogin",
      "fileId": 10002,
      "component": "UnityEngine.UI.Button",
      "shortType": "Button",
      "assemblyQualifiedName": "UnityEngine.UI.Button, UnityEngine.UI",
      "name": "btnLogin",
      "access": "Private"
    }
  ]
}
```

Agent-editable fields:

- `prefab`
- `bindings[].path`
- `bindings[].component`
- `bindings[].name`
- `bindings[].access`

Tool-maintained fields:

- `prefabGuid`
- `assetPath`
- `bindings[].fileId`
- `bindings[].shortType`
- `bindings[].assemblyQualifiedName`

### JSON to Asset

Preferred editor API:

```csharp
UIBindSpecConverter.ImportJsonToAsset(
    "Assets/UIBindSpecs/LoginPanel.uibind.json",
    "Assets/UIBindData/LoginPanel.asset");
```

BatchMode:

```powershell
Unity.exe -batchmode -quit -projectPath "D:\path\to\UnityProject" -executeMethod UIBindSpecConverter.ImportFromCommandLine -uibindSpecPath "Assets/UIBindSpecs/LoginPanel.uibind.json" -uibindAssetPath "Assets/UIBindData/LoginPanel.asset"
```

If `-uibindAssetPath` is omitted, the converter uses `assetPath` from the JSON, then falls back to `Assets/UIBindData/<PrefabName>.asset`.

### Asset to JSON

Preferred editor API:

```csharp
UIBindSpecConverter.ExportAssetToJson(
    "Assets/UIBindData/LoginPanel.asset",
    "Assets/UIBindSpecs/LoginPanel.uibind.json");
```

BatchMode:

```powershell
Unity.exe -batchmode -quit -projectPath "D:\path\to\UnityProject" -executeMethod UIBindSpecConverter.ExportFromCommandLine -uibindAssetPath "Assets/UIBindData/LoginPanel.asset" -uibindOutputPath "Assets/UIBindSpecs/LoginPanel.uibind.json"
```

Menu entries are also available under:

```text
Assets/UITools/Import UIBind Json
Assets/UITools/Export UIBind Json
```

### Open Editor Convert Request

Use this when Unity is already open or BatchMode is blocked. Create a request file under:

```text
Assets/UIBindRequests/
```

The file name must end with:

```text
.uibind-convert.json
```

Import JSON to asset:

```json
{
  "version": 1,
  "action": "importJsonToAsset",
  "specPath": "Assets/UIBindSpecs/LoginPanel.uibind.json",
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "status": "pending"
}
```

Export asset to JSON:

```json
{
  "version": 1,
  "action": "exportAssetToJson",
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "outputPath": "Assets/UIBindSpecs/LoginPanel.uibind.json",
  "status": "pending"
}
```

The request status follows the same lifecycle:

```text
pending -> processing -> completed/failed
```

If it stays `pending`, focus Unity with `Tools/UnityFocus/bin/win-x64/UnityFocus.exe` and continue waiting.

## Generate Binding Code

Use this after `*.uibind.json` has been imported into `UIPanelBindings.asset`.

### Open Editor Generate Request

Create a request file under:

```text
Assets/UIBindRequests/
```

The file name must end with:

```text
.uibind-generate.json
```

Example:

```json
{
  "version": 1,
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "registerAutoBinder": true,
  "status": "pending"
}
```

The request status follows:

```text
pending -> processing -> completed/failed
```

On success, the request is rewritten with:

- `bindingScriptPath`
- `mainScriptPath`
- `mainScriptClassName`

If `registerAutoBinder` is `true`, the processor registers the same auto-bind task used by the editor window flow, so Unity can attach the generated main script and assign serialized fields after script reload.

## End-to-End Binding Workflow

Use this workflow when the user asks an agent to bind UI with this tool, generate UI bindings, add missing bindings, update existing bindings, or sync binding data.

### 1. Locate Inputs

Resolve these paths first:

- Prefab path, for example `Assets/UI/LoginPanel.prefab`.
- UI tree output path, usually `Assets/UITreeJson/<PrefabName>.ui-tree.json`.
- Binding spec path, usually `Assets/UIBindSpecs/<PrefabName>.uibind.json`.
- Binding asset path, usually `Assets/UIBindData/<PrefabName>.asset`.

Prefer exact prefab paths over name search. If the user gives only a prefab name, search project assets and fail clearly if multiple prefabs match.

### 2. Export or Refresh UI Tree

Create or refresh `*.ui-tree.json` from the prefab. Prefer the open-editor request flow when Unity is already open:

```json
{
  "version": 1,
  "prefabPath": "Assets/UI/LoginPanel.prefab",
  "outputPath": "Assets/UITreeJson/LoginPanel.ui-tree.json",
  "status": "pending"
}
```

Wait for:

```text
pending -> processing -> completed/failed
```

If the request is still `pending` after about 5 seconds, focus Unity with `Tools/UnityFocus/bin/win-x64/UnityFocus.exe` and continue waiting. If Unity cannot be used, do not hand-edit prefab YAML unless the user explicitly accepts a partial fallback.

### 3. Read UI Tree

Use `*.ui-tree.json` to understand:

- Node paths.
- Mounted components.
- `fileId` values.
- Active/inactive hierarchy.

Do not edit `*.ui-tree.json`; regenerate it when the prefab changes.

### 4. Create or Update Binding Spec

Create or update `*.uibind.json` as the binding intent source. Include only the bindings the UI logic should access.

Recommended binding entry:

```json
{
  "path": "Root/BtnLogin",
  "fileId": 10002,
  "component": "UnityEngine.UI.Button",
  "name": "btnLogin",
  "access": "Private"
}
```

Rules:

- Use paths and component names from `*.ui-tree.json`.
- Preserve `fileId` when available.
- Field names must be valid C# identifiers and unique within the spec.
- Prefer existing naming conventions and component prefixes.
- Do not bind every node automatically; bind only nodes likely needed by UI logic.

### 5. Import Spec to Binding Asset

Convert `*.uibind.json` to `UIPanelBindings.asset` using `UIBindSpecConverter`.

Open-editor request example:

```json
{
  "version": 1,
  "action": "importJsonToAsset",
  "specPath": "Assets/UIBindSpecs/LoginPanel.uibind.json",
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "status": "pending"
}
```

Wait for `completed`. If `failed`, read `errorMessage`, fix the spec, and retry.

### 6. Generate Binding Code

After the binding asset is updated, create a generate request:

```json
{
  "version": 1,
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "registerAutoBinder": true,
  "status": "pending"
}
```

Wait for `completed`. This produces:

- `*.Bind.cs`
- Main logic script if missing
- Serialized field bindings on the target panel

If the request fails, read `errorMessage`, fix settings or binding data, and retry. Common failures are missing generation settings, missing template path, empty binding asset, or invalid output folders.

### 7. Verify

Check:

- Request status is `completed`.
- `*.uibind.json` is parseable.
- `UIPanelBindings.asset` exists and contains expected binding count.
- Generated `*.Bind.cs` contains expected fields.
- Unity compilation succeeds, if accessible.
- No requested binding was skipped due to missing path/component.

When reporting back, include changed/generated file paths and any warnings.

## Agent Rules

- Do not parse raw prefab YAML unless Unity is unavailable and the user accepts a partial fallback.
- Do not create temporary editor scripts just to export UI tree JSON.
- If BatchMode cannot start, create an open-editor request file instead of writing a temporary editor script.
- If an open-editor request is still `pending` after about 5 seconds, focus Unity with `Tools/UnityFocus/bin/win-x64/UnityFocus.exe` and continue waiting.
- If an open-editor request is `processing`, Unity has already seen it; keep waiting for `completed` or `failed`.
- Prefer `-uitreePrefabPath` over `-uitreePrefabName` when the path is known.
- If `-uitreePrefabName` reports multiple matches, rerun with `-uitreePrefabPath`.
- Do not edit generated `*.ui-tree.json` as a source of truth; regenerate it from the prefab.
- Use `*.ui-tree.json` to understand UI hierarchy, then create or update `*.uibind.json` for binding intent.
- Do not edit `UIPanelBindings.asset` directly; edit `*.uibind.json` and import it with `UIBindSpecConverter`.
- Generate binding code through `.uibind-generate.json`; do not create temporary editor scripts for this step.
