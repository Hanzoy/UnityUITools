# UnityUITools Package

## 组件

### UIBindTool

`UIBindTool` 是一个编辑器侧的 UI 绑定与代码生成工具，用于根据选中的 UI 面板生成绑定数据、字段定义和逻辑脚本模板。

包含内容：

- 编辑器脚本：`Editor/UIBindTool`
- 默认模板：`Templates/UIPanelTemplate.txt`

### UITreeJsonExporter

`UITreeJsonExporter` 用于把 Prefab UI 层级导出为精简 JSON，方便 Agent 或其他自动化工具读取节点层级和组件挂载信息。

导出内容包括：

- `version`
- `prefab`
- `prefabGuid`
- `generatedAt`
- 节点 `name`
- 节点相对 `path`
- 节点 `fileId`
- 节点 `active`
- 节点上挂载的全部组件类型名
- 子节点 `children`

## 使用方法

### 1. 安装包

可以通过 Git URL 引用：

```json
{
  "dependencies": {
    "com.hanzoy.uitools": "git@github.com:Hanzoy/UnityUITools.git"
  }
}
```

### 2. 准备模板

默认模板文件在：

`Packages/com.hanzoy.uitools/Templates/UIPanelTemplate.txt`

如果你的项目没有 `PFW.UI.UIBasePanel`，请先把模板改成你自己项目里的基类，或者删除该继承关系。

### 3. 打开工具

在 Unity 中选中一个带 `RectTransform` 的 UI 根节点，然后使用：

- 菜单：`GameObject/UI Binding`
- 快捷键：`Alt + E`

工具窗口标题为 `UI Bind Tool`。

### 3.1 导出 UI 层级 JSON

在 Project 窗口中选中一个 Prefab 资源，然后使用：

- 菜单：`Assets/UITools/Export UI Tree Json`

默认会导出到：

`Assets/UITreeJson/<PrefabName>.ui-tree.json`

也可以在编辑器脚本中直接调用：

```csharp
UITreeJsonExporter.ExportPrefabToJson(
    "Assets/UI/LoginPanel.prefab",
    "Assets/UITreeJson/LoginPanel.ui-tree.json");
```

或者通过 Unity BatchMode 调用：

```powershell
Unity.exe -batchmode -quit -projectPath "<ProjectPath>" -executeMethod UITreeJsonExporter.ExportFromCommandLine -uitreePrefabPath "Assets/UI/LoginPanel.prefab" -uitreeOutputPath "Assets/UITreeJson/LoginPanel.ui-tree.json"
```

如果 Unity 已经打开，或 BatchMode 被 licensing/project lock 阻塞，可以创建请求文件：

`Assets/UIBindRequests/LoginPanel.uitree-export.json`

```json
{
  "version": 1,
  "prefabPath": "Assets/UI/LoginPanel.prefab",
  "outputPath": "Assets/UITreeJson/LoginPanel.ui-tree.json",
  "status": "pending"
}
```

Unity 检测到该请求后会先把请求状态改为 `processing`，导出结束后再改为 `completed` 或 `failed`。该方式不依赖 Auto Refresh，编辑器内置轮询会定期扫描请求目录。

如果请求约 5 秒后仍保持 `pending`，可以在 Windows 上将焦点切到 Unity：

```powershell
Packages/com.hanzoy.uitools/Tools/UnityFocus/bin/win-x64/UnityFocus.exe --projectPath "<ProjectPath>" --timeoutSeconds 10
```

该工具只负责聚焦 Unity 编辑器窗口，不执行任意命令。如果需要 fallback，可以使用 PowerShell 脚本：

```powershell
powershell -ExecutionPolicy Bypass -File "Packages/com.hanzoy.uitools/Tools/FocusUnityEditor.ps1" -ProjectPath "<ProjectPath>" -TimeoutSeconds 10
```

Agent 调用规范见包根目录 `AGENTS.md`。

### 3.2 转换 UI 绑定规格 JSON

`*.uibind.json` 是面向 Agent 和人工 review 的绑定意图文件，`UIPanelBindings.asset` 是 Unity 工具实际使用的绑定数据。

示例：

```json
{
  "version": 1,
  "prefab": "Assets/UI/LoginPanel.prefab",
  "panelName": "LoginPanel",
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "bindings": [
    {
      "path": "Root/BtnLogin",
      "fileId": 10002,
      "component": "UnityEngine.UI.Button",
      "name": "btnLogin",
      "access": "Private"
    }
  ]
}
```

JSON 转绑定数据：

```csharp
UIBindSpecConverter.ImportJsonToAsset(
    "Assets/UIBindSpecs/LoginPanel.uibind.json",
    "Assets/UIBindData/LoginPanel.asset");
```

绑定数据转 JSON：

```csharp
UIBindSpecConverter.ExportAssetToJson(
    "Assets/UIBindData/LoginPanel.asset",
    "Assets/UIBindSpecs/LoginPanel.uibind.json");
```

菜单入口：

- `Assets/UITools/Import UIBind Json`
- `Assets/UITools/Export UIBind Json`

如果 Unity 已打开，也可以创建转换请求文件：

`Assets/UIBindRequests/LoginPanel.uibind-convert.json`

```json
{
  "version": 1,
  "action": "importJsonToAsset",
  "specPath": "Assets/UIBindSpecs/LoginPanel.uibind.json",
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "status": "pending"
}
```

`action` 也可以是 `exportAssetToJson`，此时使用 `assetPath` 和 `outputPath`。

生成绑定代码也可以创建请求文件：

`Assets/UIBindRequests/LoginPanel.uibind-generate.json`

```json
{
  "version": 1,
  "assetPath": "Assets/UIBindData/LoginPanel.asset",
  "registerAutoBinder": true,
  "status": "pending"
}
```

成功后请求会写回 `bindingScriptPath`、`mainScriptPath` 和 `mainScriptClassName`。

### 3.3 Agent 使用工具绑定 UI 的整体链路

当用户要求 Agent 使用工具完成 UI 绑定时，推荐流程是：

```text
Prefab
  -> 导出 *.ui-tree.json
  -> Agent 读取 UI 树并创建/更新 *.uibind.json
  -> 导入为 UIPanelBindings.asset
  -> 使用现有 UIBindTool 生成 *.Bind.cs / 主逻辑脚本 / 字段引用
  -> 验证生成结果
```

职责划分：

- `*.ui-tree.json`：Prefab 结构快照，只读，随 Prefab 变化重新导出。
- `*.uibind.json`：绑定意图文件，供 Agent 和人工 review 修改。
- `UIPanelBindings.asset`：Unity 内部绑定数据，由转换工具生成，不建议手改。
- `*.Bind.cs`：代码生成结果，由现有绑定生成器更新。

Agent 绑定时应优先编辑 `*.uibind.json`，再通过 `UIBindSpecConverter` 同步到 `.asset`，不要直接修改 `.asset`。

### 4. 配置生成参数

工具内部有一套设置项，首次使用至少需要确认这些字段：

- `bindDataFolder`
  - 绑定数据 `ScriptableObject` 的保存目录
- `generateUIBindScriptFolder`
  - 自动生成 `.Bind.cs` 的输出目录
- `generateUILogicScriptFolder`
  - 主逻辑脚本的输出目录
- `templateTextFilePath`
  - 逻辑脚本模板文件路径
- `useNamespace`
  - 是否启用命名空间
- `scriptNamespace`
  - 生成代码使用的命名空间
- `autoOpenGeneratedScripts`
  - 生成后是否自动打开脚本
- `generateManualBindingMethod`
  - 是否额外生成手动绑定方法

这些路径目前要求是 `Assets/` 开头的工程内路径。

### 5. 生成绑定

1. 选中目标 UI 面板根节点
2. 打开 `UI Bind Tool`
3. 在工具列表中选择要绑定的子节点和组件类型
4. 确认变量名、访问修饰符和组件前缀
5. 执行生成

生成结果通常包括：

- 一份绑定数据资源 `UIPanelBindings`
- 一份自动生成的绑定脚本 `*.Bind.cs`
- 一份逻辑脚本 `*.cs`

如果逻辑脚本已经存在，工具会跳过重新创建主脚本，但仍会更新绑定脚本。

### 6. 生成后的行为

- 工具会在脚本编译完成后，自动把生成的主脚本挂到目标对象上
- 如果开启了自动打开脚本，会尝试打开生成的主脚本
- 绑定字段会通过序列化方式自动赋值

## 当前限制

- The default generated panel template inherits from `PFW.UI.UIBasePanel`.
- If your target project does not provide `PFW.UI`, update the template before using script generation.

## 注意事项

- `UIBindTool` 目前是编辑器工具，不包含运行时组件。
- 当前版本是从测试 demo 迁移过来的初始版本，仍然建议你根据自己项目规范调整模板、输出路径和基类依赖。
- 绑定数据默认会保存到 `Assets/UIBindData/...`，代码默认会输出到 `Assets/Scripts/...`，实际使用前建议先改成你自己的目录规范。
