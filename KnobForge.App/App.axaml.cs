using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KnobForge.App.ProjectFiles;
using KnobForge.App.Views;
using KnobForge.App.Diagnostics;
using KnobForge.Core;
using System;
using System.Diagnostics;
using System.IO;

namespace KnobForge.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
                {
                    string details = e.Exception?.ToString() ?? "<null>";
                    FatalLog.Append($">>> [UIUnhandledException] {details}");
                    Console.Error.WriteLine($">>> [UIUnhandledException] {details}");
                    e.Handled = true;
                };

            string? startupProjectPath = ResolveStartupProjectPath(desktop.Args);
            if (!string.IsNullOrWhiteSpace(startupProjectPath))
            {
                desktop.MainWindow = BuildMainWindow(startupProjectPath);
            }
            else
            {
                var launcher = new ProjectLauncherWindow();
                launcher.LaunchRequested += async result =>
                {
                    try
                    {
                        FatalLog.Append($">>> [ProjectLaunch] Begin Path='{result.ProjectPath ?? "<new>"}' Type='{result.ProjectType?.ToString() ?? "<auto>"}'");
                        if (OperatingSystem.IsMacOS() &&
                            !string.IsNullOrWhiteSpace(result.ProjectPath))
                        {
                            if (!TryRelaunchProjectInFreshProcess(result.ProjectPath!, out string relaunchError))
                            {
                                FatalLog.Append($">>> [ProjectLaunch] RelaunchFailed Path='{result.ProjectPath}' Error='{relaunchError}'");
                                await launcher.ShowProjectLoadErrorDialogAsync("Open Project Failed", relaunchError);
                                return;
                            }

                            FatalLog.Append($">>> [ProjectLaunch] RelaunchStarted Path='{result.ProjectPath}'");
                            desktop.Shutdown();
                            return;
                        }

                        var mainWindow = new MainWindow(result.ProjectType ?? InteractorProjectType.RotaryKnob);
                        FatalLog.Append($">>> [ProjectLaunch] MainWindowCreated Path='{result.ProjectPath ?? "<new>"}'");
                        if (!string.IsNullOrWhiteSpace(result.ProjectPath) &&
                            !mainWindow.TryLoadProjectFromFile(result.ProjectPath, out string error))
                        {
                            FatalLog.Append($">>> [ProjectLaunch] LoadFailed Path='{result.ProjectPath}' Error='{error}'");
                            await launcher.ShowProjectLoadErrorDialogAsync("Open Project Failed", error);
                            return;
                        }

                        FatalLog.Append($">>> [ProjectLaunch] LoadSucceeded Path='{result.ProjectPath ?? "<new>"}'");
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        mainWindow.Activate();
                        FatalLog.Append($">>> [ProjectLaunch] MainWindowShown Path='{result.ProjectPath ?? "<new>"}'");
                        if (OperatingSystem.IsMacOS())
                        {
                            FatalLog.Append($">>> [ProjectLaunch] LauncherRetainedForCrashIsolation Path='{result.ProjectPath ?? "<new>"}'");
                            return;
                        }

                        launcher.Hide();
                        FatalLog.Append($">>> [ProjectLaunch] LauncherHidden Path='{result.ProjectPath ?? "<new>"}'");
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                FatalLog.Append($">>> [ProjectLaunch] LauncherClosePosted Path='{result.ProjectPath ?? "<new>"}'");
                                launcher.Close();
                                FatalLog.Append($">>> [ProjectLaunch] LauncherClosed Path='{result.ProjectPath ?? "<new>"}'");
                            }
                            catch
                            {
                            }
                        }, DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        FatalLog.Append($">>> [ProjectLaunch] {ex}");
                        Console.Error.WriteLine($">>> [ProjectLaunch] {ex}");
                        await launcher.ShowProjectLoadErrorDialogAsync(
                            "Open Project Failed",
                            $"Unexpected error while opening project: {ex.Message}");
                    }
                };

                desktop.MainWindow = launcher;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindow BuildMainWindow(string? projectPath, InteractorProjectType? projectType = null)
    {
        var window = new MainWindow(projectType ?? InteractorProjectType.RotaryKnob);
        if (!string.IsNullOrWhiteSpace(projectPath) &&
            !window.TryLoadProjectFromFile(projectPath, out string error))
        {
            Console.Error.WriteLine($">>> [ProjectLoad] Failed to load '{projectPath}': {error}");
            FatalLog.Append($">>> [ProjectLoad] Failed to load '{projectPath}': {error}");
        }

        return window;
    }

    private static bool TryRelaunchProjectInFreshProcess(string projectPath, out string error)
    {
        error = string.Empty;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(projectPath);
        }
        catch (Exception ex)
        {
            error = $"Project path is invalid: {ex.Message}";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            error = $"Project file was not found: {fullPath}";
            return false;
        }

        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            error = "Could not resolve the current application executable.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(fullPath);

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                error = "The project process did not start.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not start the project in a fresh process: {ex.Message}";
            return false;
        }
    }

    private static string? ResolveStartupProjectPath(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return null;
        }

        foreach (string arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            string trimmed = arg.Trim();
            if (!trimmed.EndsWith(KnobProjectFileStore.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                string fullPath = Path.GetFullPath(trimmed);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

}
