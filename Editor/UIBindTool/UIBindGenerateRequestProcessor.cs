#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Processes file-based binding code generation requests inside an already-open Unity editor.
/// </summary>
[InitializeOnLoad]
public static class UIBindGenerateRequestProcessor
{
    public const string REQUEST_FOLDER = "Assets/UIBindRequests";
    public const string REQUEST_SUFFIX = ".uibind-generate.json";

    private const double POLL_INTERVAL_SECONDS = 2.0d;
    private const string STATUS_PENDING = "pending";
    private const string STATUS_PROCESSING = "processing";
    private const string STATUS_COMPLETED = "completed";
    private const string STATUS_FAILED = "failed";
    private const string SETTINGS_FOLDER = "Assets/Settings";
    private const string SETTINGS_ASSET_PATH = SETTINGS_FOLDER + "/UIBindToolSettingsData.asset";

    private static bool s_IsProcessingScheduled;
    private static double s_NextPollTime;

    static UIBindGenerateRequestProcessor()
    {
        EditorApplication.update -= PollPendingRequests;
        EditorApplication.update += PollPendingRequests;
        ScheduleProcessPendingRequests();
    }

    public static void ProcessPendingRequests()
    {
        s_IsProcessingScheduled = false;

        if (!Directory.Exists(REQUEST_FOLDER))
            return;

        string[] requestPaths = Directory.GetFiles(REQUEST_FOLDER, "*" + REQUEST_SUFFIX, SearchOption.TopDirectoryOnly);
        foreach (string requestPath in requestPaths)
        {
            ProcessRequest(requestPath.Replace("\\", "/"));
        }
    }

    public static void ScheduleProcessPendingRequests()
    {
        if (s_IsProcessingScheduled)
            return;

        s_IsProcessingScheduled = true;
        EditorApplication.delayCall += ProcessPendingRequests;
    }

    public static bool IsRequestPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith(REQUEST_FOLDER + "/", StringComparison.OrdinalIgnoreCase) &&
               assetPath.EndsWith(REQUEST_SUFFIX, StringComparison.OrdinalIgnoreCase);
    }

    private static void ProcessRequest(string requestPath)
    {
        UIBindGenerateRequest request;
        try
        {
            request = JsonUtility.FromJson<UIBindGenerateRequest>(File.ReadAllText(requestPath));
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIBindGenerateRequestProcessor] Failed to read request: {requestPath}. {e.Message}");
            return;
        }

        if (request == null)
            return;

        if (!string.IsNullOrEmpty(request.status) &&
            !string.Equals(request.status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        request.status = STATUS_PROCESSING;
        request.startedAt = DateTimeOffset.Now.ToString("o");
        request.completedAt = string.Empty;
        request.bindingScriptPath = string.Empty;
        request.mainScriptPath = string.Empty;
        request.mainScriptClassName = string.Empty;
        request.errorMessage = string.Empty;
        WriteRequest(requestPath, request);

        UIBindGenerateResult result = Generate(request);
        request.completedAt = DateTimeOffset.Now.ToString("o");
        request.bindingScriptPath = result.bindingScriptPath;
        request.mainScriptPath = result.mainScriptPath;
        request.mainScriptClassName = result.mainScriptClassName;
        request.errorMessage = result.errorMessage;
        request.status = result.success ? STATUS_COMPLETED : STATUS_FAILED;
        WriteRequest(requestPath, request);

        if (result.success)
            Debug.Log($"[UIBindGenerateRequestProcessor] Generate succeeded: {request.bindingScriptPath}");
        else
            Debug.LogError($"[UIBindGenerateRequestProcessor] Generate failed: {result.errorMessage}");
    }

    private static UIBindGenerateResult Generate(UIBindGenerateRequest request)
    {
        var result = new UIBindGenerateResult { success = false };
        if (string.IsNullOrEmpty(request.assetPath))
        {
            result.errorMessage = "Binding asset path is empty.";
            return result;
        }

        UIPanelBindings bindings = AssetDatabase.LoadAssetAtPath<UIPanelBindings>(request.assetPath);
        if (bindings == null)
        {
            result.errorMessage = $"Cannot load UIPanelBindings asset: {request.assetPath}";
            return result;
        }

        if (bindings.bindings == null || bindings.bindings.Count == 0)
        {
            result.errorMessage = $"Binding asset has no bindings: {request.assetPath}";
            return result;
        }

        EnsureSettingsData();

        GenerationResult generation = UIBindScriptGenerator.GenerateScripts(bindings);
        result.success = generation.success;
        result.bindingScriptPath = generation.bindingScriptPath;
        result.mainScriptPath = generation.mainScriptPath;
        result.mainScriptClassName = generation.mainScriptClassName;
        result.errorMessage = generation.errorMessage;

        if (!generation.success)
            return result;

        if (request.registerAutoBinder &&
            !string.IsNullOrEmpty(generation.mainScriptClassName) &&
            generation.targetPanel != null)
        {
            UIAutoBinder.RegisterBindingTask(
                generation.targetPanel,
                generation.mainScriptClassName,
                generation.mainScriptPath,
                request.assetPath);
        }

        AssetDatabase.Refresh();
        return result;
    }

    private static void EnsureSettingsData()
    {
        if (UIBindDataManager.GetCurrentSettingsItem() != null)
            return;

        if (!Directory.Exists(SETTINGS_FOLDER))
        {
            Directory.CreateDirectory(SETTINGS_FOLDER);
            AssetDatabase.Refresh();
        }

        UIBindToolSettingsData settingsData = AssetDatabase.LoadAssetAtPath<UIBindToolSettingsData>(SETTINGS_ASSET_PATH);
        if (settingsData == null)
        {
            settingsData = ScriptableObject.CreateInstance<UIBindToolSettingsData>();
            AssetDatabase.CreateAsset(settingsData, SETTINGS_ASSET_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (settingsData.settingsDataItems == null)
            settingsData.settingsDataItems = new System.Collections.Generic.List<UIBindToolSettingsDataItem>();

        if (settingsData.settingsDataItems.Count == 0)
        {
            settingsData.settingsDataItems.Add(new UIBindToolSettingsDataItem());
            EditorUtility.SetDirty(settingsData);
            AssetDatabase.SaveAssets();
        }

        UIBindDataManager.SetSettingsDataContainer(settingsData);
    }

    private static void PollPendingRequests()
    {
        if (EditorApplication.timeSinceStartup < s_NextPollTime)
            return;

        s_NextPollTime = EditorApplication.timeSinceStartup + POLL_INTERVAL_SECONDS;
        if (!Directory.Exists(REQUEST_FOLDER))
            return;

        string[] requestPaths = Directory.GetFiles(REQUEST_FOLDER, "*" + REQUEST_SUFFIX, SearchOption.TopDirectoryOnly);
        foreach (string requestPath in requestPaths)
        {
            if (IsPendingRequestFile(requestPath.Replace("\\", "/")))
            {
                ScheduleProcessPendingRequests();
                return;
            }
        }
    }

    private static bool IsPendingRequestFile(string requestPath)
    {
        try
        {
            var request = JsonUtility.FromJson<UIBindGenerateRequest>(File.ReadAllText(requestPath));
            return request != null &&
                   (string.IsNullOrEmpty(request.status) ||
                    string.Equals(request.status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static void WriteRequest(string requestPath, UIBindGenerateRequest request)
    {
        File.WriteAllText(requestPath, JsonUtility.ToJson(request, true));
        AssetDatabase.ImportAsset(requestPath);
    }
}

public class UIBindGenerateRequestAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (UIBindGenerateRequestProcessor.IsRequestPath(assetPath))
            {
                UIBindGenerateRequestProcessor.ScheduleProcessPendingRequests();
                return;
            }
        }
    }
}

[Serializable]
public class UIBindGenerateRequest
{
    public int version = 1;
    public string assetPath;
    public bool registerAutoBinder = true;
    public string status = "pending";
    public string requestedAt;
    public string startedAt;
    public string completedAt;
    public string bindingScriptPath;
    public string mainScriptPath;
    public string mainScriptClassName;
    public string errorMessage;
}

public struct UIBindGenerateResult
{
    public bool success;
    public string bindingScriptPath;
    public string mainScriptPath;
    public string mainScriptClassName;
    public string errorMessage;
}
#endif
