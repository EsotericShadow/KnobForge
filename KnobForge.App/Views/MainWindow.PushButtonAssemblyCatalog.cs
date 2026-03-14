using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private static readonly string[] PushButtonModelsDirectoryCandidates =
        {
            Path.Combine("models", "button_models"),
            "button_models"
        };

        private static readonly string[] PushButtonSupportedModelExtensions = { ".glb", ".stl" };
        private static readonly string[] PushButtonBaseDirectoryNames = { "base_models", "bases", "base" };
        private static readonly string[] PushButtonCapDirectoryNames = { "cap_models", "caps", "cap" };

        private sealed class PushButtonMeshOption
        {
            public PushButtonMeshOption(string displayName, string meshPath)
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

        private void OnRefreshPushButtonLibraryButtonClicked(object? sender, RoutedEventArgs e)
        {
            RebuildPushButtonMeshOptions();
            ApplyPushButtonAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void RebuildPushButtonMeshOptions()
        {
            if (_pushButtonBaseMeshCombo == null || _pushButtonCapMeshCombo == null)
            {
                return;
            }

            string? pushButtonModelsDirectory = ResolvePushButtonModelsDirectory();

            _pushButtonBaseMeshOptions.Clear();
            _pushButtonCapMeshOptions.Clear();

            _pushButtonBaseMeshOptions.Add(new PushButtonMeshOption("Auto (procedural)", string.Empty));
            foreach (string path in EnumerateDiscoveredPushButtonModelPaths(pushButtonModelsDirectory, PushButtonBaseDirectoryNames))
            {
                _pushButtonBaseMeshOptions.Add(new PushButtonMeshOption(BuildPushButtonMeshOptionLabel(path), path));
            }

            _pushButtonCapMeshOptions.Add(new PushButtonMeshOption("Auto (procedural)", string.Empty));
            foreach (string path in EnumerateDiscoveredPushButtonModelPaths(pushButtonModelsDirectory, PushButtonCapDirectoryNames))
            {
                _pushButtonCapMeshOptions.Add(new PushButtonMeshOption(BuildPushButtonMeshOptionLabel(path), path));
            }

            EnsurePushButtonMeshOptionForConfiguredPath(_pushButtonBaseMeshOptions, _project.PushButtonBaseImportedMeshPath);
            EnsurePushButtonMeshOptionForConfiguredPath(_pushButtonCapMeshOptions, _project.PushButtonCapImportedMeshPath);

            _pushButtonBaseMeshCombo.ItemsSource = _pushButtonBaseMeshOptions.ToList();
            _pushButtonCapMeshCombo.ItemsSource = _pushButtonCapMeshOptions.ToList();

            _pushButtonBaseMeshCombo.SelectedItem = ResolvePushButtonMeshOption(_pushButtonBaseMeshOptions, _project.PushButtonBaseImportedMeshPath);
            _pushButtonCapMeshCombo.SelectedItem = ResolvePushButtonMeshOption(_pushButtonCapMeshOptions, _project.PushButtonCapImportedMeshPath);
        }

        private static string BuildPushButtonMeshOptionLabel(string path)
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

        private static void EnsurePushButtonMeshOptionForConfiguredPath(ICollection<PushButtonMeshOption> options, string configuredPath)
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

            options.Add(new PushButtonMeshOption($"Custom: {Path.GetFileName(configuredPath)}", configuredPath));
        }

        private static PushButtonMeshOption ResolvePushButtonMeshOption(IReadOnlyList<PushButtonMeshOption> options, string configuredPath)
        {
            if (options.Count == 0)
            {
                return new PushButtonMeshOption("Auto (procedural)", string.Empty);
            }

            string normalized = NormalizePathForCompare(configuredPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return options[0];
            }

            PushButtonMeshOption? match = options.FirstOrDefault(option =>
                string.Equals(NormalizePathForCompare(option.MeshPath), normalized, StringComparison.OrdinalIgnoreCase));
            return match ?? options[0];
        }

        private static string ResolveSelectedPushButtonMeshPath(object? selectedItem)
        {
            if (selectedItem is PushButtonMeshOption option)
            {
                return option.MeshPath;
            }

            return string.Empty;
        }

        private static IEnumerable<string> EnumerateDiscoveredPushButtonModelPaths(string? pushButtonModelsDirectory, string[] preferredDirectories)
        {
            if (string.IsNullOrWhiteSpace(pushButtonModelsDirectory) || !Directory.Exists(pushButtonModelsDirectory))
            {
                return Enumerable.Empty<string>();
            }

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < preferredDirectories.Length; i++)
            {
                string directory = Path.Combine(pushButtonModelsDirectory, preferredDirectories[i]);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string path in EnumerateSupportedPushButtonModelFiles(directory))
                {
                    string normalized = NormalizePathForCompare(path);
                    if (!seen.Add(normalized))
                    {
                        continue;
                    }

                    paths.Add(path);
                }
            }

            foreach (string path in EnumerateSupportedPushButtonModelFiles(pushButtonModelsDirectory))
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

        private static IEnumerable<string> EnumerateSupportedPushButtonModelFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory
                .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedPushButtonModelPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsSupportedPushButtonModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return PushButtonSupportedModelExtensions.Any(ext =>
                path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        private static string? ResolvePushButtonModelsDirectory()
        {
            string desktopRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Monozukuri");
            for (int i = 0; i < PushButtonModelsDirectoryCandidates.Length; i++)
            {
                string candidate = Path.Combine(desktopRoot, PushButtonModelsDirectoryCandidates[i]);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
