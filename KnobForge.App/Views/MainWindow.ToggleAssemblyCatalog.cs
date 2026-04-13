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

            IReadOnlyList<string> toggleModelsDirectories = ResolveToggleModelsDirectories();

            _toggleBaseMeshOptions.Clear();
            _toggleLeverMeshOptions.Clear();

            _toggleBaseMeshOptions.Add(new ToggleMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredToggleModelPaths(toggleModelsDirectories, ToggleBaseDirectoryNames))
            {
                _toggleBaseMeshOptions.Add(new ToggleMeshOption(BuildToggleMeshOptionLabel(path), path));
            }

            _toggleLeverMeshOptions.Add(new ToggleMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredToggleModelPaths(toggleModelsDirectories, ToggleLeverDirectoryNames))
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

            string normalized = NormalizePathForCompare(ResolveBestToggleMeshPath(options, configuredPath));
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

            string normalized = NormalizePathForCompare(ResolveBestToggleMeshPath(options, configuredPath));
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

        private static IEnumerable<string> EnumerateDiscoveredToggleModelPaths(IEnumerable<string> toggleModelsDirectories, string[] preferredDirectories)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string toggleModelsDirectory in toggleModelsDirectories)
            {
                if (string.IsNullOrWhiteSpace(toggleModelsDirectory) || !Directory.Exists(toggleModelsDirectory))
                {
                    continue;
                }

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

        private static string ResolveBestToggleMeshPath(IEnumerable<ToggleMeshOption> options, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            string normalizedConfiguredPath = NormalizePathForCompare(configuredPath);
            ToggleMeshOption? directMatch = options.FirstOrDefault(option =>
                string.Equals(NormalizePathForCompare(option.MeshPath), normalizedConfiguredPath, StringComparison.OrdinalIgnoreCase));
            if (directMatch != null)
            {
                return directMatch.MeshPath;
            }

            string fileName = Path.GetFileName(configuredPath.Trim());
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return configuredPath;
            }

            ToggleMeshOption? fileNameMatch = options.FirstOrDefault(option =>
                string.Equals(Path.GetFileName(option.MeshPath), fileName, StringComparison.OrdinalIgnoreCase));
            return fileNameMatch?.MeshPath ?? configuredPath;
        }

        private static IReadOnlyList<string> ResolveToggleModelsDirectories()
        {
            var directories = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in EnumerateToggleSearchRoots())
            {
                for (int i = 0; i < ToggleModelsDirectoryCandidates.Length; i++)
                {
                    TryAddExistingToggleDirectory(directories, seen, Path.Combine(root, ToggleModelsDirectoryCandidates[i]));
                }
            }

            return directories;
        }

        private static IEnumerable<string> EnumerateToggleSearchRoots()
        {
            string currentDirectory = Environment.CurrentDirectory;
            if (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                yield return currentDirectory;
            }

            string? probe = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(probe); i++)
            {
                yield return probe;
                probe = Directory.GetParent(probe)?.FullName;
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(desktop))
            {
                yield return Path.Combine(desktop, "Monozukuri");
                yield return Path.Combine(desktop, "KnobForge");
            }
        }

        private static void TryAddExistingToggleDirectory(ICollection<string> directories, ISet<string> seen, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
            {
                return;
            }

            string normalized = NormalizePathForCompare(candidate);
            if (seen.Add(normalized))
            {
                directories.Add(candidate);
            }
        }
    }
}
