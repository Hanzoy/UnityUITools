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
    private const string PREFAB_PATH_ARG = "-uitreePrefabPath";
    private const string PREFAB_NAME_ARG = "-uitreePrefabName";
    private const string OUTPUT_PATH_ARG = "-uitreeOutputPath";
    private const string OUTPUT_FOLDER_ARG = "-uitreeOutputFolder";

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

    /// <summary>
    /// Unity batchmode entry point.
    /// Example:
    /// Unity.exe -batchmode -quit -projectPath Project -executeMethod UITreeJsonExporter.ExportFromCommandLine
    ///     -uitreePrefabPath Assets/UI/LoginPanel.prefab
    ///     -uitreeOutputPath Assets/UITreeJson/LoginPanel.ui-tree.json
    /// </summary>
    public static void ExportFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string prefabPath = GetArgumentValue(args, PREFAB_PATH_ARG);
        string prefabName = GetArgumentValue(args, PREFAB_NAME_ARG);
        string outputPath = GetArgumentValue(args, OUTPUT_PATH_ARG);
        string outputFolder = GetArgumentValue(args, OUTPUT_FOLDER_ARG);

        UITreeJsonExportResult result = ExportFromCommandLine(prefabPath, prefabName, outputPath, outputFolder);
        if (result.success)
        {
            Debug.Log($"[UITreeJsonExporter] Exported UI tree json: {result.jsonPath}");
            ExitBatchMode(0);
            return;
        }

        Debug.LogError($"[UITreeJsonExporter] Export failed: {result.errorMessage}");
        ExitBatchMode(1);
    }

    public static UITreeJsonExportResult ExportFromCommandLine(
        string prefabPath,
        string prefabName,
        string outputPath,
        string outputFolder = null)
    {
        var result = new UITreeJsonExportResult
        {
            success = false,
            prefabPath = prefabPath,
            jsonPath = outputPath
        };

        if (string.IsNullOrEmpty(prefabPath))
        {
            UITreeJsonPrefabResolveResult resolveResult = ResolvePrefabPath(prefabName);
            if (!resolveResult.success)
                return result.WithError(resolveResult.errorMessage);

            prefabPath = resolveResult.prefabPath;
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            if (string.IsNullOrEmpty(outputFolder))
                outputFolder = DEFAULT_OUTPUT_FOLDER;

            string fileName = Path.GetFileNameWithoutExtension(prefabPath) + ".ui-tree.json";
            outputPath = $"{outputFolder.TrimEnd('/', '\\')}/{fileName}";
        }

        return ExportPrefabToJson(prefabPath, outputPath);
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

    private static UITreeJsonPrefabResolveResult ResolvePrefabPath(string prefabName)
    {
        var result = new UITreeJsonPrefabResolveResult { success = false };
        if (string.IsNullOrEmpty(prefabName))
            return result.WithError($"Missing {PREFAB_PATH_ARG} or {PREFAB_NAME_ARG}.");

        if (prefabName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabName);
            if (prefab != null)
            {
                result.success = true;
                result.prefabPath = prefabName;
                return result;
            }
        }

        string normalizedPrefabName = Path.GetFileNameWithoutExtension(prefabName);
        string[] guids = AssetDatabase.FindAssets($"{normalizedPrefabName} t:Prefab");
        var matches = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, normalizedPrefabName, StringComparison.OrdinalIgnoreCase))
                matches.Add(path);
        }

        if (matches.Count == 0)
            return result.WithError($"Cannot find prefab named: {prefabName}");

        if (matches.Count > 1)
            return result.WithError($"Multiple prefabs named '{prefabName}' were found. Use {PREFAB_PATH_ARG}. Matches: {string.Join(", ", matches)}");

        result.success = true;
        result.prefabPath = matches[0];
        return result;
    }

    private static string GetArgumentValue(string[] args, string argumentName)
    {
        if (args == null || string.IsNullOrEmpty(argumentName))
            return string.Empty;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return string.Empty;
    }

    private static void ExitBatchMode(int exitCode)
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(exitCode);
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

public struct UITreeJsonPrefabResolveResult
{
    public bool success;
    public string prefabPath;
    public string errorMessage;

    public UITreeJsonPrefabResolveResult WithError(string message)
    {
        errorMessage = message;
        return this;
    }
}
