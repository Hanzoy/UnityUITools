using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// UI绑定数据管理器
/// 负责创建、加载和管理UIPanelBindings ScriptableObject
/// </summary>
public class UIBindDataManager
{
    private static UIBindToolSettingsData s_SettingsDataContainer;
    private static UIBindToolSettingsDataItem s_CurrentSettingsItem;
    private static readonly string BIND_DATA_FOLDER = "Assets/UIBindData"; // 默认值，会被设置覆盖
    private static readonly string BIND_DATA_EXTENSION = ".asset";

    /// <summary>
    /// 设置设置数据容器
    /// </summary>
    public static void SetSettingsDataContainer(UIBindToolSettingsData settingsDataContainer)
    {
        s_SettingsDataContainer = settingsDataContainer;
        // 设置默认选中的设置项
        if (s_SettingsDataContainer != null)
        {
            SetCurrentSettingsItem(s_SettingsDataContainer.GetLastSelectedSettingsDataItem());
        }
    }

    /// <summary>
    /// 设置当前使用的设置项
    /// </summary>
    public static void SetCurrentSettingsItem(UIBindToolSettingsDataItem settingsItem)
    {
        s_CurrentSettingsItem = settingsItem;
        s_CurrentSettingsItem?.UpdateInfoFormTemplateTextFile();
        // 更新最后选择的设置项名称
        if (s_SettingsDataContainer != null && settingsItem != null)
        {
            s_SettingsDataContainer.lastSelectedSettingsDataName = settingsItem.settingsDataName;
        }
    }

    /// <summary>
    /// 获取当前设置项
    /// </summary>
    public static UIBindToolSettingsDataItem GetCurrentSettingsItem()
    {
        return s_CurrentSettingsItem;
    }

    /// <summary>
    /// 获取绑定数据文件夹路径
    /// </summary>
    private static string GetBindDataFolder()
    {
        return s_CurrentSettingsItem != null && !string.IsNullOrEmpty(s_CurrentSettingsItem.bindDataFolder)
            ? s_CurrentSettingsItem.bindDataFolder
            : BIND_DATA_FOLDER;
    }

    /// <summary>
    /// 获取或创建指定面板的绑定数据
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>绑定数据ScriptableObject</returns>
    public static UIPanelBindings GetOrCreateBindingsForPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
        {
            Debug.LogWarning("目标面板为空，无法创建绑定数据");
            return null;
        }

        // 确保文件夹存在
        EnsureFolderExists();

        // 检查是否已存在绑定数据
        UIPanelBindings existingBindings = LoadBindingsForPanel(targetPanel);
        if (existingBindings != null)
        {
            return existingBindings;
        }

        // 创建新的绑定数据
        UIPanelBindings newBindings = ScriptableObject.CreateInstance<UIPanelBindings>();
        newBindings.Initialize(targetPanel);

        // 保存到Assets文件夹
        string assetPath = GetBindingsAssetPath(targetPanel);
        AssetDatabase.CreateAsset(newBindings, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"已创建新的绑定数据: {assetPath}");
        return newBindings;
    }

    /// <summary>
    /// 加载指定面板的绑定数据
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>绑定数据ScriptableObject，如果不存在则返回null</returns>
    public static UIPanelBindings LoadBindingsForPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
        {
            Debug.LogWarning("目标面板为空，无法加载绑定数据");
            return null;
        }

        string assetPath = GetBindingsAssetPath(targetPanel);
        if (File.Exists(assetPath))
        {
            return AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        }

        return null;
    }

    /// <summary>
    /// 通过路径加载绑定数据
    /// </summary>
    /// <param name="assetPath">绑定数据文件路径</param>
    /// <returns>绑定数据ScriptableObject，如果不存在则返回null</returns>
    public static UIPanelBindings LoadBindings(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("绑定数据路径为空，无法加载");
            return null;
        }

        if (File.Exists(assetPath))
        {
            return AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        }
        else
        {
            Debug.LogWarning($"绑定数据文件不存在: {assetPath}");
            return null;
        }
    }

    /// <summary>
    /// 保存绑定数据
    /// </summary>
    /// <param name="bindings">要保存的绑定数据</param>
    public static void SaveBindings(UIPanelBindings bindings)
    {
        if (bindings == null)
        {
            return;
        }

        bindings.lastModifiedTime = System.DateTime.Now;
        EditorUtility.SetDirty(bindings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 获取所有面板的绑定数据
    /// </summary>
    /// <returns>所有UIPanelBindings资源</returns>
    public static UIPanelBindings[] GetAllBindings()
    {
        var allDataGUIDs = AssetDatabase.FindAssets("", new[] { GetBindDataFolder() });
        UIPanelBindings[] bindings = new UIPanelBindings[allDataGUIDs.Length];
        for (int i = 0; i < allDataGUIDs.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(allDataGUIDs[i]);
            bindings[i] = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
            Debug.Log($"找到绑定数据: {assetPath}");
        }
        return bindings;
    }

    /// <summary>
    /// 确保绑定数据文件夹存在
    /// </summary>
    private static void EnsureFolderExists()
    {
        string folderPath = GetBindDataFolder();
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
    }

    public static string GetPanelBindingKey(GameObject targetPanel)
    {
        if (targetPanel == null)
            return string.Empty;

        GUID prefabGuid = GetPrefabGUID(targetPanel);
        if (prefabGuid != default)
            return prefabGuid.ToString();

        return $"SCENE:{UIPanelBindings.GetGameObjectFullPath(targetPanel)}";
    }

    public static bool IsBindingMatch(UIPanelBindings bindings, GameObject targetPanel)
    {
        if (bindings == null || targetPanel == null)
            return false;

        string targetKey = GetPanelBindingKey(targetPanel);
        if (!string.IsNullOrEmpty(bindings.targetPrefabGUID) && bindings.targetPrefabGUID == targetKey)
            return true;

        return string.IsNullOrEmpty(bindings.targetPrefabGUID)
               && bindings.targetPathInScene == UIPanelBindings.GetGameObjectFullPath(targetPanel);
    }

    /// <summary>
    /// 获取绑定数据的Asset路径
    /// </summary>
    /// <param name="targetPanel">目标UI面板</param>
    /// <returns>Asset路径</returns>
    public static string GetBindingsAssetPath(GameObject targetPanel)
    {
        string bindingKey = GetPanelBindingKey(targetPanel);
        //获取所有绑定
        var allBindings = GetAllBindings();
        //查找对应GUID的绑定
        foreach (var binding in allBindings)
        {
            if (binding != null && binding.targetPrefabGUID == bindingKey)
            {
                return AssetDatabase.GetAssetPath(binding);
            }
        }
        // 使用面板名称作为文件名，匹配仍然依赖GUID/场景路径保证稳定性
        string fileName = string.IsNullOrEmpty(targetPanel.name) ? bindingKey.Replace("SCENE:", "") : targetPanel.name;
        string safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        string assetPath = $"{GetBindDataFolder()}/{safeFileName}{BIND_DATA_EXTENSION}";
        return AssetDatabase.GenerateUniqueAssetPath(assetPath);
    }

    /// <summary>
    /// 获取绑定数据的Asset路径
    /// </summary>
    /// <param name="bindings">绑定数据对象</param>
    /// <returns>Asset路径</returns>
    public static string GetBindingsAssetPath(UIPanelBindings bindings)
    {
        if (bindings == null)
            return "";

        // 如果已有GUID，尝试通过GUID查找
        if (!string.IsNullOrEmpty(bindings.targetPrefabGUID))
        {
            string path = AssetDatabase.GetAssetPath(bindings);
            if (!string.IsNullOrEmpty(path))
                return path;
        }

        // 使用面板名称作为文件名，确保唯一性
        string fileName = bindings.targetName;
        string safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        string assetPath = $"{GetBindDataFolder()}/{safeFileName}{BIND_DATA_EXTENSION}";
        return AssetDatabase.GenerateUniqueAssetPath(assetPath);
    }

    private static string NormalizeBindingRelativePath(string relativePath)
    {
        return string.IsNullOrEmpty(relativePath) || relativePath == "[ROOT]" ? "[ROOT]" : relativePath;
    }

    private static bool TryGetPrefabSourceFileID(GameObject obj, out long fileID)
    {
        fileID = 0;
        if (obj == null)
            return false;

        GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(obj) ?? obj;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceObject, out string guid, out fileID))
            return false;

        return fileID != 0;
    }

    private static bool TryRepairBindingFileID(
        UIBindItem bindItem,
        GameObject panelInstance,
        Dictionary<string, long> prefabFileIDByPath,
        Dictionary<long, GameObject> instanceObjectByFileID)
    {
        if (bindItem == null || bindItem.targetObjectFileID != 0 || panelInstance == null)
            return false;

        GameObject fallbackObject = EditorUtility.InstanceIDToObject(bindItem.targetInstanceID) as GameObject;
        string fallbackPath = NormalizeBindingRelativePath(bindItem.targetObjectRelativePath);

        if (fallbackObject == null)
        {
            fallbackObject = fallbackPath == "[ROOT]"
                ? panelInstance
                : panelInstance.transform.Find(fallbackPath)?.gameObject;
        }

        if (fallbackObject == null && !string.IsNullOrEmpty(bindItem.targetObjectFullPathInScene))
        {
            fallbackObject = GameObject.Find(bindItem.targetObjectFullPathInScene);
        }

        if (fallbackObject != null && TryGetPrefabSourceFileID(fallbackObject, out long fileID))
        {
            bindItem.targetObjectFileID = fileID;
            instanceObjectByFileID[fileID] = fallbackObject;
            Debug.Log($"[UIBindDataManager] 已修复绑定 FileID: {bindItem.variableName} -> {fileID}");
            return true;
        }

        if (prefabFileIDByPath.TryGetValue(fallbackPath, out fileID))
        {
            bindItem.targetObjectFileID = fileID;
            Debug.Log($"[UIBindDataManager] 已通过路径修复绑定 FileID: {bindItem.variableName} -> {fileID}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 根据当前的实例更新绑定数据中记录的实例相关信息
    /// </summary>
    /// <param name="bindings"></param>
    /// <param name="panelInstance"></param>
    public static void UpdateBindingInstanceData(UIPanelBindings bindings, GameObject panelInstance)
    {
        if (bindings == null || panelInstance == null)
            return;
        if (bindings.bindings == null)
            bindings.bindings = new List<UIBindItem>();

        //更新实例数据
        bindings.targetPrefabGUID = GetPanelBindingKey(panelInstance);
        bindings.targetInstanceID = panelInstance.GetInstanceID();
        bindings.targetPathInScene = UIPanelBindings.GetGameObjectFullPath(panelInstance);
        bindings.targetName = panelInstance.name;
        //获取Prefab
        var prefabAsset = GetPrefabSourceRoot(panelInstance);
        if (prefabAsset == null)
        {
            Debug.LogWarning(
                $"[UIBindDataManager] 无法解析 Prefab Asset，跳过 FileID 修复: " +
                $"panel={panelInstance.name}, " +
                $"isPrefabInstance={PrefabUtility.IsPartOfPrefabInstance(panelInstance)}, " +
                $"isPrefabAsset={PrefabUtility.IsPartOfPrefabAsset(panelInstance)}, " +
                $"assetPath={AssetDatabase.GetAssetPath(panelInstance)}");
        }
        //记录Prefab子对象的FileID与相对于Prefab的路径 以Dictionary形式存储
        Dictionary<long, string> prefabPathByFileID = new Dictionary<long, string>();
        Dictionary<string, long> prefabFileIDByPath = new Dictionary<string, long>();
        Dictionary<long, GameObject> instanceObjectByFileID = new Dictionary<long, GameObject>();
        if (prefabAsset != null)
        {
            foreach (Transform child in prefabAsset.GetComponentsInChildren<Transform>(true))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(child.gameObject, out string guid, out long fileID);
                if (fileID == 0)
                    continue;

                string relativePath = NormalizeBindingRelativePath(UIPanelBindings.GetGameObjectRelativePath(prefabAsset, child.gameObject));
                prefabPathByFileID[fileID] = relativePath;
                prefabFileIDByPath[relativePath] = fileID;
            }

            foreach (Transform child in panelInstance.GetComponentsInChildren<Transform>(true))
            {
                if (!TryGetPrefabSourceFileID(child.gameObject, out long fileID))
                    continue;

                instanceObjectByFileID[fileID] = child.gameObject;
            }
        }

        if (prefabAsset != null)
        {
            int removedCount = 0;
            for (int i = bindings.bindings.Count - 1; i >= 0; i--)
            {
                var bindItem = bindings.bindings[i];
                if (bindItem == null)
                {
                    bindings.bindings.RemoveAt(i);
                    removedCount++;
                    continue;
                }

                TryRepairBindingFileID(bindItem, panelInstance, prefabFileIDByPath, instanceObjectByFileID);

                bool existsByFileID = bindItem.targetObjectFileID != 0 && prefabPathByFileID.ContainsKey(bindItem.targetObjectFileID);
                bool existsByPath = prefabFileIDByPath.ContainsKey(NormalizeBindingRelativePath(bindItem.targetObjectRelativePath));
                if (!existsByFileID && !existsByPath)
                {
                    bindings.bindings.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Debug.Log($"[UIBindDataManager] 已移除 {removedCount} 条目标节点不存在的绑定数据: {bindings.targetName}");
            }
        }

        //更新绑定数据
        foreach (var bindItem in bindings.bindings)
        {
            if (bindItem == null)
                continue;

            if (prefabAsset != null)
            {
                if (bindItem.targetObjectFileID == 0)
                {
                    TryRepairBindingFileID(bindItem, panelInstance, prefabFileIDByPath, instanceObjectByFileID);
                }

                if (bindItem.targetObjectFileID != 0 && instanceObjectByFileID.TryGetValue(bindItem.targetObjectFileID, out GameObject instanceObject))
                {
                    string instanceRelativePath = NormalizeBindingRelativePath(UIPanelBindings.GetGameObjectRelativePath(panelInstance, instanceObject));
                    bindItem.targetObjectRelativePath = instanceRelativePath;
                    bindItem.targetObjectFullPathInScene = UIPanelBindings.GetGameObjectFullPath(instanceObject);
                    bindItem.targetObjectName = instanceObject.name;
                    bindItem.targetInstanceID = instanceObject.GetInstanceID();
                    continue;
                }

                if (bindItem.targetObjectFileID != 0 && prefabPathByFileID.TryGetValue(bindItem.targetObjectFileID, out string path))
                {
                    bindItem.targetObjectFullPathInScene = bindings.targetPathInScene;
                    if (path != "[ROOT]")
                    {
                        bindItem.targetObjectRelativePath = path;
                        bindItem.targetObjectFullPathInScene += "/" + path;
                    }
                    else
                    {
                        bindItem.targetObjectRelativePath = "[ROOT]";
                    }

                    string objectName = path == "[ROOT]"
                        ? panelInstance.name
                        : path.Substring(path.LastIndexOf('/') + 1);
                    bindItem.targetObjectName = objectName;
                    bindItem.targetInstanceID = GameObject.Find(bindItem.targetObjectFullPathInScene)?.GetInstanceID() ?? 0;
                    continue;
                }
            }

            GameObject boundObject = bindItem.GetTargetObject();
            if (boundObject == null && !string.IsNullOrEmpty(bindItem.targetObjectRelativePath))
            {
                boundObject = bindItem.targetObjectRelativePath == "[ROOT]"
                    ? panelInstance
                    : panelInstance.transform.Find(bindItem.targetObjectRelativePath)?.gameObject;
            }

            if (boundObject == null)
                continue;

            string relativePath = UIPanelBindings.GetGameObjectRelativePath(panelInstance, boundObject);
            bindItem.targetObjectRelativePath = string.IsNullOrEmpty(relativePath) ? "[ROOT]" : relativePath;
            bindItem.targetObjectFullPathInScene = UIPanelBindings.GetGameObjectFullPath(boundObject);
            bindItem.targetObjectName = boundObject.name;
            bindItem.targetInstanceID = boundObject.GetInstanceID();
        }
        //保存修改
        SaveBindings(bindings);
    }

    public static GUID GetPrefabGUID(GameObject panel)
    {
        if (panel == null)
            return default;
        var prefabAsset = GetPrefabSourceRoot(panel);
        if (prefabAsset == null)
            return default;
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(prefabPath))
            return default;
        string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        if (string.IsNullOrEmpty(guid))
            return default;
        return new GUID(guid);
    }

    public static GameObject GetPrefabSourceRoot(GameObject instanceObj)
    {
        if (instanceObj == null)
            return null;

        if (PrefabUtility.IsPartOfPrefabAsset(instanceObj))
        {
            string assetPath = AssetDatabase.GetAssetPath(instanceObj.transform.root.gameObject);
            return string.IsNullOrEmpty(assetPath)
                ? instanceObj.transform.root.gameObject
                : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        var prefabStage = PrefabStageUtility.GetPrefabStage(instanceObj);
        if (prefabStage != null && !string.IsNullOrEmpty(prefabStage.assetPath))
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.assetPath);
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(instanceObj))
        {
            GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(instanceObj);
            return sourceObject != null ? sourceObject.transform.root.gameObject : null;
        }

        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(instanceObj);
        GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
        //Debug.Log($"{prefabAsset.name}对应的Prefab资源路径为： {AssetDatabase.GetAssetPath(prefabAsset)}");
        string rootPath = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(rootPath)) return null;
        if(rootPath.Split('/').Last().Split('.')[0] == prefabAsset.name)
            return prefabAsset;
        // 打开 prefab asset 的内容并在其中查找对应对象
        GameObject prefabContentsRoot = PrefabUtility.LoadPrefabContents(rootPath);
        try
        {
            // 这里需要一种方式把 sceneChild 映射到 prefabContents 中的对象。
            string relativePath = UIPanelBindings.GetGameObjectRelativePath(prefabContentsRoot,prefabAsset);
            // Debug.Log($"{prefabContentsRoot.name} {prefabAsset.name} {relativePath}");
            Transform targetInPrefab = prefabContentsRoot.transform.Find(relativePath);
            if (targetInPrefab != null)
            {
                // 在 prefab 内容中，检查这个对象是否是一个嵌套 Prefab 实例（即它引用了外部 prefab asset）
                var corresponding = PrefabUtility.GetCorrespondingObjectFromSource(targetInPrefab.gameObject);
                if (corresponding != null)
                {
                    // Debug.Log(AssetDatabase.GetAssetPath(corresponding));
                    return corresponding;
                }
                else
                    return prefabAsset;
            }
            else
                return null;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
        }
    }
    public static GameObject GetPrefabSourceRootEx(GameObject instanceObj)
    {
        if (instanceObj == null || !PrefabUtility.IsPartOfPrefabInstance(instanceObj))
            return null;

        // 获取父 Prefab 实例根
        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(instanceObj);
        Debug.Log($"父 Prefab 实例根:{instanceRoot.name}");
        // 如果当前对象是父 Prefab 的子对象，继续往下处理
        GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) as GameObject;
        Debug.Log($"父 Prefab 资源:{prefabAsset.name}");
        // 遍历父 Prefab 实例的所有子对象，查找嵌套 Prefab
        foreach (Transform child in instanceRoot.transform)
        {
            // 如果子对象本身是 Prefab 实例
            if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
            {
                GameObject childPrefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) as GameObject;
                if (childPrefabAsset != null)
                {
                    // 返回子 Prefab 资源
                    return childPrefabAsset;
                }
            }
        }

        // 如果没有找到嵌套的 Prefab，返回父 Prefab 资源
        return prefabAsset;
    }
}
