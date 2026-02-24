using Avalonia.Controls;
using KnobForge.Core;
using System.Collections.Generic;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private enum InspectorSectionId
        {
            LightingTab,
            ModelTab,
            BrushTab,
            CameraTab,
            EnvironmentTab,
            ShadowsTab,
            ReferenceProfiles,
            Transform,
            BodyShape,
            SliderAssembly,
            ToggleAssembly,
            PushButtonAssembly,
            SpiralRidge,
            Grip,
            Collar,
            RotaryIndicator,
            IndicatorLightAssembly,
            Material,
            SurfaceTexture,
            MicroDetail
        }

        private void ApplyProjectTypeInspectorVisibility()
        {
            Dictionary<InspectorSectionId, Control?> sectionRegistry = BuildInspectorSectionRegistry();
            HashSet<InspectorSectionId> visibleSections = BuildVisibleInspectorSectionSet(_project.ProjectType);

            foreach (KeyValuePair<InspectorSectionId, Control?> entry in sectionRegistry)
            {
                if (entry.Value != null)
                {
                    entry.Value.IsVisible = visibleSections.Contains(entry.Key);
                }
            }

            EnsureSelectedInspectorTabIsVisible();
        }

        private Dictionary<InspectorSectionId, Control?> BuildInspectorSectionRegistry()
        {
            return new Dictionary<InspectorSectionId, Control?>
            {
                [InspectorSectionId.LightingTab] = _lightingTabItem,
                [InspectorSectionId.ModelTab] = _modelTabItem,
                [InspectorSectionId.BrushTab] = _brushTabItem,
                [InspectorSectionId.CameraTab] = _cameraTabItem,
                [InspectorSectionId.EnvironmentTab] = _environmentTabItem,
                [InspectorSectionId.ShadowsTab] = _shadowsTabItem,
                [InspectorSectionId.ReferenceProfiles] = _nodeReferenceProfilesExpander,
                [InspectorSectionId.Transform] = _nodeTransformExpander,
                [InspectorSectionId.BodyShape] = _nodeBodyShapeExpander,
                [InspectorSectionId.SliderAssembly] = _nodeSliderAssemblyExpander,
                [InspectorSectionId.ToggleAssembly] = _nodeToggleAssemblyExpander,
                [InspectorSectionId.PushButtonAssembly] = _nodePushButtonAssemblyExpander,
                [InspectorSectionId.SpiralRidge] = _nodeSpiralRidgeExpander,
                [InspectorSectionId.Grip] = _nodeGripExpander,
                [InspectorSectionId.Collar] = _nodeCollarExpander,
                [InspectorSectionId.RotaryIndicator] = _nodeRotaryIndicatorExpander,
                [InspectorSectionId.IndicatorLightAssembly] = _nodeIndicatorLightAssemblyExpander,
                [InspectorSectionId.Material] = _nodeMaterialExpander,
                [InspectorSectionId.SurfaceTexture] = _nodeSurfaceTextureExpander,
                [InspectorSectionId.MicroDetail] = _nodeMicroDetailExpander
            };
        }

        private static HashSet<InspectorSectionId> BuildVisibleInspectorSectionSet(InteractorProjectType projectType)
        {
            var sections = new HashSet<InspectorSectionId>
            {
                InspectorSectionId.LightingTab,
                InspectorSectionId.ModelTab,
                InspectorSectionId.BrushTab,
                InspectorSectionId.CameraTab,
                InspectorSectionId.EnvironmentTab,
                InspectorSectionId.ShadowsTab,
                InspectorSectionId.Transform,
                InspectorSectionId.Material,
            };

            switch (projectType)
            {
                case InteractorProjectType.ThumbSlider:
                    sections.Add(InspectorSectionId.SliderAssembly);
                    break;
                case InteractorProjectType.FlipSwitch:
                    sections.Add(InspectorSectionId.ToggleAssembly);
                    break;
                case InteractorProjectType.PushButton:
                    sections.Add(InspectorSectionId.PushButtonAssembly);
                    break;
                case InteractorProjectType.IndicatorLight:
                    sections.Add(InspectorSectionId.IndicatorLightAssembly);
                    break;
                default:
                    sections.Add(InspectorSectionId.ReferenceProfiles);
                    sections.Add(InspectorSectionId.BodyShape);
                    sections.Add(InspectorSectionId.SpiralRidge);
                    sections.Add(InspectorSectionId.Grip);
                    sections.Add(InspectorSectionId.Collar);
                    sections.Add(InspectorSectionId.RotaryIndicator);
                    sections.Add(InspectorSectionId.SurfaceTexture);
                    sections.Add(InspectorSectionId.MicroDetail);
                    break;
            }

            return sections;
        }

        private void EnsureSelectedInspectorTabIsVisible()
        {
            if (_inspectorTabControl == null)
            {
                return;
            }

            if (_inspectorTabControl.SelectedItem is TabItem selectedTab && IsInspectorTabSelectable(selectedTab))
            {
                return;
            }

            TabItem? fallback = GetFirstVisibleInspectorTab();
            if (fallback != null && !ReferenceEquals(_inspectorTabControl.SelectedItem, fallback))
            {
                _inspectorTabControl.SelectedItem = fallback;
            }
        }

        private TabItem? ResolvePreferredVisibleInspectorTab(TabItem? preferred)
        {
            if (IsInspectorTabSelectable(preferred))
            {
                return preferred;
            }

            return GetFirstVisibleInspectorTab();
        }

        private bool IsInspectorTabSelectable(TabItem? tab)
        {
            if (_inspectorTabControl?.Items == null || tab == null || !tab.IsVisible)
            {
                return false;
            }

            foreach (object? item in _inspectorTabControl.Items)
            {
                if (ReferenceEquals(item, tab))
                {
                    return true;
                }
            }

            return false;
        }

        private TabItem? GetFirstVisibleInspectorTab()
        {
            if (_inspectorTabControl?.Items == null)
            {
                return null;
            }

            foreach (object? item in _inspectorTabControl.Items)
            {
                if (item is TabItem tabItem && tabItem.IsVisible)
                {
                    return tabItem;
                }
            }

            return null;
        }
    }
}
