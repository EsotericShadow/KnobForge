using Avalonia.Controls;
using Avalonia.Interactivity;
using KnobForge.Core.Export;
using KnobForge.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class RenderSettingsWindow : Window
    {
        private void WireViewpointEditorHandlers()
        {
            _viewpointsListBox.SelectionChanged += OnViewpointListSelectionChanged;

            _addViewpointButton.Click += OnAddViewpointButtonClick;
            _duplicateViewpointButton.Click += OnDuplicateViewpointButtonClick;
            _resetViewpointsFromOrbitButton.Click += OnResetViewpointsFromOrbitButtonClick;
            _removeViewpointButton.Click += OnRemoveViewpointButtonClick;
            _moveViewpointUpButton.Click += OnMoveViewpointUpButtonClick;
            _moveViewpointDownButton.Click += OnMoveViewpointDownButtonClick;

            _viewpointEnabledCheckBox.IsCheckedChanged += OnViewpointDetailCheckedChanged;
            _viewpointNameTextBox.TextChanged += OnViewpointDetailTextChanged;
            _viewpointFileTagTextBox.TextChanged += OnViewpointDetailTextChanged;
            _viewpointAbsoluteCameraCheckBox.IsCheckedChanged += OnViewpointDetailCheckedChanged;
            _viewpointYawTextBox.TextChanged += OnViewpointDetailTextChanged;
            _viewpointPitchTextBox.TextChanged += OnViewpointDetailTextChanged;
            _viewpointOverrideZoomCheckBox.IsCheckedChanged += OnViewpointDetailCheckedChanged;
            _viewpointZoomTextBox.TextChanged += OnViewpointDetailTextChanged;
            _viewpointOverridePanCheckBox.IsCheckedChanged += OnViewpointDetailCheckedChanged;
            _viewpointPanXTextBox.TextChanged += OnViewpointDetailTextChanged;
            _viewpointPanYTextBox.TextChanged += OnViewpointDetailTextChanged;
        }

        private void ResetViewpointsFromOrbit(bool useCurrentCameraForPrimary)
        {
            bool includeOrbitVariants = _exportOrbitVariantsCheckBox.IsChecked == true;
            var defaults = new KnobExportSettings();
            float yawOffset = defaults.OrbitVariantYawOffsetDeg;
            float pitchOffset = defaults.OrbitVariantPitchOffsetDeg;
            TryParseFloat(_orbitYawOffsetTextBox.Text, MinOrbitOffsetDeg, MaxOrbitYawOffsetDeg, "Orbit yaw offset", out yawOffset, out _);
            TryParseFloat(_orbitPitchOffsetTextBox.Text, MinOrbitOffsetDeg, MaxOrbitPitchOffsetDeg, "Orbit pitch offset", out pitchOffset, out _);

            ExportViewpoint[] legacy = BuildLegacyUiViewpoints(includeOrbitVariants, yawOffset, pitchOffset);
            _viewpointEditorItems.Clear();
            for (int i = 0; i < legacy.Length; i++)
            {
                ViewpointEditorItem item = ViewpointEditorItem.FromExportViewpoint(legacy[i], i);
                if (useCurrentCameraForPrimary && i == 0)
                {
                    ViewportCameraState camera = _gpuViewport?.CurrentCameraState ?? _cameraState;
                    item.UseAbsoluteCamera = true;
                    item.YawDeg = camera.OrbitYawDeg;
                    item.PitchDeg = camera.OrbitPitchDeg;
                    item.OverrideZoom = true;
                    item.Zoom = camera.Zoom;
                    item.OverridePan = true;
                    item.PanXPx = camera.PanPx.X;
                    item.PanYPx = camera.PanPx.Y;
                }

                _viewpointEditorItems.Add(item);
            }

            ReindexViewpoints();
            _viewpointsDirtyFromOrbit = true;
            RefreshViewpointListAndDetails();
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void TrySyncViewpointsFromOrbitBaseline()
        {
            if (!_viewpointsDirtyFromOrbit)
            {
                return;
            }

            bool includeOrbitVariants = _exportOrbitVariantsCheckBox.IsChecked == true;
            if (!TryParseFloat(_orbitYawOffsetTextBox.Text, MinOrbitOffsetDeg, MaxOrbitYawOffsetDeg, "Orbit yaw offset", out float yawOffset, out _))
            {
                return;
            }

            if (!TryParseFloat(_orbitPitchOffsetTextBox.Text, MinOrbitOffsetDeg, MaxOrbitPitchOffsetDeg, "Orbit pitch offset", out float pitchOffset, out _))
            {
                return;
            }

            ExportViewpoint[] legacy = BuildLegacyUiViewpoints(includeOrbitVariants, yawOffset, pitchOffset);
            Guid? selectedId = GetSelectedViewpoint()?.Id;

            _viewpointEditorItems.Clear();
            for (int i = 0; i < legacy.Length; i++)
            {
                _viewpointEditorItems.Add(ViewpointEditorItem.FromExportViewpoint(legacy[i], i));
            }

            ReindexViewpoints();
            RefreshViewpointListAndDetails(selectedId);
        }

        private void OnAddViewpointButtonClick(object? sender, RoutedEventArgs e)
        {
            _viewpointsDirtyFromOrbit = false;
            ViewpointEditorItem? selected = GetSelectedViewpoint();
            ViewpointEditorItem item = selected != null ? CloneViewpoint(selected) : new ViewpointEditorItem();
            item.Name = BuildUniqueViewpointName(selected?.Name ?? "Viewpoint");
            item.FileTag = BuildDefaultViewpointTag(_viewpointEditorItems.Count + 1);
            item.Order = _viewpointEditorItems.Count;
            _viewpointEditorItems.Add(item);

            ReindexViewpoints();
            RefreshViewpointListAndDetails(item.Id, item.FileTag);
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnDuplicateViewpointButtonClick(object? sender, RoutedEventArgs e)
        {
            ViewpointEditorItem? selected = GetSelectedViewpoint();
            if (selected == null)
            {
                return;
            }

            _viewpointsDirtyFromOrbit = false;
            ViewpointEditorItem copy = CloneViewpoint(selected);
            copy.Name = BuildUniqueViewpointName($"{selected.Name} Copy");
            copy.FileTag = BuildDefaultViewpointTag(_viewpointEditorItems.Count + 1);
            copy.Order = _viewpointEditorItems.Count;
            _viewpointEditorItems.Add(copy);

            ReindexViewpoints();
            RefreshViewpointListAndDetails(copy.Id, copy.FileTag);
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnResetViewpointsFromOrbitButtonClick(object? sender, RoutedEventArgs e)
        {
            ResetViewpointsFromOrbit(useCurrentCameraForPrimary: false);
        }

        private void OnRemoveViewpointButtonClick(object? sender, RoutedEventArgs e)
        {
            ViewpointEditorItem? selected = GetSelectedViewpoint();
            if (selected == null)
            {
                return;
            }

            _viewpointsDirtyFromOrbit = false;
            int selectedIndex = _viewpointEditorItems.FindIndex(v => v.Id == selected.Id);
            if (selectedIndex < 0)
            {
                return;
            }

            _viewpointEditorItems.RemoveAt(selectedIndex);
            if (_viewpointEditorItems.Count == 0)
            {
                _viewpointEditorItems.Add(new ViewpointEditorItem
                {
                    Name = "Primary",
                    FileTag = string.Empty,
                    Enabled = true,
                    Order = 0
                });
            }

            ReindexViewpoints();
            Guid preferredId = _viewpointEditorItems[Math.Clamp(selectedIndex, 0, _viewpointEditorItems.Count - 1)].Id;
            RefreshViewpointListAndDetails(preferredId);
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnMoveViewpointUpButtonClick(object? sender, RoutedEventArgs e)
        {
            MoveSelectedViewpoint(-1);
        }

        private void OnMoveViewpointDownButtonClick(object? sender, RoutedEventArgs e)
        {
            MoveSelectedViewpoint(1);
        }

        private void MoveSelectedViewpoint(int direction)
        {
            ViewpointEditorItem? selected = GetSelectedViewpoint();
            if (selected == null)
            {
                return;
            }

            int index = _viewpointEditorItems.FindIndex(v => v.Id == selected.Id);
            if (index < 0)
            {
                return;
            }

            int target = index + direction;
            if (target < 0 || target >= _viewpointEditorItems.Count)
            {
                return;
            }

            _viewpointsDirtyFromOrbit = false;
            (_viewpointEditorItems[index], _viewpointEditorItems[target]) = (_viewpointEditorItems[target], _viewpointEditorItems[index]);
            ReindexViewpoints();
            RefreshViewpointListAndDetails(selected.Id, selected.FileTag);
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnViewpointListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingViewpointUi)
            {
                return;
            }

            PopulateViewpointDetailFields(GetSelectedViewpoint());
        }

        private void OnViewpointDetailTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isUpdatingViewpointUi)
            {
                return;
            }

            _viewpointsDirtyFromOrbit = false;
            TrySyncSelectedViewpointFromDetailEditors(strict: false, out _);
            UpdateViewpointDetailEnablement();
            RebuildPreviewVariantOptions();
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnViewpointDetailCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (_isUpdatingViewpointUi)
            {
                return;
            }

            _viewpointsDirtyFromOrbit = false;
            TrySyncSelectedViewpointFromDetailEditors(strict: false, out _);
            UpdateViewpointDetailEnablement();
            RebuildPreviewVariantOptions();
            RefreshViewpointListAndDetails(GetSelectedViewpoint()?.Id);
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void RefreshViewpointListAndDetails(Guid? preferredSelectionId = null, string? preferredPreviewFileTag = null)
        {
            _isUpdatingViewpointUi = true;
            try
            {
                _viewpointsListBox.ItemsSource = null;
                _viewpointsListBox.ItemsSource = _viewpointEditorItems.ToArray();

                if (_viewpointEditorItems.Count > 0)
                {
                    int selectedIndex = 0;
                    if (preferredSelectionId.HasValue)
                    {
                        int found = _viewpointEditorItems.FindIndex(v => v.Id == preferredSelectionId.Value);
                        if (found >= 0)
                        {
                            selectedIndex = found;
                        }
                    }

                    _viewpointsListBox.SelectedIndex = selectedIndex;
                }
            }
            finally
            {
                _isUpdatingViewpointUi = false;
            }

            PopulateViewpointDetailFields(GetSelectedViewpoint());
            RebuildPreviewVariantOptions(preferredPreviewFileTag);
        }

        private void PopulateViewpointDetailFields(ViewpointEditorItem? item)
        {
            _isUpdatingViewpointUi = true;
            try
            {
                bool hasSelection = item != null;
                if (!hasSelection)
                {
                    _viewpointEnabledCheckBox.IsChecked = false;
                    _viewpointNameTextBox.Text = string.Empty;
                    _viewpointFileTagTextBox.Text = string.Empty;
                    _viewpointAbsoluteCameraCheckBox.IsChecked = false;
                    _viewpointYawTextBox.Text = string.Empty;
                    _viewpointPitchTextBox.Text = string.Empty;
                    _viewpointOverrideZoomCheckBox.IsChecked = false;
                    _viewpointZoomTextBox.Text = string.Empty;
                    _viewpointOverridePanCheckBox.IsChecked = false;
                    _viewpointPanXTextBox.Text = string.Empty;
                    _viewpointPanYTextBox.Text = string.Empty;
                }
                else
                {
                    _viewpointEnabledCheckBox.IsChecked = item!.Enabled;
                    _viewpointNameTextBox.Text = item.Name;
                    _viewpointFileTagTextBox.Text = item.FileTag;
                    _viewpointAbsoluteCameraCheckBox.IsChecked = item.UseAbsoluteCamera;
                    _viewpointYawTextBox.Text = item.YawDeg.ToString("0.###", CultureInfo.InvariantCulture);
                    _viewpointPitchTextBox.Text = item.PitchDeg.ToString("0.###", CultureInfo.InvariantCulture);
                    _viewpointOverrideZoomCheckBox.IsChecked = item.OverrideZoom;
                    _viewpointZoomTextBox.Text = item.Zoom.ToString("0.###", CultureInfo.InvariantCulture);
                    _viewpointOverridePanCheckBox.IsChecked = item.OverridePan;
                    _viewpointPanXTextBox.Text = item.PanXPx.ToString("0.###", CultureInfo.InvariantCulture);
                    _viewpointPanYTextBox.Text = item.PanYPx.ToString("0.###", CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                _isUpdatingViewpointUi = false;
            }

            UpdateViewpointDetailEnablement();
        }

        private void UpdateViewpointDetailEnablement()
        {
            bool hasSelection = GetSelectedViewpoint() != null;

            _viewpointEnabledCheckBox.IsEnabled = hasSelection;
            _viewpointNameTextBox.IsEnabled = hasSelection;
            _viewpointFileTagTextBox.IsEnabled = hasSelection;
            _viewpointAbsoluteCameraCheckBox.IsEnabled = hasSelection;
            _viewpointYawTextBox.IsEnabled = hasSelection;
            _viewpointPitchTextBox.IsEnabled = hasSelection;
            _viewpointOverrideZoomCheckBox.IsEnabled = hasSelection;
            _viewpointZoomTextBox.IsEnabled = hasSelection && _viewpointOverrideZoomCheckBox.IsChecked == true;
            _viewpointOverridePanCheckBox.IsEnabled = hasSelection;
            _viewpointPanXTextBox.IsEnabled = hasSelection && _viewpointOverridePanCheckBox.IsChecked == true;
            _viewpointPanYTextBox.IsEnabled = hasSelection && _viewpointOverridePanCheckBox.IsChecked == true;

            _duplicateViewpointButton.IsEnabled = hasSelection;
            _removeViewpointButton.IsEnabled = hasSelection && _viewpointEditorItems.Count > 1;
            _moveViewpointUpButton.IsEnabled = hasSelection && _viewpointsListBox.SelectedIndex > 0;
            _moveViewpointDownButton.IsEnabled = hasSelection && _viewpointsListBox.SelectedIndex >= 0 && _viewpointsListBox.SelectedIndex < _viewpointEditorItems.Count - 1;
        }

        private bool TrySyncSelectedViewpointFromDetailEditors(bool strict, out string error)
        {
            error = string.Empty;
            ViewpointEditorItem? item = GetSelectedViewpoint();
            if (item == null)
            {
                return true;
            }

            item.Enabled = _viewpointEnabledCheckBox.IsChecked == true;
            item.Name = (_viewpointNameTextBox.Text ?? string.Empty).Trim();
            item.FileTag = (_viewpointFileTagTextBox.Text ?? string.Empty).Trim();
            item.UseAbsoluteCamera = _viewpointAbsoluteCameraCheckBox.IsChecked == true;
            item.OverrideZoom = _viewpointOverrideZoomCheckBox.IsChecked == true;
            item.OverridePan = _viewpointOverridePanCheckBox.IsChecked == true;

            if (strict)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    error = "Viewpoint name is required.";
                    return false;
                }

                string yawLabel = item.UseAbsoluteCamera ? "Absolute yaw" : "Yaw offset";
                if (!TryParseFloat(_viewpointYawTextBox.Text, -180f, 180f, yawLabel, out float yaw, out error))
                {
                    return false;
                }

                string pitchLabel = item.UseAbsoluteCamera ? "Absolute pitch" : "Pitch offset";
                if (!TryParseFloat(_viewpointPitchTextBox.Text, -85f, 85f, pitchLabel, out float pitch, out error))
                {
                    return false;
                }

                item.YawDeg = yaw;
                item.PitchDeg = pitch;

                if (item.OverrideZoom)
                {
                    if (!TryParseFloat(_viewpointZoomTextBox.Text, 0.0001f, float.MaxValue, "Viewpoint zoom", out float zoom, out error))
                    {
                        return false;
                    }

                    item.Zoom = zoom;
                }

                if (item.OverridePan)
                {
                    if (!TryParseUnboundedFloat(_viewpointPanXTextBox.Text, "Viewpoint pan X", out float panX, out error) ||
                        !TryParseUnboundedFloat(_viewpointPanYTextBox.Text, "Viewpoint pan Y", out float panY, out error))
                    {
                        return false;
                    }

                    item.PanXPx = panX;
                    item.PanYPx = panY;
                }
            }
            else
            {
                if (TryParseUnboundedFloat(_viewpointYawTextBox.Text, string.Empty, out float yaw, out _))
                {
                    item.YawDeg = yaw;
                }

                if (TryParseUnboundedFloat(_viewpointPitchTextBox.Text, string.Empty, out float pitch, out _))
                {
                    item.PitchDeg = pitch;
                }

                if (item.OverrideZoom && TryParseUnboundedFloat(_viewpointZoomTextBox.Text, string.Empty, out float zoom, out _))
                {
                    item.Zoom = zoom;
                }

                if (item.OverridePan)
                {
                    if (TryParseUnboundedFloat(_viewpointPanXTextBox.Text, string.Empty, out float panX, out _))
                    {
                        item.PanXPx = panX;
                    }

                    if (TryParseUnboundedFloat(_viewpointPanYTextBox.Text, string.Empty, out float panY, out _))
                    {
                        item.PanYPx = panY;
                    }
                }
            }

            return true;
        }

        private bool TryBuildViewpointsFromEditor(out ExportViewpoint[] viewpoints, out string error)
        {
            viewpoints = Array.Empty<ExportViewpoint>();
            error = string.Empty;

            if (!TrySyncSelectedViewpointFromDetailEditors(strict: true, out error))
            {
                return false;
            }

            if (_viewpointEditorItems.Count == 0)
            {
                ResetViewpointsFromOrbit(useCurrentCameraForPrimary: false);
            }

            ReindexViewpoints();
            var raw = new List<ExportViewpoint>(_viewpointEditorItems.Count);
            for (int i = 0; i < _viewpointEditorItems.Count; i++)
            {
                raw.Add(_viewpointEditorItems[i].ToExportViewpoint());
            }

            var settings = new KnobExportSettings
            {
                ExportViewpoints = raw,
                ExportOrbitVariants = false,
                OrbitVariantYawOffsetDeg = 0f,
                OrbitVariantPitchOffsetDeg = 0f
            };

            viewpoints = ExportViewpointResolver.ResolveViewpoints(settings);
            if (viewpoints.Length == 0)
            {
                error = "At least one export viewpoint is required.";
                return false;
            }

            return true;
        }

        private void RebuildPreviewVariantOptions(string? preferredFileTag = null)
        {
            string? currentTag = preferredFileTag;
            if (string.IsNullOrWhiteSpace(currentTag))
            {
                currentTag = (_rotaryPreviewVariantComboBox.SelectedItem as PreviewVariantOption)?.FileTag;
            }

            var options = new List<PreviewVariantOption>();
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ViewpointEditorItem item in _viewpointEditorItems.OrderBy(v => v.Order))
            {
                if (!item.Enabled)
                {
                    continue;
                }

                string tag = (item.FileTag ?? string.Empty).Trim();
                if (!tags.Add(tag))
                {
                    continue;
                }

                string display = string.IsNullOrWhiteSpace(item.Name)
                    ? (string.IsNullOrWhiteSpace(tag) ? "Primary" : tag)
                    : item.Name;
                options.Add(new PreviewVariantOption(tag, display));
            }

            if (options.Count == 0)
            {
                options.Add(new PreviewVariantOption(string.Empty, "Primary"));
            }

            _previewVariantOptions = options.ToArray();
            _rotaryPreviewVariantComboBox.ItemsSource = _previewVariantOptions;
            int selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(currentTag))
            {
                int index = Array.FindIndex(_previewVariantOptions, o => string.Equals(o.FileTag, currentTag, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    selectedIndex = index;
                }
            }

            _rotaryPreviewVariantComboBox.SelectedIndex = selectedIndex;
        }

        private ViewpointEditorItem? GetSelectedViewpoint()
        {
            return _viewpointsListBox.SelectedItem as ViewpointEditorItem;
        }

        private void ReindexViewpoints()
        {
            for (int i = 0; i < _viewpointEditorItems.Count; i++)
            {
                _viewpointEditorItems[i].Order = i;
            }
        }

        private static string BuildDefaultViewpointTag(int ordinal)
        {
            return $"view_{Math.Max(1, ordinal)}";
        }

        private string BuildUniqueViewpointName(string baseName)
        {
            string seed = string.IsNullOrWhiteSpace(baseName) ? "Viewpoint" : baseName.Trim();
            string candidate = seed;
            int suffix = 2;
            while (_viewpointEditorItems.Any(v => string.Equals(v.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{seed} {suffix}";
                suffix++;
            }

            return candidate;
        }

        private static ViewpointEditorItem CloneViewpoint(ViewpointEditorItem source)
        {
            return new ViewpointEditorItem
            {
                Name = source.Name,
                FileTag = source.FileTag,
                Enabled = source.Enabled,
                Order = source.Order,
                UseAbsoluteCamera = source.UseAbsoluteCamera,
                YawDeg = source.YawDeg,
                PitchDeg = source.PitchDeg,
                OverrideZoom = source.OverrideZoom,
                Zoom = source.Zoom,
                OverridePan = source.OverridePan,
                PanXPx = source.PanXPx,
                PanYPx = source.PanYPx
            };
        }

        private static bool TryParseUnboundedFloat(string? text, string fieldName, out float value, out string error)
        {
            if (!float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
            {
                error = string.IsNullOrWhiteSpace(fieldName)
                    ? "Value must be a number."
                    : $"{fieldName} must be a number.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
