# KnobForge Inspector UI/UX Overhaul Plan

## Executive Summary

The Phase 6 migration from SpriteKnobSlider to ValueInput was a mechanical replacement — it swapped the control type but preserved the legacy layout, naming, and grouping structure unchanged. This plan addresses the deeper UX debt: vertical space waste from stacked label/control pairs, inconsistent naming, illogical groupings, hardcoded widths, and missing affordances that professional material tools provide.

The overhaul is scoped to the inspector panel (right column, 360px) across all 7 tabs. It does not touch the scene list, viewport, toolbar, or brush transport dock.

---

## 1. Layout: Inline Label+Control Grid

### Problem

Every parameter currently takes two vertical lines:

```xml
<TextBlock Text="Roughness"/>
<controls:ValueInput x:Name="MaterialRoughnessSlider" .../>
```

In a 360px-wide panel with 10px padding on each side (340px usable), this wastes roughly 50% of vertical space. Users must scroll extensively in the Model and Brush tabs, which each contain 40+ parameters.

### Solution: Two-Column Grid Rows

Replace all stacked label/control pairs with a `Grid` using `ColumnDefinitions="120,*"` per logical group. Each parameter occupies a single row:

```xml
<Grid ColumnDefinitions="120,*" RowDefinitions="Auto,Auto,Auto" RowSpacing="4">
    <TextBlock Grid.Row="0" Grid.Column="0" Text="Roughness"
               VerticalAlignment="Center" Foreground="#A9B8C9" FontSize="12"/>
    <controls:ValueInput Grid.Row="0" Grid.Column="1"
                         x:Name="MaterialRoughnessInput" .../>

    <TextBlock Grid.Row="1" Grid.Column="0" Text="Metallic"
               VerticalAlignment="Center" Foreground="#A9B8C9" FontSize="12"/>
    <controls:ValueInput Grid.Row="1" Grid.Column="1"
                         x:Name="MaterialMetallicInput" .../>
</Grid>
```

### Label Column Width Rationale

- 120px accommodates labels up to ~18 characters ("Diffuse Influence", "Emitter Spread") without truncation at 12px font.
- Remaining ~220px for the ValueInput is sufficient (MinWidth is 80px in code).
- For exceptionally long labels (e.g. "Upper Bushing Anisotropy Strength"), shorten the label text (see Section 2) rather than widening the column.

### RGB Color Rows

Current pattern uses horizontal StackPanels with `Width="210"` hardcoded:

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="R" Width="14"/>
    <controls:ValueInput x:Name="MaterialBaseRSlider" Width="210" .../>
</StackPanel>
```

New pattern: A dedicated `ColorRowGroup` layout where R/G/B share a single label row:

```xml
<TextBlock Grid.Row="N" Grid.Column="0" Text="Base Color"
           VerticalAlignment="Top" Margin="0,4,0,0"/>
<StackPanel Grid.Row="N" Grid.Column="1" Spacing="3">
    <Grid ColumnDefinitions="18,*">
        <TextBlock Text="R" Foreground="#CC6666" FontSize="11" VerticalAlignment="Center"/>
        <controls:ValueInput Grid.Column="1" x:Name="MaterialBaseRInput" .../>
    </Grid>
    <Grid ColumnDefinitions="18,*">
        <TextBlock Text="G" Foreground="#66CC66" FontSize="11" VerticalAlignment="Center"/>
        <controls:ValueInput Grid.Column="1" x:Name="MaterialBaseGInput" .../>
    </Grid>
    <Grid ColumnDefinitions="18,*">
        <TextBlock Text="B" Foreground="#6688CC" FontSize="11" VerticalAlignment="Center"/>
        <controls:ValueInput Grid.Column="1" x:Name="MaterialBaseBInput" .../>
    </Grid>
</StackPanel>
```

Changes: Remove hardcoded `Width="210"` — let the ValueInput stretch to fill. Color-code the R/G/B letters (muted red/green/blue) for instant visual parsing. The label "Base Color" sits once on the left rather than being implied above the group.

---

## 2. Naming Conventions

### 2A. Field Names (x:Name) — Remove "Slider" Suffix

All 220 ValueInput `x:Name` attributes currently end in `Slider` — a historical artifact from the SpriteKnobSlider era. This causes confusion in code-behind, makes grep results misleading, and violates the principle that names should describe the control's semantic role.

**Convention**: `{Section}{Property}Input`

| Current | Proposed |
|---------|----------|
| `ModelRadiusSlider` | `ModelRadiusInput` |
| `MaterialMetallicSlider` | `MaterialMetallicInput` |
| `LightXSlider` | `LightXInput` |
| `EnvIntensitySlider` | `EnvIntensityInput` |
| `ShadowSoftnessSlider` | `ShadowSoftnessInput` |
| `BrushSizeSlider` | `BrushSizeInput` |
| `IndicatorWidthSlider` | `IndicatorWidthInput` |
| `CollarScaleSlider` | `CollarScaleInput` |
| `GripDensitySlider` | `GripDensityInput` |
| `ToggleLeverLengthSlider` | `ToggleLeverLengthInput` |

**Migration**: Batch find-replace `Slider"` → `Input"` across:
- `MainWindow.axaml` (x:Name attributes)
- `MainWindow/MainWindow.cs` (field declarations and FindControl calls)
- All 11 `MainWindow.*.cs` partial class files (handler references)

This is a mechanical rename with zero behavior change. A single regex handles it: `(\w+)Slider` → `$1Input`.

### 2B. Label Text — Clarity and Consistency Rules

**Rule 1: Remove embedded units when Suffix property handles it.**

| Current Label | Current Suffix | New Label | Suffix |
|--------------|----------------|-----------|--------|
| `Anisotropy Angle (deg)` | ` deg` | `Anisotropy Angle` | ` deg` |
| `Brush Size (px)` | ` px` | `Brush Size` | ` px` |
| `Crown Profile (Concave ↔ Convex)` | (none) | `Crown Profile` | (none) — add tooltip instead |

**Rule 2: Remove redundant section prefixes when inside a named Expander.**

The Expander header already establishes context. Labels inside it don't need to repeat the section name.

| Expander | Current Label | New Label |
|----------|--------------|-----------|
| Tip Sleeve (Advanced) | Sleeve Base Color R | Color R |
| Tip Sleeve (Advanced) | Sleeve Metallic | Metallic |
| Tip Sleeve (Advanced) | Sleeve Roughness | Roughness |
| Tip Sleeve (Advanced) | Sleeve Pearlescence | Pearlescence |
| Tip Sleeve (Advanced) | Sleeve Rust | Rust |
| Collar | Snake Material | Base Color |
| Collar | Snake Metallic | Metallic |
| Collar | Snake Roughness | Roughness |
| Collar | Snake Pearlescence | Pearlescence |
| Collar | Snake Rust | Rust |
| Collar | Snake Wear | Wear |
| Collar | Snake Gunk | Gunk |
| Collar | Imported Scale | Scale |
| Collar | Imported Rotation | Rotation |
| Collar | Imported Offset X | Offset X |
| Collar | Imported Offset Y | Offset Y |
| Collar | Imported Inflate | Inflate |

**Rule 3: Shorten labels to ≤18 characters where possible.**

| Current | Shortened |
|---------|-----------|
| Upper Bushing Anisotropy Strength | Aniso Strength |
| Upper Bushing Anisotropy Density | Aniso Density |
| Upper Bushing Anisotropy Angle (deg) | Aniso Angle |
| Upper Bushing Knurl Amount | Knurl Amount |
| Upper Bushing Knurl Density | Knurl Density |
| Upper Bushing Knurl Depth | Knurl Depth |
| Upper Bushing Surface Character | Surface Character |
| Lower Bushing Radius Scale | Radius Scale |
| Lower Bushing Height Ratio | Height Ratio |
| Upper Bushing Radius Scale | Radius Scale |
| Upper Bushing Height Ratio | Height Ratio |
| Reflector Base Radius | Base Radius |
| Reflector Top Radius | Top Radius |
| Lens Latitude Segments | Lat Segments |
| Lens Longitude Segments | Long Segments |
| Lens Surface Roughness | Surface Roughness |
| Lens Surface Specular | Surface Specular |
| Animation Phase Offset | Phase Offset |
| Scratch Drag Resistance | Drag Resistance |
| Option Drag Depth Ramp | Depth Ramp |
| Scratch Exposed Color | Exposed Color |
| Scratch Exposed Metallic | Exposed Metallic |
| Scratch Exposed Roughness | Exposed Roughness |
| Paint Coat Metallic | Coat Metallic |
| Paint Coat Roughness | Coat Roughness |
| Clear Coat Amount | Clear Coat |
| Clear Coat Roughness | CC Roughness |

**Rule 4: Use "(0 = Auto)" as a tooltip, not in the label.**

Current: `<TextBlock Text="Backplate Width (0 = Auto)"/>`

New: `<TextBlock Text="Backplate Width" ToolTip.Tip="Set to 0 for automatic sizing"/>`

This applies to all 18 controls that currently embed "(0 = Auto)" in their label text.

**Rule 5: Use sentence-style label text (capitalize first word only).**

Labels should read as parameter names, not titles. "Base color" not "Base Color" — except for proper nouns and acronyms (HDRI, IOR, RGB, SSR, LOD, CAD, PBR, UV).

---

## 3. Expander Header Consistency

### Current State

Headers use at least 4 different styles:
- Noun phrase: "Body Shape", "Grip", "Collar", "Material"
- Qualified: "Geometry (Advanced)", "Lens Material (Advanced)", "Dynamic Lights (Advanced)"
- Context prefix: "Selected Light: Transform & Intensity", "Selected Light: Color"
- Description: "Slider Assembly (Preview)", "Toggle Switch (Preview)", "Push Button (Preview)"

### Convention

**Format**: `{NounPhrase}` with optional qualifier tag

- Primary sections (user adjusts regularly): No qualifier. Always `IsExpanded="True"`.
- Advanced sections (power user): Suffix ` · Advanced`. Always `IsExpanded="False"`.
- Debug sections: Suffix ` · Debug`. Always `IsExpanded="False"`.
- Preview sections: Suffix ` · Preview`. Always `IsExpanded="False"`.

| Current | Proposed |
|---------|----------|
| Selected Light: Transform & Intensity | Transform |
| Selected Light: Color | Color |
| Selected Light: Artistic | Artistic |
| Body Shape | Shape |
| Slider Assembly (Preview) | Slider Assembly · Preview |
| Toggle Switch (Preview) | Toggle · Preview |
| Push Button (Preview) | Push Button · Preview |
| Base And Plate (Advanced) | Base & Plate · Advanced |
| Lever And Pivot (Advanced) | Lever & Pivot · Advanced |
| Tip Sleeve (Advanced) | Tip Sleeve · Advanced |
| Geometry (Advanced) | Geometry · Advanced |
| Lens Material (Advanced) | Lens Material · Advanced |
| Dynamic Lights (Advanced) | Dynamic Lights · Advanced |
| Per-Emitter Sources (Advanced) | Emitter Sources · Advanced |
| Micro Detail LOD (Debug) | Micro Detail · Debug |
| Quick Setup | Setup |
| Quick Light Controls | Quick Controls |

---

## 4. Grouping Reorganization

### 4A. Lighting Tab

Current structure:
```
Mode
Lights (list + add/delete)
Selected Light: Transform & Intensity (X, Y, Z, Direction, Intensity, Falloff)
Selected Light: Color (R, G, B)
Selected Light: Artistic (Diffuse Boost, Specular Boost, Specular Power)
```

**Problem**: "Transform & Intensity" mixes spatial position (X/Y/Z) with non-spatial parameters (Direction, Intensity, Falloff). Position is a 3D coordinate — it should be grouped together.

**Proposed**:
```
Mode
Lights (list)
  ├─ Transform
  │    Position: X, Y, Z
  │    Direction
  ├─ Intensity
  │    Intensity
  │    Falloff
  ├─ Color · Advanced
  │    R, G, B
  └─ Artistic · Advanced
       Diffuse Boost
       Specular Boost
       Specular Power
```

### 4B. Model Tab — Flatten One Level of Nesting

Current nesting depth is problematic in some areas:

```
Toggle Switch (Preview)
  └─ Base And Plate (Advanced)
       └─ 20+ controls in a flat StackPanel
```

The Base & Plate section has 3 implicit groups that should be visually separated:
1. Plate geometry (Width, Height, Thickness, Offsets)
2. Bushing geometry (Radius, Height, Sides, Shape)
3. Bushing surface (Knurl, Anisotropy, Surface Character)

**Solution**: Use `<Separator/>` elements and small bold sub-headers within the Advanced expander rather than adding another nesting level:

```xml
<TextBlock Text="Plate" FontSize="11" FontWeight="SemiBold" Foreground="#8899AA" Margin="0,4,0,2"/>
<!-- Plate controls -->
<Separator Margin="0,6"/>
<TextBlock Text="Bushing" FontSize="11" FontWeight="SemiBold" Foreground="#8899AA" Margin="0,4,0,2"/>
<!-- Bushing controls -->
<Separator Margin="0,6"/>
<TextBlock Text="Surface" FontSize="11" FontWeight="SemiBold" Foreground="#8899AA" Margin="0,4,0,2"/>
<!-- Surface controls -->
```

### 4C. Brush Tab — Split Into Sub-Sections

The Brush tab currently has a single massive Expander ("Brush Controls") containing: paint layer management, mask resolution, channel selection, color picker, target values, brush type, brush size, opacity, coat properties, anisotropy, scratch tools, and exposed material colors.

**Proposed structure**:
```
Brush Controls
  ├─ Layers (bordered section — keep existing)
  │    Paint Layers list
  │    Add/Delete/Rename
  │    Blend Mode, Opacity
  │    Mask Resolution
  ├─ Channel
  │    Paint Channel (combo)
  │    Color Picker (when Color channel)
  │    Target Value (when scalar channel)
  ├─ Brush
  │    Type, Size, Opacity
  │    Darkness, Spread
  ├─ Coat Properties · Advanced
  │    Coat Metallic, Coat Roughness
  │    Clear Coat, CC Roughness
  │    Anisotropy Angle
  └─ Scratch · Advanced
       Abrasion Type, Width, Depth
       Resistance, Depth Ramp
       Exposed Color (R,G,B)
       Exposed Metallic, Roughness
```

### 4D. Camera Tab — Integrate into Status Bar or Collapse

The Camera tab currently shows only 4 lines of keyboard shortcut text and a Reset button. This does not warrant a full tab.

**Options (in preference order)**:

1. **Merge into a "View" section at the bottom of the Lighting tab** — camera and lighting are conceptually related (they define what you see).
2. **Show camera shortcuts in a persistent tooltip on the viewport** — triggered by a small "?" icon in the viewport corner.
3. **Keep as tab but add FOV, near/far clip, projection mode controls** — if camera becomes configurable.

Recommended: Option 1 for now, with a `<Expander Header="Camera" IsExpanded="False">` at the bottom of the Lighting tab.

### 4E. Environment + Effects — Consider Merging

Environment (sky/ground colors, HDRI) and Glare & Effects (tone mapping, exposure, bloom) are both post-processing / scene-level settings. Having them as separate tabs adds tab clutter.

**Proposed**: Merge into a single "Scene" tab:
```
Scene
  ├─ Tone Mapping
  │    Operator (combo), Exposure
  ├─ Environment
  │    Intensity, Roughness Response
  │    Sky Color (R,G,B)
  │    Ground Color (R,G,B)
  ├─ HDRI · Advanced
  │    Path, Apply/Clear
  │    Blend, Rotation
  ├─ Bloom · Advanced
  │    Strength, Threshold, Knee
  └─ Reflections (placeholder)
```

This reduces tabs from 7 to 6 (Lighting, Model, Brush, Scene, Shadows, [future: Graph]).

---

## 5. ValueInput Sizing Refinement

### Height

Current: 26px globally via App.axaml style. This is appropriate — matches standard compact control height in professional tools (Substance Painter uses ~24px, Blender uses ~22px).

**No change recommended.**

### Width

Current behavior: No explicit width on most ValueInputs (they stretch to fill parent StackPanel). RGB inputs have `Width="210"` hardcoded.

**Recommendations**:

1. **Remove all hardcoded `Width="210"`** from RGB ValueInputs. The new Grid layout will handle sizing naturally (they fill the remaining space after the 120px label column minus the 18px R/G/B letter).

2. **Set MinWidth per data type**:
   - Float 0–1 range: `MinWidth="80"` (already the default in code)
   - Integer counts: `MinWidth="60"` (e.g., Segments, Emitter Count)
   - Pixel dimensions: `MinWidth="90"` (to show 4+ digits + suffix)
   - Angles: `MinWidth="80"`

3. **MaxWidth on standalone ValueInputs**: In the inline Grid layout, controls naturally won't exceed ~220px. No explicit MaxWidth needed.

### Compact Integer Mode

For integer-only parameters (Segments, Emitter Count, Sides, etc.), consider a future `IsInteger="True"` styled property that:
- Hides decimal display (already achieved with `DecimalPlaces="0"`)
- Increases step arrow increment to whole numbers
- Optionally reduces width

This is a nice-to-have, not a blocker.

---

## 6. Visual Polish

### 6A. Section Sub-Headers

Inside expanders that contain mixed groups of controls, add lightweight sub-headers:

```xml
<TextBlock Text="POSITION" FontSize="10" FontWeight="Bold"
           Foreground="#667788" LetterSpacing="0.5"
           Margin="0,8,0,4"/>
```

All-caps, small font, muted color — the standard pattern used by Substance Painter, Figma, and Blender for in-panel grouping.

### 6B. Consistent Separator Usage

Separators should appear between logical groups, not arbitrarily. Current AXAML has Separators in some sections but not others.

**Rule**: One `<Separator Margin="0,6"/>` between each logical group within an Expander. No separator after the last group (before the closing tag).

### 6C. Label Styling

Currently labels use the default TextBlock style (inherits from parent). Standardize:

```xml
<Style Selector="TabControl TextBlock.param-label">
    <Setter Property="Foreground" Value="#A9B8C9"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
</Style>
```

Apply `Classes="param-label"` to all parameter label TextBlocks. This enables future theming without touching individual elements.

### 6D. Modified-Value Indicator (Future)

Professional tools highlight parameters that differ from their default. This is deferred but the infrastructure should be considered: each ValueInput could carry a `DefaultValue` styled property, and the control renders the label in a brighter color (e.g., #E6EEF5 instead of #A9B8C9) when `Value != DefaultValue`.

### 6E. Tooltips on All Labels

Every parameter label should have a `ToolTip.Tip` explaining what it does, including the valid range and default value. This is a content task — the AXAML structure just needs the attribute added.

---

## 7. Implementation Strategy

### Phase A: Naming (Low Risk, Mechanical)

1. Batch rename all `x:Name="*Slider"` to `x:Name="*Input"` in MainWindow.axaml.
2. Batch rename all corresponding `private readonly ValueInput? _*Slider` fields in MainWindow.cs.
3. Batch rename all `FindControl<ValueInput>("*Slider")` calls.
4. Batch rename all handler references (`_lightXSlider` → `_lightXInput`, etc.).
5. Build. Fix any missed references. Verify zero behavior change.

**Estimated scope**: ~660 find-replace operations across ~15 files.

### Phase B: Layout (Medium Risk, Structural)

1. Define the `param-label` style in App.axaml.
2. Convert one section (e.g., Body Shape in Model tab) to the inline Grid layout as a prototype.
3. Verify it renders correctly at 360px width.
4. Roll out to all sections tab by tab.

**Estimated scope**: ~220 label+control pairs to convert.

### Phase C: Label Text Cleanup (Low Risk, Content)

1. Apply all label shortening rules from Section 2.
2. Move "(0 = Auto)" hints to tooltips.
3. Remove redundant section prefixes inside expanders.
4. Verify no labels are truncated at 120px column width.

### Phase D: Grouping Reorganization (Medium Risk, Structural)

1. Merge Camera into Lighting tab.
2. Merge Environment + Effects into Scene tab.
3. Split Brush tab into sub-expanders.
4. Add sub-headers within deep sections (Toggle base/plate, Indicator light).
5. Separate Light Transform from Intensity.

### Phase E: RGB Color Row Improvement (Low Risk)

1. Replace all horizontal R/G/B StackPanels with the new color row pattern.
2. Remove all `Width="210"` hardcodes.
3. Color-code R/G/B letters.

### Phase F: Tooltips and Polish (Low Risk, Content)

1. Add ToolTip.Tip to all 220 parameter labels.
2. Standardize Separator placement.
3. Final visual QA pass.

---

## 8. Codex Prompt Implications

This overhaul should be encoded as a self-contained Codex prompt (like Phases 1-7) with:
- Exact before/after AXAML snippets for each pattern change
- Complete field rename mapping
- Build gate verification (app must compile after each phase)
- No behavior change — purely visual/structural

The prompt should be split into the phases above (A through F) with build verification between each phase.

---

## 9. Priority Matrix

| Phase | Impact | Risk | Effort | Priority |
|-------|--------|------|--------|----------|
| A: Naming | Medium (code quality) | Low | 2-3 hours | 1 |
| B: Layout | High (vertical space savings) | Medium | 4-6 hours | 2 |
| C: Label Text | Medium (clarity) | Low | 1-2 hours | 3 |
| D: Grouping | High (discoverability) | Medium | 3-4 hours | 4 |
| E: RGB Colors | Low-Medium (polish) | Low | 1-2 hours | 5 |
| F: Tooltips | Medium (onboarding) | Low | 2-3 hours | 6 |

**Total estimated effort**: 13-20 hours of Codex time.

---

## 10. Reference Field Rename Table

Complete mapping of current → proposed field names for the Codex prompt. Only the suffix changes (`Slider` → `Input`); the prefix (semantic name) is preserved.

**Lighting Tab** (9 ValueInputs):
`LightXSlider` → `LightXInput`, `LightYSlider` → `LightYInput`, `LightZSlider` → `LightZInput`, `DirectionSlider` → `DirectionInput`, `IntensitySlider` → `IntensityInput`, `FalloffSlider` → `FalloffInput`, `LightRSlider` → `LightRInput`, `LightGSlider` → `LightGInput`, `LightBSlider` → `LightBInput`, `DiffuseBoostSlider` → `DiffuseBoostInput`, `SpecularBoostSlider` → `SpecularBoostInput`, `SpecularPowerSlider` → `SpecularPowerInput`

**Model Tab** (~100+ ValueInputs): Same pattern. Every `*Slider` → `*Input`.

**Brush Tab** (~20 ValueInputs): Same pattern.

**Environment Tab** (~10 ValueInputs): Same pattern.

**Effects Tab** (~5 ValueInputs): Same pattern.

**Shadows Tab** (~8 ValueInputs): Same pattern.

The full mapping is mechanical — any `x:Name` ending in `Slider` on a `ValueInput` control gets renamed to end in `Input`.

---

## Appendix: Before/After Comparison

### Before (Current — Stacked Layout)
```
┌─────────────────────────────┐
│ Roughness                   │  ← label line (wasted width)
│ [═══════════════════0.500]  │  ← control line
│ Metallic                    │
│ [═══════════════════0.000]  │
│ Pearlescence                │
│ [═══════════════════0.000]  │
│                             │
│ 6 lines for 3 parameters    │
└─────────────────────────────┘
```

### After (Proposed — Inline Grid)
```
┌─────────────────────────────┐
│ Roughness    [═══════0.500] │  ← 1 line per param
│ Metallic     [═══════0.000] │
│ Pearlescence [═══════0.000] │
│                             │
│ 3 lines for 3 parameters    │
└─────────────────────────────┘
```

### Before (RGB Color — Hardcoded Width)
```
┌─────────────────────────────┐
│ Base Color                  │
│ R [══════════════0.80]      │  ← Width="210" leaves dead space
│ G [══════════════0.20]      │
│ B [══════════════0.10]      │
│                             │
│ 4 lines, hardcoded width    │
└─────────────────────────────┘
```

### After (RGB Color — Flexible Width)
```
┌─────────────────────────────┐
│ Base color   R [════════0.80]│ ← color-coded, fills space
│              G [════════0.20]│
│              B [════════0.10]│
│                              │
│ 3 lines, responsive width    │
└──────────────────────────────┘
```
