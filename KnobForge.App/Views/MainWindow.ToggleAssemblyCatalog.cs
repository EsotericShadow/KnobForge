using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private static readonly string[] ToggleModelsDirectoryCandidates =
        {
            Path.Combine("models", "switch_models"),
            Path.Combine("models", "toggle_models"),
            "switch_models",
            "toggle_models"
        };
        private static readonly string[] ToggleSupportedModelExtensions = { ".glb", ".stl" };
        private static readonly string[] ToggleBaseDirectoryNames = { "base_models", "bases", "base" };
        private static readonly string[] ToggleLeverDirectoryNames = { "lever_models", "levers", "lever" };

        private sealed class ToggleMeshOption
        {
            public ToggleMeshOption(string displayName, string meshPath)
            {
                DisplayName = displayName;
                MeshPath = meshPath ?? string.Empty;
            }

            public string DisplayName { get; }

            public string MeshPath { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private void OnRefreshToggleLibraryButtonClicked(object? sender, RoutedEventArgs e)
        {
            RebuildToggleMeshOptions();
            ApplyToggleAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void RebuildToggleMeshOptions()
        {
            if (_toggleBaseMeshCombo == null || _toggleLeverMeshCombo == null)
            {
                return;
            }

            string? toggleModelsDirectory = ResolveToggleModelsDirectory();

            _toggleBaseMeshOptions.Clear();
            _toggleLeverMeshOptions.Clear();

            _toggleBaseMeshOptions.Add(new ToggleMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredToggleModelPaths(toggleModelsDirectory, ToggleBaseDirectoryNames))
            {
                _toggleBaseMeshOptions.Add(new ToggleMeshOption(BuildToggleMeshOptionLabel(path), path));
            }

            _toggleLeverMeshOptions.Add(new ToggleMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredToggleModelPaths(toggleModelsDirectory, ToggleLeverDirectoryNames))
            {
                _toggleLeverMeshOptions.Add(new ToggleMeshOption(BuildToggleMeshOptionLabel(path), path));
            }

            EnsureToggleMeshOptionForConfiguredPath(_toggleBaseMeshOptions, _project.ToggleBaseImportedMeshPath);
            EnsureToggleMeshOptionForConfiguredPath(_toggleLeverMeshOptions, _project.ToggleLeverImportedMeshPath);

            _toggleBaseMeshCombo.ItemsSource = _toggleBaseMeshOptions.ToList();
            _toggleLeverMeshCombo.ItemsSource = _toggleLeverMeshOptions.ToList();

            _toggleBaseMeshCombo.SelectedItem = ResolveToggleMeshOption(_toggleBaseMeshOptions, _project.ToggleBaseImportedMeshPath);
            _toggleLeverMeshCombo.SelectedItem = ResolveToggleMeshOption(_toggleLeverMeshOptions, _project.ToggleLeverImportedMeshPath);
        }

        private static string BuildToggleMeshOptionLabel(string path)
        {
            string fileName = Path.GetFileName(path);
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string folderName = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return fileName;
            }

            return $"{fileName} ({folderName})";
        }

        private static void EnsureToggleMeshOptionForConfiguredPath(ICollection<ToggleMeshOption> options, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return;
            }

            string normalized = NormalizePathForCompare(configuredPath);
            bool exists = options.Any(option => string.Equals(NormalizePathForCompare(option.MeshPath), normalized, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                return;
            }

            options.Add(new ToggleMeshOption($"Custom: {Path.GetFileName(configuredPath)}", configuredPath));
        }

        private static ToggleMeshOption ResolveToggleMeshOption(IReadOnlyList<ToggleMeshOption> options, string configuredPath)
        {
            if (options.Count == 0)
            {
                return new ToggleMeshOption("Auto (library/default)", string.Empty);
            }

            string normalized = NormalizePathForCompare(configuredPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return options[0];
            }

            ToggleMeshOption? match = options.FirstOrDefault(option =>
                string.Equals(NormalizePathForCompare(option.MeshPath), normalized, StringComparison.OrdinalIgnoreCase));
            return match ?? options[0];
        }

        private static string ResolveSelectedToggleMeshPath(object? selectedItem)
        {
            if (selectedItem is ToggleMeshOption option)
            {
                return option.MeshPath;
            }

            return string.Empty;
        }

        private static IEnumerable<string> EnumerateDiscoveredToggleModelPaths(string? toggleModelsDirectory, string[] preferredDirectories)
        {
            if (string.IsNullOrWhiteSpace(toggleModelsDirectory) || !Directory.Exists(toggleModelsDirectory))
            {
                return Enumerable.Empty<string>();
            }

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < preferredDirectories.Length; i++)
            {
                string directory = Path.Combine(toggleModelsDirectory, preferredDirectories[i]);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string path in EnumerateSupportedToggleModelFiles(directory))
                {
                    string normalized = NormalizePathForCompare(path);
                    if (!seen.Add(normalized))
                    {
                        continue;
                    }

                    paths.Add(path);
                }
            }

            foreach (string path in EnumerateSupportedToggleModelFiles(toggleModelsDirectory))
            {
                string normalized = NormalizePathForCompare(path);
                if (!seen.Add(normalized))
                {
                    continue;
                }

                paths.Add(path);
            }

            return paths;
        }

        private static IEnumerable<string> EnumerateSupportedToggleModelFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory
                .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedToggleModelPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsSupportedToggleModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return ToggleSupportedModelExtensions.Any(ext =>
                path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        private static string? ResolveToggleModelsDirectory()
        {
            string desktopRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Monozukuri");
            for (int i = 0; i < ToggleModelsDirectoryCandidates.Length; i++)
            {
                string candidate = Path.Combine(desktopRoot, ToggleModelsDirectoryCandidates[i]);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
