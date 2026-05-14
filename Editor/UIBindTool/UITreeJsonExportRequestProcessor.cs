using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Processes file-based UI tree export requests inside an already-open Unity editor.
/// Agents can create request files without launching Unity batchmode or adding temporary editor scripts.
/// </summary>
[InitializeOnLoad]
public static class UITreeJsonExportRequestProcessor
{
    public const string REQUEST_FOLDER = "Assets/UITreeJsonRequests";
    public const string REQUEST_SUFFIX = ".uitree-export.json";

    private const string STATUS_PENDING = "pending";
    private const string STATUS_COMPLETED = "completed";
    private const string STATUS_FAILED = "failed";

    private static bool s_IsProcessingScheduled;

    static UITreeJsonExportRequestProcessor()
    {
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

    private static void ProcessRequest(string requestPath)
    {
        UITreeJsonExportRequest request;
        try
        {
            string json = File.ReadAllText(requestPath);
            request = JsonUtility.FromJson<UITreeJsonExportRequest>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UITreeJsonExportRequestProcessor] Failed to read request: {requestPath}. {e.Message}");
            return;
        }

        if (request == null)
            return;

        if (!string.IsNullOrEmpty(request.status) &&
            !string.Equals(request.status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        request.status = STATUS_PENDING;
        UITreeJsonExportResult result = UITreeJsonExporter.ExportFromCommandLine(
            request.prefabPath,
            request.prefabName,
            request.outputPath,
            request.outputFolder);

        request.completedAt = DateTimeOffset.Now.ToString("o");
        request.resultPath = result.jsonPath;
        request.errorMessage = result.errorMessage;
        request.status = result.success ? STATUS_COMPLETED : STATUS_FAILED;

        try
        {
            File.WriteAllText(requestPath, JsonUtility.ToJson(request, true));
            AssetDatabase.ImportAsset(requestPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UITreeJsonExportRequestProcessor] Failed to write request result: {requestPath}. {e.Message}");
        }

        if (result.success)
            Debug.Log($"[UITreeJsonExportRequestProcessor] Exported UI tree json: {result.jsonPath}");
        else
            Debug.LogError($"[UITreeJsonExportRequestProcessor] Export failed: {result.errorMessage}");
    }

    public static bool IsRequestPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith(REQUEST_FOLDER + "/", StringComparison.OrdinalIgnoreCase) &&
               assetPath.EndsWith(REQUEST_SUFFIX, StringComparison.OrdinalIgnoreCase);
    }
}

public class UITreeJsonExportRequestAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (UITreeJsonExportRequestProcessor.IsRequestPath(assetPath))
            {
                UITreeJsonExportRequestProcessor.ScheduleProcessPendingRequests();
                return;
            }
        }
    }
}

[Serializable]
public class UITreeJsonExportRequest
{
    public int version = 1;
    public string prefabPath;
    public string prefabName;
    public string outputPath;
    public string outputFolder;
    public string status = "pending";
    public string requestedAt;
    public string completedAt;
    public string resultPath;
    public string errorMessage;
}
