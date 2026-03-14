using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private readonly List<InspectorSearchItem> _inspectorSearchItems = new();

        private void InitializeInspectorUx()
        {
            if (_inspectorSearchTextBox == null)
            {
                return;
            }

            BuildInspectorSearchIndex();
            _inspectorSearchTextBox.KeyDown += OnInspectorSearchTextBoxKeyDown;
            KeyDown += OnInspectorWindowKeyDown;
        }

        private void BuildInspectorSearchIndex()
        {
            _inspectorSearchItems.Clear();

            RegisterSearchItem(_referenceStyleCombo, "Reference Profile Style", "profile", "reference", "preset");
            RegisterSearchItem(_referenceStyleSaveNameTextBox, "Reference Profile Save Name", "profile", "save", "name");
            RegisterSearchItem(_modelRadiusInput, "Model Radius", "radius", "size", "shape");
            RegisterSearchItem(_modelHeightInput, "Model Height", "height", "size", "shape");
            RegisterSearchItem(_modelTopScaleInput, "Top Scale", "top", "scale");
            RegisterSearchItem(_modelBevelInput, "Bevel", "bevel", "edge");
            RegisterSearchItem(_bevelCurveInput, "Bevel Curve", "bevel", "curve");
            RegisterSearchItem(_crownProfileInput, "Crown Profile", "crown", "profile");
            RegisterSearchItem(_modelSegmentsInput, "Segments", "segments", "radial");
            RegisterSearchItem(_gripDensityInput, "Grip Density", "grip", "knurl", "density");
            RegisterSearchItem(_gripDepthInput, "Grip Depth", "grip", "knurl", "depth");

            RegisterSearchItem(_materialRoughnessInput, "Material Roughness", "rough", "roughness", "surface");
            RegisterSearchItem(_materialMetallicInput, "Material Metallic", "metal", "metallic");
            RegisterSearchItem(_materialRegionCombo, "Material Region", "material", "part", "top", "bevel", "side");
            RegisterSearchItem(_materialRustInput, "Material Rust", "rust");
            RegisterSearchItem(_materialWearInput, "Material Wear", "wear");
            RegisterSearchItem(_materialGunkInput, "Material Gunk", "gunk");

            RegisterSearchItem(_brushPaintChannelCombo, "Paint Channel", "paint", "channel", "scratch");
            RegisterSearchItem(_brushTypeCombo, "Brush Type", "paint", "brush");
            RegisterSearchItem(_brushPaintColorPicker, "Paint Color", "paint", "color", "picker", "rgb");
            RegisterSearchItem(_brushSizeInput, "Brush Size", "brush", "size");
            RegisterSearchItem(_brushOpacityInput, "Brush Opacity", "brush", "opacity");
            RegisterSearchItem(_paintCoatMetallicInput, "Paint Coat Metallic", "paint", "coat", "metallic", "layer");
            RegisterSearchItem(_paintCoatRoughnessInput, "Paint Coat Roughness", "paint", "coat", "roughness", "hardness", "layer");
            RegisterSearchItem(_clearCoatAmountInput, "Clear Coat Amount", "clearcoat", "coat", "varnish", "amount");
            RegisterSearchItem(_clearCoatRoughnessInput, "Clear Coat Roughness", "clearcoat", "coat", "roughness", "varnish");
            RegisterSearchItem(_anisotropyAngleInput, "Anisotropy Angle", "anisotropy", "brushed", "direction", "angle");
            RegisterSearchItem(_scratchAbrasionTypeCombo, "Scratch Abrasion Type", "scratch", "abrasion", "tool");
            RegisterSearchItem(_scratchWidthInput, "Scratch Width", "scratch", "width");
            RegisterSearchItem(_scratchDepthInput, "Scratch Depth", "scratch", "depth", "carve");
            RegisterSearchItem(_scratchResistanceInput, "Scratch Drag Resistance", "scratch", "resistance", "drag");
            RegisterSearchItem(_scratchDepthRampInput, "Scratch Depth Ramp", "scratch", "ramp", "option", "alt");
            RegisterSearchItem(_scratchExposeColorRInput, "Scratch Exposed Color R", "scratch", "exposed", "silver", "color");
            RegisterSearchItem(_scratchExposeColorGInput, "Scratch Exposed Color G", "scratch", "exposed", "silver", "color");
            RegisterSearchItem(_scratchExposeColorBInput, "Scratch Exposed Color B", "scratch", "exposed", "silver", "color");
            RegisterSearchItem(_scratchExposeMetallicInput, "Scratch Exposed Metallic", "scratch", "exposed", "metallic", "metal");
            RegisterSearchItem(_scratchExposeRoughnessInput, "Scratch Exposed Roughness", "scratch", "exposed", "roughness");

            RegisterSearchItem(_collarPresetCombo, "Collar Preset", "collar", "snake", "ouroboros");
            RegisterSearchItem(_collarMeshPathTextBox, "Collar Mesh Path", "collar", "mesh", "import", "glb", "stl");
            RegisterSearchItem(_collarScaleInput, "Collar Imported Scale", "collar", "scale", "import");
            RegisterSearchItem(_collarOffsetXInput, "Collar Imported Offset X", "collar", "offset", "x");
            RegisterSearchItem(_collarOffsetYInput, "Collar Imported Offset Y", "collar", "offset", "y");
            RegisterSearchItem(_collarInflateInput, "Collar Imported Inflate", "collar", "inflate");

            RegisterSearchItem(_envIntensityInput, "Environment Intensity", "env", "environment", "intensity");
            RegisterSearchItem(_envRoughnessMixInput, "Environment Roughness Mix", "env", "environment", "roughness");
            RegisterSearchItem(_shadowEnabledCheckBox, "Shadows Enabled", "shadow", "enable");
            RegisterSearchItem(_shadowSoftnessInput, "Shadow Softness", "shadow", "softness");
            RegisterSearchItem(_shadowQualityInput, "Shadow Quality", "shadow", "quality");
            RegisterSearchItem(_shadowStrengthInput, "Shadow Strength", "shadow", "strength");

            RegisterSearchItem(_intensityInput, "Selected Light Intensity", "light", "intensity");
            RegisterSearchItem(_falloffInput, "Selected Light Falloff", "light", "falloff");
            RegisterSearchItem(_directionInput, "Selected Light Direction", "light", "direction");
        }

        private void RegisterSearchItem(Control? control, string displayName, params string[] aliases)
        {
            if (control == null)
            {
                return;
            }

            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in displayName.Split(new[] { ' ', '-', '/', '(', ')', ':', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                terms.Add(token);
            }

            foreach (string alias in aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    terms.Add(alias.Trim());
                }
            }

            _inspectorSearchItems.Add(new InspectorSearchItem(displayName, control, terms.ToArray()));
        }

        private void OnInspectorWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.K && e.Key != Key.F)
            {
                return;
            }

            bool commandDown = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (!commandDown || _inspectorSearchTextBox == null)
            {
                return;
            }

            e.Handled = true;
            _inspectorSearchTextBox.Focus();
            _inspectorSearchTextBox.SelectAll();
        }

        private void OnInspectorSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (_inspectorSearchTextBox == null || e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            string query = NormalizeSearchText(_inspectorSearchTextBox.Text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            InspectorSearchItem? best = FindBestSearchItem(query);
            if (best == null)
            {
                return;
            }

            JumpToSearchItem(best);
            _inspectorSearchTextBox.SelectAll();
        }

        private InspectorSearchItem? FindBestSearchItem(string query)
        {
            return _inspectorSearchItems
                .Select(item => (Item: item, Score: ScoreSearchItem(item, query)))
                .Where(entry => entry.Score > 0 && entry.Item.TargetControl.IsVisible)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Item)
                .FirstOrDefault();
        }

        private static string NormalizeSearchText(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static int ScoreSearchItem(InspectorSearchItem item, string query)
        {
            int score = 0;
            if (item.DisplayNameLower.StartsWith(query, StringComparison.Ordinal))
            {
                score += 120;
            }
            else if (item.DisplayNameLower.Contains(query, StringComparison.Ordinal))
            {
                score += 70;
            }

            foreach (string keyword in item.KeywordsLower)
            {
                if (keyword.StartsWith(query, StringComparison.Ordinal))
                {
                    score += 45;
                }
                else if (keyword.Contains(query, StringComparison.Ordinal))
                {
                    score += 20;
                }
            }

            return score;
        }

        private void JumpToSearchItem(InspectorSearchItem item)
        {
            JumpToControl(item.TargetControl);
        }

        private void JumpToControl(Control targetControl)
        {
            SelectInspectorTabForControl(targetControl);
            ExpandAncestorExpanders(targetControl);
            Dispatcher.UIThread.Post(() =>
            {
                targetControl.BringIntoView();
                targetControl.Focus();
                if (targetControl is TextBox textBox)
                {
                    textBox.SelectAll();
                }
            }, DispatcherPriority.Background);
        }

        private static void ExpandAncestorExpanders(Control control)
        {
            Visual? visual = control;
            while (visual != null)
            {
                if (visual is Expander expander)
                {
                    expander.IsExpanded = true;
                }

                visual = visual.GetVisualParent();
            }
        }

        private sealed class InspectorSearchItem
        {
            public InspectorSearchItem(string displayName, Control targetControl, IReadOnlyList<string> keywords)
            {
                DisplayName = displayName;
                TargetControl = targetControl;
                DisplayNameLower = displayName.ToLowerInvariant();
                KeywordsLower = keywords.Select(keyword => keyword.ToLowerInvariant()).Distinct().ToArray();
            }

            public string DisplayName { get; }
            public Control TargetControl { get; }
            public string DisplayNameLower { get; }
            public IReadOnlyList<string> KeywordsLower { get; }
        }
    }
}
