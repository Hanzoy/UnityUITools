# UnityUITools Package

## 组件

### UIBindTool

`UIBindTool` 是一个编辑器侧的 UI 绑定与代码生成工具，用于根据选中的 UI 面板生成绑定数据、字段定义和逻辑脚本模板。

包含内容：

- 编辑器脚本：`Editor/UIBindTool`
- 默认模板：`Templates/UIPanelTemplate.txt`

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
