using System;
using System.IO;

namespace KnobForge.App.Diagnostics;

internal static class FatalLog
{
    private static readonly string PrimaryPath = Path.Combine(Path.GetTempPath(), "knobforge_fatal.log");
    private static readonly string FallbackPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Logs",
        "KnobForge",
        "knobforge_fatal.log");

    public static void Append(string line)
    {
        try
        {
            File.AppendAllText(
                PrimaryPath,
                $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
        }
        catch
        {
            // best effort only
        }

        try
        {
            string? directory = Path.GetDirectoryName(FallbackPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                FallbackPath,
                $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
        }
        catch
        {
            // best effort only
        }
    }
}
