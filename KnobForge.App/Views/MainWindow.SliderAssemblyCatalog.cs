using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private static readonly string[] SliderModelsDirectoryCandidates =
        {
            Path.Combine("models", "slider_models"),
            "slider_models"
        };
        private static readonly string[] SliderSupportedModelExtensions = { ".glb", ".stl" };
        private static readonly string[] SliderBackplateDirectoryNames = { "backplate_models", "backplates", "backplate" };
        private static readonly string[] SliderThumbDirectoryNames = { "sliderthumb_models", "thumb_models", "thumbs", "slider_thumbs" };

        private sealed class SliderMeshOption
        {
            public SliderMeshOption(string displayName, string meshPath)
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

        private void OnRefreshSliderLibraryButtonClicked(object? sender, RoutedEventArgs e)
        {
            RebuildSliderMeshOptions();
            ApplySliderAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void RebuildSliderMeshOptions()
        {
            if (_sliderBackplateMeshCombo == null || _sliderThumbMeshCombo == null)
            {
                return;
            }

            string? sliderModelsDirectory = ResolveSliderModelsDirectory();

            _sliderBackplateMeshOptions.Clear();
            _sliderThumbMeshOptions.Clear();

            _sliderBackplateMeshOptions.Add(new SliderMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredSliderModelPaths(sliderModelsDirectory, SliderBackplateDirectoryNames))
            {
                _sliderBackplateMeshOptions.Add(new SliderMeshOption(BuildSliderMeshOptionLabel(path), path));
            }

            _sliderThumbMeshOptions.Add(new SliderMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredSliderModelPaths(sliderModelsDirectory, SliderThumbDirectoryNames))
            {
                _sliderThumbMeshOptions.Add(new SliderMeshOption(BuildSliderMeshOptionLabel(path), path));
            }

            EnsureSliderMeshOptionForConfiguredPath(_sliderBackplateMeshOptions, _project.SliderBackplateImportedMeshPath);
            EnsureSliderMeshOptionForConfiguredPath(_sliderThumbMeshOptions, _project.SliderThumbImportedMeshPath);

            _sliderBackplateMeshCombo.ItemsSource = _sliderBackplateMeshOptions.ToList();
            _sliderThumbMeshCombo.ItemsSource = _sliderThumbMeshOptions.ToList();

            _sliderBackplateMeshCombo.SelectedItem = ResolveSliderMeshOption(_sliderBackplateMeshOptions, _project.SliderBackplateImportedMeshPath);
            _sliderThumbMeshCombo.SelectedItem = ResolveSliderMeshOption(_sliderThumbMeshOptions, _project.SliderThumbImportedMeshPath);
        }

        private static string BuildSliderMeshOptionLabel(string path)
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

        private static void EnsureSliderMeshOptionForConfiguredPath(ICollection<SliderMeshOption> options, string configuredPath)
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

            options.Add(new SliderMeshOption($"Custom: {Path.GetFileName(configuredPath)}", configuredPath));
        }

        private static SliderMeshOption ResolveSliderMeshOption(IReadOnlyList<SliderMeshOption> options, string configuredPath)
        {
            if (options.Count == 0)
            {
                return new SliderMeshOption("Auto (library/default)", string.Empty);
            }

            string normalized = NormalizePathForCompare(configuredPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return options[0];
            }

            SliderMeshOption? match = options.FirstOrDefault(option =>
                string.Equals(NormalizePathForCompare(option.MeshPath), normalized, StringComparison.OrdinalIgnoreCase));
            return match ?? options[0];
        }

        private static string ResolveSelectedSliderMeshPath(object? selectedItem)
        {
            if (selectedItem is SliderMeshOption option)
            {
                return option.MeshPath;
            }

            return string.Empty;
        }

        private static IEnumerable<string> EnumerateDiscoveredSliderModelPaths(string? sliderModelsDirectory, string[] preferredDirectories)
        {
            if (string.IsNullOrWhiteSpace(sliderModelsDirectory) || !Directory.Exists(sliderModelsDirectory))
            {
                return Enumerable.Empty<string>();
            }

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < preferredDirectories.Length; i++)
            {
                string directory = Path.Combine(sliderModelsDirectory, preferredDirectories[i]);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string path in EnumerateSupportedModelFiles(directory))
                {
                    string normalized = NormalizePathForCompare(path);
                    if (!seen.Add(normalized))
                    {
                        continue;
                    }

                    paths.Add(path);
                }
            }

            foreach (string path in EnumerateSupportedModelFiles(sliderModelsDirectory))
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

        private static IEnumerable<string> EnumerateSupportedModelFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory
                .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedSliderModelPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsSupportedSliderModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return SliderSupportedModelExtensions.Any(ext =>
                path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        private static string? ResolveSliderModelsDirectory()
        {
            string desktopRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Monozukuri");
            for (int i = 0; i < SliderModelsDirectoryCandidates.Length; i++)
            {
                string candidate = Path.Combine(desktopRoot, SliderModelsDirectoryCandidates[i]);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
