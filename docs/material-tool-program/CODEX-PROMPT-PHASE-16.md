# Phase 16: Toolbar, Buttons, Controls & Input Behavior

## Your Role

You are implementing Phase 16 of the Monozukuri Material Tool Transformation. Phase 15 established the foundation (tokens, radii, typography). Phase 16 builds on that foundation with concrete control-level changes: a modernized toolbar, refined button chrome, updated control styling, and a critical scroll-to-adjust fix for the ValueInput control.

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–15 are complete. Phase 15 updated surface tokens, border colors, corner radii, typography, and spacing. The app now has wider surface level gaps, 8px button radii, 6px input radii, 11px labels, and 14px inter-section spacing. But the toolbar is still plain text buttons, buttons are uniformly styled with no hierarchy, and the ValueInput control has a UX issue where users must click to focus before scroll-to-adjust works.

## What Phase 16 Does

### Four Subphases (execute in order):

1. **16A — ValueInput Scroll-to-Adjust Fix**: Fix the PointerWheelChanged routing so users can scroll-to-adjust ValueInput controls without clicking first.
2. **16B — Toolbar Modernization**: Restyle the toolbar with icon-label pill buttons and an accent Render CTA.
3. **16C — Button Chrome Refinement**: Introduce button hierarchy (ghost, default, accent), reduce visual weight on default buttons, add border-on-hover behavior.
4. **16D — ComboBox, TextBox & CheckBox Updates**: Subtle inner shadow on input fields, accent-colored checkbox indicator, reduced control heights.

**Explicitly deferred** (do NOT implement):
- Expander restyling (Phase 17)
- Interaction state animations (Phase 18)
- Color swatch circles (Phase 18)

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT change any `x:Name` values.** Code-behind resolves controls by name.
2. **Do NOT change handler wiring in AXAML.** Event handlers keep their existing signatures.
3. **Do NOT modify Core or Rendering code.** Only `Controls/ValueInput.cs`, `App.axaml`, and `MainWindow.axaml` are touched.
4. **The app must compile and run after each subphase.**
5. **Preserve all existing token names.** You may change values and add new tokens, but never remove or rename existing ones.
6. **Test with all 5 project types** (RotaryKnob, ThumbSlider, FlipSwitch, PushButton, IndicatorLight) to ensure no layout breakage.

---

## Existing Architecture (Read Before Coding)

### ValueInput Control (`KnobForge.App/Controls/ValueInput.cs`)

The `ValueInput` is a custom `UserControl` that combines a drag-to-adjust TextBox with scroll-wheel support. Key details:

- **Constructor (line 157)**: Registers the scroll handler on **Bubble** routing strategy:
  ```csharp
  AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Bubble);
  ```
- **OnPointerWheelChanged (lines 343-356)**: Reads `e.Delta.Y`, scales by `Step` and modifier multiplier, updates `Value`, sets `e.Handled = true`. **No focus or IsPointerOver check.**
- **Modifier multiplier (lines 445-464)**: Shift=0.2x, Ctrl/Meta=0.25x, Alt=5.0x, default=1.0x.
- **The problem**: Every inspector tab wraps its content in a `<ScrollViewer VerticalScrollBarVisibility="Auto">`. The ScrollViewer handles `PointerWheelChanged` on the Bubble phase *before* the ValueInput's bubble handler fires. The user must click the ValueInput first to give it focus, which changes event routing to prioritize the focused control. This makes scroll-to-adjust feel broken.

### Current Toolbar (`MainWindow.axaml` lines 1-76)

```xml
<Border Grid.Row="0" Background="{StaticResource Surface0Brush}" ...>
    <Grid ColumnDefinitions="Auto,*,Auto">
        <!-- Left: App name badge + project status -->
        <StackPanel Orientation="Horizontal" Spacing="10">
            <Border Background="{StaticResource Surface0Brush}"
                    BorderBrush="{StaticResource BorderDefaultBrush}"
                    CornerRadius="6" Padding="8,4">
                <TextBlock Classes="title" Text="Monozukuri"/>
            </Border>
            <TextBlock x:Name="TopProjectStatusText" Classes="hint" Text="Project: Untitled"/>
        </StackPanel>

        <!-- Right: 6 text-only buttons -->
        <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="8">
            <Button x:Name="NewProjectButton" Content="New" MinWidth="72"/>
            <Button x:Name="ChangeProjectTypeButton" Content="Change type..." MinWidth="140"/>
            <Button x:Name="OpenProjectButton" Content="Open..." MinWidth="100"/>
            <Button x:Name="SaveProjectButton" Content="Save" MinWidth="72"/>
            <Button x:Name="SaveProjectAsButton" Content="Save as..." MinWidth="140"/>
            <Button x:Name="RenderButton" Content="Render..." MinWidth="100"/>
        </StackPanel>
    </Grid>
</Border>
```

All buttons use the same default style. No visual hierarchy between routine actions (Save) and the primary CTA (Render).

### Current Button Style (`App.axaml` lines 207-224)

```xml
<Style Selector="Button">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>  <!-- Phase 15 changes to RadiusMd (8) -->
    <Setter Property="Padding" Value="10,4"/>
    <Setter Property="FontSize" Value="{StaticResource FontLabel}"/>
</Style>

<Style Selector="Button:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface3Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderStrongBrush}"/>
</Style>

<Style Selector="Button:pressed">
    <Setter Property="Background" Value="{StaticResource Surface1Brush}"/>
</Style>
```

### Current ComboBox, TextBox, CheckBox Styles (`App.axaml`)

- **ComboBox**: `Background=Surface0`, `BorderBrush=BorderDefault`, `CornerRadius=4`, `Height=ControlHeight (28)`.
- **TextBox**: `Background=Surface0`, `BorderBrush=BorderDefault`, `CornerRadius=4`. Focus state adds `AccentBrush` border.
- **CheckBox**: `Foreground=TextSecondary`, `FontSize=FontLabel`.

### Files to Modify

| Subphase | File | What Changes |
|---|---|---|
| 16A | `KnobForge.App/Controls/ValueInput.cs` | Scroll event routing fix |
| 16B | `MainWindow.axaml` | Toolbar layout, button classes, Render button accent |
| 16B | `App.axaml` | Add `toolbar-button` and `toolbar-button-accent` styles |
| 16C | `App.axaml` | Button default style changes, add `ghost-button` class |
| 16D | `App.axaml` | ComboBox/TextBox inner shadow, CheckBox accent, control heights |

---

## Subphase 16A — ValueInput Scroll-to-Adjust Fix

### Goal

Allow users to scroll-to-adjust any ValueInput control the cursor is hovering over, without clicking first. This is the standard UX in creative tools (Blender, Substance, Houdini).

### The Fix

**File**: `KnobForge.App/Controls/ValueInput.cs`

**Step 1**: Change the event registration from Bubble to Tunnel routing strategy. Tunnel events fire *before* Bubble events, so the ValueInput will see the scroll event before the parent ScrollViewer.

Change line 157 from:
```csharp
AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Bubble);
```
to:
```csharp
AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
```

**Step 2**: Add an `IsPointerOver` guard to the handler so it only intercepts scroll events when the cursor is actually over the control. Without this guard, a ValueInput would steal scroll events from the entire visual tree during tunneling.

Change the handler at lines 343-356 from:
```csharp
private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
{
    _lastKnownModifiers = e.KeyModifiers;
    double deltaY = e.Delta.Y;
    if (Math.Abs(deltaY) < double.Epsilon)
    {
        return;
    }

    bool discrete = Math.Abs(Math.Abs(deltaY) - 1.0) < 0.001;
    double steps = discrete ? deltaY * 4.0 : deltaY * 16.0;
    Value = SanitizeValue(Value + steps * Step * GetModifierMultiplier(e.KeyModifiers));
    e.Handled = true;
}
```

to:
```csharp
private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
{
    if (!IsPointerOver)
    {
        return;
    }

    _lastKnownModifiers = e.KeyModifiers;
    double deltaY = e.Delta.Y;
    if (Math.Abs(deltaY) < double.Epsilon)
    {
        return;
    }

    bool discrete = Math.Abs(Math.Abs(deltaY) - 1.0) < 0.001;
    double steps = discrete ? deltaY * 4.0 : deltaY * 16.0;
    Value = SanitizeValue(Value + steps * Step * GetModifierMultiplier(e.KeyModifiers));
    e.Handled = true;
}
```

### Why This Works

Avalonia routes pointer events in two phases: Tunnel (parent→child) then Bubble (child→parent). The parent `ScrollViewer` uses Bubble routing. By registering on Tunnel, the ValueInput sees the event first. The `IsPointerOver` guard ensures it only consumes the event when the cursor is directly over the control — otherwise it passes through and the ScrollViewer scrolls the panel normally.

### Verification

- Hover over any ValueInput in the inspector and scroll — the value should change without clicking first.
- Scroll over empty space between controls — the inspector panel should scroll normally.
- Click a ValueInput and scroll — should still work (focus + tunnel both active).
- Modifier keys (Shift for fine, Alt for coarse) should still work.
- All 5 project types should work normally.

---

## Subphase 16B — Toolbar Modernization

### Goal

Transform the toolbar from 6 identical text-only buttons into a modern toolbar with pill-shaped tool buttons and an accent-colored primary CTA (Render). The Render button should be visually prominent as the app's primary action.

### Step 1: Add Toolbar Button Styles

**File**: `App.axaml` — add these styles after the existing `Button:pressed` style:

```xml
<!-- Toolbar pill button: ghost by default, border on hover -->
<Style Selector="Button.toolbar-button">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    <Setter Property="Padding" Value="12,6"/>
    <Setter Property="MinWidth" Value="0"/>
    <Setter Property="MinHeight" Value="0"/>
</Style>

<Style Selector="Button.toolbar-button:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
</Style>

<Style Selector="Button.toolbar-button:pressed">
    <Setter Property="Background" Value="{StaticResource Surface1Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
</Style>

<!-- Toolbar accent CTA button -->
<Style Selector="Button.toolbar-button-accent">
    <Setter Property="Background" Value="{StaticResource AccentSubtleBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="Padding" Value="14,6"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="MinWidth" Value="0"/>
    <Setter Property="MinHeight" Value="0"/>
</Style>

<Style Selector="Button.toolbar-button-accent:pointerover">
    <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="Foreground" Value="#FFFFFF"/>
</Style>

<Style Selector="Button.toolbar-button-accent:pressed">
    <Setter Property="Background" Value="{StaticResource AccentSubtleBrush}"/>
</Style>
```

### Step 2: Apply Classes to Toolbar Buttons

**File**: `MainWindow.axaml` — update the toolbar buttons (lines ~44-73)

Add `Classes="toolbar-button"` to New, Change type, Open, Save, Save as. Add `Classes="toolbar-button-accent"` to Render. Remove the `MinWidth` attributes (the pill style handles sizing via padding):

```xml
<Button x:Name="NewProjectButton"
        Classes="toolbar-button"
        Content="New"
        ToolTip.Tip="Open a new Monozukuri window"/>

<Button x:Name="ChangeProjectTypeButton"
        Classes="toolbar-button"
        Content="Change type..."
        ToolTip.Tip="Switch this project between Rotary Knob, Flip Switch, Thumb Slider, or Push Button"/>

<Button x:Name="OpenProjectButton"
        Classes="toolbar-button"
        Content="Open..."
        ToolTip.Tip="Open an existing .knob project"/>

<Button x:Name="SaveProjectButton"
        Classes="toolbar-button"
        Content="Save"
        ToolTip.Tip="Save current project"/>

<Button x:Name="SaveProjectAsButton"
        Classes="toolbar-button"
        Content="Save as..."
        ToolTip.Tip="Save current project to a new file"/>

<Button x:Name="RenderButton"
        Classes="toolbar-button-accent"
        Content="Render..."
        ToolTip.Tip="Open spritesheet render window"/>
```

### Verification

- Toolbar buttons appear as ghost text (no background, secondary text color) at rest.
- Hovering shows a subtle pill background with border.
- Render button is visually distinct: accent background + accent border at rest, brighter on hover.
- All toolbar buttons still function (New, Open, Save, Save as, Change type, Render).
- No `x:Name` values changed.

---

## Subphase 16C — Button Chrome Refinement

### Goal

Reduce the visual weight of default buttons throughout the inspector. Currently all buttons (including small inline actions like "Clear," "Browse," "Reset") have the same heavy chrome as primary actions. Introduce a ghost button class for inline/secondary actions.

### Step 1: Soften the Default Button Style

**File**: `App.axaml` — update the existing `Button` base style:

Change the default button from solid `Surface2` background with visible border to a subtler style:

```xml
<Style Selector="Button">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>  <!-- was BorderDefault -->
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>  <!-- Phase 15 may have updated this -->
    <Setter Property="Padding" Value="10,4"/>
    <Setter Property="FontSize" Value="{StaticResource FontLabel}"/>
</Style>
```

The only change: `BorderDefault` → `BorderSubtle`. This makes the default button border less prominent, creating a calmer inspector surface.

### Step 2: Add Ghost Button Style

**File**: `App.axaml` — add after the Button styles:

```xml
<!-- Ghost button for inline/secondary actions (Clear, Browse, Reset, etc.) -->
<Style Selector="Button.ghost-button">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    <Setter Property="Padding" Value="8,4"/>
</Style>

<Style Selector="Button.ghost-button:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
</Style>

<Style Selector="Button.ghost-button:pressed">
    <Setter Property="Background" Value="{StaticResource Surface1Brush}"/>
</Style>
```

### Step 3: Apply Ghost Class to Inline Buttons

**File**: `MainWindow.axaml` — add `Classes="ghost-button"` to these button categories:

1. **Texture map Clear/Browse buttons**: Search for buttons with Content containing "Clear" or "Browse" that sit inline with texture map paths. These are the `_materialAlbedoMapClearButton`, `_materialAlbedoMapBrowseButton`, `_materialNormalMapClearButton`, `_materialNormalMapBrowseButton`, `_materialRoughnessMapClearButton`, `_materialRoughnessMapBrowseButton`, `_materialMetallicMapClearButton`, `_materialMetallicMapBrowseButton`.

2. **Library refresh buttons**: `_refreshCollarLibraryButton`, `_refreshSliderLibraryButton`, `_refreshPushButtonLibraryButton`, `_refreshToggleLibraryButton`.

3. **Clear paint mask button**: `_clearPaintMaskButton`.

4. **Debug buttons** (Reset all, Print state in the Debug axes section): `_debugResetAxesButton`, `_debugPrintStateButton`.

Do NOT apply ghost to:
- `_addLightButton`, `_removeLightButton` (structural actions, keep default)
- `_renderButton` (has its own accent class from 16B)
- `_saveReferenceProfileButton` (structural action)
- `_centerLightButton` (structural action)

### Verification

- Default buttons throughout the inspector have a softer, less prominent border.
- Clear/Browse/Refresh/Reset buttons appear as ghost text, showing a pill on hover.
- The visual hierarchy is clear: accent (Render) > default (Add light, Save) > ghost (Clear, Browse, Reset).
- All buttons still function correctly.

---

## Subphase 16D — ComboBox, TextBox & CheckBox Updates

### Goal

Refine the remaining form controls for a consistent modern feel. Add a subtle inner shadow to input fields (ComboBox, TextBox) to make them feel "inset" into the surface. Update the CheckBox to use the accent color for its checked indicator. Reduce the ComboBox height slightly.

### Step 1: Update ComboBox Style

**File**: `App.axaml` — update the ComboBox style:

```xml
<Style Selector="ComboBox">
    <Setter Property="Background" Value="{StaticResource Surface0Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>  <!-- was BorderDefault -->
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>  <!-- Phase 15 may have updated this -->
    <Setter Property="Height" Value="26"/>  <!-- was 28 -->
</Style>

<Style Selector="ComboBox:pointerover">
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
</Style>

<Style Selector="ComboBox:focus">
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
</Style>
```

### Step 2: Update TextBox Style

**File**: `App.axaml` — update the TextBox style:

```xml
<Style Selector="TextBox">
    <Setter Property="Background" Value="{StaticResource Surface0Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>  <!-- was BorderDefault -->
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>  <!-- Phase 15 may have updated this -->
</Style>

<Style Selector="TextBox:pointerover">
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
</Style>

<Style Selector="TextBox:focus">
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
</Style>
```

### Step 3: Update CheckBox Checked State

**File**: `App.axaml` — add checked state style after the existing CheckBox style:

```xml
<Style Selector="CheckBox">
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    <Setter Property="FontSize" Value="{StaticResource FontLabel}"/>
</Style>

<Style Selector="CheckBox:checked /template/ Border#NormalRectangle">
    <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
</Style>
```

This makes the checkbox indicator fill with the accent color when checked, instead of the default Fluent theme blue.

### Step 4: Update ValueInput Internal Styling

**File**: `KnobForge.App/Controls/ValueInput.cs` — in the constructor where the `_rootBorder` is created, update the border to use `BorderSubtle` level instead of the hardcoded field colors, to match the new softer input field borders. Locate the `_rootBorder` creation (around lines 139-145):

```csharp
_rootBorder = new Border
{
    Background = new SolidColorBrush(FieldBackgroundColor),
    BorderBrush = new SolidColorBrush(FieldBorderColor),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(2.5),
    Child = contentGrid
};
```

Update the `CornerRadius` to `4` to align with the Phase 15 `RadiusSm` level (ValueInput is a custom control that doesn't use AXAML styles, so its radius must be set in code):

```csharp
CornerRadius = new CornerRadius(4),
```

Do NOT change `FieldBackgroundColor` or `FieldBorderColor` — these are private constants defined elsewhere in the file and may be used for other purposes.

### Verification

- ComboBoxes are slightly shorter (26px vs 28px) and have softer borders.
- TextBoxes show softer borders at rest, stronger on hover, accent on focus.
- Checked checkboxes show the accent blue fill instead of default Fluent blue.
- ValueInput corners are slightly more rounded (4px vs 2.5px).
- No control layout breakage across all 5 project types.
- ComboBox dropdown still opens and displays correctly.

---

## File Touchpoint Table

| Subphase | File | Action |
|---|---|---|
| 16A | `KnobForge.App/Controls/ValueInput.cs` | Change scroll handler from Bubble→Tunnel routing, add IsPointerOver guard |
| 16B | `App.axaml` | Add `toolbar-button` and `toolbar-button-accent` styles |
| 16B | `MainWindow.axaml` | Add Classes to toolbar buttons, remove MinWidth, accent on Render |
| 16C | `App.axaml` | Soften default Button border, add `ghost-button` style |
| 16C | `MainWindow.axaml` | Add `Classes="ghost-button"` to inline/secondary buttons |
| 16D | `App.axaml` | Update ComboBox (height, border), TextBox (hover/focus), CheckBox (accent checked) |
| 16D | `KnobForge.App/Controls/ValueInput.cs` | Update CornerRadius from 2.5 to 4 |

## Appendix: Style Summary After Phase 16

### Button Hierarchy

| Style | Rest State | Hover State | Use Case |
|---|---|---|---|
| Default `Button` | Surface2 bg, BorderSubtle border | Surface3 bg, BorderStrong border | Inspector structural actions (Add/Remove light, Save profile) |
| `Button.ghost-button` | Transparent bg+border | Surface2 bg, BorderSubtle border | Inline actions (Clear, Browse, Reset, Refresh) |
| `Button.toolbar-button` | Transparent bg+border, TextSecondary | Surface2 bg, BorderSubtle border | Toolbar actions (New, Open, Save) |
| `Button.toolbar-button-accent` | AccentSubtle bg, Accent border | Accent bg, white text | Primary CTA (Render) |

### Control Border Strategy

| Control | Rest | Hover | Focus |
|---|---|---|---|
| ComboBox | BorderSubtle | BorderDefault | Accent |
| TextBox | BorderSubtle | BorderDefault | Accent |
| ValueInput | FieldBorderColor (code) | — (drag behavior) | AccentBrush (code) |
| Button | BorderSubtle | BorderStrong | — |
