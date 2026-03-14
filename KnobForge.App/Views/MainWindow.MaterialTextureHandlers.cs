using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KnobForge.App.Controls;
using KnobForge.Core.Scene;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private static readonly FilePickerFileType[] MaterialTextureFileTypes =
        {
            new("Image Files")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
            }
        };

        private async void OnMaterialAlbedoMapBrowseClicked(object? sender, RoutedEventArgs e)
        {
            await BrowseMaterialTextureAsync(MaterialTextureSlot.Albedo);
        }

        private void OnMaterialAlbedoMapClearClicked(object? sender, RoutedEventArgs e)
        {
            ClearMaterialTexture(MaterialTextureSlot.Albedo);
        }

        private async void OnMaterialNormalMapBrowseClicked(object? sender, RoutedEventArgs e)
        {
            await BrowseMaterialTextureAsync(MaterialTextureSlot.Normal);
        }

        private void OnMaterialNormalMapClearClicked(object? sender, RoutedEventArgs e)
        {
            ClearMaterialTexture(MaterialTextureSlot.Normal);
        }

        private async void OnMaterialRoughnessMapBrowseClicked(object? sender, RoutedEventArgs e)
        {
            await BrowseMaterialTextureAsync(MaterialTextureSlot.Roughness);
        }

        private void OnMaterialRoughnessMapClearClicked(object? sender, RoutedEventArgs e)
        {
            ClearMaterialTexture(MaterialTextureSlot.Roughness);
        }

        private async void OnMaterialMetallicMapBrowseClicked(object? sender, RoutedEventArgs e)
        {
            await BrowseMaterialTextureAsync(MaterialTextureSlot.Metallic);
        }

        private void OnMaterialMetallicMapClearClicked(object? sender, RoutedEventArgs e)
        {
            ClearMaterialTexture(MaterialTextureSlot.Metallic);
        }

        private void OnMaterialNormalMapStrengthChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) || _materialNormalMapStrengthInput == null)
            {
                return;
            }

            material.NormalMapStrength = Math.Clamp((float)_materialNormalMapStrengthInput.Value, 0f, 2f);
            ApplyMaterialTextureValuesToUi(material);
            NotifyProjectStateChanged();
        }

        private async Task BrowseMaterialTextureAsync(MaterialTextureSlot slot)
        {
            if (!TryGetSelectedMaterialNode(out MaterialNode material))
            {
                return;
            }

            string? currentPath = GetMaterialTexturePath(material, slot);
            FilePickerOpenOptions options = new()
            {
                AllowMultiple = false,
                Title = $"Select {slot} Texture",
                FileTypeFilter = MaterialTextureFileTypes
            };

            string? suggestedDirectory = GetSuggestedMaterialTextureDirectory(currentPath);
            if (!string.IsNullOrWhiteSpace(suggestedDirectory) && Directory.Exists(suggestedDirectory))
            {
                IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(suggestedDirectory);
                if (folder != null)
                {
                    options.SuggestedStartLocation = folder;
                }
            }

            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                return;
            }

            string? selectedPath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                selectedPath = files[0].Path.LocalPath;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            _metalViewport?.InvalidateMaterialTexturePath(currentPath);
            SetMaterialTexturePath(material, slot, selectedPath);
            ApplyMaterialTextureValuesToUi(material);
            NotifyProjectStateChanged();
        }

        private void ClearMaterialTexture(MaterialTextureSlot slot)
        {
            if (!TryGetSelectedMaterialNode(out MaterialNode material))
            {
                return;
            }

            string? currentPath = GetMaterialTexturePath(material, slot);
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return;
            }

            _metalViewport?.InvalidateMaterialTexturePath(currentPath);
            SetMaterialTexturePath(material, slot, null);
            ApplyMaterialTextureValuesToUi(material);
            NotifyProjectStateChanged();
        }

        private void ApplyMaterialTextureValuesToUi(MaterialNode material)
        {
            if (_materialAlbedoMapPathText == null ||
                _materialNormalMapPathText == null ||
                _materialRoughnessMapPathText == null ||
                _materialMetallicMapPathText == null ||
                _materialNormalMapStrengthInput == null ||
                _materialNormalMapStrengthPanel == null)
            {
                return;
            }

            bool previousUpdatingUi = _updatingUi;
            _updatingUi = true;
            try
            {
                _materialAlbedoMapPathText.Text = FormatMaterialTexturePath(material.AlbedoMapPath);
                _materialNormalMapPathText.Text = FormatMaterialTexturePath(material.NormalMapPath);
                _materialRoughnessMapPathText.Text = FormatMaterialTexturePath(material.RoughnessMapPath);
                _materialMetallicMapPathText.Text = FormatMaterialTexturePath(material.MetallicMapPath);
                _materialNormalMapStrengthInput.Value = Math.Clamp(material.NormalMapStrength, 0f, 2f);
                _materialNormalMapStrengthPanel.IsVisible = material.HasNormalMap;
            }
            finally
            {
                _updatingUi = previousUpdatingUi;
            }

            UpdateReadouts();
        }

        private string? GetSuggestedMaterialTextureDirectory(string? currentPath)
        {
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                string? textureDirectory = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(textureDirectory))
                {
                    return textureDirectory;
                }
            }

            if (!string.IsNullOrWhiteSpace(_currentProjectFilePath))
            {
                string? projectDirectory = Path.GetDirectoryName(_currentProjectFilePath);
                if (!string.IsNullOrWhiteSpace(projectDirectory))
                {
                    return projectDirectory;
                }
            }

            return null;
        }

        private static string FormatMaterialTexturePath(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? "None" : path;
        }

        private static string? GetMaterialTexturePath(MaterialNode material, MaterialTextureSlot slot)
        {
            return slot switch
            {
                MaterialTextureSlot.Albedo => material.AlbedoMapPath,
                MaterialTextureSlot.Normal => material.NormalMapPath,
                MaterialTextureSlot.Roughness => material.RoughnessMapPath,
                MaterialTextureSlot.Metallic => material.MetallicMapPath,
                _ => null
            };
        }

        private static void SetMaterialTexturePath(MaterialNode material, MaterialTextureSlot slot, string? path)
        {
            switch (slot)
            {
                case MaterialTextureSlot.Albedo:
                    material.AlbedoMapPath = path;
                    break;
                case MaterialTextureSlot.Normal:
                    material.NormalMapPath = path;
                    break;
                case MaterialTextureSlot.Roughness:
                    material.RoughnessMapPath = path;
                    break;
                case MaterialTextureSlot.Metallic:
                    material.MetallicMapPath = path;
                    break;
            }
        }

        private enum MaterialTextureSlot
        {
            Albedo,
            Normal,
            Roughness,
            Metallic
        }
    }
}
