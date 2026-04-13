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
        private static readonly string[] SliderStandaloneThumbDirectoryCandidates =
        {
            Path.Combine("models", "thumb_models"),
            "thumb_models"
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

            IReadOnlyList<string> sliderModelsDirectories = ResolveSliderModelsDirectories();
            IReadOnlyList<string> sliderThumbModelsDirectories = ResolveSliderThumbModelsDirectories(sliderModelsDirectories);

            _sliderBackplateMeshOptions.Clear();
            _sliderThumbMeshOptions.Clear();

            _sliderBackplateMeshOptions.Add(new SliderMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredSliderModelPaths(sliderModelsDirectories, SliderBackplateDirectoryNames))
            {
                _sliderBackplateMeshOptions.Add(new SliderMeshOption(BuildSliderMeshOptionLabel(path), path));
            }

            _sliderThumbMeshOptions.Add(new SliderMeshOption("Auto (library/default)", string.Empty));
            foreach (string path in EnumerateDiscoveredSliderModelPaths(sliderThumbModelsDirectories, SliderThumbDirectoryNames))
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

            string resolvedPath = ResolveBestSliderMeshPath(options, configuredPath);
            string normalized = NormalizePathForCompare(resolvedPath);
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

            string normalized = NormalizePathForCompare(ResolveBestSliderMeshPath(options, configuredPath));
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

        private static IEnumerable<string> EnumerateDiscoveredSliderModelPaths(IEnumerable<string> sliderModelsDirectories, string[] preferredDirectories)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string sliderModelsDirectory in sliderModelsDirectories)
            {
                if (string.IsNullOrWhiteSpace(sliderModelsDirectory) || !Directory.Exists(sliderModelsDirectory))
                {
                    continue;
                }

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

        private static string ResolveBestSliderMeshPath(IEnumerable<SliderMeshOption> options, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            string normalizedConfiguredPath = NormalizePathForCompare(configuredPath);
            SliderMeshOption? directMatch = options.FirstOrDefault(option =>
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

            SliderMeshOption? fileNameMatch = options.FirstOrDefault(option =>
                string.Equals(Path.GetFileName(option.MeshPath), fileName, StringComparison.OrdinalIgnoreCase));
            return fileNameMatch?.MeshPath ?? configuredPath;
        }

        private static IReadOnlyList<string> ResolveSliderModelsDirectories()
        {
            var directories = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in EnumerateSliderSearchRoots())
            {
                for (int i = 0; i < SliderModelsDirectoryCandidates.Length; i++)
                {
                    TryAddExistingDirectory(directories, seen, Path.Combine(root, SliderModelsDirectoryCandidates[i]));
                }
            }

            return directories;
        }

        private static IReadOnlyList<string> ResolveSliderThumbModelsDirectories(IReadOnlyList<string> sliderModelsDirectories)
        {
            var directories = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < sliderModelsDirectories.Count; i++)
            {
                TryAddExistingDirectory(directories, seen, sliderModelsDirectories[i]);
            }

            foreach (string root in EnumerateSliderSearchRoots())
            {
                for (int i = 0; i < SliderStandaloneThumbDirectoryCandidates.Length; i++)
                {
                    TryAddExistingDirectory(directories, seen, Path.Combine(root, SliderStandaloneThumbDirectoryCandidates[i]));
                }
            }

            return directories;
        }

        private static IEnumerable<string> EnumerateSliderSearchRoots()
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
                yield return desktop;
            }
        }

        private static void TryAddExistingDirectory(ICollection<string> directories, ISet<string> seen, string candidate)
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
