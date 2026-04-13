# Phase 15: Design System Foundation — Tokens, Radii & Typography

## Your Role

You are implementing Phase 15 of the Monozukuri Material Tool Transformation. This is the first of four design-system refresh phases (15–18) that modernize the visual language from a 2018-era flat dark theme to a confident, contemporary "Dark Slate" aesthetic. Phase 15 establishes the foundation: simplified surface tokens, increased corner radii, tightened typography scale, and updated spacing primitives. Later phases build on this foundation.

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–14 are complete. The app has a mature design token system in `App.axaml` with 5 surface levels, 3 border levels, accent colors, and global styles for all standard controls. The visual design is functional but dated — uniform grey boxes, 4px radii, 12px labels, and minimal visual hierarchy.

**This phase DOES modify `App.axaml`.** The constraint against modifying `App.axaml` applied to feature phases where accidental token breakage was the concern. This IS the design phase — `App.axaml` is the primary file being changed.

## What Phase 15 Does

### Three Subphases (execute in order):

1. **15A — Surface Token Simplification**: Collapse the 5-level surface ramp into 3 effective levels and add semantic aliases.
2. **15B — Corner Radius Upgrade**: Increase radii across all control types for a modern, softer appearance.
3. **15C — Typography & Spacing Tightening**: Reduce font sizes, tighten the label column, and increase inter-section spacing for better density management.

**Explicitly deferred** (do NOT implement):
- Toolbar redesign (Phase 16)
- Button border/hover changes (Phase 16)
- Expander restyling (Phase 17)
- Interaction state polish (Phase 18)

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT change any `x:Name` values.** Code-behind resolves controls by name.
2. **Do NOT change handler wiring.** Event handlers keep their existing signatures.
3. **Do NOT modify Core, Rendering, or Controls code.** This is purely visual.
4. **The app must compile and run after each subphase.** Visual changes are expected; functional changes are not.
5. **Preserve all existing token names.** Other files reference `Surface0Brush`, `Surface1Brush`, etc. These names must continue to exist. You may change their *values* and add new tokens, but never remove or rename existing ones.
6. **Test with all 5 project types** (RotaryKnob, ThumbSlider, FlipSwitch, PushButton, IndicatorLight) to ensure no layout breakage.

---

## Existing Architecture (Read Before Coding)

### Current Token System (`App.axaml`)

**Surface ramp (5 levels):**
| Token | Current Value | Used For |
|---|---|---|
| `Surface0` | `#0F1317` | Deepest background, toolbar, context strip |
| `Surface1` | `#141820` | Panel backgrounds (scene list, inspector) |
| `Surface2` | `#1A1F28` | Cards, expander headers, button default |
| `Surface3` | `#222830` | Hover states, raised elements |
| (no Surface4) | — | — |

**Border ramp (3 levels):**
| Token | Current Value |
|---|---|
| `BorderSubtle` | `#252C35` |
| `BorderDefault` | `#2E3640` |
| `BorderStrong` | `#3A4450` |

**Text ramp:**
| Token | Current Value |
|---|---|
| `TextPrimary` | `#E2EAF2` |
| `TextSecondary` | `#A8B4C0` |
| `TextTertiary` | `#707C88` |

**Current dimensions:**
| Token | Current Value |
|---|---|
| `ControlHeight` | 28 |
| `LabelColumnWidth` | 120 |
| `ColumnGap` | 8 |
| `ExpanderSpacing` | 10 |
| `RowSpacing` | 6 |
| `FontTitle` | 14 |
| `FontSubtitle` | 12 |
| `FontLabel` | 12 |
| `FontHint` | 11 |
| `FontSectionTag` | 10 |

**Corner radii (hardcoded in styles, not tokens):**
- Button: `CornerRadius="4"`
- ComboBox: `CornerRadius="4"`
- TextBox: `CornerRadius="4"`
- ListBox: `CornerRadius="4"`
- `inset-card`: `CornerRadius="6"`
- `banner-card`: `CornerRadius="6"`

### Files That Reference Tokens

These files use `StaticResource` references to tokens from `App.axaml`:

- `MainWindow.axaml` — primary workspace layout
- `ProjectLauncherWindow.axaml` — launcher window
- `RenderSettingsWindow.axaml` — export dialog
- `MainWindow.ProjectTypeCommands.cs` — programmatic dialog construction (uses hex constants, not StaticResource)

### Files to Modify

| File | Subphases | What Changes |
|---|---|---|
| `App.axaml` | 15A, 15B, 15C | Token values, new tokens, corner radii in styles, font sizes, spacing values |
| `MainWindow.axaml` | 15A, 15C | Surface references where simplification applies, label column widths, section spacing |
| `ProjectLauncherWindow.axaml` | 15B | Corner radii on launcher cards (currently 12px — verify consistency) |

---

## Subphase 15A — Surface Token Simplification

### Goal

Collapse the effective surface ramp from 5 near-identical dark greys into 3 clearly distinguishable levels. The human eye can barely distinguish 5 shades across a 13-unit hex range. Three well-spaced levels create confident visual hierarchy.

### Step 1: Update Surface Color Values

**File**: `App.axaml`

Change the Color definitions:

| Token | Old Value | New Value | Rationale |
|---|---|---|---|
| `Surface0` | `#0F1317` | `#0D1117` | Slightly deeper — true background, used for window chrome |
| `Surface1` | `#141820` | `#151B24` | Panel background — wider gap from Surface0 for clear distinction |
| `Surface2` | `#1A1F28` | `#1E2430` | Cards/elevated — noticeably lighter than panels |
| `Surface3` | `#222830` | `#262D38` | Hover/interactive — clear step up from Surface2 |

The key change: widen the gaps between levels. Old range was 19 units (0F→22). New range is 25 units (0D→26). Each step is now ~8 units instead of ~5, making the hierarchy visible.

### Step 2: Update Border Colors to Match

| Token | Old Value | New Value |
|---|---|---|
| `BorderSubtle` | `#252C35` | `#2A3140` |
| `BorderDefault` | `#2E3640` | `#333D4A` |
| `BorderStrong` | `#3A4450` | `#404C5A` |

Wider gaps between border levels too. `BorderSubtle` should be barely visible against `Surface2`. `BorderDefault` should be clearly visible. `BorderStrong` should be prominent.

### Step 3: Add Semantic Token Aliases

Add new brush resources that reference the surface tokens with semantic names. This does NOT replace the existing tokens — it adds aliases for clarity:

```xml
<!-- Semantic aliases (reference existing colors) -->
<SolidColorBrush x:Key="WindowBgBrush" Color="{StaticResource Surface0}"/>
<SolidColorBrush x:Key="PanelBgBrush" Color="{StaticResource Surface1}"/>
<SolidColorBrush x:Key="CardBgBrush" Color="{StaticResource Surface2}"/>
<SolidColorBrush x:Key="HoverBgBrush" Color="{StaticResource Surface3}"/>
<SolidColorBrush x:Key="ControlBgBrush" Color="{StaticResource Surface0}"/>
<SolidColorBrush x:Key="InteractiveBorderBrush" Color="{StaticResource BorderDefault}"/>
```

These are for future use and documentation — don't change existing `StaticResource` references to use them yet (that would touch too many files).

### Step 4: Add Accent Tokens

Add two new accent tokens for richer interaction states (Phase 18 will use these, but define them now):

```xml
<Color x:Key="AccentGlow">#1A4A90B8</Color>  <!-- Accent at 10% opacity -->
<Color x:Key="AccentStrong">#5AA0C8</Color>   <!-- Brighter accent for active states -->

<SolidColorBrush x:Key="AccentGlowBrush" Color="{StaticResource AccentGlow}"/>
<SolidColorBrush x:Key="AccentStrongBrush" Color="{StaticResource AccentStrong}"/>
```

### Verification

- App compiles and runs.
- Surface hierarchy is visible: window background < panel < cards/expander headers < hover states.
- No broken resource references (grep for all existing token names — they must all still resolve).
- Inspector panel is visually distinct from the viewport area.
- Expander headers are clearly elevated above the panel background.

---

## Subphase 15B — Corner Radius Upgrade

### Goal

Increase corner radii across all controls. This is the single highest-ROI visual change — going from 4px to 8px on buttons alone makes the entire UI feel 5 years newer.

### Step 1: Add Radius Tokens

**File**: `App.axaml`

Add radius tokens in the `<Application.Resources>` section:

```xml
<CornerRadius x:Key="RadiusSm">6</CornerRadius>
<CornerRadius x:Key="RadiusMd">8</CornerRadius>
<CornerRadius x:Key="RadiusLg">12</CornerRadius>
<CornerRadius x:Key="RadiusXl">16</CornerRadius>
```

### Step 2: Apply to Global Styles

**File**: `App.axaml`

Update the `<Application.Styles>` section:

**Button** (currently `CornerRadius="4"`):
```xml
<Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>  <!-- 4 → 8 -->
```

**ComboBox** (currently `CornerRadius="4"`):
```xml
<Setter Property="CornerRadius" Value="{StaticResource RadiusSm}"/>  <!-- 4 → 6 -->
```

**TextBox** (currently `CornerRadius="4"`):
```xml
<Setter Property="CornerRadius" Value="{StaticResource RadiusSm}"/>  <!-- 4 → 6 -->
```

**ListBox** (currently `CornerRadius="4"`):
```xml
<Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>  <!-- 4 → 8 -->
```

**`inset-card`** (currently `CornerRadius="6"`):
```xml
<Setter Property="CornerRadius" Value="{StaticResource RadiusLg}"/>  <!-- 6 → 12 -->
```

**`banner-card`** (currently `CornerRadius="6"`):
```xml
<Setter Property="CornerRadius" Value="{StaticResource RadiusLg}"/>  <!-- 6 → 12 -->
```

### Step 3: Update Hardcoded Radii in MainWindow.axaml

Search `MainWindow.axaml` for `CornerRadius=` and update:
- Toolbar app name border (line ~26): `CornerRadius="6"` → `CornerRadius="10"`
- Context strip badges (lines ~89, ~100): `CornerRadius="5"` → `CornerRadius="8"`

### Step 4: Update Launcher Window Radii

**File**: `ProjectLauncherWindow.axaml`

The launcher already uses `CornerRadius="12"` for project cards — this is already aligned with the new system. Verify no smaller radii need updating.

### Step 5: Update Programmatic Dialogs

**File**: `MainWindow.ProjectTypeCommands.cs`

Search for `CornerRadius = new CornerRadius(` and update:
- Type change card: `CornerRadius(10)` → `CornerRadius(12)` (align with `RadiusLg`)
- Cancel button: `CornerRadius(6)` → `CornerRadius(8)` (align with `RadiusMd`)
- Confirm dialog card: `CornerRadius(12)` → keep at `12` (already aligned)
- Confirm dialog buttons: `CornerRadius(6)` → `CornerRadius(8)`

### Verification

- All buttons have visibly rounded corners (8px).
- ComboBoxes and TextBoxes have subtle rounding (6px).
- Cards and panels have generous rounding (12px).
- No clipping or overflow issues on any control.
- Launcher window cards still look correct.
- Type change and confirm dialogs have updated radii.

---

## Subphase 15C — Typography & Spacing Tightening

### Goal

Tighten the typography scale and increase inter-section spacing. The current 12px labels compete with section titles for attention. By reducing labels to 11px and increasing the gap between sections, controls feel more organized and the hierarchy becomes clear through *space* rather than through barely-distinguishable grey shades.

### Step 1: Update Font Size Tokens

**File**: `App.axaml`

| Token | Old Value | New Value | Rationale |
|---|---|---|---|
| `FontTitle` | 14 | 13 | Slightly smaller — less dominant in the toolbar |
| `FontSubtitle` | 12 | 12 | Keep — used for expander headers, needs weight |
| `FontLabel` | 12 | 11 | Tighter — labels should be secondary to values |
| `FontHint` | 11 | 10 | Smaller — hints are tertiary information |
| `FontSectionTag` | 10 | 9.5 | Fractionally smaller — section tags are structural, not content |

### Step 2: Update Spacing Tokens

| Token | Old Value | New Value | Rationale |
|---|---|---|---|
| `ExpanderSpacing` | 10 | 14 | More breathing room between top-level sections |
| `RowSpacing` | 6 | 5 | Slightly tighter rows to offset the increased section spacing |
| `LabelColumnWidth` | 120 | 110 | Narrower labels give more space to controls |

### Step 3: Update Section Tag Margin

In the `section-tag` style, increase the top margin to create more visual separation:

```xml
<Style Selector="TextBlock.section-tag">
    <Setter Property="Margin" Value="0,14,0,3"/>  <!-- was 0,10,0,2 -->
</Style>
```

### Step 4: Update Expander Content Padding

Increase expander content left-padding slightly for better indentation hierarchy:

```xml
<Style Selector="Expander /template/ Border#ExpanderContent">
    <Setter Property="Padding" Value="6,4,0,0"/>  <!-- was 4,4,0,0 -->
</Style>
```

### Step 5: Update MainWindow.axaml Hardcoded Column Widths

Search `MainWindow.axaml` for `ColumnDefinitions="120,*"` and replace with `ColumnDefinitions="110,*"`:

This appears ~150+ times across all inspector sections. Use find-and-replace:
- `ColumnDefinitions="120,*"` → `ColumnDefinitions="110,*"`

**IMPORTANT**: Only replace the 2-column inspector parameter grids. Do NOT change `ColumnDefinitions` that have 3 columns (e.g., `"120,*,Auto"`) or other patterns. Handle 3-column grids separately: `ColumnDefinitions="120,*,Auto"` → `ColumnDefinitions="110,*,Auto"`.

### Step 6: Update MainWindow.axaml Hardcoded Spacing

Search `MainWindow.axaml` for `Spacing="6"` on StackPanels that are direct children of Expander content. These represent the row spacing within sections. Update to `Spacing="5"` to match the new `RowSpacing` token.

**Be selective**: Only change StackPanels inside expander content areas. Do NOT change Spacing on toolbar StackPanels, dialog layouts, or other structural elements.

### Verification

- Labels are visibly smaller than expander headers.
- Section gaps between expanders are generous — the inspector feels "airy" rather than "cramped."
- No label truncation after the column width reduction (110px is sufficient for all labels like "Longitude segments", "Backplate thickness", etc.).
- Hint text is small but still legible.
- The overall inspector density feels intentional — tighter rows within sections, wider gaps between sections.
- App compiles and runs with all 5 project types.

---

## File Touchpoint Table

| Subphase | File | Action |
|---|---|---|
| 15A | `App.axaml` | Update Surface0–3 values, update Border values, add semantic aliases, add accent tokens |
| 15B | `App.axaml` | Add radius tokens, update Button/ComboBox/TextBox/ListBox/card styles |
| 15B | `MainWindow.axaml` | Update hardcoded `CornerRadius` on toolbar and context strip |
| 15B | `ProjectLauncherWindow.axaml` | Verify/update launcher card radii |
| 15B | `MainWindow.ProjectTypeCommands.cs` | Update programmatic dialog radii |
| 15C | `App.axaml` | Update font size tokens, spacing tokens, section-tag margin, expander padding |
| 15C | `MainWindow.axaml` | Bulk update `ColumnDefinitions="120,*"` → `"110,*"`, update StackPanel `Spacing` |

## Appendix: Token Value Reference (After Phase 15)

### Surfaces
| Token | Value |
|---|---|
| `Surface0` | `#0D1117` |
| `Surface1` | `#151B24` |
| `Surface2` | `#1E2430` |
| `Surface3` | `#262D38` |

### Borders
| Token | Value |
|---|---|
| `BorderSubtle` | `#2A3140` |
| `BorderDefault` | `#333D4A` |
| `BorderStrong` | `#404C5A` |

### Radii
| Token | Value |
|---|---|
| `RadiusSm` | 6 |
| `RadiusMd` | 8 |
| `RadiusLg` | 12 |
| `RadiusXl` | 16 |

### Typography
| Token | Value |
|---|---|
| `FontTitle` | 13 |
| `FontSubtitle` | 12 |
| `FontLabel` | 11 |
| `FontHint` | 10 |
| `FontSectionTag` | 9.5 |

### Spacing
| Token | Value |
|---|---|
| `ExpanderSpacing` | 14 |
| `RowSpacing` | 5 |
| `LabelColumnWidth` | 110 |
