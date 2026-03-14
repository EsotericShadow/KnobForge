using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Media;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void RefreshMaterialInspectorUi(ModelNode? model, CollarNode? collar, MaterialNode[] materials)
        {
            int selectedIndex = ClampSelectedMaterialIndex(materials);
            bool importedMesh = collar != null && CollarNode.IsImportedMeshPreset(collar.Preset);
            bool showMultiMaterialList = importedMesh && materials.Length > 1;
            bool showMaterialRegion = model != null && !importedMesh;

            SyncMaterialInspectorItems(materials);
            RefreshMaterialRegionUi(showMaterialRegion);

            if (_materialListPanel != null)
            {
                _materialListPanel.IsVisible = showMultiMaterialList;
            }

            if (_materialRegionPanel != null)
            {
                _materialRegionPanel.IsVisible = showMaterialRegion;
            }

            RefreshAssemblyMaterialPresetUi();

            if (_materialListBox != null)
            {
                _materialListBox.ItemsSource = _materialItems;
                _materialListBox.SelectedIndex = showMultiMaterialList ? selectedIndex : -1;
            }

            if (_materialNameTextBox != null)
            {
                _materialNameTextBox.IsVisible = showMultiMaterialList;
                _materialNameTextBox.IsEnabled = showMultiMaterialList && selectedIndex >= 0 && selectedIndex < materials.Length;
                _materialNameTextBox.Text = selectedIndex >= 0 && selectedIndex < materials.Length
                    ? materials[selectedIndex].Name
                    : string.Empty;
            }

            if (!showMaterialRegion && _materialRegionCombo != null)
            {
                SelectMaterialRegionOption(MaterialRegionTarget.WholeKnob);
            }
        }

        private void RefreshMaterialRegionUi(bool showMaterialRegion)
        {
            if (_materialRegionCombo == null)
            {
                return;
            }

            MaterialRegionTarget selectedRegion = ResolveSelectedMaterialRegion();
            bool previousUpdatingUi = _updatingUi;
            _updatingUi = true;
            try
            {
                RebuildMaterialRegionOptions(_project.ProjectType);
                _materialRegionCombo.ItemsSource = _materialRegionOptions;
                SelectMaterialRegionOption(showMaterialRegion ? selectedRegion : MaterialRegionTarget.WholeKnob);
            }
            finally
            {
                _updatingUi = previousUpdatingUi;
            }
        }

        private void RebuildMaterialRegionOptions(InteractorProjectType projectType)
        {
            _materialRegionOptions.Clear();
            AddMaterialRegionOption(MaterialRegionTarget.WholeKnob, projectType switch
            {
                InteractorProjectType.ThumbSlider => "Whole Slider",
                InteractorProjectType.FlipSwitch => "Whole Switch",
                InteractorProjectType.PushButton => "Whole Button",
                InteractorProjectType.IndicatorLight => "Whole Indicator",
                _ => "Whole Knob"
            });

            switch (projectType)
            {
                case InteractorProjectType.ThumbSlider:
                    AddMaterialRegionOption(MaterialRegionTarget.TopCap, "Thumb");
                    AddMaterialRegionOption(MaterialRegionTarget.Side, "Backplate");
                    break;
                case InteractorProjectType.FlipSwitch:
                    AddMaterialRegionOption(MaterialRegionTarget.TopCap, "Lever");
                    AddMaterialRegionOption(MaterialRegionTarget.Side, "Base");
                    break;
                case InteractorProjectType.PushButton:
                    AddMaterialRegionOption(MaterialRegionTarget.TopCap, "Cap");
                    AddMaterialRegionOption(MaterialRegionTarget.Side, "Body");
                    break;
                case InteractorProjectType.IndicatorLight:
                    AddMaterialRegionOption(MaterialRegionTarget.TopCap, "Housing");
                    AddMaterialRegionOption(MaterialRegionTarget.Side, "Base");
                    break;
                default:
                    AddMaterialRegionOption(MaterialRegionTarget.TopCap, "Top Cap");
                    AddMaterialRegionOption(MaterialRegionTarget.Bevel, "Bevel");
                    AddMaterialRegionOption(MaterialRegionTarget.Side, "Side");
                    break;
            }
        }

        private void AddMaterialRegionOption(MaterialRegionTarget target, string name)
        {
            _materialRegionOptions.Add(new MaterialRegionOption
            {
                Target = target,
                Name = name
            });
        }

        private void SelectMaterialRegionOption(MaterialRegionTarget region)
        {
            if (_materialRegionCombo == null)
            {
                return;
            }

            MaterialRegionOption? selectedOption = _materialRegionOptions
                .FirstOrDefault(option => option.Target == region)
                ?? _materialRegionOptions.FirstOrDefault(option => option.Target == MaterialRegionTarget.WholeKnob);

            _materialRegionCombo.SelectedItem = selectedOption;
        }

        private void RefreshAssemblyMaterialPresetUi()
        {
            InteractorProjectType projectType = _project.ProjectType;
            bool showPresetPanel = SupportsAssemblyMaterialPreset(projectType);
            if (_assemblyMaterialPresetPanel != null)
            {
                _assemblyMaterialPresetPanel.IsVisible = showPresetPanel;
            }

            if (_assemblyMaterialPresetHintText != null)
            {
                _assemblyMaterialPresetHintText.Text = showPresetPanel
                    ? "Preset overrides manual material values while active."
                    : string.Empty;
            }

            if (_assemblyMaterialPresetCombo != null)
            {
                if (!showPresetPanel)
                {
                    bool previousUpdatingUi = _updatingUi;
                    _updatingUi = true;
                    try
                    {
                        _assemblyMaterialPresetOptions.Clear();
                        _assemblyMaterialPresetCombo.ItemsSource = _assemblyMaterialPresetOptions;
                        _assemblyMaterialPresetCombo.SelectedItem = null;
                    }
                    finally
                    {
                        _updatingUi = previousUpdatingUi;
                    }
                }
                else
                {
                    RebuildAssemblyMaterialPresetOptions(projectType);
                    int selectedValue = GetSelectedAssemblyMaterialPresetValue(projectType);
                    AssemblyMaterialPresetOption? selectedOption = _assemblyMaterialPresetOptions
                        .FirstOrDefault(option => option.Value == selectedValue);

                    bool previousUpdatingUi = _updatingUi;
                    _updatingUi = true;
                    try
                    {
                        _assemblyMaterialPresetCombo.ItemsSource = _assemblyMaterialPresetOptions;
                        _assemblyMaterialPresetCombo.SelectedItem = selectedOption;
                    }
                    finally
                    {
                        _updatingUi = previousUpdatingUi;
                    }
                }
            }

            bool presetActive = showPresetPanel && GetSelectedAssemblyMaterialPresetValue(projectType) != -1;
            if (_materialRegionPanel != null)
            {
                _materialRegionPanel.IsEnabled = !presetActive;
                _materialRegionPanel.Opacity = presetActive ? 0.55 : 1.0;
            }
            if (_materialManualControlsPanel != null)
            {
                _materialManualControlsPanel.IsEnabled = !presetActive;
                _materialManualControlsPanel.Opacity = presetActive ? 0.55 : 1.0;
            }
        }

        private void RebuildAssemblyMaterialPresetOptions(InteractorProjectType projectType)
        {
            _assemblyMaterialPresetOptions.Clear();
            _assemblyMaterialPresetOptions.Add(new AssemblyMaterialPresetOption
            {
                Value = -1,
                Name = "Custom"
            });

            switch (projectType)
            {
                case InteractorProjectType.ThumbSlider:
                    foreach (SliderMaterialPresetId id in SliderMaterialPresets.GetPresetIds())
                    {
                        _assemblyMaterialPresetOptions.Add(new AssemblyMaterialPresetOption
                        {
                            Value = (int)id,
                            Name = SliderMaterialPresets.GetDisplayName(id)
                        });
                    }
                    break;
                case InteractorProjectType.FlipSwitch:
                    foreach (ToggleMaterialPresetId id in ToggleMaterialPresets.GetPresetIds())
                    {
                        _assemblyMaterialPresetOptions.Add(new AssemblyMaterialPresetOption
                        {
                            Value = (int)id,
                            Name = ToggleMaterialPresets.GetDisplayName(id)
                        });
                    }
                    break;
                case InteractorProjectType.PushButton:
                    foreach (PushButtonMaterialPresetId id in PushButtonMaterialPresets.GetPresetIds())
                    {
                        _assemblyMaterialPresetOptions.Add(new AssemblyMaterialPresetOption
                        {
                            Value = (int)id,
                            Name = PushButtonMaterialPresets.GetDisplayName(id)
                        });
                    }
                    break;
            }
        }

        private int GetSelectedAssemblyMaterialPresetValue(InteractorProjectType projectType)
        {
            return projectType switch
            {
                InteractorProjectType.ThumbSlider => (int)_project.SliderMaterialPreset,
                InteractorProjectType.FlipSwitch => (int)_project.ToggleMaterialPreset,
                InteractorProjectType.PushButton => (int)_project.PushButtonMaterialPreset,
                _ => -1
            };
        }

        private bool SupportsAssemblyMaterialPreset(InteractorProjectType projectType)
        {
            return projectType == InteractorProjectType.ThumbSlider ||
                   projectType == InteractorProjectType.FlipSwitch ||
                   projectType == InteractorProjectType.PushButton;
        }

        private void OnMaterialListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingUi || _materialListBox == null)
            {
                return;
            }

            SelectMaterialIndex(_materialListBox.SelectedIndex, syncSceneSelection: true);
        }

        private void OnMaterialNameTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || e.Property != TextBox.TextProperty || _materialNameTextBox == null)
            {
                return;
            }

            if (!TryGetSelectedMaterialNode(out MaterialNode material))
            {
                return;
            }

            string updatedName = string.IsNullOrWhiteSpace(_materialNameTextBox.Text)
                ? $"Material {_selectedMaterialIndex + 1}"
                : _materialNameTextBox.Text.Trim();
            if (string.Equals(material.Name, updatedName, StringComparison.Ordinal))
            {
                return;
            }

            material.Name = updatedName;
            SyncMaterialInspectorItems(GetAvailableMaterialNodes());
            RefreshSceneTree();
            CaptureUndoSnapshotIfChanged();
        }

        private void SelectMaterialIndex(int index, bool syncSceneSelection)
        {
            MaterialNode[] materials = GetAvailableMaterialNodes();
            if (materials.Length == 0)
            {
                _selectedMaterialIndex = 0;
                return;
            }

            int clampedIndex = Math.Clamp(index, 0, materials.Length - 1);
            if (clampedIndex == _selectedMaterialIndex &&
                (!syncSceneSelection || ReferenceEquals(_project.SelectedNode, materials[clampedIndex])))
            {
                return;
            }

            _selectedMaterialIndex = clampedIndex;
            if (syncSceneSelection)
            {
                _project.SetSelectedNode(materials[clampedIndex]);
                RefreshSceneTree();
            }

            RefreshInspectorFromProject(InspectorRefreshTabPolicy.PreserveCurrentTab);
        }

        private MaterialNode[] GetAvailableMaterialNodes()
        {
            return GetModelNode()?.GetMaterialNodes() ?? Array.Empty<MaterialNode>();
        }

        private int ClampSelectedMaterialIndex(MaterialNode[] materials)
        {
            if (materials.Length == 0)
            {
                _selectedMaterialIndex = 0;
                return -1;
            }

            if (_project.SelectedNode is MaterialNode selectedNode)
            {
                int selectedNodeIndex = Array.FindIndex(materials, material => ReferenceEquals(material, selectedNode));
                if (selectedNodeIndex >= 0)
                {
                    _selectedMaterialIndex = selectedNodeIndex;
                    return selectedNodeIndex;
                }
            }

            _selectedMaterialIndex = Math.Clamp(_selectedMaterialIndex, 0, materials.Length - 1);
            return _selectedMaterialIndex;
        }

        private void SyncMaterialInspectorItems(MaterialNode[] materials)
        {
            int sharedCount = Math.Min(_materialItems.Count, materials.Length);
            for (int i = 0; i < sharedCount; i++)
            {
                MaterialInspectorListItem updated = CreateMaterialInspectorItem(materials[i], i);
                if (_materialItems[i].MaterialId != updated.MaterialId ||
                    _materialItems[i].Name != updated.Name ||
                    !Equals(_materialItems[i].SwatchBrush, updated.SwatchBrush))
                {
                    _materialItems[i] = updated;
                }
            }

            if (_materialItems.Count > materials.Length)
            {
                for (int i = _materialItems.Count - 1; i >= materials.Length; i--)
                {
                    _materialItems.RemoveAt(i);
                }
            }
            else if (_materialItems.Count < materials.Length)
            {
                for (int i = _materialItems.Count; i < materials.Length; i++)
                {
                    _materialItems.Add(CreateMaterialInspectorItem(materials[i], i));
                }
            }
        }

        private static MaterialInspectorListItem CreateMaterialInspectorItem(MaterialNode material, int index)
        {
            byte r = (byte)Math.Clamp((int)Math.Round(material.BaseColor.X * 255f), 0, 255);
            byte g = (byte)Math.Clamp((int)Math.Round(material.BaseColor.Y * 255f), 0, 255);
            byte b = (byte)Math.Clamp((int)Math.Round(material.BaseColor.Z * 255f), 0, 255);
            string name = string.IsNullOrWhiteSpace(material.Name)
                ? $"Material {index + 1}"
                : material.Name;
            return new MaterialInspectorListItem
            {
                MaterialId = material.Id,
                Name = name,
                SwatchBrush = new SolidColorBrush(Color.FromRgb(r, g, b))
            };
        }
    }
}
