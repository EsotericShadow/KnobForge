# Phase 8: Inspector UI/UX Overhaul — Codex Implementation Prompt

## Context

You are modifying **KnobForge**, a .NET 8 / Avalonia 11.x / Metal desktop app for rendering skeuomorphic knobs. The inspector panel (right column, 360px) contains 7 tabs, ~220 ValueInput controls, ~35 expanders, and ~30 code-behind partial class files. Phase 6 migrated SpriteKnobSlider → ValueInput mechanically but preserved the legacy layout. This phase fixes the remaining UX debt through a professional design-system-first approach.

**This is a purely visual/structural refactor.** Zero behavior changes. The app must compile and function identically after each subphase.

### Design Philosophy

This overhaul follows the conventions of professional DCC parameter editors (Substance Painter, Blender, Houdini, Unity):

- **Design tokens over magic numbers** — Every color, spacing, and size is defined once as a named resource and referenced everywhere. No raw hex values in MainWindow.axaml.
- **4px baseline grid** — All vertical spacing, margins, padding, and control heights are multiples of 4px. The base unit is 4px; the comfortable unit is 8px.
- **Surface elevation hierarchy** — Dark themes use progressively lighter backgrounds to communicate depth. KnobForge uses exactly 4 surface levels.
- **Typographic scale** — Exactly 4 text styles: title, subtitle, label, and hint. No ad-hoc font sizes.
- **Consistent control dimensions** — Every control type has one canonical height. ValueInput = 28px. ComboBox inherits Fluent defaults. Buttons use 3 size tiers.

---

## Subphase 8A: Design Token System

### What to Do

Define a complete set of design tokens as Avalonia `StaticResource` values in `App.axaml`, then replace every raw hex color, font size, margin, and dimension in MainWindow.axaml with resource references.

### Step 1: Define Color Tokens in App.axaml

Add a `<Application.Resources>` block **before** `<Application.Styles>`:

```xml
<Application.Resources>
    <!-- ═══════════ SURFACE ELEVATION ═══════════ -->
    <!-- 4-level surface system: darkest → lightest -->
    <Color x:Key="Surface0">#0F1317</Color>   <!-- App chrome, top bar bg -->
    <Color x:Key="Surface1">#141820</Color>   <!-- Panel backgrounds (scene, inspector) -->
    <Color x:Key="Surface2">#1A1F28</Color>   <!-- Card/inset backgrounds (paint layers, material list) -->
    <Color x:Key="Surface3">#222830</Color>   <!-- Raised elements (context banners, hover states) -->

    <!-- ═══════════ BORDERS ═══════════ -->
    <Color x:Key="BorderSubtle">#252C35</Color>    <!-- Panel/section dividers -->
    <Color x:Key="BorderDefault">#2E3640</Color>   <!-- Control borders, separators -->
    <Color x:Key="BorderStrong">#3A4450</Color>    <!-- Focused/active borders, prominent cards -->

    <!-- ═══════════ TEXT ═══════════ -->
    <Color x:Key="TextPrimary">#E2EAF2</Color>     <!-- Headings, primary content -->
    <Color x:Key="TextSecondary">#A8B4C0</Color>   <!-- Labels, parameter names -->
    <Color x:Key="TextTertiary">#707C88</Color>     <!-- Hints, disabled, section headers -->
    <Color x:Key="TextError">#FF6B6B</Color>        <!-- Validation errors -->

    <!-- ═══════════ ACCENT / SEMANTIC ═══════════ -->
    <Color x:Key="ChannelR">#CC6666</Color>   <!-- RGB channel red -->
    <Color x:Key="ChannelG">#66CC66</Color>   <!-- RGB channel green -->
    <Color x:Key="ChannelB">#6688CC</Color>   <!-- RGB channel blue -->

    <!-- ═══════════ SPACING (as Thickness) ═══════════ -->
    <Thickness x:Key="SpaceNone">0</Thickness>
    <Thickness x:Key="SpaceXs">0,2,0,2</Thickness>       <!-- 2px vertical -->
    <Thickness x:Key="SpaceSm">0,4,0,4</Thickness>       <!-- 4px vertical -->
    <Thickness x:Key="SpaceMd">0,8,0,8</Thickness>       <!-- 8px vertical -->
    <Thickness x:Key="SpaceLg">0,12,0,12</Thickness>     <!-- 12px vertical -->
    <Thickness x:Key="SectionGap">0,6,0,0</Thickness>    <!-- Expander content top margin -->
    <Thickness x:Key="PanelPad">8</Thickness>             <!-- Standard panel padding -->
    <Thickness x:Key="TopBarPad">10,8</Thickness>         <!-- Top bar padding -->
    <Thickness x:Key="CardPad">8</Thickness>              <!-- Card/inset padding -->

    <!-- ═══════════ DIMENSIONS ═══════════ -->
    <x:Double x:Key="ControlHeight">28</x:Double>         <!-- ValueInput, ComboBox target height -->
    <x:Double x:Key="LabelColumnWidth">120</x:Double>     <!-- Grid label column width -->
    <x:Double x:Key="ColumnGap">8</x:Double>              <!-- Grid column spacing -->
    <x:Double x:Key="RgbLetterWidth">18</x:Double>        <!-- R/G/B letter column -->
    <x:Double x:Key="ExpanderSpacing">10</x:Double>       <!-- Between expanders in a tab -->
    <x:Double x:Key="RowSpacing">6</x:Double>             <!-- Between rows inside an expander -->
    <x:Double x:Key="RgbRowSpacing">3</x:Double>          <!-- Between R/G/B rows -->

    <!-- ═══════════ TYPOGRAPHY ═══════════ -->
    <x:Double x:Key="FontTitle">14</x:Double>     <!-- App name, panel titles -->
    <x:Double x:Key="FontSubtitle">12</x:Double>  <!-- Sub-section headers, "Paint layers" -->
    <x:Double x:Key="FontLabel">12</x:Double>     <!-- Parameter labels (param-label) -->
    <x:Double x:Key="FontHint">11</x:Double>      <!-- Hints, secondary info, RGB letters -->
    <x:Double x:Key="FontSectionTag">10</x:Double> <!-- PLATE / BUSHING sub-headers -->

    <!-- ═══════════ BRUSHES (for use in Setter values) ═══════════ -->
    <SolidColorBrush x:Key="Surface0Brush" Color="{StaticResource Surface0}"/>
    <SolidColorBrush x:Key="Surface1Brush" Color="{StaticResource Surface1}"/>
    <SolidColorBrush x:Key="Surface2Brush" Color="{StaticResource Surface2}"/>
    <SolidColorBrush x:Key="Surface3Brush" Color="{StaticResource Surface3}"/>
    <SolidColorBrush x:Key="BorderSubtleBrush" Color="{StaticResource BorderSubtle}"/>
    <SolidColorBrush x:Key="BorderDefaultBrush" Color="{StaticResource BorderDefault}"/>
    <SolidColorBrush x:Key="BorderStrongBrush" Color="{StaticResource BorderStrong}"/>
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="{StaticResource TextPrimary}"/>
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="{StaticResource TextSecondary}"/>
    <SolidColorBrush x:Key="TextTertiaryBrush" Color="{StaticResource TextTertiary}"/>
    <SolidColorBrush x:Key="TextErrorBrush" Color="{StaticResource TextError}"/>
    <SolidColorBrush x:Key="ChannelRBrush" Color="{StaticResource ChannelR}"/>
    <SolidColorBrush x:Key="ChannelGBrush" Color="{StaticResource ChannelG}"/>
    <SolidColorBrush x:Key="ChannelBBrush" Color="{StaticResource ChannelB}"/>
</Application.Resources>
```

### Step 2: Update Styles in App.axaml

Replace the existing styles block with:

```xml
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" />

    <!-- ValueInput global style -->
    <Style Selector="controls|ValueInput">
        <Setter Property="Margin" Value="{StaticResource SpaceXs}"/>
        <Setter Property="Height" Value="{StaticResource ControlHeight}"/>
    </Style>

    <!-- Parameter label (inline with control) -->
    <Style Selector="TextBlock.param-label">
        <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
        <Setter Property="FontSize" Value="{StaticResource FontLabel}"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- UPPERCASE sub-section headers (PLATE, BUSHING, SURFACE, etc.) -->
    <Style Selector="TextBlock.section-tag">
        <Setter Property="FontSize" Value="{StaticResource FontSectionTag}"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Foreground" Value="{StaticResource TextTertiaryBrush}"/>
        <Setter Property="Margin" Value="0,10,0,2"/>
        <Setter Property="LetterSpacing" Value="1"/>
    </Style>

    <!-- Sub-section title (e.g., "Paint layers", "Imported Materials") -->
    <Style Selector="TextBlock.section-title">
        <Setter Property="FontSize" Value="{StaticResource FontSubtitle}"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    </Style>

    <!-- Hint / description text -->
    <Style Selector="TextBlock.hint">
        <Setter Property="FontSize" Value="{StaticResource FontHint}"/>
        <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    </Style>

    <!-- RGB channel letter -->
    <Style Selector="TextBlock.channel-r">
        <Setter Property="Foreground" Value="{StaticResource ChannelRBrush}"/>
        <Setter Property="FontSize" Value="{StaticResource FontHint}"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style Selector="TextBlock.channel-g">
        <Setter Property="Foreground" Value="{StaticResource ChannelGBrush}"/>
        <Setter Property="FontSize" Value="{StaticResource FontHint}"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style Selector="TextBlock.channel-b">
        <Setter Property="Foreground" Value="{StaticResource ChannelBBrush}"/>
        <Setter Property="FontSize" Value="{StaticResource FontHint}"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- Inset card (paint layers, material list, context banners) -->
    <Style Selector="Border.inset-card">
        <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="6"/>
        <Setter Property="Padding" Value="{StaticResource CardPad}"/>
    </Style>

    <!-- SpriteSheetPreviewKnob (unchanged from before) -->
    <Style Selector="controls|SpriteSheetPreviewKnob">
        <Setter Property="FrameCount" Value="156"/>
        <Setter Property="ColumnCount" Value="13"/>
        <Setter Property="FrameWidth" Value="128"/>
        <Setter Property="FrameHeight" Value="128"/>
        <Setter Property="FramePadding" Value="12"/>
        <Setter Property="FrameStartX" Value="12"/>
        <Setter Property="FrameStartY" Value="12"/>
        <Setter Property="KnobDiameter" Value="64"/>
        <Setter Property="DragPixelsForFullRange" Value="220"/>
        <Setter Property="Margin" Value="{StaticResource SpaceXs}"/>
        <Setter Property="Template">
            <ControlTemplate>
                <Grid HorizontalAlignment="Left"
                      VerticalAlignment="Center"
                      Width="{Binding EffectiveKnobDiameter, RelativeSource={RelativeSource TemplatedParent}}"
                      Height="{Binding EffectiveKnobDiameter, RelativeSource={RelativeSource TemplatedParent}}">
                    <Image Source="{Binding CurrentFrame, RelativeSource={RelativeSource TemplatedParent}}"
                           Stretch="UniformToFill"/>
                </Grid>
            </ControlTemplate>
        </Setter>
    </Style>
</Application.Styles>
```

### Step 3: Replace All Raw Hex Values in MainWindow.axaml

Perform a systematic find-and-replace across the entire MainWindow.axaml. The mapping is:

**Backgrounds:**
| Old Value | New Value | Context |
|-----------|-----------|---------|
| `#12161A` | `{StaticResource Surface0Brush}` | Top bar |
| `#0F1317` | `{StaticResource Surface0Brush}` | Logo box |
| `#101419` | `{StaticResource Surface0Brush}` | Context strip |
| `#111417` | `{StaticResource Surface1Brush}` | Scene panel, ListBox |
| `#161A1D` | `{StaticResource Surface1Brush}` | Inspector panel |
| `#151B22` | `{StaticResource Surface1Brush}` | Context boxes |
| `#131920` | `{StaticResource Surface1Brush}` | Viewport brush dock |
| `#12171C` | `{StaticResource Surface2Brush}` | Material list |
| `#1D242C` | `{StaticResource Surface2Brush}` | Paint layer cards |
| `#27303A` | `{StaticResource Surface3Brush}` | Context banners |

**Borders:**
| Old Value | New Value |
|-----------|-----------|
| `#2A3138` | `{StaticResource BorderSubtleBrush}` |
| `#2E3740` | `{StaticResource BorderDefaultBrush}` |
| `#2C3845` | `{StaticResource BorderDefaultBrush}` |
| `#304052` | `{StaticResource BorderDefaultBrush}` |
| `#354352` | `{StaticResource BorderStrongBrush}` |
| `#394654` | `{StaticResource BorderStrongBrush}` |
| `#3B4653` | `{StaticResource BorderStrongBrush}` |
| `#4A5663` | `{StaticResource BorderStrongBrush}` |

**Text foregrounds:**
| Old Value | New Value | Context |
|-----------|-----------|---------|
| `#E6EEF5` | `{StaticResource TextPrimaryBrush}` | Main text |
| `#D6E0EA` | `{StaticResource TextPrimaryBrush}` | Context strip text |
| `#D8E2EA` | `{StaticResource TextPrimaryBrush}` | Banner text |
| `#D4DCE5` | `{StaticResource TextPrimaryBrush}` | Status text |
| `#BFD0E0` | `{StaticResource TextPrimaryBrush}` | Brush transport |
| `#DCE7F2` | `{StaticResource TextPrimaryBrush}` | Icon SVG fills |
| `#A9B4BF` | `{StaticResource TextSecondaryBrush}` | Hint text |
| `#A9B8C9` | `{StaticResource TextSecondaryBrush}` | Labels (App.axaml already handled) |
| `#A5B2BF` | `{StaticResource TextSecondaryBrush}` | Context hint |
| `#667788` | `{StaticResource TextTertiaryBrush}` | Section headers |
| `#FF6B6B` | `{StaticResource TextErrorBrush}` | Validation |

**RGB channels (replace inline Foreground values):**
| Old | New |
|-----|-----|
| `Foreground="#CC6666"` | `Classes="channel-r"` |
| `Foreground="#66CC66"` | `Classes="channel-g"` |
| `Foreground="#6688CC"` | `Classes="channel-b"` |

Also remove the `FontSize="11" VerticalAlignment="Center"` attributes from RGB letter TextBlocks — the channel-* class handles that.

**Font sizes (replace with StaticResource):**
| Old | New |
|-----|-----|
| `FontSize="14"` (logo) | `FontSize="{StaticResource FontTitle}"` |
| `FontSize="12"` (sub-headers) | `FontSize="{StaticResource FontSubtitle}"` |
| `FontSize="11"` (hints) | `FontSize="{StaticResource FontHint}"` |
| `FontSize="16"` (scene title) | `FontSize="{StaticResource FontTitle}"` — also change to 14 |
| `FontSize="10"` (section tags) | handled by `.section-tag` class |

### Goal After 8A

Zero raw hex color values in MainWindow.axaml (except inside SVG `<Path Data=...>` attributes, which are geometry data not colors — wait, the Fill attributes on Path/Ellipse/Rectangle elements inside icon Canvases also need converting). Every `Background`, `Foreground`, `BorderBrush`, and `Fill` attribute references a `StaticResource`.

### Build Gate 8A

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Expected: 0 errors. Verify:
```bash
grep -Pn '#[0-9A-Fa-f]{6}' KnobForge.App/Views/MainWindow.axaml | grep -v 'Data=' | grep -v 'x:Key=' | head -20
```
Should return zero matches (all colors now use StaticResource).

---

## Subphase 8A½: Visual Styling Layer (Component Theming)

### Why This Subphase Exists

Subphase 8A established the token system — named colors, spacing, and typography. But tokens alone don't create visual hierarchy. The inspector still looks "default Avalonia with dark colors" rather than a professional DCC tool because the **controls themselves** are unstyled. This subphase applies the tokens to actual control chrome: expander headers, tabs, buttons, inputs, separators, and list items.

The goal is to match the visual quality of Substance Painter / Blender / Houdini properties panels, where section headers are clearly distinct from content, active tabs are obvious, and interactive controls have visible affordances.

### Design Principles Applied

- **Darktable contrast rule**: 50% luminance distance between background and foreground for active controls, 30% for labels, 10% for disabled elements
- **Blender HIG**: Section headers are the primary navigation landmark — they must visually "pop" against content
- **Substance Painter**: Collapsible groups have distinct header backgrounds; parameters are visually nested under their group
- **Unity Inspector**: 16px base line height; clear separation between property groups

### What to Do

Add the following styles to `App.axaml` inside `<Application.Styles>`, **after** the existing TextBlock class styles and **before** the `SpriteSheetPreviewKnob` style.

### Step 1: Accent Color Token

Add to `<Application.Resources>` (after the existing Channel tokens):

```xml
<!-- ═══════════ ACCENT ═══════════ -->
<Color x:Key="Accent">#4A90B8</Color>           <!-- Steel blue — active tab, focus ring -->
<Color x:Key="AccentSubtle">#2A4A60</Color>      <!-- Muted accent for hover states -->
<SolidColorBrush x:Key="AccentBrush" Color="{StaticResource Accent}"/>
<SolidColorBrush x:Key="AccentSubtleBrush" Color="{StaticResource AccentSubtle}"/>
```

### Step 2: Expander Header Lightweight Resources

Add these **inside `<Application.Resources>`** to override Fluent's built-in Expander header colors. These are "lightweight styling resources" — Avalonia's Fluent theme reads them by key name:

```xml
<!-- ═══════════ EXPANDER HEADER OVERRIDES ═══════════ -->
<!-- These keys are read by FluentExpanderToggleButtonTheme -->
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
<!-- Expander content area (below the header) -->
<SolidColorBrush x:Key="ExpanderContentBackground" Color="Transparent"/>
<SolidColorBrush x:Key="ExpanderContentBorderBrush" Color="Transparent"/>
```

### Step 3: Expander Style Refinements

Add to `<Application.Styles>`:

```xml
<!-- ═══════════ EXPANDER ═══════════ -->
<!-- Give expander headers a rounded, elevated appearance -->
<Style Selector="Expander">
    <Setter Property="Margin" Value="0,2,0,2"/>
</Style>

<!-- Ensure expander header text is semibold for scannability -->
<Style Selector="Expander /template/ ToggleButton TextBlock">
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="FontSize" Value="13"/>
</Style>

<!-- Indent content slightly from header to create visual nesting -->
<Style Selector="Expander /template/ Border#ExpanderContent">
    <Setter Property="Padding" Value="4,4,0,0"/>
</Style>
```

### Step 4: Tab Control Styling

```xml
<!-- ═══════════ INSPECTOR TAB CONTROL ═══════════ -->
<!-- Override the tab strip to match the dark chrome -->
<Style Selector="TabControl">
    <Setter Property="Background" Value="Transparent"/>
</Style>

<!-- Individual tab items -->
<Style Selector="TabItem">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="10,8,10,8"/>
    <Setter Property="Foreground" Value="{StaticResource TextTertiaryBrush}"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Margin" Value="0"/>
</Style>

<Style Selector="TabItem:pointerover">
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
</Style>

<Style Selector="TabItem:selected">
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <!-- Active tab underline via bottom border -->
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="0,0,0,2"/>
</Style>
```

### Step 5: Button Styling

```xml
<!-- ═══════════ BUTTONS ═══════════ -->
<!-- Base button style for the dark chrome -->
<Style Selector="Button">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Padding" Value="10,4"/>
    <Setter Property="FontSize" Value="12"/>
</Style>

<Style Selector="Button:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface3Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderStrongBrush}"/>
</Style>

<Style Selector="Button:pressed">
    <Setter Property="Background" Value="{StaticResource Surface1Brush}"/>
</Style>
```

### Step 6: TextBox / Search Input Styling

```xml
<!-- ═══════════ TEXT INPUT ═══════════ -->
<Style Selector="TextBox">
    <Setter Property="Background" Value="{StaticResource Surface0Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>
</Style>

<Style Selector="TextBox:focus">
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
</Style>
```

### Step 7: ComboBox Styling

```xml
<!-- ═══════════ COMBOBOX ═══════════ -->
<Style Selector="ComboBox">
    <Setter Property="Background" Value="{StaticResource Surface0Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Height" Value="{StaticResource ControlHeight}"/>
</Style>
```

### Step 8: CheckBox Styling

```xml
<!-- ═══════════ CHECKBOX ═══════════ -->
<Style Selector="CheckBox">
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    <Setter Property="FontSize" Value="12"/>
</Style>
```

### Step 9: ListBox Styling

```xml
<!-- ═══════════ LISTBOX ═══════════ -->
<Style Selector="ListBox">
    <Setter Property="Background" Value="{StaticResource Surface0Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="4"/>
</Style>

<Style Selector="ListBoxItem:selected">
    <Setter Property="Background" Value="{StaticResource AccentSubtleBrush}"/>
</Style>

<Style Selector="ListBoxItem:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
</Style>
```

### Step 10: Separator Styling

```xml
<!-- ═══════════ SEPARATOR ═══════════ -->
<Style Selector="Separator">
    <Setter Property="Background" Value="{StaticResource BorderSubtleBrush}"/>
    <Setter Property="Height" Value="1"/>
    <Setter Property="Margin" Value="0,6,0,6"/>
</Style>
```

### Step 11: ScrollViewer Thumb Styling (Optional but Polished)

```xml
<!-- ═══════════ SCROLLBAR ═══════════ -->
<Style Selector="ScrollViewer /template/ ScrollBar /template/ Thumb">
    <Setter Property="Background" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="MinWidth" Value="6"/>
</Style>
```

### What This Achieves

After applying all styles:

| Element | Before | After |
|---------|--------|-------|
| Expander header | Invisible, same bg as content | Distinct `Surface2` bg, `Surface3` on hover, semibold text |
| Active tab | Same color as inactive tabs | Bright text + steel blue bottom border |
| Inactive tab | White/default text | Tertiary (dim) text, brightens on hover |
| Buttons | Default Fluent (bright, clashing) | `Surface2` bg, subtle border, matches chrome |
| TextBox/Search | Barely visible | `Surface0` bg (recessed), accent border on focus |
| ComboBox | Default Fluent | Matching dark style, consistent height |
| ListBox selection | Green highlight (clashing) | Steel blue subtle highlight |
| Separators | Inconsistent | 1px, `BorderSubtle`, 6px vertical margin |
| Expander content | Flush with header | 4px left indent for visual nesting |

### Build Gate 8A½

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Style-only changes — zero compile risk. But verify the build anyway, then **visually inspect** the running app:
- Expander headers should clearly stand out from content
- Active tab should have a blue underline
- Buttons should look integrated with the dark chrome
- Inputs should look recessed
- List selection should use blue instead of green

---

## Subphase 8A¾: Layout & Toolbar Improvements (Already Applied)

> **Status**: These changes have already been applied manually. This section documents what was done for reference.

### 1. Resizable Sidebars via GridSplitters

The main 3-panel layout was converted from a 3-column grid to a 5-column grid with GridSplitters between each sidebar and the viewport:

```
BEFORE: ColumnDefinitions="240,*,360"
         [Scene][Viewport][Inspector]

AFTER:  ColumnDefinitions="240,Auto,*,Auto,360"
         [Scene][Splitter][Viewport][Splitter][Inspector]
```

**Changes in `MainWindow.axaml`**:
- Column 0 (Scene): added `MinWidth="160" MaxWidth="400"`
- Column 1: `<GridSplitter Width="4" Background="Transparent" ResizeDirection="Columns" ResizeBehavior="PreviousAndNext"/>`
- Column 2: Viewport (was column 1)
- Column 3: Same GridSplitter pattern
- Column 4 (Inspector): added `MinWidth="280" MaxWidth="520"`, was column 2
- Both sidebar Border elements: `BorderThickness="0,0,0,0"` (splitters handle visual separation)

**Changes in `App.axaml`**: Added GridSplitter styles:
```xml
<Style Selector="GridSplitter">
    <Setter Property="Background" Value="{StaticResource BorderSubtleBrush}"/>
</Style>
<Style Selector="GridSplitter:pointerover">
    <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
</Style>
```

**Safety**: No C# code references column indices — verified via grep for `Grid.Column` and `Grid.SetColumn` in `*.cs` files.

### 2. Inspector Header — Search Bar Fix

The inspector header was restructured from DockPanel (which caused search bar content clipping) to a proper Grid layout:

```
BEFORE (DockPanel, clipping):
  DockPanel LastChildFill=True
    ├─ [DockPanel.Dock=Right] Undo/Redo buttons
    ├─ [DockPanel.Dock=Right] Diagnostics text
    └─ TextBox (search — gets leftover space, clips)

AFTER (Grid, correct):
  Grid RowDefinitions="Auto,Auto,*"
    Row 0: Grid ColumnDefinitions="*,Auto"
      ├─ TextBox (search — star-sized, always gets space)
      └─ StackPanel (compact Undo/Redo buttons)
    Row 1: TextBlock (diagnostics on its own line)
    Row 2: TabControl
```

### 3. Floating Brush Toolbar Redesign

The `ViewportBrushDock` was redesigned from a 2-row vertical layout with mixed grouping to a single horizontal bar with visually separated groups:

**Layout**: Horizontal bar, 4 groups separated by 1px vertical dividers:
1. **PAINT** — Master toggle (1 button)
2. **CHANNEL** — Color, Scratch, Erase, Rust, Wear, Gunk (6 buttons)
3. **TOOL** — Spray, Stroke, Needle, Scuff (4 buttons)
4. **ACTIONS** — Add Layer, Clear Mask (2 buttons)

**Visual treatment**:
- Surface1 background at 94% opacity
- BorderDefault border, CornerRadius 8
- 9px uppercase section tags above each group (TextTertiary color)
- Uniform 30×30 icon buttons with 2px spacing
- 1px vertical dividers (BorderSubtle) between groups

**Active button state** (in `MainWindow.BrushQuickToolbar.cs`):
- Active: AccentSubtle (#2A4A60) background, Accent (#4A90B8) border, 1.5px border, full opacity
- Inactive: Surface2 (#1A1F28) background, BorderDefault (#2E3640) border, 1px border, 75% opacity

**Files modified**:
- `KnobForge.App/Views/MainWindow.axaml` — Toolbar AXAML replacement
- `KnobForge.App/Views/MainWindow.BrushQuickToolbar.cs` — `ApplyQuickButtonState` updated to use design token colors
- `KnobForge.App/App.axaml` — Added toolbar TextBlock hit-test style

---

## Subphase 8B: Field Name Rename (Slider → Input)

### What to Do

Batch rename all ValueInput-related identifiers from `*Slider` to `*Input` across the entire app project. Many controls were already renamed during a prior partial pass — this subphase catches any stragglers.

### Scope

**Files to modify** (all under `KnobForge.App/Views/`):

| File | What changes |
|------|-------------|
| `MainWindow.axaml` | Any remaining `x:Name="*Slider"` → `x:Name="*Input"` |
| `MainWindow/MainWindow.cs` | Field declarations (`_*Slider`→`_*Input`), FindControl strings, null checks |
| `MainWindow.Initialization.cs` | Handler wiring (`_*Slider.PropertyChanged += ...`) |
| `MainWindow/MainWindow.SceneAndInspector.cs` | Value read/write references |
| All `MainWindow.*.cs` handler files | Field references |

### CRITICAL: SliderAssembly Is Not a UI Slider

The word "Slider" appears in two completely different contexts:

1. **ValueInput field names** ending in `Slider` — e.g., `_modelRadiusSlider`, `_lightXSlider`. These are legacy names. **These get renamed to `Input`.**

2. **SliderAssembly** — the physical thumb-slider hardware component (a fader knob). These are domain terms that must **NOT** be renamed:
   - `_nodeSliderAssemblyExpander`, `SliderAssemblyModeCombo`
   - `SliderBackplateMeshCombo`, `SliderThumbMeshCombo`
   - `SliderBackplateWidthSlider` → `SliderBackplateWidthInput` (the `Slider` suffix gets renamed, but the `Slider` prefix that means "physical slider" stays)
   - `RefreshSliderLibraryButton`
   - `MainWindow.SliderAssemblyHandlers.cs`, `MainWindow.SliderAssemblyCatalog.cs` — file names stay
   - Any `SliderAssembly*` type, enum, or property name in KnobForge.Core

**Rule**: Only rename the trailing `Slider` on ValueInput control names. Never rename `SliderAssembly`, `SliderBackplate`, `SliderThumb`, `SliderLibrary`, or any Core/Rendering type containing "Slider".

### Verification

```bash
grep -rn 'x:Name=".*Slider"' KnobForge.App/Views/MainWindow.axaml | grep ValueInput
```
Should return zero. All ValueInput x:Names should end in `Input`.

### Build Gate 8B

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

---

## Subphase 8C: Label Text Cleanup & Sentence Case

### What to Do

Apply label shortening, remove embedded units, remove redundant prefixes, move "(0 = Auto)" hints to tooltips, and enforce sentence case on all labels.

### Rule 1: Remove Redundant Section Prefixes Inside Expanders

The Expander header provides context. Don't repeat it in child labels.

**Inside Collar expander** — prefix "Snake" or "Imported" is removed since the Collar context is clear.

**Inside Tip Sleeve expander** — prefix "Sleeve" is removed (e.g., "Sleeve Coverage" → "Coverage", "Sleeve Sides" → "Sides", "Sleeve Pattern Count" → "Pattern count", "Sleeve Pattern Depth" → "Pattern depth", "Sleeve Tip Amount" → "Tip amount", "Sleeve Tip Style" → "Tip style", "Sleeve Style" → "Style").

**Inside Indicator Light Geometry** — prefixes like "Housing", "Lens", "Base", "Reflector", "Emitter" are kept because they disambiguate sub-components within the same expander. Instead, these become sub-header groups (handled in 8F).

### Rule 2: Shorten Labels to ≤18 Characters Where Possible

| Current | New |
|---------|-----|
| `Backplate Mesh Source` | `Backplate mesh` |
| `Thumb Mesh Source` | `Thumb mesh` |
| `Base Mesh Source` | `Base mesh` |
| `Lever Mesh Source` | `Lever mesh` |
| `State Count` | `States` |
| `State Index` | `State` |
| `Max Lever Angle` | `Max angle` |
| `Pivot Clearance` | `Clearance` |
| `Lever Pivot Offset` | `Pivot offset` |
| `Tip Latitude Segments` | `Tip lat segments` |
| `Tip Longitude Segments` | `Tip long segments` |
| `Body Length` | `Body length` |
| `Body Thickness` | `Body thickness` |
| `Head Length` | `Head length` |
| `Head Thickness` | `Head thickness` |
| `Lower Bushing Shape` | `Lower shape` |
| `Upper Bushing Shape` | `Upper shape` |
| `Bushing Sides` | `Sides` |
| `Lever Sides` | `Sides` |
| `Paint Mask Resolution` (standalone) | keep |
| `Invert Base/Bushing Winding` | `Invert winding` |
| `Invert Lever Winding` | `Invert winding` |
| `Enable Tip Sleeve` | `Enable sleeve` |
| `Enable Surface Painting` | `Enable painting` |
| `Enable Dynamic Light-Driven Shadow` | `Enable shadows` |
| `Enable Dynamic Lights` | `Enable` |
| `Enable Assembly` | `Enable` |
| `Enable Collar` | `Enable` |
| `Enable Indicator` | `Enable` |
| `CAD Hard Walls` | `CAD walls` |
| `Reset Industrial Defaults` | `Reset defaults` |
| `Focus selected layer (dim others 25%)` | `Focus layer` |
| `Preview Bake (256x256)` | `Preview bake` |
| `Clear Paint Mask` | `Clear mask` |
| `Layer Opacity` | `Opacity` |
| `Blend Mode` | `Blend` |
| `Paint channel` | `Channel` |
| `Brush darkness` | `Darkness` |
| `Spray spread` | `Spread` |
| `Animation Phase Offset` → `Phase offset` (already done) |
| `Auto Phase Spread` | `Auto phase` |
| `Roughness response` | keep |
| `Diffuse influence` | keep |

### Rule 3: Move "(0 = Auto)" to ToolTip

Already completed in prior phase pass — verify all 22 labels containing "(0 = Auto)" have been converted to `ToolTip.Tip="Set to 0 for automatic sizing"`.

### Rule 4: Sentence-Style Capitalization

All label text uses sentence case: capitalize only the first word, except proper nouns and acronyms (HDRI, IOR, RGB, SSR, LOD, CAD, PBR, UV, CC).

Scan through MainWindow.axaml and fix any remaining Title Case labels:
- `Plate Offset Y` → `Plate offset Y`
- `Plate Offset Z` → `Plate offset Z`
- `Bushing Sides` → `Bushing sides` → `Sides`
- `Lever Sides` → `Lever sides` → `Sides`
- `Housing Radius` → `Housing radius` (already lowercase in some places)
- etc.

### Build Gate 8C

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Label text changes are AXAML-only — they cannot break compilation.

---

## Subphase 8D: Expander Header Standardization

### What to Do

Rename all Expander headers to a consistent format and verify expansion defaults.

### Convention

- Primary sections: `Header="{NounPhrase}"`, `IsExpanded="True"`
- Advanced sections: `Header="{NounPhrase} · Advanced"`, `IsExpanded="False"`
- Debug sections: `Header="{NounPhrase} · Debug"`, `IsExpanded="False"`
- Preview sections: `Header="{NounPhrase} · Preview"`, `IsExpanded="False"`

All headers use sentence case.

### Complete Rename Table

| Current Header | New Header | IsExpanded |
|---|---|---|
| `Mode` | `Mode` | True |
| `Lights` | `Lights` | True |
| `Transform` (Lighting) | `Transform` | True |
| `Intensity` | `Intensity` | True |
| `Color · Advanced` | `Color · Advanced` | False |
| `Artistic · Advanced` | `Artistic · Advanced` | False |
| `Camera` | `Camera` | False |
| `Reference profiles` | `Reference profiles` | False |
| `Transform` (Model) | `Transform` | True |
| `Shape` | `Shape` | True |
| `Slider assembly · Preview` | `Slider assembly · Preview` | False |
| `Setup` (×3) | `Setup` | True |
| `Geometry · Advanced` (×2) | `Geometry · Advanced` | False |
| `Toggle · Preview` | `Toggle · Preview` | False |
| `Base &amp; plate · Advanced` | `Base &amp; plate · Advanced` | False |
| `Lever &amp; pivot · Advanced` | `Lever &amp; pivot · Advanced` | False |
| `Tip sleeve · Advanced` | `Tip sleeve · Advanced` | False |
| `Push button · Preview` | `Push button · Preview` | False |
| `Spiral ridge` | `Spiral ridge` | False |
| `Grip` | `Grip` | True |
| `Collar` | `Collar` | True |
| `Indicator` | `Indicator` | True |
| `Indicator light` | `Indicator light` | True |
| `Quick controls` | `Quick controls` | True |
| `Lens material · Advanced` | `Lens material · Advanced` | False |
| `Dynamic lights · Advanced` | `Dynamic lights · Advanced` | False |
| `Emitter sources · Advanced` | `Emitter sources · Advanced` | False |
| `Material` | `Material` | True |
| `Surface texture` | `Surface texture` | False |
| `Micro detail · Debug` | `Micro detail · Debug` | False |
| `Brush Controls` | `Brush controls` | True |
| `Enable` (Shadows) | `Enable` | True |
| `Projection` | `Projection` | True |
| `Softness` | `Softness` | True |
| `Appearance · Advanced` | `Appearance · Advanced` | False |
| `Tone mapping` | `Tone mapping` | True |
| `Environment` | `Environment` | True |
| `HDRI · Advanced` | `HDRI · Advanced` | False |
| `Bloom · Advanced` | `Bloom · Advanced` | False |
| `Reflections` | `Reflections` | False |
| `Nodes` (Graph) | `Nodes` | True |
| `Node Properties` | `Node properties` | True |
| `Connections` | `Connections` | True |
| `Preview` (Graph) | `Preview` | False |

### Build Gate 8D

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

---

## Subphase 8E: Inline Grid & Controls That Missed Prior Pass

### What to Do

Audit the AXAML for any remaining control patterns that don't use the standard inline grid. Some controls were left in stacked layout or have standalone labels. Convert them all.

### Pattern to Find

Look for any remaining instances of:
```xml
<TextBlock Text="Some Label"/>
<ComboBox x:Name="SomeCombo"/>
```

These stacked label+ComboBox pairs should become:
```xml
<Grid ColumnDefinitions="120,*" ColumnSpacing="8">
    <TextBlock Grid.Column="0" Classes="param-label" Text="Some label"/>
    <ComboBox Grid.Column="1" x:Name="SomeCombo"/>
</Grid>
```

### Known Stacked Controls to Convert

1. **Grip expander**: "Grip style" + `GripStyleCombo` and "Grip type" + `GripTypeCombo` are still stacked
2. **Collar expander**: "Preset" is standalone above `CollarPresetCombo`; "Mesh path (.glb/.stl)" is standalone above `CollarMeshPathTextBox`; "Mirror flip" is standalone above the checkbox row
3. **Indicator expander**: "Relief" + `IndicatorReliefCombo` is stacked
4. **Indicator Light**: "Indicator assembly" is a standalone heading (convert to section-title class)
5. **Emitter sources**: "Name" + `IndicatorEmitterSourceNameTextBox` is stacked
6. **Material expander**: "Texture maps" is a standalone heading; "Albedo map" / "Normal map" / "Roughness map" / "Metallic map" are standalone labels above path displays
7. **Reference profiles**: "Reference style" + `ReferenceStyleCombo` and "Save current as" + `ReferenceStyleSaveNameTextBox`
8. **Brush controls**: "Color brush" is standalone; "Paint mask resolution" is standalone
9. **Model tab**: "Selected: Model" context text should use `Classes="hint"`

### Texture Map Pattern

The texture map section uses a unique pattern (label → path display → Browse/Clear buttons). Keep this as a stacked pattern but apply consistent styling:

```xml
<TextBlock Classes="section-title" Text="Texture maps"/>

<TextBlock Classes="param-label" Text="Albedo map"/>
<TextBlock x:Name="MaterialAlbedoMapPathText" Classes="hint" Text="None" TextWrapping="Wrap"/>
<StackPanel Orientation="Horizontal" Spacing="8">
    <Button x:Name="MaterialAlbedoMapBrowseButton" Content="Browse..."/>
    <Button x:Name="MaterialAlbedoMapClearButton" Content="Clear"/>
</StackPanel>
```

### Build Gate 8E

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

---

## Subphase 8F: Sub-Headers in Deep Sections

### What to Do

Add `section-tag` sub-headers inside long Expanders to break them into scannable groups, following the Blender HIG pattern of sub-grouped parameters.

### Indicator Light Geometry · Advanced

This expander has 15+ controls covering 5 sub-components. Add section tags:

```xml
<TextBlock Classes="section-tag" Text="BASE"/>
<!-- Base Width, Base Height, Base Thickness -->

<TextBlock Classes="section-tag" Text="HOUSING"/>
<!-- Housing Radius, Housing Height -->

<TextBlock Classes="section-tag" Text="LENS"/>
<!-- Lens Radius, Lens Height, Lat segments, Long segments -->

<TextBlock Classes="section-tag" Text="REFLECTOR"/>
<!-- Base radius (Reflector), Top radius (Reflector), Reflector depth -->

<TextBlock Classes="section-tag" Text="EMITTER"/>
<!-- Emitter radius, Emitter spread, Emitter depth, Emitter count, Radial segments -->
```

### Base & Plate · Advanced (Toggle)

```xml
<TextBlock Classes="section-tag" Text="PLATE"/>
<!-- Plate width, height, thickness, Offset Y, Offset Z -->

<TextBlock Classes="section-tag" Text="BUSHING"/>
<!-- Bushing radius, height, Sides, Lower shape, Upper shape, Radius scale (×2), Height ratio (×2) -->

<TextBlock Classes="section-tag" Text="SURFACE"/>
<!-- Knurl amount, density, depth, Aniso strength, density, angle, Surface character -->
```

### Lever & Pivot · Advanced (Toggle)

```xml
<TextBlock Classes="section-tag" Text="HOUSING"/>
<!-- Housing radius, depth, bevel, Ball radius, Clearance -->

<TextBlock Classes="section-tag" Text="LEVER"/>
<!-- Lever length, Bottom radius, Top radius, Sides, Pivot offset -->

<TextBlock Classes="section-tag" Text="TIP"/>
<!-- Tip radius, Tip lat segments, Tip long segments -->
```

### Build Gate 8F

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

---

## Subphase 8G: Visual Uniformity Pass

### What to Do

A final pass ensuring visual consistency across the entire inspector.

### 8G.1: Separator Consistency

- Use `<Separator Margin="0,4,0,4"/>` as the standard separator style everywhere.
- Remove any separator that appears as the last child before `</StackPanel>`.
- Current inconsistency: some use `Margin="0,2,0,2"`, some `Margin="0,6,0,6"`, some `Margin="0,4,0,4"`, some have no margin. Normalize all to `Margin="0,4,0,4"`.

### 8G.2: Button Width Tiers

Standardize button widths to exactly 3 tiers:

| Tier | Width | Usage |
|------|-------|-------|
| Small | `MinWidth="72"` | Single-word actions: Add, Delete, Clear, Undo, Redo, Rename |
| Medium | `MinWidth="100"` | Two-word actions: Browse..., Add Layer, Delete Layer, Apply HDRI, Clear HDRI, Reset view, Add Node, Connect..., Disconnect |
| Large | `MinWidth="140"` | Compound actions: Refresh Library, Reset defaults, Save Profile, Auto phase, Preview bake, Change Type..., Save As... |

Replace all existing hardcoded `Width="..."` and `MinWidth="..."` on buttons with the appropriate tier. Use `MinWidth` (not `Width`) so buttons can grow for longer localized text.

Remove `Width` from buttons that already have the correct `MinWidth`.

### 8G.3: Icon Size Normalization

The viewport brush dock uses icon buttons with varying dimensions. Normalize:
- Primary tool icons: `Width="32" Height="32"` (currently mix of 30/34/36)
- Secondary tool icons: `Width="36" Height="28"` (keep as-is, these are wider format)
- Inner Viewbox: `Width="14" Height="14"` (normalize from current 12-16 variance)
- Canvas: `Width="16" Height="16"` (normalize)

### 8G.4: ListBox Height Standardization

| Context | Height |
|---------|--------|
| Light list (`LightListBox`) | `Height="120"` (from 125) |
| Material list (`MaterialListBox`) | `MaxHeight="160"` (keep) |
| Paint layer list (`PaintLayerListBox`) | `Height="120"` (from 112) |
| Graph node list (`GraphNodeListBox`) | `Height="120"` (from 150) |
| Graph connection list (`GraphConnectionListBox`) | `Height="120"` (keep) |

Use `Height="120"` as the standard list height.

### 8G.5: Consistent StackPanel Spacing

Audit and enforce:
- Tab-level StackPanel (between expanders): `Spacing="10"`
- Expander content StackPanel: `Spacing="6"`
- Button row StackPanel: `Spacing="8"`
- RGB rows StackPanel: `Spacing="3"`
- Tight icon bars: `Spacing="4"`

The current `Spacing="12"` outlier (Collar mirror checkboxes) → change to `Spacing="8"`.
The current `Spacing="4"` on some StackPanels inside cards/panels that should be `Spacing="6"` → fix.

### 8G.6: Apply `.inset-card` Class to Cards

Replace the inline `Background`/`BorderBrush`/`BorderThickness`/`CornerRadius`/`Padding` on card-like Borders with `Classes="inset-card"`:

- Paint layers card (around `PaintLayerListBox`)
- Paint mask resolution card
- Scratch context banner (uses `Surface3Brush` instead — add a `Classes="banner-card"` variant or just override Background)

### 8G.7: Remove Redundant TextBlocks

Remove standalone help text that duplicates context:
- `"Viewport: Cmd+LMB orbit, MMB pan, wheel zoom, R reset"` in Lighting tab is now redundant with the Camera expander
- `"Build procedural materials by connecting nodes"` in Graph tab — keep but apply `Classes="hint"`
- `"Procedural push-button assembly controls."` in Push Button — keep but apply `Classes="hint"`
- `"Fast controls for on/off tuning."` in Quick controls — keep but apply `Classes="hint"`
- `"Used when Paint Channel is Color"` — keep but apply `Classes="hint"`

### 8G.8: Update 00-PROGRAM.md

In `docs/material-tool-program/00-PROGRAM.md`, mark Phase 8 as complete.

### Build Gate 8G (Final)

```bash
dotnet build KnobForge.sln -c Release
```

Expected: 0 errors, 0 warnings.

---

## Verification Checklist

After all subphases complete, verify:

- [ ] `grep -Pn '#[0-9A-Fa-f]{6}' KnobForge.App/Views/MainWindow.axaml | grep -v 'Data=' | wc -l` returns 0
- [ ] `grep -rn 'x:Name=".*Slider"' KnobForge.App/Views/MainWindow.axaml | grep ValueInput` returns 0
- [ ] `grep -rn 'Width="210"' KnobForge.App/Views/MainWindow.axaml` returns 0
- [ ] `grep -rn '(0 = Auto)' KnobForge.App/Views/MainWindow.axaml` returns 0
- [ ] Every `Foreground`, `Background`, `BorderBrush`, and non-SVG `Fill` uses a `StaticResource`
- [ ] Every Expander header uses sentence case with optional ` · Advanced` / ` · Debug` / ` · Preview` qualifier
- [ ] Every TextBlock that is a parameter label has `Classes="param-label"`
- [ ] Every hint/description TextBlock has `Classes="hint"`
- [ ] Every sub-section title uses `Classes="section-title"`
- [ ] Every section tag uses `Classes="section-tag"`
- [ ] Every RGB channel letter uses `Classes="channel-r"`, `channel-g"`, or `channel-b"`
- [ ] No inline `FontSize` attributes remain in MainWindow.axaml (all handled by classes or StaticResource)
- [ ] Button widths use only the 3 MinWidth tiers (72, 100, 140)
- [ ] All ListBox heights are 120 (standard) or 160 (max, material list only)
- [ ] The full Release solution build passes with 0 errors

### Visual Verification (8A½)

- [ ] Expander headers have a visible `Surface2` background that distinguishes them from content
- [ ] Hovering an expander header visibly lightens it to `Surface3`
- [ ] The active inspector tab has a blue underline (`AccentBrush`)
- [ ] Inactive tabs are dimmed (`TextTertiary`) and brighten on hover
- [ ] Buttons match the dark chrome (no bright/clashing Fluent default)
- [ ] TextBox/Search inputs appear recessed (darker `Surface0` background)
- [ ] TextBox focus state shows a blue border ring
- [ ] ListBox selected item uses blue highlight (not green)
- [ ] Separators are subtle 1px lines
- [ ] Expander content is slightly indented from the header
- [ ] App.axaml contains `Accent` and `AccentSubtle` color tokens
- [ ] App.axaml contains `ExpanderHeaderBackground` lightweight resource overrides

---

## Architecture Reminder

- **AXAML**: `KnobForge.App/Views/MainWindow.axaml` (~2475 lines)
- **Styles + tokens**: `KnobForge.App/App.axaml`
- **Field declarations + FindControl + null checks**: `KnobForge.App/Views/MainWindow/MainWindow.cs`
- **Handler wiring**: `MainWindow.Initialization.cs`
- **Inspector value push**: `MainWindow/MainWindow.SceneAndInspector.cs`
- **Handler files** (each accesses ValueInput fields by name):
  - `MainWindow.LightingHandlers.cs`
  - `MainWindow.ModelHandlers.cs`
  - `MainWindow.EnvironmentShadowReadouts.cs`
  - `MainWindow.InspectorUx.cs`
  - `MainWindow.PaintBrushHandlers.cs`
  - `MainWindow.CollarIndicatorMaterialHandlers.cs`
  - `MainWindow.ToggleAssemblyHandlers.cs`
  - `MainWindow.IndicatorLightHandlers.cs`
  - `MainWindow.UpdatePolicy.cs`
  - `MainWindow.ProjectFiles.cs`
  - `MainWindow.PaintLayers.cs`
  - `MainWindow.PrecisionControls.cs`
  - `MainWindow.MaterialTextureHandlers.cs`
  - `MainWindow.SliderAssemblyHandlers.cs`
  - `MainWindow.PushButtonAssemblyHandlers.cs`
  - `MainWindow.BrushContextAndHud.cs`
  - `MainWindow.ProjectTypeCommands.cs`
- **Program doc**: `docs/material-tool-program/00-PROGRAM.md`

### CRITICAL: SliderAssembly Is Not a UI Slider

Repeated for emphasis. The word "Slider" appears in two completely different contexts:

1. **ValueInput field names** ending in `Slider` — legacy names. **Rename suffix to `Input`.**
2. **SliderAssembly** — physical thumb-slider hardware. **Never rename.**

Rule: Only rename the trailing `Slider` on ValueInput control names (x:Name, field declarations, FindControl strings, handler references). Never rename `SliderAssembly`, `SliderBackplate`, `SliderThumb`, `SliderLibrary`, or any Core/Rendering type containing "Slider".

---

## File Index

### Modified Files (Phase 8)

| File | Subphases |
|------|-----------|
| `KnobForge.App/App.axaml` | 8A (tokens + styles), 8A¾ (GridSplitter + toolbar styles) |
| `KnobForge.App/Views/MainWindow.axaml` | 8A, 8A¾ (resizable sidebars, search fix, brush toolbar), 8B, 8C, 8D, 8E, 8F, 8G |
| `KnobForge.App/Views/MainWindow.BrushQuickToolbar.cs` | 8A¾ (design token colors) |
| `KnobForge.App/Views/MainWindow/MainWindow.cs` | 8B |
| `KnobForge.App/Views/MainWindow.Initialization.cs` | 8B |
| `KnobForge.App/Views/MainWindow/MainWindow.SceneAndInspector.cs` | 8B |
| `KnobForge.App/Views/MainWindow.LightingHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.ModelHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.EnvironmentShadowReadouts.cs` | 8B |
| `KnobForge.App/Views/MainWindow.InspectorUx.cs` | 8B |
| `KnobForge.App/Views/MainWindow.PaintBrushHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.CollarIndicatorMaterialHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.ToggleAssemblyHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.IndicatorLightHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.UpdatePolicy.cs` | 8B |
| `KnobForge.App/Views/MainWindow.ProjectFiles.cs` | 8B |
| `KnobForge.App/Views/MainWindow.PaintLayers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.PrecisionControls.cs` | 8B |
| `KnobForge.App/Views/MainWindow.MaterialTextureHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.SliderAssemblyHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.PushButtonAssemblyHandlers.cs` | 8B |
| `KnobForge.App/Views/MainWindow.BrushContextAndHud.cs` | 8B |
| `KnobForge.App/Views/MainWindow.ProjectTypeCommands.cs` | 8B |
| `docs/material-tool-program/00-PROGRAM.md` | 8G |

### New Files

None.

### Deleted Files

None.
