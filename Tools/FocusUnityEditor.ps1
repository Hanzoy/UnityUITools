param(
    [string]$ProjectPath = "",
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = "Stop"

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class UnityWindowFocus
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
"@

function Normalize-PathText {
    param([string]$PathText)

    if ([string]::IsNullOrWhiteSpace($PathText)) {
        return ""
    }

    try {
        return [System.IO.Path]::GetFullPath($PathText).TrimEnd('\', '/').ToLowerInvariant()
    }
    catch {
        return $PathText.TrimEnd('\', '/').ToLowerInvariant()
    }
}

function Get-UnityEditorProcess {
    param([string]$TargetProjectPath)

    $normalizedProjectPath = Normalize-PathText $TargetProjectPath
    $unityProcesses = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -notmatch "AssetImportWorker" }

    if (-not [string]::IsNullOrEmpty($normalizedProjectPath)) {
        $unityProcesses = $unityProcesses | Where-Object {
            (Normalize-PathText $_.CommandLine).Contains($normalizedProjectPath)
        }
    }

    foreach ($processInfo in $unityProcesses) {
        $process = Get-Process -Id $processInfo.ProcessId -ErrorAction SilentlyContinue
        if ($null -ne $process -and $process.MainWindowHandle -ne 0) {
            return $process
        }
    }

    return $null
}

$deadline = (Get-Date).AddSeconds([Math]::Max(1, $TimeoutSeconds))
$unityProcess = $null

while ((Get-Date) -lt $deadline) {
    $unityProcess = Get-UnityEditorProcess -TargetProjectPath $ProjectPath
    if ($null -ne $unityProcess) {
        break
    }

    Start-Sleep -Milliseconds 250
}

if ($null -eq $unityProcess) {
    Write-Error "Unity editor window was not found. ProjectPath='$ProjectPath'"
    exit 1
}

[UnityWindowFocus]::ShowWindowAsync($unityProcess.MainWindowHandle, 9) | Out-Null
[UnityWindowFocus]::SetForegroundWindow($unityProcess.MainWindowHandle) | Out-Null

Write-Output "Focused Unity editor process $($unityProcess.Id): $($unityProcess.MainWindowTitle)"
