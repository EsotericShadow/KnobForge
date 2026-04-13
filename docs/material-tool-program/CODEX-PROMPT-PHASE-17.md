# Phase 17: Inspector Sections, Expander Restyle & Spacing

## Your Role

You are implementing Phase 17 of the Monozukuri Material Tool Transformation. Phase 15 established tokens, radii, and typography. Phase 16 modernized the toolbar, buttons, and input controls. Phase 17 tackles the element that dominates every inspector tab: Expander headers. The current chunky Fluent-default expander boxes make the inspector feel heavy and dated. This phase transforms them into thin, elegant section-header bars with a left accent stripe, adds generous breathing room between sections, and introduces a lighter "advanced" sub-section treatment.

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–16 are complete. The app now has a Dark Slate surface token system, 8px button radii, ghost/default/accent button hierarchy, scroll-to-adjust ValueInput, and a modernized toolbar. But the inspector is still dominated by chunky Fluent-default Expander headers that visually compete with the actual parameter content.

## What Phase 17 Does

### Three Subphases (execute in order):

1. **17A — Expander Header Restyle**: Transform expander headers from full-width boxes into thin section-header bars with a left accent stripe.
2. **17B — Inter-Section Spacing & Separator Treatment**: Add generous vertical spacing between expanders and optional thin separator lines.
3. **17C — Advanced Sub-Section Treatment**: Style expanders whose Header contains "· Advanced" with a lighter, recessed appearance.

**Explicitly deferred** (do NOT implement):
- Animated expand/collapse transitions (Phase 18)
- Hover glow effects (Phase 18)
- Color swatch circles (Phase 18)

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT change any `x:Name` values.** Code-behind resolves controls by name.
2. **Do NOT change handler wiring.** Event handlers keep their existing signatures.
3. **Do NOT modify Core, Rendering, or Controls code.** This is purely visual.
4. **Do NOT change any `Header=` text values on Expanders.** The header strings are referenced in code.
5. **The app must compile and run after each subphase.**
6. **Preserve all existing token names.** You may change values and add new tokens, but never remove or rename existing ones.
7. **Test with all 5 project types** to ensure no layout breakage.

---

## Existing Architecture (Read Before Coding)

### Current Expander Styling

The Fluent theme provides the default Expander template. The app overrides the header colors via resource brushes in `App.axaml`:

```xml
<!-- Resource brushes (Fluent theme picks these up automatically) -->
<SolidColorBrush x:Key="ExpanderHeaderBackground" Color="{StaticResource Surface2}"/>
<SolidColorBrush x:Key="ExpanderHeaderBackgroundPointerOver" Color="{StaticResource Surface3}"/>
<SolidColorBrush x:Key="ExpanderHeaderBackgroundPressed" Color="{StaticResource Surface3}"/>
<SolidColorBrush x:Key="ExpanderHeaderBorderBrush" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderHeaderBorderBrushPointerOver" Color="{StaticResource BorderSubtle}"/>
<SolidColorBrush x:Key="ExpanderHeaderBorderBrushPressed" Color="{StaticResource BorderSubtle}"/>
<SolidColorBrush x:Key="ExpanderHeaderForeground" Color="{StaticResource TextPrimary}"/>
<SolidColorBrush x:Key="ExpanderHeaderForegroundPointerOver" Color="{StaticResource TextPrimary}"/>
<SolidColorBrush x:Key="ExpanderHeaderForegroundPressed" Color="{StaticResource TextPrimary}"/>
<SolidColorBrush x:Key="ExpanderHeaderForegroundDisabled" Color="{StaticResource TextTertiary}"/>
<SolidColorBrush x:Key="ExpanderContentBackground" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderContentBorderBrush" Color="Transparent"/>
```

Additional style overrides:
```xml
<Style Selector="Expander">
    <Setter Property="Margin" Value="0,2,0,2"/>
</Style>

<Style Selector="Expander /template/ ToggleButton TextBlock">
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="FontSize" Value="{StaticResource FontSubtitle}"/>
</Style>

<Style Selector="Expander /template/ Border#ExpanderContent">
    <Setter Property="Padding" Value="4,4,0,0"/>
</Style>
```

### Current Expander Usage Pattern

There are 33 Expanders across the inspector. They follow two patterns:

1. **Primary sections** (always visible): `Header="Setup"`, `Header="Environment"`, `Header="Tone mapping"`, `Header="Lights"`, `Header="Transform"`, etc. Typically `IsExpanded="True"`.

2. **Advanced sub-sections**: `Header="Geometry · Advanced"`, `Header="Color · Advanced"`, `Header="HDRI · Advanced"`, `Header="Bloom · Advanced"`, etc. Typically `IsExpanded="False"`. These contain less frequently used controls.

### Inspector Tab Structure

Each tab wraps content in:
```xml
<ScrollViewer VerticalScrollBarVisibility="Auto">
    <StackPanel Spacing="10" Margin="4">
        <Expander Header="..." IsExpanded="True">...</Expander>
        <Expander Header="..." IsExpanded="False">...</Expander>
        ...
    </StackPanel>
</ScrollViewer>
```

The `Spacing="10"` on the StackPanel controls inter-section gaps. Phase 15 may have updated `ExpanderSpacing` to 14, but the hardcoded AXAML values need to match.

### Files to Modify

| Subphase | File | What Changes |
|---|---|---|
| 17A | `App.axaml` | Expander header resource brushes, Expander style overrides |
| 17B | `App.axaml` | Expander margin update |
| 17B | `MainWindow.axaml` | StackPanel Spacing updates in all tab ScrollViewers |
| 17C | `App.axaml` | Add `advanced-expander` class styles |
| 17C | `MainWindow.axaml` | Add `Classes="advanced-expander"` to all "· Advanced" expanders |

---

## Subphase 17A — Expander Header Restyle

### Goal

Transform expander headers from chunky full-width boxes into thin, minimal section-header bars. The key visual change: a 3px left accent stripe on the header, reduced vertical padding, and a flatter background that blends more with the panel surface.

### Step 1: Update Expander Header Resource Brushes

**File**: `App.axaml` — update the existing resource brushes:

```xml
<SolidColorBrush x:Key="ExpanderHeaderBackground" Color="{StaticResource Surface1}"/>
<SolidColorBrush x:Key="ExpanderHeaderBackgroundPointerOver" Color="{StaticResource Surface2}"/>
<SolidColorBrush x:Key="ExpanderHeaderBackgroundPressed" Color="{StaticResource Surface2}"/>
<SolidColorBrush x:Key="ExpanderHeaderBorderBrush" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderHeaderBorderBrushPointerOver" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderHeaderBorderBrushPressed" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderHeaderForeground" Color="{StaticResource TextPrimary}"/>
<SolidColorBrush x:Key="ExpanderHeaderForegroundPointerOver" Color="{StaticResource TextPrimary}"/>
<SolidColorBrush x:Key="ExpanderHeaderForegroundPressed" Color="{StaticResource TextPrimary}"/>
<SolidColorBrush x:Key="ExpanderHeaderForegroundDisabled" Color="{StaticResource TextTertiary}"/>
<SolidColorBrush x:Key="ExpanderContentBackground" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderContentBorderBrush" Color="Transparent"/>
```

Key changes:
- `ExpanderHeaderBackground`: `Surface2` → `Surface1` (headers blend with the panel instead of floating above it)
- `ExpanderHeaderBackgroundPointerOver`: `Surface3` → `Surface2` (subtler hover)
- `ExpanderHeaderBorderBrushPointerOver/Pressed`: `BorderSubtle` → `Transparent` (no border, cleaner look)

### Step 2: Add Left Accent Stripe via Border Style

**File**: `App.axaml` — update the Expander style to add a left border that acts as the accent stripe:

Replace the existing Expander style block:
```xml
<Style Selector="Expander">
    <Setter Property="Margin" Value="0,2,0,2"/>
</Style>
```

With:
```xml
<Style Selector="Expander">
    <Setter Property="Margin" Value="0,2,0,2"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentSubtleBrush}"/>
    <Setter Property="BorderThickness" Value="3,0,0,0"/>
    <Setter Property="CornerRadius" Value="0"/>
</Style>

<Style Selector="Expander:pointerover">
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
</Style>
```

This adds a 3px left border in the accent-subtle color, brightening to full accent on hover. The `CornerRadius="0"` ensures sharp corners for the section-bar look.

### Step 3: Reduce Header Vertical Padding

**File**: `App.axaml` — add a ToggleButton height constraint to make headers thinner:

```xml
<Style Selector="Expander /template/ ToggleButton">
    <Setter Property="MinHeight" Value="28"/>
    <Setter Property="Padding" Value="8,4,8,4"/>
</Style>
```

### Step 4: Increase Content Left Padding

**File**: `App.axaml` — update the content padding to align with the accent stripe:

Change:
```xml
<Style Selector="Expander /template/ Border#ExpanderContent">
    <Setter Property="Padding" Value="4,4,0,0"/>
</Style>
```

To:
```xml
<Style Selector="Expander /template/ Border#ExpanderContent">
    <Setter Property="Padding" Value="6,6,0,2"/>
</Style>
```

Slightly more top and bottom breathing room in the content area.

### Verification

- Expander headers appear as thin bars that blend with the panel background.
- A 3px accent-subtle left stripe is visible on every expander.
- Hovering an expander brightens the left stripe to full accent blue.
- The expand/collapse chevron still works and is visible.
- Header text is SemiBold and clearly readable.
- Content is indented slightly more from the left edge.
- No layout breakage across all 5 project types.

---

## Subphase 17B — Inter-Section Spacing & Separator Treatment

### Goal

Add more vertical breathing room between expander sections. The inspector should feel organized by *space* — clear gaps between conceptual groups of controls.

### Step 1: Update Expander Margin

**File**: `App.axaml` — update the Expander margin (from 17A):

Change the Expander margin from `0,2,0,2` to `0,3,0,3`:

```xml
<Style Selector="Expander">
    <Setter Property="Margin" Value="0,3,0,3"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentSubtleBrush}"/>
    <Setter Property="BorderThickness" Value="3,0,0,0"/>
    <Setter Property="CornerRadius" Value="0"/>
</Style>
```

This adds 6px total vertical gap between adjacent expanders (3 bottom + 3 top).

### Step 2: Update StackPanel Spacing in All Tabs

**File**: `MainWindow.axaml` — update the `Spacing` on the StackPanels that are direct children of the tab ScrollViewers. Find every instance of this pattern:

```xml
<ScrollViewer VerticalScrollBarVisibility="Auto">
    <StackPanel Spacing="10" Margin="4">
```

And change `Spacing="10"` to `Spacing="6"`. The combined effect of the expander margin (6px) plus the StackPanel spacing (6px) gives 12px between sections, which is generous without being wasteful.

There are 7 tab ScrollViewers in MainWindow.axaml:
1. Lighting tab (line ~221)
2. Model tab (line ~349)
3. Brush tab (line ~1899)
4. Scene tab (line ~2159)
5. Shadows tab (line ~2368)
6. Graph tab (line ~2434)

Update all of them. Be careful: some StackPanels inside expander content areas also have `Spacing` values — do NOT change those (they control intra-section row spacing, not inter-section gaps).

**How to identify the right StackPanels**: They are always the DIRECT child of a `<ScrollViewer>`, and they contain `<Expander>` elements as children. StackPanels inside expander content (containing Grid rows with param-labels and ValueInputs) should be left alone.

### Step 3: Update Section Tag Top Margin

**File**: `App.axaml` — increase the section-tag top margin for more visual separation before section tags:

The current section-tag style has `Margin="0,10,0,2"`. Update to:

```xml
<Style Selector="TextBlock.section-tag">
    <Setter Property="FontSize" Value="{StaticResource FontSectionTag}"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{StaticResource TextTertiaryBrush}"/>
    <Setter Property="Margin" Value="0,16,0,4"/>
    <Setter Property="LetterSpacing" Value="1"/>
</Style>
```

More top margin (16px) creates a clear break before a new conceptual group. Slightly more bottom margin (4px) gives the tag label some breathing room above the content it labels.

### Verification

- There is visible space between each expander section.
- The inspector feels "airy" — sections are clearly separated without needing divider lines.
- Section tags (e.g., "MATERIAL", "GEOMETRY") have generous top margin, creating clear conceptual groups.
- Scrolling the inspector panel feels natural — content isn't cramped.
- No layout breakage across all 5 project types.

---

## Subphase 17C — Advanced Sub-Section Treatment

### Goal

The "· Advanced" expanders should be visually lighter than primary sections. They contain less frequently used controls and should recede from visual attention. This is achieved by removing the accent stripe and using a more recessed header background.

### Step 1: Add Advanced Expander Class Style

**File**: `App.axaml` — add new styles after the base Expander styles:

```xml
<!-- Advanced sub-section expanders: lighter, no accent stripe -->
<Style Selector="Expander.advanced-expander">
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Margin" Value="0,1,0,1"/>
</Style>

<Style Selector="Expander.advanced-expander:pointerover">
    <Setter Property="BorderBrush" Value="Transparent"/>
</Style>

<Style Selector="Expander.advanced-expander /template/ ToggleButton TextBlock">
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    <Setter Property="FontSize" Value="{StaticResource FontLabel}"/>
</Style>
```

Key differences from primary expanders:
- No left accent stripe (`BorderBrush=Transparent`, `BorderThickness=0`)
- Less margin (1px vs 3px)
- Header text is normal weight and secondary color instead of SemiBold primary
- Slightly smaller font (FontLabel instead of FontSubtitle)

### Step 2: Apply Class to Advanced Expanders

**File**: `MainWindow.axaml` — add `Classes="advanced-expander"` to every Expander whose `Header` contains "· Advanced". These are:

1. `Header="Color · Advanced"` (Lighting tab)
2. `Header="Artistic · Advanced"` (Lighting tab)
3. `Header="Geometry · Advanced"` (Model tab — appears in multiple project type panels)
4. `Header="Base &amp; plate · Advanced"` (Toggle model)
5. `Header="Lever &amp; pivot · Advanced"` (Toggle model)
6. `Header="Tip sleeve · Advanced"` (Toggle model)
7. `Header="Lens material · Advanced"` (Indicator model)
8. `Header="Dynamic lights · Advanced"` (Indicator model)
9. `Header="Emitter sources · Advanced"` (Indicator model)
10. `Header="HDRI · Advanced"` (Scene tab)
11. `Header="Bloom · Advanced"` (Scene tab)
12. `Header="Appearance · Advanced"` (Shadows tab)

For each one, add `Classes="advanced-expander"`:

```xml
<!-- Before -->
<Expander Header="Geometry · Advanced" IsExpanded="False">

<!-- After -->
<Expander Header="Geometry · Advanced" IsExpanded="False" Classes="advanced-expander">
```

Do NOT apply this class to:
- `Header="Camera"` (not an "Advanced" section, just collapsed by default)
- `Header="Debug axes"` (structural, not "Advanced")
- `Header="Reflections"` (structural)
- `Header="Preview"` (structural)
- Any expander that doesn't have "· Advanced" in its Header

### Verification

- Primary expanders have the accent-blue left stripe.
- "· Advanced" expanders have NO left stripe and their header text is smaller, lighter weight, and secondary color.
- The visual hierarchy is clear: primary sections demand attention, advanced sections recede.
- Expanding/collapsing advanced sections still works correctly.
- All "· Advanced" expanders across all tabs have the new class applied.
- No layout breakage across all 5 project types.

---

## File Touchpoint Table

| Subphase | File | Action |
|---|---|---|
| 17A | `App.axaml` | Update ExpanderHeader resource brushes, update Expander style (accent stripe, reduced padding), update content padding |
| 17B | `App.axaml` | Update Expander margin, update section-tag margin |
| 17B | `MainWindow.axaml` | Update StackPanel `Spacing` in all 7 tab ScrollViewers (10→6) |
| 17C | `App.axaml` | Add `advanced-expander` class styles |
| 17C | `MainWindow.axaml` | Add `Classes="advanced-expander"` to all 12 "· Advanced" expanders |

## Appendix: Visual Hierarchy Summary After Phase 17

### Expander Hierarchy

| Type | Left Stripe | Header Background | Header Text | Margin |
|---|---|---|---|---|
| Primary | 3px AccentSubtle → Accent on hover | Surface1 → Surface2 on hover | SemiBold, FontSubtitle, TextPrimary | 0,3,0,3 |
| Advanced | None | Surface1 → Surface2 on hover | Normal, FontLabel, TextSecondary | 0,1,0,1 |

### Spacing Strategy

| Element | Value | Purpose |
|---|---|---|
| StackPanel Spacing (tabs) | 6px | Base gap between adjacent items |
| Expander Margin (primary) | 3+3=6px | Combined with StackPanel = 12px between sections |
| Expander Margin (advanced) | 1+1=2px | Tighter, advanced sections cluster together |
| Section Tag top margin | 16px | Clear break before new conceptual group |
| Expander content padding | 6,6,0,2 | Breathing room inside expanded sections |
