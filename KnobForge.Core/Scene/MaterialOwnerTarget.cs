using System;

namespace KnobForge.Core.Scene
{
    public enum MaterialOwnerTarget
    {
        KnobSurface = 0,
        CollarImported = 1,
        SliderBackplateImported = 2,
        SliderThumbImported = 3,
        ToggleBaseImported = 4,
        ToggleLeverImported = 5,
        PushButtonBaseImported = 6,
        PushButtonCapImported = 7
    }

    public static class MaterialOwnerTargetExtensions
    {
        public static bool IsPrimaryModelMaterial(this MaterialOwnerTarget target)
        {
            return target == MaterialOwnerTarget.KnobSurface;
        }

        public static bool IsImportedPartMaterial(this MaterialOwnerTarget target)
        {
            return target != MaterialOwnerTarget.KnobSurface;
        }

        public static string GetDefaultMaterialName(this MaterialOwnerTarget target)
        {
            return target switch
            {
                MaterialOwnerTarget.KnobSurface => "DefaultMaterial",
                MaterialOwnerTarget.CollarImported => "Collar Material",
                MaterialOwnerTarget.SliderBackplateImported => "Slider Backplate Material",
                MaterialOwnerTarget.SliderThumbImported => "Slider Thumb Material",
                MaterialOwnerTarget.ToggleBaseImported => "Toggle Base Material",
                MaterialOwnerTarget.ToggleLeverImported => "Toggle Lever Material",
                MaterialOwnerTarget.PushButtonBaseImported => "PushButton Base Material",
                MaterialOwnerTarget.PushButtonCapImported => "PushButton Cap Material",
                _ => "Material"
            };
        }
    }
}
