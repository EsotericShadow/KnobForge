# Phase 18: Interaction States, Color Swatches & Final Polish

## Your Role

You are implementing Phase 18 of the Monozukuri Material Tool Transformation. This is the final design-system phase. Phases 15–17 established tokens, control hierarchy, and section structure. Phase 18 adds the layer that makes the UI feel *alive*: polished interaction states, micro-feedback cues, RGB color swatch previews, and tab underline refinement. These are the details that separate "functional" from "professional."

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–17 are complete. The app now has a Dark Slate surface token system, modern toolbar with accent CTA, ghost/default/accent button hierarchy, scroll-to-adjust ValueInput, thin expander headers with accent stripes, advanced sub-section treatment, and generous section spacing. What's missing: interaction feedback feels flat (no transitions, no glow), there's no visual preview of RGB values, and the tab underline is too thin and squared off.

## What Phase 18 Does

### Four Subphases (execute in order):

1. **18A — Button & Control Hover Polish**: Add subtle background transitions and accent glow on interactive elements.
2. **18B — ListBox Selection Indicator**: Add a left accent bar on selected list items for clearer selection feedback.
3. **18C — RGB Color Swatch Circles**: Add small circular color previews next to RGB channel inputs throughout the inspector.
4. **18D — Tab Underline Refinement**: Thicken the selected tab indicator and round its ends.

**Explicitly deferred** (do NOT implement):
- Animated expand/collapse (requires custom panel, out of scope)
- GPU shader changes (purely UI phase)

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT change any `x:Name` values.** Code-behind resolves controls by name.
2. **Do NOT change handler wiring signatures.** Event handlers keep their existing signatures.
3. **Do NOT modify Core or Rendering code.**
4. **The app must compile and run after each subphase.**
5. **Preserve all existing token names.**
6. **Test with all 5 project types** to ensure no layout breakage.

---

## Existing Architecture (Read Before Coding)

### Current Interaction States (`App.axaml`)

**Button** (after Phase 16):
```xml
<Style Selector="Button">
    Background: Surface2, BorderBrush: BorderSubtle
</Style>
<Style Selector="Button:pointerover">
    Background: Surface3, BorderBrush: BorderStrong
</Style>
<Style Selector="Button:pressed">
    Background: Surface1
</Style>
```

**ListBoxItem**:
```xml
<Style Selector="ListBoxItem:selected">
    Background: AccentSubtle
</Style>
<Style Selector="ListBoxItem:pointerover">
    Background: Surface2
</Style>
```

**TabItem**:
```xml
<Style Selector="TabItem:selected">
    Foreground: TextPrimary, BorderBrush: Accent, BorderThickness: 0,0,0,2
</Style>
```

### RGB Channel Inputs Pattern

Throughout `MainWindow.axaml`, RGB inputs follow this pattern:
```xml
<Grid ColumnDefinitions="18,*" ColumnSpacing="4">
    <TextBlock Grid.Column="0" Text="R" Classes="channel-r"/>
    <controls:ValueInput Grid.Column="1" x:Name="EnvTopRInput" .../>
</Grid>
<Grid ColumnDefinitions="18,*" ColumnSpacing="4">
    <TextBlock Grid.Column="0" Text="G" Classes="channel-g"/>
    <controls:ValueInput Grid.Column="1" x:Name="EnvTopGInput" .../>
</Grid>
<Grid ColumnDefinitions="18,*" ColumnSpacing="4">
    <TextBlock Grid.Column="0" Text="B" Classes="channel-b"/>
    <controls:ValueInput Grid.Column="1" x:Name="EnvTopBInput" .../>
</Grid>
```

These appear in multiple locations:
- Environment top/bottom color (Scene tab)
- Material base color (Model tab)
- Collar material color (Model tab)
- Indicator color (Model tab)
- Scratch expose color (Brush tab)
- Light color (Lighting tab)
- Bloom tint (Scene tab)

### Current Accent Tokens

From `App.axaml`:
```xml
<Color x:Key="Accent">#4A90B8</Color>
<Color x:Key="AccentSubtle">#2A4A60</Color>
```

Phase 15 may have added:
```xml
<Color x:Key="AccentGlow">#1A4A90B8</Color>  <!-- 10% opacity accent -->
<Color x:Key="AccentStrong">#5AA0C8</Color>
```

### Files to Modify

| Subphase | File | What Changes |
|---|---|---|
| 18A | `App.axaml` | Button/toolbar-button transitions, accent glow tokens usage |
| 18B | `App.axaml` | ListBoxItem selection style with left accent bar |
| 18C | `MainWindow.axaml` | Add color swatch Borders next to RGB groups |
| 18C | Code-behind files | Add swatch update logic |
| 18D | `App.axaml` | TabItem selected border styling |

---

## Subphase 18A — Button & Control Hover Polish

### Goal

Add subtle visual feedback that makes interactions feel responsive. In Avalonia 11.x, CSS-like `Transitions` can animate property changes. Adding a short transition to background and border color changes makes hover states feel smooth instead of abrupt.

### Step 1: Add Transitions to Default Button Style

**File**: `App.axaml` — add a `Transitions` setter to the base `Button` style:

```xml
<Style Selector="Button">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Padding" Value="10,4"/>
    <Setter Property="FontSize" Value="{StaticResource FontLabel}"/>
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.15"/>
            <BrushTransition Property="BorderBrush" Duration="0:0:0.15"/>
        </Transitions>
    </Setter>
</Style>
```

### Step 2: Add Transitions to Toolbar Buttons

**File**: `App.axaml` — add transitions to the `toolbar-button` style:

```xml
<Style Selector="Button.toolbar-button">
    <!-- existing setters... -->
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.12"/>
            <BrushTransition Property="BorderBrush" Duration="0:0:0.12"/>
            <BrushTransition Property="Foreground" Duration="0:0:0.12"/>
        </Transitions>
    </Setter>
</Style>
```

Slightly faster than default buttons (120ms vs 150ms) for a snappier toolbar feel.

### Step 3: Add Transitions to Toolbar Accent Button

**File**: `App.axaml` — add transitions to the `toolbar-button-accent` style:

```xml
<Style Selector="Button.toolbar-button-accent">
    <!-- existing setters... -->
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.12"/>
            <BrushTransition Property="Foreground" Duration="0:0:0.12"/>
        </Transitions>
    </Setter>
</Style>
```

### Step 4: Add Transitions to Ghost Button

**File**: `App.axaml` — add transitions to the `ghost-button` style:

```xml
<Style Selector="Button.ghost-button">
    <!-- existing setters... -->
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.12"/>
            <BrushTransition Property="BorderBrush" Duration="0:0:0.12"/>
            <BrushTransition Property="Foreground" Duration="0:0:0.12"/>
        </Transitions>
    </Setter>
</Style>
```

### Step 5: Add Transition to Expander Left Stripe

**File**: `App.axaml` — add a border transition to the Expander style:

```xml
<Style Selector="Expander">
    <Setter Property="Margin" Value="0,3,0,3"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentSubtleBrush}"/>
    <Setter Property="BorderThickness" Value="3,0,0,0"/>
    <Setter Property="CornerRadius" Value="0"/>
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="BorderBrush" Duration="0:0:0.15"/>
        </Transitions>
    </Setter>
</Style>
```

This makes the expander's accent stripe smoothly brighten on hover.

### Verification

- Hover over a button — the background transition is smooth, not instant.
- Hover over a toolbar button — foreground and background transition smoothly.
- Hover over the Render accent button — smooth background transition.
- Hover over an expander — the left accent stripe smoothly brightens.
- No jank or flicker during transitions.
- Performance is acceptable (transitions are GPU-composited in Avalonia).

---

## Subphase 18B — ListBox Selection Indicator

### Goal

Add a left accent bar to selected list items. This is a standard pattern in modern creative tools (VS Code, Figma, Blender) and provides a strong visual anchor for the current selection.

### Step 1: Update ListBoxItem Selected Style

**File**: `App.axaml` — replace the existing `ListBoxItem:selected` style:

```xml
<Style Selector="ListBoxItem:selected">
    <Setter Property="Background" Value="{StaticResource AccentSubtleBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="3,0,0,0"/>
</Style>
```

This adds a 3px left accent bar on selected items, matching the expander stripe width.

### Step 2: Add Transition to ListBoxItem

**File**: `App.axaml` — add a hover transition to ListBoxItem:

```xml
<Style Selector="ListBoxItem">
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.1"/>
        </Transitions>
    </Setter>
</Style>

<Style Selector="ListBoxItem:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
</Style>
```

### Verification

- Select a scene node in the scene list — a 3px accent-blue left bar appears.
- Select a light in the light list — same left bar treatment.
- Select a material in the material list — same treatment.
- Hover over unselected items shows a subtle background transition.
- The selected item's accent bar contrasts clearly with the hover background.

---

## Subphase 18C — RGB Color Swatch Circles

### Goal

Add a small circular color preview next to each RGB input group. When users adjust R/G/B values, the swatch shows the composed color in real-time. This provides immediate visual feedback without requiring the user to look at the viewport.

### Implementation Strategy

For each RGB group in `MainWindow.axaml`, add a 16×16 circle `Border` that displays the composed color. The swatch is updated in code-behind whenever any of the three channel values change.

### Step 1: Add Swatch to Environment Top Color

**File**: `MainWindow.axaml` — find the environment top color RGB group (in the Environment expander, Scene tab). Currently there's a label "Sky color" followed by three R/G/B rows. Add a swatch Border after the label:

Locate the grid row containing `Text="Sky color"` and the RGB inputs for `EnvTopRInput`, `EnvTopGInput`, `EnvTopBInput`. Add a swatch to the label row:

```xml
<Grid ColumnDefinitions="110,*,Auto">
    <TextBlock Grid.Column="0" Classes="param-label" Text="Sky color"/>
    <!-- The RGB rows are below in a sub-StackPanel -->
    <Border Grid.Column="2"
            x:Name="EnvTopColorSwatch"
            Width="16" Height="16"
            CornerRadius="8"
            Background="#0F1317"
            BorderBrush="{StaticResource BorderSubtleBrush}"
            BorderThickness="1"
            VerticalAlignment="Center"
            Margin="4,0,0,0"/>
</Grid>
```

If the label and RGB inputs share a different layout structure, adapt accordingly. The key is: a 16×16 circle Border with `x:Name="EnvTopColorSwatch"` placed visually near the RGB group.

Apply the same pattern to these RGB groups:

1. **Environment top color** (`EnvTopRInput/G/B`) → swatch name: `EnvTopColorSwatch`
2. **Environment bottom color** (`EnvBottomRInput/G/B`) → swatch name: `EnvBottomColorSwatch`
3. **Material base color** (`MaterialBaseRInput/G/B`) → swatch name: `MaterialBaseColorSwatch`
4. **Light color** (`LightRInput/G/B`) → swatch name: `LightColorSwatch`
5. **Indicator color** (`IndicatorColorRInput/G/B`) → swatch name: `IndicatorColorSwatch`
6. **Bloom tint** (`EnvBloomTintRInput/G/B`) → swatch name: `BloomTintColorSwatch`

Do NOT add swatches to:
- Collar material color (too nested, would break layout)
- Scratch expose color (rarely used, not worth the complexity)

### Step 2: Declare Swatch Fields in Code-Behind

**File**: `KnobForge.App/Views/MainWindow/MainWindow.cs` — add field declarations and `FindControl` calls:

In the field declarations section, add:
```csharp
private Border? _envTopColorSwatch;
private Border? _envBottomColorSwatch;
private Border? _materialBaseColorSwatch;
private Border? _lightColorSwatch;
private Border? _indicatorColorSwatch;
private Border? _bloomTintColorSwatch;
```

In the `FindControl` initialization section, add:
```csharp
_envTopColorSwatch = this.FindControl<Border>("EnvTopColorSwatch");
_envBottomColorSwatch = this.FindControl<Border>("EnvBottomColorSwatch");
_materialBaseColorSwatch = this.FindControl<Border>("MaterialBaseColorSwatch");
_lightColorSwatch = this.FindControl<Border>("LightColorSwatch");
_indicatorColorSwatch = this.FindControl<Border>("IndicatorColorSwatch");
_bloomTintColorSwatch = this.FindControl<Border>("BloomTintColorSwatch");
```

Do NOT add swatches to the `HasRequiredControls()` null check — they are optional visual elements.

### Step 3: Add Swatch Update Helper

**File**: `KnobForge.App/Views/MainWindow/MainWindow.cs` (or a suitable partial class file) — add a helper method:

```csharp
private void UpdateColorSwatch(Border? swatch, double r, double g, double b)
{
    if (swatch == null) return;
    byte rByte = (byte)Math.Clamp(r * 255.0, 0, 255);
    byte gByte = (byte)Math.Clamp(g * 255.0, 0, 255);
    byte bByte = (byte)Math.Clamp(b * 255.0, 0, 255);
    swatch.Background = new Avalonia.Media.SolidColorBrush(
        Avalonia.Media.Color.FromRgb(rByte, gByte, bByte));
}
```

### Step 4: Call Swatch Updates from Existing Handlers

The RGB inputs already trigger handler methods when values change. Add swatch update calls at the end of the existing handler paths:

**In `CommitEnvironmentStateFromUi`** (in `MainWindow.EnvironmentShadowReadouts.cs`), after the existing environment color commits, add:

```csharp
// Update environment color swatches
UpdateColorSwatch(_envTopColorSwatch,
    _envTopRInput?.Value ?? 0, _envTopGInput?.Value ?? 0, _envTopBInput?.Value ?? 0);
UpdateColorSwatch(_envBottomColorSwatch,
    _envBottomRInput?.Value ?? 0, _envBottomGInput?.Value ?? 0, _envBottomBInput?.Value ?? 0);
UpdateColorSwatch(_bloomTintColorSwatch,
    _envBloomTintRInput?.Value ?? 1, _envBloomTintGInput?.Value ?? 1, _envBloomTintBInput?.Value ?? 1);
```

**In the material color commit path** (wherever `_materialBaseRInput` values are committed to the project), add:

```csharp
UpdateColorSwatch(_materialBaseColorSwatch,
    _materialBaseRInput?.Value ?? 0, _materialBaseGInput?.Value ?? 0, _materialBaseBInput?.Value ?? 0);
```

**In the light color commit path** (wherever `_lightRInput` values are committed), add:

```csharp
UpdateColorSwatch(_lightColorSwatch,
    _lightRInput?.Value ?? 0, _lightGInput?.Value ?? 0, _lightBInput?.Value ?? 0);
```

**In the indicator color commit path**, add:

```csharp
UpdateColorSwatch(_indicatorColorSwatch,
    _indicatorColorRInput?.Value ?? 0, _indicatorColorGInput?.Value ?? 0, _indicatorColorBInput?.Value ?? 0);
```

### Step 5: Update Swatches on Inspector Refresh

When the inspector refreshes from project state (e.g., when switching scenes, undoing, loading a project), the swatches need to update too. Find the method that refreshes environment/material/light UI from project state and add swatch updates there as well. Look for methods like `RefreshInspectorFromProject`, `SyncSceneTab`, `SyncLightingTab`, etc.

### Verification

- Adjust the R/G/B sliders for environment sky color — the swatch circle updates in real-time.
- Switch to a different project type — the material base color swatch reflects the new color.
- The swatch circles are 16×16 pixels with a subtle border and rounded corners.
- Swatches display the correct composed color (not just one channel).
- Swatches update when switching scenes, undoing, or loading projects.
- No layout breakage — swatches fit within the existing grid structure.

---

## Subphase 18D — Tab Underline Refinement

### Goal

Thicken the selected tab indicator from 2px to 3px and round the ends for a more polished look. This is a small change with outsized visual impact — the tab strip is always visible and the underline is one of the most frequently seen UI elements.

### Step 1: Update TabItem Selected Style

**File**: `App.axaml` — update the `TabItem:selected` style:

```xml
<Style Selector="TabItem:selected">
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="0,0,0,3"/>
</Style>
```

Change: `BorderThickness` from `0,0,0,2` to `0,0,0,3`.

### Step 2: Add Transition to TabItem

**File**: `App.axaml` — add a foreground transition to TabItem for smoother tab switching:

```xml
<Style Selector="TabItem">
    <Setter Property="FontSize" Value="{StaticResource FontSubtitle}"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="10,8,10,8"/>
    <Setter Property="Foreground" Value="{StaticResource TextTertiaryBrush}"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Margin" Value="0"/>
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Foreground" Duration="0:0:0.12"/>
        </Transitions>
    </Setter>
</Style>
```

### Verification

- The selected tab underline is noticeably thicker (3px vs 2px).
- Switching between tabs shows a smooth foreground color transition.
- The underline color remains accent blue.
- All inspector tabs (Lighting, Model, Brush, Scene, Shadows, Graph) render correctly.

---

## File Touchpoint Table

| Subphase | File | Action |
|---|---|---|
| 18A | `App.axaml` | Add BrushTransition to Button, toolbar-button, toolbar-button-accent, ghost-button, Expander |
| 18B | `App.axaml` | Update ListBoxItem:selected (add left accent bar), add ListBoxItem transition |
| 18C | `MainWindow.axaml` | Add 6 color swatch Borders next to RGB groups |
| 18C | `MainWindow.cs` | Declare swatch fields, FindControl calls |
| 18C | `MainWindow.EnvironmentShadowReadouts.cs` | Add UpdateColorSwatch calls for env colors and bloom tint |
| 18C | Material/Light/Indicator commit paths | Add UpdateColorSwatch calls |
| 18D | `App.axaml` | Update TabItem:selected border thickness, add TabItem transition |

## Appendix: Interaction State Summary After Phase 18

### Transition Durations

| Element | Duration | Properties |
|---|---|---|
| Default Button | 150ms | Background, BorderBrush |
| Toolbar Button | 120ms | Background, BorderBrush, Foreground |
| Toolbar Accent | 120ms | Background, Foreground |
| Ghost Button | 120ms | Background, BorderBrush, Foreground |
| Expander stripe | 150ms | BorderBrush |
| ListBoxItem | 100ms | Background |
| TabItem | 120ms | Foreground |

### Selection Indicators

| Element | Indicator | Size |
|---|---|---|
| ListBoxItem selected | Left accent bar | 3px |
| TabItem selected | Bottom accent underline | 3px |
| Expander primary | Left accent stripe | 3px |

### Color Swatches

| Location | Swatch Name | RGB Source |
|---|---|---|
| Environment sky color | `EnvTopColorSwatch` | EnvTopR/G/B |
| Environment ground color | `EnvBottomColorSwatch` | EnvBottomR/G/B |
| Material base color | `MaterialBaseColorSwatch` | MaterialBaseR/G/B |
| Light color | `LightColorSwatch` | LightR/G/B |
| Indicator color | `IndicatorColorSwatch` | IndicatorColorR/G/B |
| Bloom tint | `BloomTintColorSwatch` | EnvBloomTintR/G/B |
