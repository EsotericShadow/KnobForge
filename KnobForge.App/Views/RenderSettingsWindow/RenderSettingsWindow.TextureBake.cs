using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KnobForge.App.Views
{
    public partial class RenderSettingsWindow
    {
        private sealed class TextureBakeMaterialOption
        {
            public TextureBakeMaterialOption(int? materialIndex, string displayName, string nameToken)
            {
                MaterialIndex = materialIndex;
                DisplayName = displayName;
                NameToken = nameToken;
            }

            public int? MaterialIndex { get; }

            public string DisplayName { get; }

            public string NameToken { get; }

            public bool RepresentsAllMaterials => MaterialIndex is null;

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private readonly record struct TextureBakePlanItem(
            MaterialNode Material,
            int MaterialIndex,
            string DisplayName,
            string BaseName);

        private string BuildDefaultTextureBakeBaseName()
        {
            if (!string.IsNullOrWhiteSpace(_projectFilePath))
            {
                string projectName = Path.GetFileNameWithoutExtension(_projectFilePath);
                if (!string.IsNullOrWhiteSpace(projectName))
                {
                    return projectName;
                }
            }

            ModelNode? modelNode = _project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            if (modelNode != null && !string.IsNullOrWhiteSpace(modelNode.Name))
            {
                return modelNode.Name.Trim();
            }

            return "bake";
        }

        private void RefreshTextureBakeMaterialOptions()
        {
            MaterialNode[] materials = _project.GetMaterialNodes().ToArray();
            if (materials.Length == 0)
            {
                _textureBakeMaterialOptions = new[]
                {
                    new TextureBakeMaterialOption(0, "Default Material", "default")
                };
                _textureBakeMaterialComboBox.ItemsSource = _textureBakeMaterialOptions;
                _textureBakeMaterialComboBox.SelectedIndex = 0;
                return;
            }

            int? previouslySelectedIndex = (_textureBakeMaterialComboBox.SelectedItem as TextureBakeMaterialOption)?.MaterialIndex;
            bool previouslyAll = (_textureBakeMaterialComboBox.SelectedItem as TextureBakeMaterialOption)?.RepresentsAllMaterials == true;
            int optionCount = materials.Length > 1 ? materials.Length + 1 : materials.Length;
            TextureBakeMaterialOption[] options = new TextureBakeMaterialOption[optionCount];
            int writeIndex = 0;
            if (materials.Length > 1)
            {
                options[writeIndex++] = new TextureBakeMaterialOption(null, "All Materials", "all");
            }

            for (int i = 0; i < materials.Length; i++)
            {
                string displayName = string.IsNullOrWhiteSpace(materials[i].Name)
                    ? $"Material {i + 1}"
                    : materials[i].Name.Trim();
                string token = SanitizeFileNamePart(displayName, $"material_{i + 1:00}");
                options[writeIndex++] = new TextureBakeMaterialOption(i, $"{i + 1}. {displayName}", token);
            }

            _textureBakeMaterialOptions = options;
            _textureBakeMaterialComboBox.ItemsSource = _textureBakeMaterialOptions;

            TextureBakeMaterialOption? selection = null;
            if (previouslyAll)
            {
                selection = _textureBakeMaterialOptions.FirstOrDefault(option => option.RepresentsAllMaterials);
            }
            else if (previouslySelectedIndex is int selectedIndex)
            {
                selection = _textureBakeMaterialOptions.FirstOrDefault(option => option.MaterialIndex == selectedIndex);
            }

            _textureBakeMaterialComboBox.SelectedItem = selection ?? _textureBakeMaterialOptions.FirstOrDefault();
        }

        private async void OnBrowseTextureBakeOutputButtonClick(object? sender, RoutedEventArgs e)
        {
            FolderPickerOpenOptions options = new()
            {
                AllowMultiple = false,
                Title = "Select texture bake folder"
            };

            if (Directory.Exists(_textureBakeOutputFolderTextBox.Text))
            {
                IStorageFolder? suggested = await StorageProvider.TryGetFolderFromPathAsync(_textureBakeOutputFolderTextBox.Text);
                if (suggested != null)
                {
                    options.SuggestedStartLocation = suggested;
                }
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count == 0)
            {
                return;
            }

            string? selectedPath = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                selectedPath = folders[0].Path.LocalPath;
            }

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                _textureBakeOutputFolderTextBox.Text = selectedPath;
                UpdateTextureBakeSummary();
            }
        }

        private async void OnBakeTexturesButtonClick(object? sender, RoutedEventArgs e)
        {
            if (_isBakingTextures)
            {
                return;
            }

            if (!TryBuildTextureBakePlan(out TextureBakeSettings settings, out TextureBakePlanItem[] plan, out string error))
            {
                await ShowInfoDialogAsync("Invalid bake settings", error);
                return;
            }

            _textureBakeCts = new CancellationTokenSource();
            _textureBakeProgressBar.Value = 0d;
            _textureBakeStatusTextBlock.Text = "Preparing texture bake...";
            SetTextureBakeState(true);

            try
            {
                BakeResult[] bakeResults = await Task.Run(() =>
                {
                    var baker = new TextureBaker();
                    BakeResult[] results = new BakeResult[plan.Length];
                    for (int i = 0; i < plan.Length; i++)
                    {
                        _textureBakeCts.Token.ThrowIfCancellationRequested();
                        TextureBakePlanItem item = plan[i];
                        Dispatcher.UIThread.Post(() =>
                        {
                            _textureBakeStatusTextBlock.Text = $"Baking {item.DisplayName} ({i + 1}/{plan.Length})...";
                        });

                        var perMaterialSettings = new TextureBakeSettings
                        {
                            Resolution = settings.Resolution,
                            BakeAlbedo = settings.BakeAlbedo,
                            BakeNormal = settings.BakeNormal,
                            BakeRoughness = settings.BakeRoughness,
                            BakeMetallic = settings.BakeMetallic,
                            OutputFolder = settings.OutputFolder,
                            BaseName = item.BaseName
                        };

                        var progress = new Progress<float>(value =>
                        {
                            double percent = ((i + Math.Clamp(value, 0f, 1f)) / Math.Max(1, (double)plan.Length)) * 100d;
                            Dispatcher.UIThread.Post(() => _textureBakeProgressBar.Value = percent);
                        });

                        BakeResult result = baker.Bake(_project, item.Material, perMaterialSettings, progress, _textureBakeCts.Token);
                        if (!result.Success)
                        {
                            throw new InvalidOperationException(result.Error ?? $"Texture bake failed for {item.DisplayName}.");
                        }

                        results[i] = result;
                    }

                    return results;
                }, _textureBakeCts.Token);

                _lastTextureBakeOutputFolder = settings.OutputFolder;
                _textureBakeProgressBar.Value = 100d;
                int exportedMapCount = bakeResults.Sum(CountExportedBakeFiles);
                _textureBakeStatusTextBlock.Text = $"Exported {exportedMapCount} map file(s) for {plan.Length} material(s).";

                bool shouldOpen = await ShowConfirmDialogAsync(
                    "Texture bake complete",
                    $"Texture bake complete.\nExported {exportedMapCount} map file(s) to:\n{settings.OutputFolder}\n\nOpen folder?");
                if (shouldOpen)
                {
                    OpenFolder(settings.OutputFolder);
                }
            }
            catch (OperationCanceledException)
            {
                _textureBakeStatusTextBlock.Text = "Texture bake cancelled.";
            }
            catch (Exception ex)
            {
                _textureBakeStatusTextBlock.Text = "Texture bake failed.";
                await ShowInfoDialogAsync("Texture bake failed", ex.Message);
            }
            finally
            {
                _textureBakeCts?.Dispose();
                _textureBakeCts = null;
                SetTextureBakeState(false);
            }
        }

        private void OnCancelTextureBakeButtonClick(object? sender, RoutedEventArgs e)
        {
            if (!_isBakingTextures)
            {
                return;
            }

            _textureBakeStatusTextBlock.Text = "Cancelling texture bake...";
            _textureBakeCts?.Cancel();
        }

        private async void OnOpenTextureBakeFolderButtonClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastTextureBakeOutputFolder) || !Directory.Exists(_lastTextureBakeOutputFolder))
            {
                await ShowInfoDialogAsync("Open folder failed", "No baked texture output folder is available yet.");
                return;
            }

            try
            {
                OpenFolder(_lastTextureBakeOutputFolder);
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Open folder failed", ex.Message);
            }
        }

        private bool TryBuildTextureBakePlan(out TextureBakeSettings settings, out TextureBakePlanItem[] plan, out string error)
        {
            settings = new TextureBakeSettings();
            plan = Array.Empty<TextureBakePlanItem>();
            error = string.Empty;

            if (!TryParseInt(_textureBakeResolutionComboBox.SelectedItem?.ToString() ?? _textureBakeResolutionComboBox.Text, 256, 4096, "Bake Resolution", out int resolution, out error))
            {
                return false;
            }

            if (resolution is not (256 or 512 or 1024 or 2048 or 4096))
            {
                error = "Bake Resolution must be 256, 512, 1024, 2048, or 4096.";
                return false;
            }

            string outputFolder = (_textureBakeOutputFolderTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                error = "Texture bake output folder is required.";
                return false;
            }

            string baseName = (_textureBakeBaseNameTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseName))
            {
                error = "Texture bake base name is required.";
                return false;
            }

            settings = new TextureBakeSettings
            {
                Resolution = resolution,
                BakeAlbedo = _textureBakeAlbedoCheckBox.IsChecked == true,
                BakeNormal = _textureBakeNormalCheckBox.IsChecked == true,
                BakeRoughness = _textureBakeRoughnessCheckBox.IsChecked == true,
                BakeMetallic = _textureBakeMetallicCheckBox.IsChecked == true,
                OutputFolder = outputFolder,
                BaseName = baseName
            };

            if (!settings.BakeAlbedo && !settings.BakeNormal && !settings.BakeRoughness && !settings.BakeMetallic)
            {
                error = "Select at least one bake map.";
                return false;
            }

            MaterialNode[] materials = _project.GetMaterialNodes().ToArray();
            if (materials.Length == 0)
            {
                materials = new[] { _project.EnsureMaterialNode() };
            }

            TextureBakeMaterialOption? selectedOption = _textureBakeMaterialComboBox.SelectedItem as TextureBakeMaterialOption
                ?? _textureBakeMaterialOptions.FirstOrDefault();
            if (selectedOption is null)
            {
                error = "No material is available for baking.";
                return false;
            }

            if (selectedOption.RepresentsAllMaterials)
            {
                plan = materials
                    .Select((material, index) => new TextureBakePlanItem(
                        material,
                        index,
                        GetMaterialDisplayName(material, index),
                        BuildPerMaterialBakeBaseName(baseName, index, material.Name)))
                    .ToArray();
                return plan.Length > 0;
            }

            int materialIndex = Math.Clamp(selectedOption.MaterialIndex ?? 0, 0, materials.Length - 1);
            bool includeMaterialSuffix = materials.Length > 1;
            plan = new[]
            {
                new TextureBakePlanItem(
                    materials[materialIndex],
                    materialIndex,
                    GetMaterialDisplayName(materials[materialIndex], materialIndex),
                    includeMaterialSuffix
                        ? BuildPerMaterialBakeBaseName(baseName, materialIndex, materials[materialIndex].Name)
                        : baseName)
            };
            return true;
        }

        private static string GetMaterialDisplayName(MaterialNode material, int index)
        {
            return string.IsNullOrWhiteSpace(material.Name)
                ? $"Material {index + 1}"
                : material.Name.Trim();
        }

        private static string BuildPerMaterialBakeBaseName(string baseName, int materialIndex, string? materialName)
        {
            string safeBase = SanitizeFileNamePart(baseName, "bake");
            string safeMaterial = SanitizeFileNamePart(materialName, $"material_{materialIndex + 1:00}");
            return $"{safeBase}_{materialIndex + 1:00}_{safeMaterial}";
        }

        private static string SanitizeFileNamePart(string? value, string fallback)
        {
            string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        private int GetSelectedTextureBakeMapCount(TextureBakeSettings? settings = null)
        {
            TextureBakeSettings active = settings ?? new TextureBakeSettings
            {
                BakeAlbedo = _textureBakeAlbedoCheckBox.IsChecked == true,
                BakeNormal = _textureBakeNormalCheckBox.IsChecked == true,
                BakeRoughness = _textureBakeRoughnessCheckBox.IsChecked == true,
                BakeMetallic = _textureBakeMetallicCheckBox.IsChecked == true
            };

            int count = 0;
            if (active.BakeAlbedo)
            {
                count++;
            }
            if (active.BakeNormal)
            {
                count++;
            }
            if (active.BakeRoughness)
            {
                count++;
            }
            if (active.BakeMetallic)
            {
                count++;
            }

            return count;
        }

        private int GetSelectedTextureBakeMaterialCount()
        {
            MaterialNode[] materials = _project.GetMaterialNodes().ToArray();
            if (materials.Length <= 1)
            {
                return Math.Max(1, materials.Length);
            }

            if ((_textureBakeMaterialComboBox.SelectedItem as TextureBakeMaterialOption)?.RepresentsAllMaterials == true)
            {
                return materials.Length;
            }

            return 1;
        }

        private static int CountExportedBakeFiles(BakeResult result)
        {
            int count = 0;
            if (!string.IsNullOrWhiteSpace(result.AlbedoPath))
            {
                count++;
            }
            if (!string.IsNullOrWhiteSpace(result.NormalPath))
            {
                count++;
            }
            if (!string.IsNullOrWhiteSpace(result.RoughnessPath))
            {
                count++;
            }
            if (!string.IsNullOrWhiteSpace(result.MetallicPath))
            {
                count++;
            }

            return count;
        }

        private void UpdateTextureBakeSummary()
        {
            int mapCount = GetSelectedTextureBakeMapCount();
            string resolutionText = _textureBakeResolutionComboBox.SelectedItem?.ToString()
                ?? _textureBakeResolutionComboBox.Text
                ?? string.Empty;
            if (!int.TryParse(resolutionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int resolution))
            {
                resolution = 1024;
            }

            int materialCount = GetSelectedTextureBakeMaterialCount();
            long estimatedBytes = (long)resolution * resolution * 4L * Math.Max(1, mapCount) * Math.Max(1, materialCount);
            double estimatedMb = estimatedBytes / (1024d * 1024d);
            string materialScopeText = materialCount == 1 ? "1 material" : $"{materialCount} materials";
            _textureBakeEstimateTextBlock.Text =
                $"{resolution}x{resolution} x {Math.Max(1, mapCount)} map(s) across {materialScopeText} ≈ {estimatedMb:0.#} MB raw RGBA before PNG compression.";

            string baseName = (_textureBakeBaseNameTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "bake";
            }

            MaterialNode[] materials = _project.GetMaterialNodes().ToArray();
            TextureBakeMaterialOption? option = _textureBakeMaterialComboBox.SelectedItem as TextureBakeMaterialOption;
            string previewBaseName;
            if (materials.Length > 1 && option != null && option.RepresentsAllMaterials)
            {
                previewBaseName = BuildPerMaterialBakeBaseName(baseName, 0, materials[0].Name);
                _textureBakeFilePreviewTextBlock.Text = $"Example output: {previewBaseName}_albedo.png (+ matching normal/roughness/metallic files per material).";
                return;
            }

            if (materials.Length > 1 && option?.MaterialIndex is int selectedIndex)
            {
                previewBaseName = BuildPerMaterialBakeBaseName(baseName, selectedIndex, materials[Math.Clamp(selectedIndex, 0, materials.Length - 1)].Name);
            }
            else
            {
                previewBaseName = SanitizeFileNamePart(baseName, "bake");
            }

            _textureBakeFilePreviewTextBlock.Text = $"Output files: {previewBaseName}_albedo.png, {previewBaseName}_normal.png, {previewBaseName}_roughness.png, {previewBaseName}_metallic.png.";
        }

        private void UpdateTextureBakeControlsState()
        {
            bool controlsEnabled = !_isRendering && !_isBakingTextures;
            bool hasMaterials = _textureBakeMaterialOptions.Length > 0;

            _textureBakeSection.IsEnabled = true;
            _textureBakeMaterialComboBox.IsEnabled = controlsEnabled && hasMaterials;
            _textureBakeResolutionComboBox.IsEnabled = controlsEnabled;
            _textureBakeAlbedoCheckBox.IsEnabled = controlsEnabled;
            _textureBakeNormalCheckBox.IsEnabled = controlsEnabled;
            _textureBakeRoughnessCheckBox.IsEnabled = controlsEnabled;
            _textureBakeMetallicCheckBox.IsEnabled = controlsEnabled;
            _textureBakeOutputFolderTextBox.IsEnabled = false;
            _browseTextureBakeOutputButton.IsEnabled = controlsEnabled;
            _textureBakeBaseNameTextBox.IsEnabled = controlsEnabled;
            _bakeTexturesButton.IsEnabled = controlsEnabled && hasMaterials;
            _cancelTextureBakeButton.IsEnabled = _isBakingTextures;
            _openTextureBakeFolderButton.IsEnabled =
                !_isBakingTextures &&
                !string.IsNullOrWhiteSpace(_lastTextureBakeOutputFolder) &&
                Directory.Exists(_lastTextureBakeOutputFolder);
        }

        private void SetTextureBakeState(bool isBaking)
        {
            _isBakingTextures = isBaking;
            if (!isBaking && _textureBakeStatusTextBlock.Text == "Preparing texture bake...")
            {
                _textureBakeStatusTextBlock.Text = "Ready to bake textures.";
            }

            UpdateTextureBakeControlsState();
            UpdateStartRenderAvailability(preserveCurrentNonErrorStatus: true);
        }

        private void OnTextureBakeSettingsChanged(object? sender, EventArgs e)
        {
            UpdateTextureBakeSummary();
        }
    }
}
