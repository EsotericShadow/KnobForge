using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System;
using System.Linq;
using System.Numerics;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnCollarSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _collarEnabledCheckBox == null ||
                _collarPresetCombo == null ||
                _collarMeshPathTextBox == null ||
                _collarScaleInput == null ||
                _collarBodyLengthInput == null ||
                _collarBodyThicknessInput == null ||
                _collarHeadLengthInput == null ||
                _collarHeadThicknessInput == null ||
                _collarRotateInput == null ||
                _collarMirrorXCheckBox == null ||
                _collarMirrorYCheckBox == null ||
                _collarMirrorZCheckBox == null ||
                _collarOffsetXInput == null ||
                _collarOffsetYInput == null ||
                _collarElevationInput == null ||
                _collarInflateInput == null)
            {
                return;
            }

            bool sliderChange = false;
            if (ReferenceEquals(sender, _collarEnabledCheckBox) ||
                ReferenceEquals(sender, _collarMirrorXCheckBox) ||
                ReferenceEquals(sender, _collarMirrorYCheckBox) ||
                ReferenceEquals(sender, _collarMirrorZCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _collarPresetCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }

                if (_collarPresetCombo.SelectedItem is CollarPresetOption candidate && !candidate.IsSelectable)
                {
                    CollarPresetOption fallback = ResolveSelectedCollarPresetOption();
                    WithUiRefreshSuppressed(() =>
                    {
                        _collarPresetCombo.SelectedItem = fallback;
                    });
                    return;
                }
            }
            else if (ReferenceEquals(sender, _collarMeshPathTextBox))
            {
                if (e.Property != TextBox.TextProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }
            else
            {
                sliderChange = true;
            }

            if (GetModelNode() == null)
            {
                return;
            }

            CollarNode collar = EnsureCollarNode();
            collar.Enabled = _collarEnabledCheckBox.IsChecked ?? false;
            CollarPresetOption selectedOption = ResolveSelectedCollarPresetOption();
            _lastSelectableCollarPresetOption = selectedOption;
            if (ReferenceEquals(sender, _collarPresetCombo) &&
                e.Property == ComboBox.SelectedItemProperty &&
                selectedOption.TryGetImportedMirrorDefaults(out bool defaultMirrorX, out bool defaultMirrorY, out bool defaultMirrorZ))
            {
                bool mirrorXChanged = (_collarMirrorXCheckBox.IsChecked ?? false) != defaultMirrorX;
                bool mirrorYChanged = (_collarMirrorYCheckBox.IsChecked ?? false) != defaultMirrorY;
                bool mirrorZChanged = (_collarMirrorZCheckBox.IsChecked ?? false) != defaultMirrorZ;
                if (mirrorXChanged || mirrorYChanged || mirrorZChanged)
                {
                    WithUiRefreshSuppressed(() =>
                    {
                        _collarMirrorXCheckBox.IsChecked = defaultMirrorX;
                        _collarMirrorYCheckBox.IsChecked = defaultMirrorY;
                        _collarMirrorZCheckBox.IsChecked = defaultMirrorZ;
                    });
                }
            }

            collar.Preset = selectedOption.Preset;
            string resolvedImportedMeshPath = ResolveBestImportedCollarPath(
                selectedOption.Preset,
                selectedOption.ResolveImportedMeshPath(_collarMeshPathTextBox.Text));
            collar.ImportedMeshPath = resolvedImportedMeshPath;
            collar.ImportedScale = (float)_collarScaleInput.Value;
            collar.ImportedBodyLengthScale = (float)_collarBodyLengthInput.Value;
            collar.ImportedBodyThicknessScale = (float)_collarBodyThicknessInput.Value;
            collar.ImportedHeadLengthScale = (float)_collarHeadLengthInput.Value;
            collar.ImportedHeadThicknessScale = (float)_collarHeadThicknessInput.Value;
            collar.ImportedRotationRadians = (float)DegreesToRadians(_collarRotateInput.Value);
            collar.ImportedMirrorX = _collarMirrorXCheckBox.IsChecked ?? false;
            collar.ImportedMirrorY = _collarMirrorYCheckBox.IsChecked ?? false;
            collar.ImportedMirrorZ = _collarMirrorZCheckBox.IsChecked ?? false;
            collar.ImportedOffsetXRatio = (float)_collarOffsetXInput.Value;
            collar.ImportedOffsetYRatio = (float)_collarOffsetYInput.Value;
            collar.ElevationRatio = (float)_collarElevationInput.Value;
            collar.ImportedInflateRatio = (float)_collarInflateInput.Value;

            bool importedMaterialSourceChanged =
                ReferenceEquals(sender, _collarPresetCombo) ||
                ReferenceEquals(sender, _collarMeshPathTextBox);
            if (importedMaterialSourceChanged)
            {
                SyncImportedCollarMaterialNodes();
            }

            if (e.Property == ComboBox.SelectedItemProperty &&
                !string.Equals(_collarMeshPathTextBox.Text, resolvedImportedMeshPath, StringComparison.Ordinal))
            {
                WithUiRefreshSuppressed(() =>
                {
                    _collarMeshPathTextBox.Text = resolvedImportedMeshPath;
                });
            }

            if (sliderChange)
            {
                UpdateReadouts();
                RequestHeavyGeometryRefresh();
            }
            else
            {
                UpdateCollarControlEnablement(true, collar.Preset);
                NotifyProjectStateChanged();
            }
        }

        private void OnCollarMaterialChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                e.Property != ValueInput.ValueProperty ||
                _collarMaterialBaseRInput == null ||
                _collarMaterialBaseGInput == null ||
                _collarMaterialBaseBInput == null ||
                _collarMaterialMetallicInput == null ||
                _collarMaterialRoughnessInput == null ||
                _collarMaterialPearlescenceInput == null ||
                _collarMaterialRustInput == null ||
                _collarMaterialWearInput == null ||
                _collarMaterialGunkInput == null)
            {
                return;
            }

            if (GetModelNode() == null)
            {
                return;
            }

            CollarNode collar = EnsureCollarNode();
            Vector3 baseColor = new(
                (float)_collarMaterialBaseRInput.Value,
                (float)_collarMaterialBaseGInput.Value,
                (float)_collarMaterialBaseBInput.Value);
            float metallic = (float)_collarMaterialMetallicInput.Value;
            float roughness = (float)_collarMaterialRoughnessInput.Value;
            float pearlescence = (float)_collarMaterialPearlescenceInput.Value;
            float rust = (float)_collarMaterialRustInput.Value;
            float wear = (float)_collarMaterialWearInput.Value;
            float gunk = (float)_collarMaterialGunkInput.Value;

            if (CollarNode.IsImportedMeshPreset(collar.Preset) &&
                TryGetSelectedMaterialNode(out MaterialNode importedMaterial))
            {
                importedMaterial.BaseColor = baseColor;
                importedMaterial.Metallic = metallic;
                importedMaterial.Roughness = roughness;
                importedMaterial.Pearlescence = pearlescence;
                importedMaterial.RustAmount = rust;
                importedMaterial.WearAmount = wear;
                importedMaterial.GunkAmount = gunk;

                // Keep the legacy collar node in sync so single-material imported collars
                // still have sensible fallback values if material extraction is unavailable.
                collar.BaseColor = baseColor;
                collar.Metallic = metallic;
                collar.Roughness = roughness;
                collar.Pearlescence = pearlescence;
                collar.RustAmount = rust;
                collar.WearAmount = wear;
                collar.GunkAmount = gunk;
            }
            else
            {
                collar.BaseColor = baseColor;
                collar.Metallic = metallic;
                collar.Roughness = roughness;
                collar.Pearlescence = pearlescence;
                collar.RustAmount = rust;
                collar.WearAmount = wear;
                collar.GunkAmount = gunk;
            }

            NotifyProjectStateChanged();
        }

        private void OnIndicatorSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _indicatorEnabledCheckBox == null ||
                _indicatorCadWallsCheckBox == null ||
                _indicatorShapeCombo == null ||
                _indicatorReliefCombo == null ||
                _indicatorProfileCombo == null ||
                _indicatorWidthInput == null ||
                _indicatorLengthInput == null ||
                _indicatorPositionInput == null ||
                _indicatorThicknessInput == null ||
                _indicatorRoundnessInput == null ||
                _indicatorColorBlendInput == null ||
                _indicatorColorRInput == null ||
                _indicatorColorGInput == null ||
                _indicatorColorBInput == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _indicatorEnabledCheckBox) ||
                ReferenceEquals(sender, _indicatorCadWallsCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _indicatorShapeCombo) ||
                     ReferenceEquals(sender, _indicatorReliefCombo) ||
                     ReferenceEquals(sender, _indicatorProfileCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            var model = GetModelNode();
            if (model == null)
            {
                return;
            }

            model.IndicatorEnabled = _indicatorEnabledCheckBox.IsChecked ?? true;
            model.IndicatorCadWallsEnabled = _indicatorCadWallsCheckBox.IsChecked ?? true;
            model.IndicatorShape = _indicatorShapeCombo.SelectedItem is IndicatorShape shape ? shape : IndicatorShape.Bar;
            model.IndicatorRelief = _indicatorReliefCombo.SelectedItem is IndicatorRelief relief ? relief : IndicatorRelief.Extrude;
            model.IndicatorProfile = _indicatorProfileCombo.SelectedItem is IndicatorProfile profile ? profile : IndicatorProfile.Straight;
            model.IndicatorWidthRatio = (float)_indicatorWidthInput.Value;
            model.IndicatorLengthRatioTop = (float)_indicatorLengthInput.Value;
            model.IndicatorPositionRatio = (float)_indicatorPositionInput.Value;
            model.IndicatorThicknessRatio = (float)_indicatorThicknessInput.Value;
            model.IndicatorRoundness = (float)_indicatorRoundnessInput.Value;
            model.IndicatorColorBlend = (float)_indicatorColorBlendInput.Value;
            model.IndicatorColor = new Vector3(
                (float)_indicatorColorRInput.Value,
                (float)_indicatorColorGInput.Value,
                (float)_indicatorColorBInput.Value);

            NotifyProjectStateChanged();
        }

        private void OnMaterialBaseColorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) ||
                _materialBaseRInput == null || _materialBaseGInput == null || _materialBaseBInput == null)
            {
                return;
            }

            Vector3 color = new(
                (float)_materialBaseRInput.Value,
                (float)_materialBaseGInput.Value,
                (float)_materialBaseBInput.Value);
            MaterialRegionTarget region = ResolveSelectedMaterialRegion();
            if (region == MaterialRegionTarget.WholeKnob)
            {
                material.BaseColor = color;
                if (material.PartMaterialsEnabled)
                {
                    material.PartMaterialsEnabled = false;
                }
                material.SyncPartMaterialsFromGlobal();
            }
            else
            {
                EnsurePartMaterialsEnabled(material);
                SetPartBaseColor(material, region, color);
            }

            NotifyProjectStateChanged();
        }

        private void OnMaterialMetallicChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) || _materialMetallicInput == null)
            {
                return;
            }

            float metallic = (float)_materialMetallicInput.Value;
            MaterialRegionTarget region = ResolveSelectedMaterialRegion();
            if (region == MaterialRegionTarget.WholeKnob)
            {
                material.Metallic = metallic;
                if (material.PartMaterialsEnabled)
                {
                    material.PartMaterialsEnabled = false;
                }
                material.SyncPartMaterialsFromGlobal();
            }
            else
            {
                EnsurePartMaterialsEnabled(material);
                SetPartMetallic(material, region, metallic);
            }

            NotifyProjectStateChanged();
        }

        private void OnMaterialRoughnessChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) || _materialRoughnessInput == null)
            {
                return;
            }

            float roughness = (float)_materialRoughnessInput.Value;
            MaterialRegionTarget region = ResolveSelectedMaterialRegion();
            if (region == MaterialRegionTarget.WholeKnob)
            {
                material.Roughness = roughness;
                if (material.PartMaterialsEnabled)
                {
                    material.PartMaterialsEnabled = false;
                }
                material.SyncPartMaterialsFromGlobal();
            }
            else
            {
                EnsurePartMaterialsEnabled(material);
                SetPartRoughness(material, region, roughness);
            }

            NotifyProjectStateChanged();
        }

        private void OnMaterialRegionChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _materialRegionCombo == null || e.Property != ComboBox.SelectedItemProperty)
            {
                return;
            }

            if (!TryGetSelectedMaterialNode(out MaterialNode material))
            {
                return;
            }

            MaterialRegionTarget region = ResolveSelectedMaterialRegion();
            bool mutated = false;
            if (region != MaterialRegionTarget.WholeKnob && !material.PartMaterialsEnabled)
            {
                material.SyncPartMaterialsFromGlobal();
                material.PartMaterialsEnabled = true;
                mutated = true;
            }

            ApplyMaterialRegionValuesToSliders(material);
            if (mutated)
            {
                NotifyProjectStateChanged();
            }
        }

        private void OnAssemblyMaterialPresetChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _assemblyMaterialPresetCombo == null || e.Property != ComboBox.SelectedItemProperty)
            {
                return;
            }

            if (_assemblyMaterialPresetCombo.SelectedItem is not AssemblyMaterialPresetOption option)
            {
                return;
            }

            switch (_project.ProjectType)
            {
                case InteractorProjectType.ThumbSlider:
                    _project.SliderMaterialPreset = Enum.IsDefined(typeof(SliderMaterialPresetId), option.Value)
                        ? (SliderMaterialPresetId)option.Value
                        : SliderMaterialPresetId.Custom;
                    break;
                case InteractorProjectType.FlipSwitch:
                    _project.ToggleMaterialPreset = Enum.IsDefined(typeof(ToggleMaterialPresetId), option.Value)
                        ? (ToggleMaterialPresetId)option.Value
                        : ToggleMaterialPresetId.Custom;
                    break;
                case InteractorProjectType.PushButton:
                    _project.PushButtonMaterialPreset = Enum.IsDefined(typeof(PushButtonMaterialPresetId), option.Value)
                        ? (PushButtonMaterialPresetId)option.Value
                        : PushButtonMaterialPresetId.Custom;
                    break;
                default:
                    return;
            }

            RefreshAssemblyMaterialPresetUi();
            NotifyProjectStateChanged();
        }

        private void OnMaterialPearlescenceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) || _materialPearlescenceInput == null)
            {
                return;
            }

            material.Pearlescence = (float)_materialPearlescenceInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnMaterialAgingChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) ||
                _materialRustInput == null || _materialWearInput == null || _materialGunkInput == null)
            {
                return;
            }

            material.RustAmount = (float)_materialRustInput.Value;
            material.WearAmount = (float)_materialWearInput.Value;
            material.GunkAmount = (float)_materialGunkInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnMaterialSurfaceCharacterChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!CanMutateSelectedMaterial(e, ValueInput.ValueProperty, out var material) ||
                _materialBrushStrengthInput == null || _materialBrushDensityInput == null || _materialCharacterInput == null)
            {
                return;
            }

            material.RadialBrushStrength = (float)_materialBrushStrengthInput.Value;
            material.RadialBrushDensity = (float)_materialBrushDensityInput.Value;
            material.SurfaceCharacter = (float)_materialCharacterInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnMicroDetailSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _spiralNormalInfluenceCheckBox == null ||
                _basisDebugModeCombo == null ||
                _microLodFadeStartInput == null ||
                _microLodFadeEndInput == null ||
                _microRoughnessLodBoostInput == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _spiralNormalInfluenceCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _basisDebugModeCombo))
            {
                if (e.Property != SelectingItemsControl.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            float fadeStart = Math.Clamp((float)_microLodFadeStartInput.Value, 0.1f, 10f);
            float fadeEnd = Math.Clamp((float)_microLodFadeEndInput.Value, 0.1f, 12f);
            float minEnd = fadeStart + 0.01f;
            if (fadeEnd < minEnd)
            {
                fadeEnd = minEnd;
                bool previousUpdatingUi = _updatingUi;
                _updatingUi = true;
                try
                {
                    _microLodFadeEndInput.Value = fadeEnd;
                }
                finally
                {
                    _updatingUi = previousUpdatingUi;
                }
            }

            _project.SpiralNormalInfluenceEnabled = _spiralNormalInfluenceCheckBox.IsChecked ?? true;
            _project.BasisDebug = _basisDebugModeCombo.SelectedItem is BasisDebugMode mode
                ? mode
                : BasisDebugMode.Off;
            _project.SpiralNormalLodFadeStart = fadeStart;
            _project.SpiralNormalLodFadeEnd = fadeEnd;
            _project.SpiralRoughnessLodBoost = Math.Clamp((float)_microRoughnessLodBoostInput.Value, 0f, 1f);
            NotifyRenderOnly();
        }
        private bool CanMutateSelectedMaterial(AvaloniaPropertyChangedEventArgs e, AvaloniaProperty expectedProperty, out MaterialNode material)
        {
            material = null!;
            if (_updatingUi || e.Property != expectedProperty)
            {
                return false;
            }

            if (IsAssemblyMaterialPresetActive())
            {
                return false;
            }

            if (!TryGetSelectedMaterialNode(out MaterialNode selected))
            {
                return false;
            }

            material = selected;
            return true;
        }

        private bool IsAssemblyMaterialPresetActive()
        {
            return _project.ProjectType switch
            {
                InteractorProjectType.ThumbSlider => _project.SliderMaterialPreset != SliderMaterialPresetId.Custom,
                InteractorProjectType.FlipSwitch => _project.ToggleMaterialPreset != ToggleMaterialPresetId.Custom,
                InteractorProjectType.PushButton => _project.PushButtonMaterialPreset != PushButtonMaterialPresetId.Custom,
                _ => false
            };
        }

        private bool TryGetSelectedMaterialNode(out MaterialNode material)
        {
            material = null!;
            MaterialNode[] materials = GetAvailableMaterialNodes();
            if (materials.Length == 0)
            {
                return false;
            }

            int selectedIndex = ClampSelectedMaterialIndex(materials);
            if (selectedIndex < 0 || selectedIndex >= materials.Length)
            {
                return false;
            }

            material = materials[selectedIndex];
            return true;
        }

        private MaterialRegionTarget ResolveSelectedMaterialRegion()
        {
            if (_materialRegionCombo?.SelectedItem is MaterialRegionOption option)
            {
                return option.Target;
            }

            if (_materialRegionCombo?.SelectedItem is MaterialRegionTarget region)
            {
                return region;
            }

            return MaterialRegionTarget.WholeKnob;
        }

        private void EnsurePartMaterialsEnabled(MaterialNode material)
        {
            if (material.PartMaterialsEnabled)
            {
                return;
            }

            material.SyncPartMaterialsFromGlobal();
            material.PartMaterialsEnabled = true;
        }

        private static Vector3 GetPartBaseColor(MaterialNode material, MaterialRegionTarget region)
        {
            return region switch
            {
                MaterialRegionTarget.TopCap => material.TopBaseColor,
                MaterialRegionTarget.Bevel => material.BevelBaseColor,
                MaterialRegionTarget.Side => material.SideBaseColor,
                _ => material.BaseColor
            };
        }

        private static float GetPartMetallic(MaterialNode material, MaterialRegionTarget region)
        {
            return region switch
            {
                MaterialRegionTarget.TopCap => material.TopMetallic,
                MaterialRegionTarget.Bevel => material.BevelMetallic,
                MaterialRegionTarget.Side => material.SideMetallic,
                _ => material.Metallic
            };
        }

        private static float GetPartRoughness(MaterialNode material, MaterialRegionTarget region)
        {
            return region switch
            {
                MaterialRegionTarget.TopCap => material.TopRoughness,
                MaterialRegionTarget.Bevel => material.BevelRoughness,
                MaterialRegionTarget.Side => material.SideRoughness,
                _ => material.Roughness
            };
        }

        private static void SetPartBaseColor(MaterialNode material, MaterialRegionTarget region, Vector3 color)
        {
            switch (region)
            {
                case MaterialRegionTarget.TopCap:
                    material.TopBaseColor = color;
                    break;
                case MaterialRegionTarget.Bevel:
                    material.BevelBaseColor = color;
                    break;
                case MaterialRegionTarget.Side:
                    material.SideBaseColor = color;
                    break;
                default:
                    material.BaseColor = color;
                    break;
            }
        }

        private static void SetPartMetallic(MaterialNode material, MaterialRegionTarget region, float metallic)
        {
            switch (region)
            {
                case MaterialRegionTarget.TopCap:
                    material.TopMetallic = metallic;
                    break;
                case MaterialRegionTarget.Bevel:
                    material.BevelMetallic = metallic;
                    break;
                case MaterialRegionTarget.Side:
                    material.SideMetallic = metallic;
                    break;
                default:
                    material.Metallic = metallic;
                    break;
            }
        }

        private static void SetPartRoughness(MaterialNode material, MaterialRegionTarget region, float roughness)
        {
            switch (region)
            {
                case MaterialRegionTarget.TopCap:
                    material.TopRoughness = roughness;
                    break;
                case MaterialRegionTarget.Bevel:
                    material.BevelRoughness = roughness;
                    break;
                case MaterialRegionTarget.Side:
                    material.SideRoughness = roughness;
                    break;
                default:
                    material.Roughness = roughness;
                    break;
            }
        }

        private void ApplyMaterialRegionValuesToSliders(MaterialNode material)
        {
            if (_materialRegionCombo == null ||
                _materialBaseRInput == null ||
                _materialBaseGInput == null ||
                _materialBaseBInput == null ||
                _materialMetallicInput == null ||
                _materialRoughnessInput == null)
            {
                return;
            }

            bool previousUpdatingUi = _updatingUi;
            _updatingUi = true;
            try
            {
                if (_materialRegionCombo.SelectedItem is not MaterialRegionOption &&
                    _materialRegionCombo.SelectedItem is not MaterialRegionTarget)
                {
                    SelectMaterialRegionOption(MaterialRegionTarget.WholeKnob);
                }

                MaterialRegionTarget region = ResolveSelectedMaterialRegion();
                if (!material.PartMaterialsEnabled && region != MaterialRegionTarget.WholeKnob)
                {
                    SelectMaterialRegionOption(MaterialRegionTarget.WholeKnob);
                    region = MaterialRegionTarget.WholeKnob;
                }
                Vector3 color = GetPartBaseColor(material, region);
                _materialBaseRInput.Value = color.X;
                _materialBaseGInput.Value = color.Y;
                _materialBaseBInput.Value = color.Z;
                _materialMetallicInput.Value = GetPartMetallic(material, region);
                _materialRoughnessInput.Value = GetPartRoughness(material, region);
                ApplyMaterialTextureValuesToUi(material);
            }
            finally
            {
                _updatingUi = previousUpdatingUi;
            }

            UpdateReadouts();
        }
    }
}
