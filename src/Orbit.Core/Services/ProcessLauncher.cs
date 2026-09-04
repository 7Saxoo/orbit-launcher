using System.ComponentModel;
using System.Diagnostics;
using Orbit.Core.Infrastructure;
using Orbit.Core.Models;
using Serilog;

namespace Orbit.Core.Services;

/// <summary>
/// Launches executables via <see cref="ProcessStartInfo"/> with
/// <c>UseShellExecute = true</c>. That path goes through the Windows shell
/// (ShellExecuteEx) – it handles UAC elevation prompts and file associations
/// without ever spawning <c>cmd.exe</c> or a shell interpreter.
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    // Win32 error codes surfaced through Win32Exception.NativeErrorCode.
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorCancelled = 1223; // UAC prompt dismissed

    private readonly IExecutableInspector _inspector;
    private readonly ILogger _log;
    private readonly Func<ProcessStartInfo, Process?> _starter;

    public ProcessLauncher(IExecutableInspector inspector, ILogger log)
        : this(inspector, log, static psi => Process.Start(psi))
    {
    }

    // Test seam: lets unit tests simulate the OS without starting real processes.
    internal ProcessLauncher(IExecutableInspector inspector, ILogger log, Func<ProcessStartInfo, Process?> starter)
    {
        _inspector = inspector;
        _log = log.ForContext<ProcessLauncher>();
        _starter = starter;
    }

    public LaunchOutcome Launch(AppEntry entry)
    {
        var path = PathHelper.Normalize(entry.ExecutablePath);
        var exeName = path.Length > 0 ? Path.GetFileName(path) : entry.Name;
        var uri = entry.LaunchUri?.Trim();
        var viaUri = !string.IsNullOrEmpty(uri);

        // With a launch URI (e.g. steam://rungameid/…) the target is not our exe,
        // so we don't gate on the exe existing – Steam owns the game files.
        if (!viaUri)
        {
            switch (_inspector.Evaluate(path))
            {
                case AppAvailability.Missing:
                    _log.Warning("Launch aborted, file missing: {Path}", path);
                    return new LaunchOutcome(LaunchStatus.FileNotFound,
                        $"Le fichier est introuvable :\n{path}");
                case AppAvailability.Invalid:
                    _log.Warning("Launch aborted, not an executable: {Path}", path);
                    return new LaunchOutcome(LaunchStatus.NotAnExecutable,
                        $"Ce fichier n'est pas un exécutable (.exe) :\n{path}");
            }
        }

        var workingDirectory = FirstUsableDirectory(entry.WorkingDirectory, path);

        var psi = new ProcessStartInfo
        {
            FileName = viaUri ? uri! : path,
            Arguments = viaUri ? string.Empty : ComposeArguments(entry),
            WorkingDirectory = viaUri ? string.Empty : (workingDirectory ?? string.Empty),
            UseShellExecute = true
        };

        if (entry.RunAsAdmin && !viaUri)
            psi.Verb = "runas"; // ShellExecute elevation prompt

        try
        {
            var process = _starter(psi);
            _log.Information("Launched {Target} (pid {Pid})", viaUri ? uri : exeName, SafePid(process));
            return LaunchOutcome.Ok(entry.Name);
        }
        catch (Win32Exception ex)
        {
            var status = ex.NativeErrorCode switch
            {
                ErrorFileNotFound or ErrorPathNotFound => LaunchStatus.FileNotFound,
                ErrorAccessDenied => LaunchStatus.AccessDenied,
                ErrorCancelled => LaunchStatus.CancelledByUser,
                _ => LaunchStatus.Failed
            };
            _log.Error(ex, "Launch failed for {Target} (win32 {Code})",
                viaUri ? uri : exeName, ex.NativeErrorCode);

            if (viaUri && uri!.StartsWith("steam:", StringComparison.OrdinalIgnoreCase))
                return new LaunchOutcome(LaunchStatus.Failed,
                    "Impossible de démarrer ce jeu via Steam. Steam est-il installé et connecté ?", ex);

            return new LaunchOutcome(status, DescribeWin32(status, exeName), ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or IOException)
        {
            _log.Error(ex, "Launch failed for {Exe}", exeName);
            return new LaunchOutcome(LaunchStatus.Failed,
                $"Impossible de lancer « {exeName} » : {ex.Message}", ex);
        }
    }

    public bool IsRunning(AppEntry entry)
    {
        var path = PathHelper.Normalize(entry.ExecutablePath);
        if (path.Length == 0)
            return false;

        var imageName = Path.GetFileNameWithoutExtension(path);
        try
        {
            foreach (var process in Process.GetProcessesByName(imageName))
            {
                using (process)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Enumeration can race with process exit; treat as "not running".
        }

        return false;
    }

    public IReadOnlySet<string> GetRunningImageNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    if (!string.IsNullOrEmpty(process.ProcessName))
                        names.Add(process.ProcessName);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Best effort – an incomplete snapshot is fine for a UI hint.
        }

        return names;
    }

    internal static string ComposeArguments(AppEntry entry)
    {
        var userArgs = entry.Arguments?.Trim() ?? string.Empty;

        if (entry.JavaMaxMemoryMb is > 0 and var mb)
        {
            var heap = $"-Xmx{mb}M -Xms{mb}M";
            return userArgs.Length > 0 ? $"{heap} {userArgs}" : heap;
        }

        return userArgs;
    }

    private static string? FirstUsableDirectory(string? preferred, string executablePath)
    {
        var candidate = PathHelper.Normalize(preferred);
        if (candidate.Length > 0 && Directory.Exists(candidate))
            return candidate;

        return PathHelper.GetContainingDirectory(executablePath);
    }

    private static int SafePid(Process? process)
    {
        try
        {
            return process?.Id ?? -1;
        }
        catch (InvalidOperationException)
        {
            return -1; // process already exited
        }
    }

    private static string DescribeWin32(LaunchStatus status, string exeName) => status switch
    {
        LaunchStatus.FileNotFound => $"Le fichier « {exeName} » est introuvable.",
        LaunchStatus.AccessDenied =>
            $"Windows a refusé le lancement de « {exeName} ». Vérifiez les droits d'accès.",
        LaunchStatus.CancelledByUser =>
            $"Le lancement de « {exeName} » a été annulé (demande d'élévation refusée).",
        _ => $"Le lancement de « {exeName} » a échoué."
    };
}
