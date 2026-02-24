using KnobForge.Core;
using System;

namespace KnobForge.App.ProjectFiles
{
    public readonly record struct ProjectTypeSnapshotHint(
        bool HasProjectType,
        InteractorProjectType ProjectType,
        SliderAssemblyMode SliderMode,
        ToggleAssemblyMode ToggleMode,
        string SliderBackplateImportedMeshPath,
        string SliderThumbImportedMeshPath,
        float SliderBackplateWidth,
        float SliderBackplateHeight,
        float SliderBackplateThickness,
        float SliderThumbWidth,
        float SliderThumbHeight,
        float SliderThumbDepth,
        string ToggleBaseImportedMeshPath,
        string ToggleLeverImportedMeshPath,
        float TogglePlateWidth,
        float TogglePlateHeight,
        float TogglePlateThickness,
        float ToggleBushingRadius,
        float ToggleBushingHeight,
        float ToggleLeverLength,
        float ToggleLeverRadius,
        float ToggleLeverTopRadius,
        float ToggleTipRadius,
        ToggleAssemblyStateCount ToggleStateCount,
        float ToggleMaxAngleDeg);

    public static class InteractorProjectTypeResolver
    {
        public static InteractorProjectType ResolveFromSnapshotHint(in ProjectTypeSnapshotHint hint)
        {
            if (hint.HasProjectType)
            {
                return hint.ProjectType;
            }

            bool sliderEnabled = hint.SliderMode == SliderAssemblyMode.Enabled ||
                (hint.SliderMode == SliderAssemblyMode.Auto && HasLegacySliderAssemblyConfiguration(hint));
            bool toggleEnabled = hint.ToggleMode == ToggleAssemblyMode.Enabled ||
                (hint.ToggleMode == ToggleAssemblyMode.Auto && HasLegacyToggleAssemblyConfiguration(hint));

            if (sliderEnabled && !toggleEnabled)
            {
                return InteractorProjectType.ThumbSlider;
            }

            if (toggleEnabled && !sliderEnabled)
            {
                return InteractorProjectType.FlipSwitch;
            }

            return InteractorProjectType.RotaryKnob;
        }

        private static bool HasLegacySliderAssemblyConfiguration(in ProjectTypeSnapshotHint hint)
        {
            if (!string.IsNullOrWhiteSpace(hint.SliderBackplateImportedMeshPath) ||
                !string.IsNullOrWhiteSpace(hint.SliderThumbImportedMeshPath))
            {
                return true;
            }

            return hint.SliderBackplateWidth > 0f ||
                hint.SliderBackplateHeight > 0f ||
                hint.SliderBackplateThickness > 0f ||
                hint.SliderThumbWidth > 0f ||
                hint.SliderThumbHeight > 0f ||
                hint.SliderThumbDepth > 0f;
        }

        private static bool HasLegacyToggleAssemblyConfiguration(in ProjectTypeSnapshotHint hint)
        {
            if (!string.IsNullOrWhiteSpace(hint.ToggleBaseImportedMeshPath) ||
                !string.IsNullOrWhiteSpace(hint.ToggleLeverImportedMeshPath))
            {
                return true;
            }

            return hint.TogglePlateWidth > 0f ||
                hint.TogglePlateHeight > 0f ||
                hint.TogglePlateThickness > 0f ||
                hint.ToggleBushingRadius > 0f ||
                hint.ToggleBushingHeight > 0f ||
                hint.ToggleLeverLength > 0f ||
                hint.ToggleLeverRadius > 0f ||
                hint.ToggleLeverTopRadius > 0f ||
                hint.ToggleTipRadius > 0f ||
                hint.ToggleStateCount == ToggleAssemblyStateCount.ThreePosition ||
                Math.Abs(hint.ToggleMaxAngleDeg - 24f) > 0.001f;
        }
    }
}
