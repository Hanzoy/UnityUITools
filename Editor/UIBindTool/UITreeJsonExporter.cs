using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports a prefab hierarchy into a compact JSON snapshot for AI-assisted UI binding.
/// </summary>
public static class UITreeJsonExporter
{
    private const int CURRENT_VERSION = 1;
    private const string ROOT_PATH = "[ROOT]";
    private const string DEFAULT_OUTPUT_FOLDER = "Assets/UITreeJson";

    [MenuItem("Assets/UITools/Export UI Tree Json", false, 1000)]
    private static void ExportSelectedPrefab()
    {
        string prefabPath = GetSelectedPrefabPath();
        if (string.IsNullOrEmpty(prefabPath))
        {
            EditorUtility.DisplayDialog("Export UI Tree Json", "Please select a prefab asset.", "OK");
            return;
        }

        string outputFolder = DEFAULT_OUTPUT_FOLDER;
        EnsureFolderExists(outputFolder);

        string fileName = Path.GetFileNameWithoutExtension(prefabPath) + ".ui-tree.json";
        string jsonPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{fileName}");

        UITreeJsonExportResult result = ExportPrefabToJson(prefabPath, jsonPath);
        if (!result.success)
        {
            EditorUtility.DisplayDialog("Export UI Tree Json", result.errorMessage, "OK");
            return;
        }

        EditorUtility.DisplayDialog("Export UI Tree Json", $"Exported:\n{jsonPath}", "OK");
    }

    [MenuItem("Assets/UITools/Export UI Tree Json", true)]
    private static bool ValidateExportSelectedPrefab()
    {
        return !string.IsNullOrEmpty(GetSelectedPrefabPath());
    }

    /// <summary>
    /// Exports the prefab at <paramref name="prefabPath"/> into <paramref name="jsonPath"/>.
    /// Paths must be Unity project-relative asset paths.
    /// </summary>
    public static UITreeJsonExportResult ExportPrefabToJson(string prefabPath, string jsonPath)
    {
        var result = new UITreeJsonExportResult
        {
            success = false,
            prefabPath = prefabPath,
            jsonPath = jsonPath
        };

        if (string.IsNullOrEmpty(prefabPath))
            return result.WithError("Prefab path is empty.");

        if (string.IsNullOrEmpty(jsonPath))
            return result.WithError("Json path is empty.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return result.WithError($"Cannot load prefab: {prefabPath}");

        string outputFolder = Path.GetDirectoryName(jsonPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(outputFolder))
            EnsureFolderExists(outputFolder);

        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        var document = new UITreeJsonDocument
        {
            version = CURRENT_VERSION,
            prefab = prefabPath,
            prefabGuid = prefabGuid,
            generatedAt = DateTimeOffset.Now.ToString("o"),
            root = BuildNode(prefab, prefab)
        };

        string json = JsonUtility.ToJson(document, true);
        File.WriteAllText(jsonPath, json);
        AssetDatabase.ImportAsset(jsonPath);
        AssetDatabase.Refresh();

        result.success = true;
        return result;
    }

    public static string ExportPrefabToJsonString(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return string.Empty;

        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        var document = new UITreeJsonDocument
        {
            version = CURRENT_VERSION,
            prefab = prefabPath,
            prefabGuid = prefabGuid,
            generatedAt = DateTimeOffset.Now.ToString("o"),
            root = BuildNode(prefab, prefab)
        };

        return JsonUtility.ToJson(document, true);
    }

    private static UITreeJsonNode BuildNode(GameObject root, GameObject current)
    {
        string relativePath = UIPanelBindings.GetGameObjectRelativePath(root, current);
        if (string.IsNullOrEmpty(relativePath))
            relativePath = ROOT_PATH;

        var node = new UITreeJsonNode
        {
            name = current.name,
            path = relativePath,
            fileId = GetLocalFileId(current),
            active = current.activeSelf,
            components = CollectComponentTypeNames(current),
            children = new List<UITreeJsonNode>()
        };

        foreach (Transform child in current.transform)
        {
            node.children.Add(BuildNode(root, child.gameObject));
        }

        return node;
    }

    private static List<string> CollectComponentTypeNames(GameObject obj)
    {
        var componentNames = new List<string>();
        Component[] components = obj.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null)
            {
                componentNames.Add("MissingScript");
                continue;
            }

            Type type = component.GetType();
            componentNames.Add(type.FullName ?? type.Name);
        }

        return componentNames;
    }

    private static long GetLocalFileId(UnityEngine.Object obj)
    {
        if (obj == null)
            return 0;

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string _, out long fileId))
            return fileId;

        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        return (long)globalObjectId.targetObjectId;
    }

    private static string GetSelectedPrefabPath()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
            return string.Empty;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null ? path : string.Empty;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string normalizedPath = folderPath.Replace("\\", "/");
        string[] parts = normalizedPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            throw new ArgumentException($"Folder path must start with Assets/: {folderPath}");

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}

[Serializable]
public class UITreeJsonDocument
{
    public int version;
    public string prefab;
    public string prefabGuid;
    public string generatedAt;
    public UITreeJsonNode root;
}

[Serializable]
public class UITreeJsonNode
{
    public string name;
    public string path;
    public long fileId;
    public bool active;
    public List<string> components;
    public List<UITreeJsonNode> children;
}

public struct UITreeJsonExportResult
{
    public bool success;
    public string prefabPath;
    public string jsonPath;
    public string errorMessage;

    public UITreeJsonExportResult WithError(string message)
    {
        errorMessage = message;
        return this;
    }
}
