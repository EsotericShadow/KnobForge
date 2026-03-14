# Codex Implementation Prompt — Phase 6: Inspector Control Overhaul

## Your Role

You are implementing Phase 6 of the KnobForge Material Tool Transformation. Your job is to replace all 219 `SpriteKnobSlider` instances (sprite-sheet-animated rotary knob dials) with a new `ValueInput` control — a compact numeric input combining a text editor, step arrows, mouse drag, and scroll wheel. You will also remove the ~220 paired `TextBlock` readouts and ~33 paired precision `TextBox` inputs that are now redundant. Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–5 are complete:
- **Phase 1**: UV infrastructure — vertex UVs flow through the pipeline.
- **Phase 2**: Texture map import — PBR textures on slots 4–7.
- **Phase 3**: Paint system upgrades — variable resolution paint masks, layer compositing, roughness/metallic paint channels.
- **Phase 4**: Multi-material support — per-SubMesh draw calls with per-material textures.
- **Phase 5**: Texture bake pipeline — CPU material evaluator exports PBR texture map PNGs.

## What Phase 6 Does

Replaces the inspector's 219 `SpriteKnobSlider` controls (64px rotary knob sprites) with a new `ValueInput` control (compact text + arrows + drag + scroll). Each `SpriteKnobSlider` is currently paired with a `TextBlock` readout showing the formatted value, and ~33 also have an additional precision `TextBox` for exact entry. The `ValueInput` consolidates all three into one control.

**Why**: Sprite knobs are space-inefficient (64px diameter), don't expose numeric values inline, and require separate text fields for precision entry. The replacement saves ~70% vertical space per control, eliminates ~440 redundant controls, and removes significant bitmap memory overhead (219 copies of a 2048×1664 sprite sheet).

## ⚠️ CRITICAL CONSTRAINTS

1. **The `Value` styled property MUST be an `AvaloniaProperty`** so that `PropertyChanged` event handlers work identically to the current `Slider.ValueProperty` pattern.
2. **Every handler checks `e.Property != Slider.ValueProperty`** — after migration, these checks become `e.Property != ValueInput.ValueProperty`. The property type must be `double` and the property name must be `"Value"`.
3. **Do NOT change the `_fooSlider` field names.** Keep the existing field names so handler code changes are minimal. Only the field *type* changes from `SpriteKnobSlider` (which inherits `Slider`) to `ValueInput`.
4. **The `_updatingUi` guard pattern must continue to work.** Setting `_fooSlider.Value = x` in `UpdateUiFromProject()` fires `PropertyChanged` — the existing `if (_updatingUi) return;` guard suppresses handler re-entry.
5. **Reset buttons** that set `slider.Value = defaultValue` must continue to work unchanged.
6. **Do not touch GPU pipeline, rendering, project serialization, or any non-UI code.**

## Subphase 6A: Create ValueInput Control

### 6A.1: New File — `KnobForge.App/Controls/ValueInput.cs`

Create a single self-contained Avalonia `UserControl` with NO companion `.axaml` file. All layout is built in code.

#### Styled Properties

```csharp
public static readonly StyledProperty<double> ValueProperty =
    AvaloniaProperty.Register<ValueInput, double>(nameof(Value), defaultValue: 0.0);

public static readonly StyledProperty<double> MinimumProperty =
    AvaloniaProperty.Register<ValueInput, double>(nameof(Minimum), defaultValue: 0.0);

public static readonly StyledProperty<double> MaximumProperty =
    AvaloniaProperty.Register<ValueInput, double>(nameof(Maximum), defaultValue: 1.0);

public static readonly StyledProperty<double> StepProperty =
    AvaloniaProperty.Register<ValueInput, double>(nameof(Step), defaultValue: 0.01);

public static readonly StyledProperty<double> SkewFactorProperty =
    AvaloniaProperty.Register<ValueInput, double>(nameof(SkewFactor), defaultValue: 1.0);

public static readonly StyledProperty<int> DecimalPlacesProperty =
    AvaloniaProperty.Register<ValueInput, int>(nameof(DecimalPlaces), defaultValue: 3);

public static readonly StyledProperty<string> SuffixProperty =
    AvaloniaProperty.Register<ValueInput, string>(nameof(Suffix), defaultValue: "");
```

Each has a standard CLR wrapper (`get => GetValue(…); set => SetValue(…);`).

#### Layout Structure

```
Border (1px rounded rect, CornerRadius=2.5, Background=#1A1E24, BorderBrush=#3A4550)
  └─ Grid (ColumnDefinitions: "*,Auto")
       ├─ [Col 0] TextBox — right-aligned, single-line, no border, dark background
       └─ [Col 1] Grid (RowDefinitions: "*,*", Width=16)
            ├─ [Row 0] UpArrowButton (RepeatButton) — draws "▲" chevron
            └─ [Row 1] DownArrowButton (RepeatButton) — draws "▼" chevron
```

Use Avalonia `RepeatButton` with `Delay=420` and `Interval=110` for autorepeat. (The 45ms fast tier is optional — RepeatButton doesn't natively support acceleration, and the standard repeat is sufficient.)

#### Height and Sizing

- Fixed height: 26px (matches compact inspector row)
- The control should be `HorizontalAlignment="Stretch"` by default
- MinWidth: 80

#### Text Editor Behavior

- `TextBox` is right-aligned, transparent background, no border, font size 12
- On `KeyDown` Enter/Return: parse text → sanitize → clamp → snap → update `Value` property
- On `KeyDown` Escape: revert text display to current `Value`, defocus
- On `LostFocus`: commit (same as Enter)
- Update display text when `Value` changes programmatically, UNLESS the TextBox has focus (user is editing)
- Display format: `value.ToString($"F{DecimalPlaces}")` + Suffix (e.g., " deg")

#### Value Sanitization Pipeline

```csharp
private double SanitizeValue(double raw)
{
    double clamped = Math.Clamp(raw, Minimum, Maximum);
    double step = Step;
    if (step > 0)
    {
        clamped = Math.Round((clamped - Minimum) / step) * step + Minimum;
        clamped = Math.Clamp(clamped, Minimum, Maximum);
    }
    return clamped;
}
```

SkewFactor is reserved for future use — do NOT implement skewed normalization now. Just clamp + snap.

#### Step Arrow Buttons

- `RepeatButton` with `Delay=420, Interval=110`
- Click/repeat: `Value = SanitizeValue(Value ± Step × modifierMultiplier)`
- Draw chevron paths: up arrow "M3,7 L8,3 L13,7" and down arrow "M3,3 L8,7 L13,3" (adjust to button bounds)
- Background: transparent default, #2A3440 on hover, #354555 on press
- Arrow stroke: #8899AA default, #BBCCDD on hover

#### Mouse Drag on TextBox

- Attach `PointerPressed`, `PointerMoved`, `PointerReleased` handlers on the TextBox
- On press: record `_dragStartY = screenPosition.Y`, `_dragStartValue = Value`, `_isDragging = false`
- On move: if `|deltaY| > 2px` → enter drag mode (`_isDragging = true`), capture pointer
  - Once dragging: `int steps = (int)((_dragStartY - currentScreenY) / 6.0)`
  - Acceleration (when NOT holding Shift): `double accel = Math.Clamp(1.0 + Math.Abs(totalDeltaY) / 220.0, 1.0, 6.0)`
  - Final: `Value = SanitizeValue(_dragStartValue + steps * Step * modifierMultiplier * accel)`
- On release: if was dragging → end drag; if NOT → let TextBox handle the click (focus for text edit)

#### Mouse Wheel

- Handle `PointerWheelChanged` on the control
- `double steps = e.Delta.Y * 4.0` (discrete wheel); for smooth/trackpad `e.Delta.Y * 16.0`
  - Distinguish by checking if `Math.Abs(e.Delta.Y)` is close to 1.0 (discrete) or fractional (smooth)
- `Value = SanitizeValue(Value + steps * Step * modifierMultiplier)`

#### Modifier Key Multipliers

All interaction modes (arrows, drag, wheel) share the same modifier logic:

```csharp
private static double GetModifierMultiplier(KeyModifiers mods)
{
    double mult = 1.0;
    if (mods.HasFlag(KeyModifiers.Shift)) mult *= 0.2;
    if (mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Meta)) mult *= 0.25;
    if (mods.HasFlag(KeyModifiers.Alt)) mult *= 5.0;
    return mult;
}
```

### 6A.2: Add ValueInput Style to App.axaml

Add a minimal style block:

```xml
<Style Selector="controls|ValueInput">
    <Setter Property="Margin" Value="0,2,0,2"/>
    <Setter Property="Height" Value="26"/>
</Style>
```

### 6A.3: Build Gate

After creating ValueInput.cs and updating App.axaml:
- `dotnet build KnobForge.App` must succeed with zero errors
- Add a single test ValueInput to MainWindow.axaml (e.g., in the Lighting tab) temporarily, verify it renders, then remove it

---

## Subphase 6B: Replace All SpriteKnobSliders

### Strategy

Replace section-by-section with a build gate between each. The sections are defined by the TabItems in MainWindow.axaml.

### 6B.1: AXAML Replacement Pattern

For each control, transform:

**BEFORE:**
```xml
<TextBlock Text="Radius"/>
<controls:SpriteKnobSlider x:Name="ModelRadiusSlider" Minimum="40" Maximum="500"/>
<TextBlock x:Name="ModelRadiusValueText"/>
```

**AFTER:**
```xml
<TextBlock Text="Radius"/>
<controls:ValueInput x:Name="ModelRadiusSlider" Minimum="40" Maximum="500" Step="1" DecimalPlaces="0"/>
```

Key decisions per control:
- **Step**: Derive from the readout format string. `"0"` → Step=1, `"0.0"` → Step=0.1, `"0.00"` → Step=0.01, `"0.000"` → Step=0.001, `"0.0000"` → Step=0.0001
- **DecimalPlaces**: Same logic. `"0"` → 0, `"0.0"` → 1, `"0.00"` → 2, `"0.000"` → 3, `"0.0000"` → 4
- **Suffix**: If the readout appended a unit (e.g., `$"{value:0.0} deg"`, `$"{value:0} px"`), set `Suffix=" deg"` or `Suffix=" px"`
- **Remove the TextBlock readout** (`x:Name="FooValueText"`) — the ValueInput shows the value inline
- **Remove the precision TextBox** where present (e.g., `_envIntensityInputTextBox`) — the ValueInput IS the text input
- **Keep the label TextBlock** (`<TextBlock Text="Radius"/>`)

### 6B.2: Readout Format Reference (from EnvironmentShadowReadouts.cs)

Use this to determine Step/DecimalPlaces/Suffix for each control:

| Slider | Format | Suffix | → Step | DecPlaces |
|--------|--------|--------|--------|-----------|
| LightX/Y/Z | `0` | — | 1 | 0 |
| Direction | `0.0` | ` deg` | 0.1 | 1 |
| Intensity | `0.00` | — | 0.01 | 2 |
| Falloff | `0.00` | — | 0.01 | 2 |
| LightR/G/B | `0` | — | 1 | 0 |
| DiffuseBoost | `0.00` | — | 0.01 | 2 |
| SpecularBoost | `0.00` | — | 0.01 | 2 |
| SpecularPower | `0.0` | — | 0.1 | 1 |
| ModelRadius | `0` | — | 1 | 0 |
| ModelHeight | `0` | — | 1 | 0 |
| ModelTopScale | `0.00` | — | 0.01 | 2 |
| ModelBevel | `0` | — | 1 | 0 |
| BevelCurve | `0.00` | — | 0.01 | 2 |
| CrownProfile | `0.00` | — | 0.01 | 2 |
| BodyTaper | `0.00` | — | 0.01 | 2 |
| BodyBulge | `0.00` | — | 0.01 | 2 |
| ModelSegments | `0` | — | 1 | 0 |
| Rotation | `0.0` | ` deg` | 0.1 | 1 |
| CollarScale | `0.000` | — | 0.001 | 3 |
| CollarBodyLength | `0.000` | — | 0.001 | 3 |
| CollarBodyThickness | `0.000` | — | 0.001 | 3 |
| CollarHeadLength | `0.000` | — | 0.001 | 3 |
| CollarHeadThickness | `0.000` | — | 0.001 | 3 |
| CollarRotate | `0.00` | — | 0.01 | 2 |
| CollarOffsetX/Y | `0.000` | — | 0.001 | 3 |
| CollarElevation | `0.000` | — | 0.001 | 3 |
| CollarInflate | `0.0000` | — | 0.0001 | 4 |
| CollarMaterial R/G/B | `0` | — | 1 | 0 |
| CollarMaterial Metallic/Roughness/Pearlescence | `0.00` | — | 0.01 | 2 |
| CollarMaterial Rust/Wear/Gunk | `0.00` | — | 0.01 | 2 |
| Material R/G/B | `0` | — | 1 | 0 |
| Material Metallic/Roughness/Pearlescence | `0.00` | — | 0.01 | 2 |
| Material Rust/Wear/Gunk | `0.00` | — | 0.01 | 2 |
| MaterialBrushStrength/Density/Character | `0.00` | — | 0.01 | 2 |
| MaterialNormalMapStrength | `0.00` | — | 0.01 | 2 |
| EnvIntensity | `0.000` | — | 0.001 | 3 |
| EnvRoughnessMix | `0.000` | — | 0.001 | 3 |
| EnvTop/Bottom R/G/B | `0` | — | 1 | 0 |
| Shadow Strength/Softness/Quality | `0.000` | — | 0.001 | 3 |
| ShadowDistance | `0` | — | 1 | 0 |
| ShadowScale | `0.00` | — | 0.01 | 2 |
| ShadowGray | `0` | — | 1 | 0 |
| ShadowDiffuseInfluence | `0.00` | — | 0.01 | 2 |
| BrushSize | `0.0` | — | 0.1 | 1 |
| BrushOpacity | `0.000` | — | 0.001 | 3 |
| BrushDarkness | `0.000` | — | 0.001 | 3 |
| BrushSpread | `0.000` | — | 0.001 | 3 |
| PaintCoatMetallic/Roughness | `0.000` | — | 0.001 | 3 |
| ClearCoatAmount/Roughness | `0.000` | — | 0.001 | 3 |
| AnisotropyAngle | `0.0` | — | 0.1 | 1 |
| ScratchWidth | `0.0` | — | 0.1 | 1 |
| ScratchDepth | `0.000` | — | 0.001 | 3 |
| ScratchResistance | `0.000` | — | 0.001 | 3 |
| ScratchDepthRamp | `0.0000` | — | 0.0001 | 4 |
| ScratchExposeColor R/G/B | `0.000` | — | 0.001 | 3 |
| ScratchExposeMetallic | `0.000` | — | 0.001 | 3 |
| ScratchExposeRoughness | `0.000` | — | 0.001 | 3 |
| Indicator Width/Length/Position/Thickness/Roundness/ColorBlend | `0.00` | — | 0.01 | 2 |
| Indicator R/G/B | `0` | — | 1 | 0 |
| MicroLodFadeStart/End | `0.00` | — | 0.01 | 2 |
| MicroRoughnessLodBoost | `0.00` | — | 0.01 | 2 |
| SpiralRidgeHeight/Width | `0.00` | — | 0.01 | 2 |
| SpiralTurns | `0.0` | — | 0.1 | 1 |
| Grip Start/Height/Density/Pitch/Depth/Width/Sharpness | `0.00` | — | 0.01 | 2 |

For any controls not listed here (slider assembly, toggle assembly, push button controls, etc.), examine their readout format strings in `MainWindow.EnvironmentShadowReadouts.cs` and apply the same pattern. If a readout uses `$"{value:0.0}px"` → `Step=0.1, DecimalPlaces=1, Suffix=" px"`. If no readout format is visible, default to `Step=0.01, DecimalPlaces=2`.

### 6B.3: Handler Code Changes

#### Pattern 1: Property check guard (in ALL handler methods)

**BEFORE:**
```csharp
if (e.Property != Slider.ValueProperty)
```

**AFTER:**
```csharp
if (e.Property != ValueInput.ValueProperty)
```

This is the ONLY handler code change needed for most handlers. The `_fooSlider.Value` reads and writes continue to work because `ValueInput.Value` is a `double` styled property, same as `Slider.Value`.

**Important**: Some handlers check `Slider.ValueProperty` explicitly, others check against the property of `RangeBase` or `Slider`. Search for ALL occurrences of `Slider.ValueProperty` across all MainWindow partial classes and replace with `ValueInput.ValueProperty`.

Also update all null checks in `HasRequiredControls()` — the field types change but the null checks remain. No logic change needed if field names stay the same.

#### Pattern 2: Readout updates (DELETE)

**DELETE** all lines following this pattern:
```csharp
if (_fooValueText != null && _fooSlider != null)
    _fooValueText.Text = $"{_fooSlider.Value:0.00}";
```

These are concentrated in `MainWindow.EnvironmentShadowReadouts.cs` in the `UpdateReadouts()` method (and similar methods). Delete every readout assignment. If `UpdateReadouts()` becomes empty, delete the method and remove its call site.

#### Pattern 3: Precision text entry (DELETE)

**DELETE** all of `MainWindow.PrecisionControls.cs` contents — or delete the file entirely.

- Remove `InitializePrecisionControls()` call from `MainWindow.Initialization.cs`
- Remove `UpdatePrecisionControlEntryText()` call from wherever it's invoked
- Remove all `_fooInputTextBox` field declarations

### 6B.4: Field Declarations and FindControl Initialization

⚠️ **CRITICAL**: Fields are NOT auto-generated. They are **manually declared** in `MainWindow/MainWindow.cs` and **manually initialized** via `FindControl<T>()`.

#### Field Declarations (MainWindow/MainWindow.cs, starting around line 76)

**BEFORE:**
```csharp
private readonly Slider? _rotationSlider;
private readonly Slider? _lightXSlider;
private readonly Slider? _lightYSlider;
// ... ~219 Slider? fields
```

**AFTER:**
```csharp
private readonly ValueInput? _rotationSlider;
private readonly ValueInput? _lightXSlider;
private readonly ValueInput? _lightYSlider;
// ... change ALL Slider? fields that correspond to SpriteKnobSliders to ValueInput?
```

Change ALL `Slider?` fields that correspond to inspector SpriteKnobSliders to `ValueInput?`. Do NOT change `Slider?` fields that are actual Avalonia Sliders used elsewhere (e.g., in RenderSettingsWindow).

Also **delete** all `TextBlock?` fields that were readout displays (e.g., `_rotationValueText`, `_lightXValueText`, etc.) and all `TextBox?` fields that were precision inputs (e.g., `_envIntensityInputTextBox`, etc.).

#### FindControl Initialization (MainWindow/MainWindow.cs, starting around line 809)

**BEFORE:**
```csharp
_rotationSlider = this.FindControl<Slider>("RotationSlider");
_lightXSlider = this.FindControl<Slider>("LightXSlider");
// ...
_rotationValueText = this.FindControl<TextBlock>("RotationValueText");
_lightXValueText = this.FindControl<TextBlock>("LightXValueText");
// ...
_envIntensityInputTextBox = this.FindControl<TextBox>("EnvIntensityInputTextBox");
```

**AFTER:**
```csharp
_rotationSlider = this.FindControl<ValueInput>("RotationSlider");
_lightXSlider = this.FindControl<ValueInput>("LightXSlider");
// ... change ALL FindControl<Slider> to FindControl<ValueInput> for inspector sliders
// DELETE all FindControl<TextBlock> lines for readout fields
// DELETE all FindControl<TextBox> lines for precision input fields
```

**Add the `using` directive** at the top of MainWindow.cs:
```csharp
using KnobForge.App.Controls;
```
(if not already present).

### 6B.5: PropertyChanged Wiring

In `MainWindow.Initialization.cs`, the handlers are wired like:
```csharp
_lightXSlider.PropertyChanged += OnLightXChanged;
```

This continues to work unchanged — `ValueInput` inherits `AvaloniaObject` which fires `PropertyChanged`.

### 6B.6: Section-by-Section Build Gates

Replace in this order, running `dotnet build KnobForge.App` after each:

1. **Lighting tab** — LightX/Y/Z, Direction, Intensity, Falloff, LightR/G/B, DiffuseBoost, SpecularBoost, SpecularPower (~15 controls)
2. **Model tab** — Rotation, ModelRadius/Height/TopScale/Bevel, BevelCurve, CrownProfile, BodyTaper/Bulge, ModelSegments, SpiralRidgeHeight/Width/Turns, Grip controls (~25 controls)
3. **Model tab continued** — Collar controls (Scale, BodyLength, BodyThickness, HeadLength, HeadThickness, Rotate, OffsetX/Y, Elevation, Inflate), Collar material (R/G/B, Metallic, Roughness, Pearlescence, Rust, Wear, Gunk) (~20 controls)
4. **Model tab continued** — Indicator controls (~10 controls)
5. **Material tab** — MaterialBase R/G/B, Metallic, Roughness, Pearlescence, Rust, Wear, Gunk, BrushStrength/Density/Character, NormalMapStrength, MicroLod controls (~20 controls)
6. **Environment tab** — EnvIntensity, EnvRoughnessMix, EnvTop/Bottom R/G/B, Shadow controls (~15 controls)
7. **Paint tab** — BrushSize, BrushOpacity, BrushDarkness, BrushSpread, PaintCoatMetallic/Roughness, ClearCoatAmount/Roughness, AnisotropyAngle, Scratch controls (~20 controls)
8. **Slider Assembly tab** — Backplate/Thumb width/height/depth/thickness controls
9. **Toggle Assembly tab** — Toggle controls (StateIndex, MaxAngle, plate/bushing/lever controls)
10. **Push Button tab** — PressAmount and any other push button controls
11. **Any remaining** — Final grep for `SpriteKnobSlider` in *.axaml

After each section: `dotnet build KnobForge.App` must succeed.

### 6B.7: Remove Readout TextBlock Fields and Updates

After all AXAML sliders are replaced:

1. Delete all `_fooValueText` field references (these should generate compile errors after removing TextBlocks from AXAML)
2. Delete all readout update code in `MainWindow.EnvironmentShadowReadouts.cs` and any other `*Readouts*` files
3. Delete all `_fooInputTextBox` fields and the entire `MainWindow.PrecisionControls.cs` file
4. Remove `InitializePrecisionControls()` call
5. Remove `UpdatePrecisionControlEntryText()` calls
6. Remove `UpdateReadouts()` calls (or the methods themselves)
7. Build gate: `dotnet build KnobForge.App`

### 6B.8: HasRequiredControls() Update

The `HasRequiredControls()` method in `MainWindow.Initialization.cs` checks every slider and value text field for null. After migration:
- Remove all `_fooValueText != null` checks (those fields no longer exist)
- Remove all `_fooInputTextBox != null` checks
- Keep all `_fooSlider != null` checks (they still exist, just as `ValueInput` type)

---

## Subphase 6C: Cleanup

### 6C.1: Delete SpriteKnobSlider

- Delete `KnobForge.App/Controls/SpriteKnobSlider.cs`
- Delete sprite sheet asset: `KnobForge.App/Assets/green_channel_strip_over_right_spritesheet.png`
- Remove `<Style Selector="controls|SpriteKnobSlider">` block from `App.axaml`

### 6C.2: Remove Empty Readout Files

If `MainWindow.EnvironmentShadowReadouts.cs` is now empty or nearly empty (only containing the class declaration with no methods), consider removing the file or keeping it as a shell.

### 6C.3: Final Verification

```bash
# Must pass
dotnet build KnobForge.App

# Must return zero matches
grep -r "SpriteKnobSlider" --include="*.cs" --include="*.axaml" KnobForge.App/

# Should return zero matches (no orphaned readout fields)
grep -r "_.*ValueText" --include="*.cs" KnobForge.App/Views/MainWindow*.cs

# Should return zero matches (no orphaned precision input fields)
grep -r "_.*InputTextBox" --include="*.cs" KnobForge.App/Views/MainWindow*.cs
```

---

## Current State (Verified)

### SpriteKnobSlider (KnobForge.App/Controls/SpriteKnobSlider.cs)

Inherits `Avalonia.Controls.Slider`. Key properties:
- `SpriteSheetPath` (string, default "Assets/green_channel_strip_over_right_spritesheet.png")
- `FrameCount` (int, 156), `ColumnCount` (int, 13), `FrameWidth`/`FrameHeight` (int, 128)
- `KnobDiameter` (double, default 128 — rendered at 64 after scaling)
- `DragPixelsForFullRange` (double, 220)
- `CurrentFrame` (IImage? — the cropped sprite bitmap)

It loads a 2048×1664 sprite sheet, crops 156 frames, selects frame based on normalized value. Drag interaction: records start point, computes `deltaY + deltaX * 0.35`, maps to value via `DragPixelsForFullRange`.

### App.axaml SpriteKnobSlider Style

```xml
<Style Selector="controls|SpriteKnobSlider">
    <Setter Property="SpriteSheetPath" Value="Assets/green_channel_strip_over_right_spritesheet.png"/>
    <Setter Property="FrameCount" Value="156"/>
    <Setter Property="ColumnCount" Value="13"/>
    <Setter Property="FrameWidth" Value="128"/>
    <Setter Property="FrameHeight" Value="128"/>
    <Setter Property="FramePadding" Value="12"/>
    <Setter Property="FrameStartX" Value="12"/>
    <Setter Property="FrameStartY" Value="12"/>
    <Setter Property="KnobDiameter" Value="64"/>
    <Setter Property="DragPixelsForFullRange" Value="220"/>
    <Setter Property="Margin" Value="0,2,0,2"/>
    <Setter Property="Template">
        <ControlTemplate>
            <Grid HorizontalAlignment="Left" VerticalAlignment="Center"
                  Width="{Binding EffectiveKnobDiameter, ...}" Height="{Binding EffectiveKnobDiameter, ...}">
                <Image Source="{Binding CurrentFrame, ...}" Stretch="UniformToFill"/>
            </Grid>
        </ControlTemplate>
    </Setter>
</Style>
```

This entire style block will be deleted in 6C.

### AXAML Slider Declaration Pattern (MainWindow.axaml)

Typical pattern (repeated 219 times):
```xml
<TextBlock Text="Radius"/>
<controls:SpriteKnobSlider x:Name="ModelRadiusSlider" Minimum="40" Maximum="500"/>
<TextBlock x:Name="ModelRadiusValueText"/>
```

Some controls (RGB color rows) use horizontal layout:
```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="R" Width="14"/>
    <controls:SpriteKnobSlider x:Name="LightRSlider" Minimum="0" Maximum="255" Width="210"/>
    <TextBlock x:Name="LightRValueText" Width="36"/>
</StackPanel>
```

For the horizontal layout cases, replace similarly but remove the ValueText TextBlock:
```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="R" Width="14"/>
    <controls:ValueInput x:Name="LightRSlider" Minimum="0" Maximum="255" Step="1" DecimalPlaces="0"/>
</StackPanel>
```

Note: Remove the explicit `Width="210"` from the ValueInput (it should stretch to fill). If layout breaks, add `Width="210"` back.

### Handler Wiring Pattern (MainWindow.Initialization.cs)

```csharp
_lightXSlider.PropertyChanged += OnLightXChanged;
_lightYSlider.PropertyChanged += OnLightYChanged;
// ... 219 more lines like this
```

These remain unchanged.

### Handler Implementation Pattern (e.g., MainWindow.ModelHandlers.cs)

```csharp
private void OnModelRadiusChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
{
    if (_updatingUi) return;
    if (_modelRadiusSlider == null || e.Property != Slider.ValueProperty)
    {
        return;
    }

    var model = GetModelNode();
    if (model == null) return;

    model.Radius = (float)_modelRadiusSlider.Value;
    RequestHeavyGeometryRefresh();
}
```

**Change required**: `Slider.ValueProperty` → `ValueInput.ValueProperty`

Handlers are spread across these partial class files:
- `MainWindow.LightingHandlers.cs`
- `MainWindow.ModelHandlers.cs`
- `MainWindow.EnvironmentShadowReadouts.cs` (also contains handlers like `OnEnvironmentChanged`)
- `MainWindow.PaintBrushHandlers.cs`
- `MainWindow.MaterialTextureHandlers.cs`
- `MainWindow.SliderAssemblyHandlers.cs`
- `MainWindow.ToggleAssemblyHandlers.cs`
- `MainWindow.PushButtonAssemblyHandlers.cs`
- `MainWindow.IndicatorLightHandlers.cs`
- `MainWindow.CollarIndicatorMaterialHandlers.cs`
- `MainWindow.MultiMaterialInspector.cs`

Search ALL `*.cs` files under `KnobForge.App/Views/` for `Slider.ValueProperty` and replace with `ValueInput.ValueProperty`. There are ~40 occurrences across these files:
- MainWindow.LightingHandlers.cs (12)
- MainWindow.CollarIndicatorMaterialHandlers.cs (10)
- MainWindow.ModelHandlers.cs (8)
- MainWindow.EnvironmentShadowReadouts.cs (2)
- MainWindow.IndicatorLightHandlers.cs (2)
- MainWindow.SliderAssemblyHandlers.cs (1)
- MainWindow.PaintBrushHandlers.cs (1)
- MainWindow.PushButtonAssemblyHandlers.cs (1)
- MainWindow.ToggleAssemblyHandlers.cs (1)
- MainWindow.MaterialTextureHandlers.cs (1)
- MainWindow.PaintLayers.cs (1)

Do NOT change `Slider.ValueProperty` in `RenderSettingsWindow` files — those use actual Avalonia Sliders, not SpriteKnobSliders.

### Readout Update Pattern (MainWindow.EnvironmentShadowReadouts.cs)

```csharp
private void UpdateReadouts()
{
    if (_rotationValueText != null && _rotationSlider != null)
        _rotationValueText.Text = $"{RadiansToDegrees(_rotationSlider.Value):0.0} deg";

    if (_lightXValueText != null && _lightXSlider != null)
        _lightXValueText.Text = $"{_lightXSlider.Value:0}";
    // ... ~100 more lines like this
}
```

Delete all of these. The ValueInput displays its own value.

**Special case — Rotation**: The readout converts radians to degrees (`RadiansToDegrees`). The ValueInput will display the raw radian value. Two options:
1. Keep the slider in radians (0 to 6.28) and set `Suffix=" rad"` — simplest, no handler changes
2. Change the slider range to degrees (0 to 360) and update the handler to convert — more user-friendly but requires handler modification

**Recommendation**: Option 1 (keep radians). The control already works in radians internally. If the user wants degrees display, that can be added later with a custom display formatter.

**Actually** — looking again at the readout: `$"{RadiansToDegrees(_rotationSlider.Value):0.0} deg"`. The slider stores radians but displays degrees. To maintain the same UX, change the AXAML to `Minimum="0" Maximum="360" Step="0.1" DecimalPlaces="1" Suffix=" deg"` and update the handler to convert degrees back to radians: `model.RotationRadians = (float)DegreesToRadians(_rotationSlider.Value)`. Also update `UpdateUiFromProject()` to set `_rotationSlider.Value = RadiansToDegrees(model.RotationRadians)`. This is a better UX.

Similarly, the Direction slider (0-360 degrees with `" deg"` suffix) already has its range in degrees, so just add `Suffix=" deg"`.

### Precision Control Pattern (MainWindow.PrecisionControls.cs)

```csharp
private void InitializePrecisionControls()
{
    WirePrecisionTextEntry(_envIntensityInputTextBox, _envIntensitySlider);
    WirePrecisionTextEntry(_collarScaleInputTextBox, _collarScaleSlider, "0.000");
    // ... 33 lines

    WireResetButton(_envIntensityResetButton, _envIntensitySlider, DefaultEnvIntensity);
    // ... 5 lines

    AttachPrecisionNudgeHandlers(_envIntensitySlider);
    // ... 5 lines
}
```

Delete all `WirePrecisionTextEntry` calls and the method itself.
Keep `WireResetButton` calls — these wire reset buttons that set `slider.Value = default`. These still work because `ValueInput.Value` is settable. Move them to `MainWindow.Initialization.cs` if `PrecisionControls.cs` is deleted, or keep them in a slimmed-down file.
Delete `AttachPrecisionNudgeHandlers` calls — the ValueInput has its own wheel/key handling.
Delete `UpdatePrecisionControlEntryText()` and all `SyncPrecisionEntry` calls.

### CrownProfile Special Case

The CrownProfile readout has special formatting:
```csharp
string shape = v < -0.01f ? "Concave" : v > 0.01f ? "Convex" : "Flat";
_crownProfileValueText.Text = $"{v:0.00} ({shape})";
```

This is informational — the ValueInput will just show `"0.00"`. The label already says "Crown Profile (Concave ↔ Convex)" so the user still has context. Acceptable.

### Special Readouts That Compute Derived Values

Some readouts show computed values rather than raw slider values:
```csharp
_rotationValueText.Text = $"{RadiansToDegrees(_rotationSlider.Value):0.0} deg";
```

As discussed above, handle Rotation by changing the slider range to degrees. For any other computed readouts, check if the transformation is trivial (unit conversion) or complex (derived calculation). Trivial → change slider range. Complex → accept raw value display.

---

## File Impact Summary

### New Files
| File | Purpose |
|------|---------|
| `KnobForge.App/Controls/ValueInput.cs` | Choroboros-style compact numeric input control |

### Deleted Files
| File | Reason |
|------|--------|
| `KnobForge.App/Controls/SpriteKnobSlider.cs` | Replaced by ValueInput |
| `KnobForge.App/Views/MainWindow.PrecisionControls.cs` | Absorbed into ValueInput |
| `KnobForge.App/Assets/green_channel_strip_over_right_spritesheet.png` | No longer needed |

### Modified Files
| File | Change |
|------|--------|
| `MainWindow.axaml` | Replace 219 SpriteKnobSlider + ~220 readout TextBlocks + ~33 precision TextBoxes with ValueInput |
| `App.axaml` | Remove SpriteKnobSlider style, add ValueInput style |
| `MainWindow.Initialization.cs` | Remove `InitializePrecisionControls()` call, update `HasRequiredControls()` |
| `MainWindow.EnvironmentShadowReadouts.cs` | Delete readout update code, keep handler methods, change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.LightingHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.ModelHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.PaintBrushHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.MaterialTextureHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.SliderAssemblyHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.ToggleAssemblyHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.PushButtonAssemblyHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.IndicatorLightHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.CollarIndicatorMaterialHandlers.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.MultiMaterialInspector.cs` | Change `Slider.ValueProperty` → `ValueInput.ValueProperty` |
| `MainWindow.MaterialSnapshots.cs` | If it reads slider values, no change needed (`.Value` API identical) |
| Any other `MainWindow.*.cs` | Search and replace `Slider.ValueProperty` → `ValueInput.ValueProperty` |

---

## Visual Theme Constants

For the ValueInput control's visual styling, use these colors to match the existing dark hacker aesthetic:

```csharp
private static readonly Color FieldBackground = Color.Parse("#1A1E24");
private static readonly Color FieldBorder = Color.Parse("#3A4550");
private static readonly Color TextColor = Color.Parse("#E6EEF5");
private static readonly Color ArrowColor = Color.Parse("#8899AA");
private static readonly Color ArrowHoverColor = Color.Parse("#BBCCDD");
private static readonly Color ButtonHoverBg = Color.Parse("#2A3440");
private static readonly Color ButtonPressBg = Color.Parse("#354555");
private static readonly Color DividerColor = Color.Parse("#2E3740");
```

These are sampled from the existing MainWindow UI to ensure visual consistency.

---

## Build & Verification Checklist

After all subphases:

- [ ] `dotnet build KnobForge.App` succeeds with zero errors
- [ ] `grep -r "SpriteKnobSlider"` returns zero matches in KnobForge.App
- [ ] Zero orphaned `_fooValueText` or `_fooInputTextBox` field references
- [ ] Every inspector control still reads/writes correct project properties (manual spot check)
- [ ] ValueInput text editing: type value, press Enter → committed
- [ ] ValueInput text editing: press Escape → reverts to previous value
- [ ] ValueInput arrows: click up/down → value increments/decrements by Step
- [ ] ValueInput drag: vertical drag on text area → value changes smoothly
- [ ] ValueInput wheel: scroll on control → value changes
- [ ] Modifier keys: Shift (fine), Alt (coarse) modify step size
- [ ] Reset buttons still work (set value back to default)
- [ ] `_updatingUi` guard prevents feedback loops when loading project state
