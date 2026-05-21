using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UnityFocus;

internal static class Program
{
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    private static int Main(string[] args)
    {
        FocusOptions options = FocusOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, options.TimeoutSeconds));
        Process? unityProcess = null;

        while (DateTime.UtcNow < deadline)
        {
            unityProcess = FindUnityProcess(options);
            if (unityProcess != null)
                break;

            Thread.Sleep(250);
        }

        if (unityProcess == null)
        {
            Console.Error.WriteLine("Unity editor window was not found.");
            return 1;
        }

        ShowWindowAsync(unityProcess.MainWindowHandle, SW_RESTORE);
        SetForegroundWindow(unityProcess.MainWindowHandle);
        Console.WriteLine($"Focused Unity editor process {unityProcess.Id}: {unityProcess.MainWindowTitle}");
        return 0;
    }

    private static Process? FindUnityProcess(FocusOptions options)
    {
        string projectName = GetProjectName(options.ProjectPath);
        string titleFilter = options.Title ?? string.Empty;

        Process[] processes = Process.GetProcessesByName("Unity");
        IEnumerable<Process> candidates = processes
            .Where(process => process.MainWindowHandle != IntPtr.Zero)
            .Where(process => !IsAssetImportWorker(process));

        if (!string.IsNullOrEmpty(projectName))
        {
            Process? projectMatch = candidates.FirstOrDefault(process =>
                process.MainWindowTitle.Contains(projectName, StringComparison.OrdinalIgnoreCase));
            if (projectMatch != null)
                return projectMatch;
        }

        if (!string.IsNullOrEmpty(titleFilter))
        {
            Process? titleMatch = candidates.FirstOrDefault(process =>
                process.MainWindowTitle.Contains(titleFilter, StringComparison.OrdinalIgnoreCase));
            if (titleMatch != null)
                return titleMatch;
        }

        return candidates.FirstOrDefault(process =>
            process.MainWindowTitle.Contains("Unity", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAssetImportWorker(Process process)
    {
        try
        {
            string title = process.MainWindowTitle;
            return title.Contains("AssetImportWorker", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static string GetProjectName(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return string.Empty;

        string trimmedPath = projectPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmedPath);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("UnityFocus --projectPath <UnityProjectPath> [--timeoutSeconds 10] [--title <WindowTitleText>]");
        Console.WriteLine();
        Console.WriteLine("Focuses a Unity editor window. This tool does not execute arbitrary commands.");
    }
}

internal sealed record FocusOptions
{
    public string? ProjectPath { get; private init; }
    public string? Title { get; private init; }
    public int TimeoutSeconds { get; private init; } = 10;
    public bool ShowHelp { get; private init; }

    public static FocusOptions Parse(string[] args)
    {
        var options = new FocusOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? value = i + 1 < args.Length ? args[i + 1] : null;

            switch (arg)
            {
                case "-h":
                case "--help":
                case "/?":
                    options = options with { ShowHelp = true };
                    break;
                case "--projectPath":
                case "-projectPath":
                    if (value != null)
                    {
                        options = options with { ProjectPath = value };
                        i++;
                    }
                    break;
                case "--title":
                case "-title":
                    if (value != null)
                    {
                        options = options with { Title = value };
                        i++;
                    }
                    break;
                case "--timeoutSeconds":
                case "-timeoutSeconds":
                    if (value != null && int.TryParse(value, out int timeoutSeconds))
                    {
                        options = options with { TimeoutSeconds = timeoutSeconds };
                        i++;
                    }
                    break;
            }
        }

        return options;
    }
}
