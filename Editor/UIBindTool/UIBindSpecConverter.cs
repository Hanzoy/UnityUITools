#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts between AI-friendly *.uibind.json files and Unity UIPanelBindings assets.
/// </summary>
public static class UIBindSpecConverter
{
    private const int CURRENT_VERSION = 1;
    private const string ROOT_PATH = "[ROOT]";
    private const string DEFAULT_SPEC_FOLDER = "Assets/UIBindSpecs";
    private const string DEFAULT_ASSET_FOLDER = "Assets/UIBindData";

    private const string SPEC_PATH_ARG = "-uibindSpecPath";
    private const string ASSET_PATH_ARG = "-uibindAssetPath";
    private const string PREFAB_PATH_ARG = "-uibindPrefabPath";
    private const string OUTPUT_PATH_ARG = "-uibindOutputPath";
    private const string OUTPUT_FOLDER_ARG = "-uibindOutputFolder";

    [MenuItem("Assets/UITools/Export UIBind Json", false, 1001)]
    private static void ExportSelectedAssetToJson()
    {
        string assetPath = GetSelectedAssetPath<UIPanelBindings>();
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("Export UIBind Json", "Please select a UIPanelBindings asset.", "OK");
            return;
        }

        UIPanelBindings bindings = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        string outputFolder = DEFAULT_SPEC_FOLDER;
        EnsureFolderExists(outputFolder);

        string fileName = string.IsNullOrEmpty(bindings.targetName) ? "UIBinding" : bindings.targetName;
        string jsonPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{fileName}.uibind.json");
        UIBindSpecConvertResult result = ExportAssetToJson(assetPath, jsonPath);
        if (!result.success)
        {
            EditorUtility.DisplayDialog("Export UIBind Json", result.errorMessage, "OK");
            return;
        }

        EditorUtility.DisplayDialog("Export UIBind Json", $"Exported:\n{jsonPath}", "OK");
    }

    [MenuItem("Assets/UITools/Export UIBind Json", true)]
    private static bool ValidateExportSelectedAssetToJson()
    {
        return !string.IsNullOrEmpty(GetSelectedAssetPath<UIPanelBindings>());
    }

    [MenuItem("Assets/UITools/Import UIBind Json", false, 1002)]
    private static void ImportSelectedJsonToAsset()
    {
        string specPath = GetSelectedTextAssetPath(".uibind.json");
        if (string.IsNullOrEmpty(specPath))
        {
            EditorUtility.DisplayDialog("Import UIBind Json", "Please select a *.uibind.json asset.", "OK");
            return;
        }

        UIBindSpecConvertResult result = ImportJsonToAsset(specPath, string.Empty);
        if (!result.success)
        {
            EditorUtility.DisplayDialog("Import UIBind Json", result.errorMessage, "OK");
            return;
        }

        EditorUtility.DisplayDialog("Import UIBind Json", $"Imported:\n{result.assetPath}", "OK");
    }

    [MenuItem("Assets/UITools/Import UIBind Json", true)]
    private static bool ValidateImportSelectedJsonToAsset()
    {
        return !string.IsNullOrEmpty(GetSelectedTextAssetPath(".uibind.json"));
    }

    public static UIBindSpecConvertResult ImportJsonToAsset(string specPath, string assetPath)
    {
        var result = new UIBindSpecConvertResult
        {
            success = false,
            specPath = specPath,
            assetPath = assetPath,
            warnings = new List<string>()
        };

        if (string.IsNullOrEmpty(specPath))
            return result.WithError("Spec path is empty.");

        if (!File.Exists(specPath))
            return result.WithError($"Spec file does not exist: {specPath}");

        UIBindSpecDocument spec;
        try
        {
            spec = JsonUtility.FromJson<UIBindSpecDocument>(File.ReadAllText(specPath));
        }
        catch (Exception e)
        {
            return result.WithError($"Failed to parse spec json: {e.Message}");
        }

        if (spec == null)
            return result.WithError("Spec json is empty or invalid.");

        if (string.IsNullOrEmpty(spec.prefab))
            return result.WithError("Spec prefab path is empty.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);
        if (prefab == null)
            return result.WithError($"Cannot load prefab: {spec.prefab}");

        if (string.IsNullOrEmpty(assetPath))
            assetPath = string.IsNullOrEmpty(spec.assetPath) ? GetDefaultAssetPath(prefab) : spec.assetPath;

        EnsureFolderExists(Path.GetDirectoryName(assetPath)?.Replace("\\", "/"));

        UIPanelBindings bindings = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        bool createdAsset = false;
        if (bindings == null)
        {
            bindings = ScriptableObject.CreateInstance<UIPanelBindings>();
            AssetDatabase.CreateAsset(bindings, assetPath);
            createdAsset = true;
        }

        bindings.Initialize(prefab);
        bindings.targetPrefabGUID = AssetDatabase.AssetPathToGUID(spec.prefab);
        bindings.targetPathInScene = UIPanelBindings.GetGameObjectFullPath(prefab);
        bindings.targetName = string.IsNullOrEmpty(spec.panelName) ? prefab.name : spec.panelName;
        bindings.bindings = new List<UIBindItem>();

        Dictionary<long, GameObject> objectByFileId = BuildObjectByFileId(prefab);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UIBindSpecBinding bindingSpec in spec.bindings ?? new List<UIBindSpecBinding>())
        {
            if (bindingSpec == null)
                continue;

            string normalizedPath = NormalizePath(bindingSpec.path);
            GameObject target = ResolveTargetObject(prefab, bindingSpec.fileId, normalizedPath, objectByFileId);
            if (target == null)
            {
                result.warnings.Add($"Skipped binding '{bindingSpec.name}': target not found. path={bindingSpec.path}, fileId={bindingSpec.fileId}");
                continue;
            }

            Type componentType = ResolveComponentType(bindingSpec);
            if (componentType == null)
            {
                result.warnings.Add($"Skipped binding '{bindingSpec.name}': component type not found. component={bindingSpec.component}");
                continue;
            }

            if (componentType != typeof(GameObject) && target.GetComponent(componentType) == null)
            {
                result.warnings.Add($"Skipped binding '{bindingSpec.name}': component '{componentType.FullName}' is not on '{normalizedPath}'.");
                continue;
            }

            string variableName = string.IsNullOrEmpty(bindingSpec.name)
                ? GenerateVariableName(componentType, target.name)
                : bindingSpec.name;
            variableName = MakeUnique(variableName, usedNames);

            AccessModifier access = ParseAccess(bindingSpec.access);
            var bindItem = new UIBindItem(target, target, prefab, componentType, access, variableName);
            bindItem.targetObjectRelativePath = NormalizePath(UIPanelBindings.GetGameObjectRelativePath(prefab, target));
            bindItem.targetObjectFullPathInScene = UIPanelBindings.GetGameObjectFullPath(target);
            bindItem.targetObjectName = target.name;
            bindItem.targetObjectFileID = GetLocalFileId(target);
            bindings.bindings.Add(bindItem);
        }

        bindings.lastModifiedTime = DateTime.Now;
        EditorUtility.SetDirty(bindings);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        AssetDatabase.Refresh();

        result.success = true;
        result.assetPath = assetPath;
        result.createdAsset = createdAsset;
        return result;
    }

    public static UIBindSpecConvertResult ExportAssetToJson(string assetPath, string specPath)
    {
        var result = new UIBindSpecConvertResult
        {
            success = false,
            assetPath = assetPath,
            specPath = specPath,
            warnings = new List<string>()
        };

        if (string.IsNullOrEmpty(assetPath))
            return result.WithError("Asset path is empty.");

        UIPanelBindings bindings = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(assetPath);
        if (bindings == null)
            return result.WithError($"Cannot load UIPanelBindings asset: {assetPath}");

        if (string.IsNullOrEmpty(specPath))
        {
            EnsureFolderExists(DEFAULT_SPEC_FOLDER);
            string fileName = string.IsNullOrEmpty(bindings.targetName) ? "UIBinding" : bindings.targetName;
            specPath = $"{DEFAULT_SPEC_FOLDER}/{fileName}.uibind.json";
        }

        string prefabPath = ResolvePrefabPath(bindings);
        var document = new UIBindSpecDocument
        {
            version = CURRENT_VERSION,
            prefab = prefabPath,
            prefabGuid = bindings.targetPrefabGUID,
            panelName = bindings.targetName,
            className = bindings.targetName,
            assetPath = assetPath,
            generatedAt = DateTimeOffset.Now.ToString("o"),
            bindings = new List<UIBindSpecBinding>()
        };

        foreach (UIBindItem bindItem in bindings.bindings ?? new List<UIBindItem>())
        {
            if (bindItem == null)
                continue;

            document.bindings.Add(new UIBindSpecBinding
            {
                path = NormalizePath(bindItem.targetObjectRelativePath),
                fileId = bindItem.targetObjectFileID,
                component = bindItem.componentTypeName,
                shortType = bindItem.shortTypeName,
                assemblyQualifiedName = bindItem.assemblyQualifiedName,
                name = bindItem.variableName,
                access = bindItem.accessModifier.ToString()
            });
        }

        EnsureFolderExists(Path.GetDirectoryName(specPath)?.Replace("\\", "/"));
        File.WriteAllText(specPath, JsonUtility.ToJson(document, true));
        AssetDatabase.ImportAsset(specPath);
        AssetDatabase.Refresh();

        result.success = true;
        result.specPath = specPath;
        return result;
    }

    public static void ImportFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string specPath = GetArgumentValue(args, SPEC_PATH_ARG);
        string assetPath = GetArgumentValue(args, ASSET_PATH_ARG);

        UIBindSpecConvertResult result = ImportJsonToAsset(specPath, assetPath);
        LogAndExit(result, "Import");
    }

    public static void ExportFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string assetPath = GetArgumentValue(args, ASSET_PATH_ARG);
        string outputPath = GetArgumentValue(args, OUTPUT_PATH_ARG);
        string outputFolder = GetArgumentValue(args, OUTPUT_FOLDER_ARG);
        string prefabPath = GetArgumentValue(args, PREFAB_PATH_ARG);

        if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(prefabPath))
            assetPath = FindBindingAssetForPrefab(prefabPath);

        if (string.IsNullOrEmpty(outputPath) && !string.IsNullOrEmpty(outputFolder) && !string.IsNullOrEmpty(assetPath))
            outputPath = $"{outputFolder.TrimEnd('/', '\\')}/{Path.GetFileNameWithoutExtension(assetPath)}.uibind.json";

        UIBindSpecConvertResult result = ExportAssetToJson(assetPath, outputPath);
        LogAndExit(result, "Export");
    }

    private static void LogAndExit(UIBindSpecConvertResult result, string operation)
    {
        if (result.success)
        {
            foreach (string warning in result.warnings)
                Debug.LogWarning($"[UIBindSpecConverter] {warning}");

            Debug.Log($"[UIBindSpecConverter] {operation} succeeded. asset={result.assetPath}, spec={result.specPath}");
            ExitBatchMode(0);
            return;
        }

        Debug.LogError($"[UIBindSpecConverter] {operation} failed: {result.errorMessage}");
        ExitBatchMode(1);
    }

    private static Dictionary<long, GameObject> BuildObjectByFileId(GameObject prefab)
    {
        var result = new Dictionary<long, GameObject>();
        foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            long fileId = GetLocalFileId(transform.gameObject);
            if (fileId != 0)
                result[fileId] = transform.gameObject;
        }

        return result;
    }

    private static GameObject ResolveTargetObject(GameObject prefab, long fileId, string path, Dictionary<long, GameObject> objectByFileId)
    {
        if (fileId != 0 && objectByFileId.TryGetValue(fileId, out GameObject targetByFileId))
            return targetByFileId;

        if (string.IsNullOrEmpty(path) || path == ROOT_PATH)
            return prefab;

        return prefab.transform.Find(path)?.gameObject;
    }

    private static Type ResolveComponentType(UIBindSpecBinding bindingSpec)
    {
        if (!string.IsNullOrEmpty(bindingSpec.assemblyQualifiedName))
        {
            Type type = Type.GetType(bindingSpec.assemblyQualifiedName);
            if (type != null)
                return type;
        }

        string component = bindingSpec.component;
        if (string.IsNullOrEmpty(component))
            component = bindingSpec.shortType;

        if (string.IsNullOrEmpty(component))
            return null;

        if (string.Equals(component, "GameObject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(component, typeof(GameObject).FullName, StringComparison.OrdinalIgnoreCase))
            return typeof(GameObject);

        Type directType = Type.GetType(component);
        if (directType != null)
            return directType;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            Type type = types.FirstOrDefault(t =>
                t != null &&
                (string.Equals(t.FullName, component, StringComparison.Ordinal) ||
                 string.Equals(t.Name, component, StringComparison.Ordinal)));
            if (type != null)
                return type;
        }

        return null;
    }

    private static string ResolvePrefabPath(UIPanelBindings bindings)
    {
        if (bindings == null || string.IsNullOrEmpty(bindings.targetPrefabGUID) || bindings.targetPrefabGUID.StartsWith("SCENE:"))
            return string.Empty;

        return AssetDatabase.GUIDToAssetPath(bindings.targetPrefabGUID);
    }

    private static string FindBindingAssetForPrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return string.Empty;

        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        string[] guids = AssetDatabase.FindAssets("t:UIPanelBindings");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UIPanelBindings bindings = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(path);
            if (bindings != null && bindings.targetPrefabGUID == prefabGuid)
                return path;
        }

        return string.Empty;
    }

    private static string GetDefaultAssetPath(GameObject prefab)
    {
        EnsureFolderExists(DEFAULT_ASSET_FOLDER);
        string safeFileName = string.Join("_", prefab.name.Split(Path.GetInvalidFileNameChars()));
        return $"{DEFAULT_ASSET_FOLDER}/{safeFileName}.asset";
    }

    private static string GenerateVariableName(Type componentType, string objectName)
    {
        string prefix = componentType == typeof(GameObject) ? "go" : componentType.Name.ToLowerInvariant();
        return UIBindNameUtility.ToCamelCaseWithPrefix(prefix, objectName, "binding");
    }

    private static string MakeUnique(string variableName, HashSet<string> usedNames)
    {
        string baseName = string.IsNullOrEmpty(variableName) ? "binding" : variableName;
        string finalName = baseName;
        int suffix = 1;
        while (usedNames.Contains(finalName))
        {
            finalName = $"{baseName}{suffix}";
            suffix++;
        }

        usedNames.Add(finalName);
        return finalName;
    }

    private static AccessModifier ParseAccess(string access)
    {
        if (Enum.TryParse(access, true, out AccessModifier value))
            return value;

        return AccessModifier.Private;
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

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) || path == ROOT_PATH ? ROOT_PATH : path;
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

    private static string GetSelectedAssetPath<T>() where T : UnityEngine.Object
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
            return string.Empty;

        string path = AssetDatabase.GetAssetPath(selected);
        return AssetDatabase.LoadAssetAtPath<T>(path) != null ? path : string.Empty;
    }

    private static string GetSelectedTextAssetPath(string suffix)
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
            return string.Empty;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return path;
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
}

[Serializable]
public class UIBindSpecDocument
{
    public int version = 1;
    public string prefab;
    public string prefabGuid;
    public string panelName;
    public string className;
    public string assetPath;
    public string generatedAt;
    public List<UIBindSpecBinding> bindings = new List<UIBindSpecBinding>();
}

[Serializable]
public class UIBindSpecBinding
{
    public string path;
    public long fileId;
    public string component;
    public string shortType;
    public string assemblyQualifiedName;
    public string name;
    public string access = "Private";
}

public struct UIBindSpecConvertResult
{
    public bool success;
    public bool createdAsset;
    public string specPath;
    public string assetPath;
    public string errorMessage;
    public List<string> warnings;

    public UIBindSpecConvertResult WithError(string message)
    {
        if (warnings == null)
            warnings = new List<string>();

        errorMessage = message;
        return this;
    }
}
#endif
