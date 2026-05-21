#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Processes file-based *.uibind.json conversion requests inside an already-open Unity editor.
/// </summary>
[InitializeOnLoad]
public static class UIBindSpecConvertRequestProcessor
{
    public const string REQUEST_FOLDER = "Assets/UIBindRequests";
    public const string REQUEST_SUFFIX = ".uibind-convert.json";

    private const double POLL_INTERVAL_SECONDS = 2.0d;
    private const string ACTION_IMPORT = "importJsonToAsset";
    private const string ACTION_EXPORT = "exportAssetToJson";
    private const string STATUS_PENDING = "pending";
    private const string STATUS_PROCESSING = "processing";
    private const string STATUS_COMPLETED = "completed";
    private const string STATUS_FAILED = "failed";

    private static bool s_IsProcessingScheduled;
    private static double s_NextPollTime;

    static UIBindSpecConvertRequestProcessor()
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
        UIBindSpecConvertRequest request;
        try
        {
            request = JsonUtility.FromJson<UIBindSpecConvertRequest>(File.ReadAllText(requestPath));
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIBindSpecConvertRequestProcessor] Failed to read request: {requestPath}. {e.Message}");
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
        request.resultPath = string.Empty;
        request.errorMessage = string.Empty;
        WriteRequest(requestPath, request);

        UIBindSpecConvertResult result;
        if (string.Equals(request.action, ACTION_EXPORT, StringComparison.OrdinalIgnoreCase))
        {
            result = UIBindSpecConverter.ExportAssetToJson(request.assetPath, request.outputPath);
            request.resultPath = result.specPath;
        }
        else if (string.Equals(request.action, ACTION_IMPORT, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(request.action))
        {
            result = UIBindSpecConverter.ImportJsonToAsset(request.specPath, request.assetPath);
            request.resultPath = result.assetPath;
        }
        else
        {
            result = new UIBindSpecConvertResult
            {
                success = false,
                errorMessage = $"Unsupported action: {request.action}"
            };
        }

        request.completedAt = DateTimeOffset.Now.ToString("o");
        request.errorMessage = result.errorMessage;
        request.status = result.success ? STATUS_COMPLETED : STATUS_FAILED;
        WriteRequest(requestPath, request);

        if (result.success)
            Debug.Log($"[UIBindSpecConvertRequestProcessor] Convert succeeded: {request.resultPath}");
        else
            Debug.LogError($"[UIBindSpecConvertRequestProcessor] Convert failed: {result.errorMessage}");
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
            var request = JsonUtility.FromJson<UIBindSpecConvertRequest>(File.ReadAllText(requestPath));
            return request != null &&
                   (string.IsNullOrEmpty(request.status) ||
                    string.Equals(request.status, STATUS_PENDING, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static void WriteRequest(string requestPath, UIBindSpecConvertRequest request)
    {
        File.WriteAllText(requestPath, JsonUtility.ToJson(request, true));
        AssetDatabase.ImportAsset(requestPath);
    }
}

public class UIBindSpecConvertRequestAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (UIBindSpecConvertRequestProcessor.IsRequestPath(assetPath))
            {
                UIBindSpecConvertRequestProcessor.ScheduleProcessPendingRequests();
                return;
            }
        }
    }
}

[Serializable]
public class UIBindSpecConvertRequest
{
    public int version = 1;
    public string action = "importJsonToAsset";
    public string specPath;
    public string assetPath;
    public string outputPath;
    public string status = "pending";
    public string requestedAt;
    public string startedAt;
    public string completedAt;
    public string resultPath;
    public string errorMessage;
}
#endif
