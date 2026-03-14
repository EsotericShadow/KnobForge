# Phase 11: Inspector & Dialog UI Overhaul — All Project Types

## Your Role

You are implementing Phase 11 of the Monozukuri Material Tool Transformation. This phase brings professional visual consistency to all project-type-specific UI: the inspector panels for Slider, Toggle (Flip Switch), Push Button, and Indicator Light, plus the "Change Project Type" dialog in the main workspace. The launcher is already polished (Phases 10–10F). Now the workspace itself needs to match that quality bar.

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–10 are complete. The design token system in `App.axaml` is mature. The launcher window is fully polished. The main workspace uses tokens for the shared chrome (toolbar, sidebars, tabs), but the **project-type-specific inspector sections** were built incrementally and have inconsistent styling, naming, and structure.

**Branding**: The app is now called "Monozukuri" with tagline "Kaizen DSP Asset Creator". All user-facing strings should say "Monozukuri", not "KnobForge". C# namespaces remain `KnobForge.*` (internal).

## What Phase 11 Does

1. **Change Project Type dialog** — redesign from raw unstyled buttons to the same card-based treatment used in the launcher's type picker, with icons, design tokens, and hover states.
2. **Confirm dialog** — redesign from raw unstyled message box to token-based styled card dialog matching the launcher's error dialog pattern.
3. **Inspector consistency** — normalize all 4 non-knob inspector sections so they use consistent naming, section tags, spacing, hint text, and visual hierarchy.

**Explicitly deferred** (do NOT implement):
- New parameters for Push Button (geometry expansion is a separate feature phase).
- Animated transitions or expander animations.
- Color picker widgets to replace R/G/B inputs.
- Thumbnail support for slider/toggle catalogs (collar has it; others don't yet).
- Changes to the RotaryKnob inspector (already the most mature).

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify `App.axaml`** design tokens. Use existing tokens only.
2. **Do NOT modify `App.axaml.cs`.** Startup flow stays identical.
3. **Do NOT modify Core or Rendering code.** This is purely UI.
4. **Keep all existing `x:Name` values.** Code-behind resolves controls by name.
5. **Do NOT change handler wiring.** The event handler methods in `*Handlers.cs` files must keep their existing signatures and logic. You are only changing visual layout, not behavior.
6. **The app must compile and run identically after each subphase.**

---

## Existing Architecture (Read Before Coding)

### Design Token System (App.axaml)

| Token | Hex | Purpose |
|-------|-----|---------|
| `Surface0` / `Surface0Brush` | `#0F1317` | Deepest background |
| `Surface1` / `Surface1Brush` | `#141820` | Panel backgrounds |
| `Surface2` / `Surface2Brush` | `#1A1F28` | Card/inset backgrounds |
| `Surface3` / `Surface3Brush` | `#222830` | Hover/raised elements |
| `BorderSubtle` / `BorderSubtleBrush` | `#252C35` | Section dividers |
| `BorderDefault` / `BorderDefaultBrush` | `#2E3640` | Control borders |
| `BorderStrong` / `BorderStrongBrush` | `#3A4450` | Focused/active borders |
| `TextPrimary` / `TextPrimaryBrush` | `#E2EAF2` | Headings, primary content |
| `TextSecondary` / `TextSecondaryBrush` | `#A8B4C0` | Labels, descriptions |
| `TextTertiary` / `TextTertiaryBrush` | `#707C88` | Hints, paths, timestamps |
| `Accent` / `AccentBrush` | `#4A90B8` | Primary accent |
| `AccentSubtle` / `AccentSubtleBrush` | `#2A4A60` | Accent background |

Text classes: `param-label` (12pt, TextSecondary), `section-tag` (10pt, SemiBold, TextTertiary, letterspaced), `section-title` (12pt, SemiBold, TextPrimary), `hint` (11pt, TextSecondary).

### Code-Behind Hex Constants

The launcher's `ProjectLauncherWindow.axaml.cs` defines `private const string` fields for all token hex values, used when building dialogs programmatically. The same pattern exists in `MainWindow.ProjectTypeCommands.cs` but is **NOT YET** applied — it still uses `Brushes.Gray` and no token colors. This phase fixes that.

### Current Inspector Layout Per Project Type

All 4 non-knob types share the same AXAML pattern:
```
<Expander x:Name="Node{Type}AssemblyExpander" Header="..." IsExpanded="False/True">
    <StackPanel Spacing="6" Margin="0,6,0,0">
        <Expander Header="Setup/Quick controls" IsExpanded="True">
            ...param grids (ColumnDefinitions="120,*" ColumnSpacing="8")...
        </Expander>
        <Expander Header="... · Advanced" IsExpanded="False">
            ...more param grids...
            <TextBlock Classes="section-tag" Text="TAG"/>
            ...grouped params...
        </Expander>
    </StackPanel>
</Expander>
```

### Files to Modify

| File | Subphases | What Changes |
|------|-----------|-------------|
| `MainWindow.axaml` lines ~441–512 | 11C | Slider inspector AXAML |
| `MainWindow.axaml` lines ~514–885 | 11C | Toggle inspector AXAML |
| `MainWindow.axaml` lines ~887–900 | 11C | PushButton inspector AXAML |
| `MainWindow.axaml` lines ~1223–1519 | 11C | IndicatorLight inspector AXAML |
| `MainWindow.ProjectTypeCommands.cs` | 11A, 11B | Change Type dialog + confirm dialog |

### Files NOT to Modify

- All `*Handlers.cs` files (keep event handler signatures and wiring intact)
- All `*Catalog.cs` files (data loading stays the same)
- `App.axaml`, `App.axaml.cs`
- Core / Rendering assemblies

---

## Subphase 11A: Change Project Type Dialog Redesign

### What to Do

Replace `ShowProjectTypeChangePickerAsync()` and `CreateProjectTypePickerButton()` in `MainWindow.ProjectTypeCommands.cs` with the same card-based design pattern used in the launcher's `ShowProjectTypePickerAsync()`.

### Step 1: Add Hex Constants

At the top of the `MainWindow` partial class (in `MainWindow.ProjectTypeCommands.cs`), add or reuse the same token hex constants that the launcher uses:

```csharp
// Design token hex values for programmatic dialog construction
private const string Surface0Hex = "#0F1317";
private const string Surface1Hex = "#141820";
private const string Surface2Hex = "#1A1F28";
private const string Surface3Hex = "#222830";
private const string BorderSubtleHex = "#252C35";
private const string BorderDefaultHex = "#2E3640";
private const string BorderStrongHex = "#3A4450";
private const string TextPrimaryHex = "#E2EAF2";
private const string TextSecondaryHex = "#A8B4C0";
private const string TextTertiaryHex = "#707C88";
private const string AccentHex = "#4A90B8";
private const string AccentSubtleHex = "#2A4A60";
```

**IMPORTANT**: These constants may already exist somewhere in the MainWindow partial class hierarchy. Check `MainWindow.cs` or other partial files first. If they already exist, reuse them instead of redeclaring.

Add a local helper if not already present:
```csharp
private static SolidColorBrush BrushFromHex(string hex) => new(Color.Parse(hex));
```

### Step 2: Rewrite ShowProjectTypeChangePickerAsync

Follow the launcher's pattern exactly: Grid layout with Title, Subtitle, Separator, ScrollViewer of cards, Cancel button.

Key differences from the launcher's picker:
- Title: "Change Project Type" (not "Choose Project Type")
- Subtitle: "Switch the current project to a different workflow."
- The **current type's card is disabled** with reduced opacity and "(Current)" suffix
- The dialog background uses `Surface0Hex`

```csharp
private async Task<InteractorProjectType?> ShowProjectTypeChangePickerAsync()
{
    InteractorProjectType currentType = _project.ProjectType;
    InteractorProjectType? selectedType = null;

    var dialog = new Window
    {
        Title = "Change Project Type",
        Width = 580,
        Height = 560,
        MinWidth = 540,
        MinHeight = 500,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = BrushFromHex(Surface0Hex)
    };

    var root = new Grid
    {
        Margin = new Thickness(28, 24, 28, 20),
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto")
    };

    // Row 0: Title
    var titleBlock = new TextBlock
    {
        Text = "Change Project Type",
        FontSize = 22,
        FontWeight = FontWeight.SemiBold,
        Foreground = BrushFromHex(TextPrimaryHex)
    };
    Grid.SetRow(titleBlock, 0);
    root.Children.Add(titleBlock);

    // Row 1: Subtitle with current type
    var subtitleBlock = new TextBlock
    {
        Text = $"Current: {GetProjectTypeDisplayName(currentType)}",
        FontSize = 12,
        Foreground = BrushFromHex(TextSecondaryHex),
        Margin = new Thickness(0, 6, 0, 0)
    };
    Grid.SetRow(subtitleBlock, 1);
    root.Children.Add(subtitleBlock);

    // Row 2: Separator
    var separator = new Border
    {
        Height = 1,
        Background = BrushFromHex(BorderSubtleHex),
        Margin = new Thickness(0, 16, 0, 16)
    };
    Grid.SetRow(separator, 2);
    root.Children.Add(separator);

    // Row 3: Card list (scrollable)
    var cardList = new StackPanel { Spacing = 10 };

    // Add all 5 project type cards
    // Use the same CreateTypeChangeCard helper (below)
    // Pass currentType so the current type's card is visually marked
    cardList.Children.Add(CreateTypeChangeCard(dialog, currentType,
        InteractorProjectType.RotaryKnob, "Rotary knob",
        "Encoder and knob-focused workflow with rotation animation.",
        CreateKnobIcon(), v => selectedType = v));
    cardList.Children.Add(CreateTypeChangeCard(dialog, currentType,
        InteractorProjectType.FlipSwitch, "Flip switch",
        "Toggle switch workflow with base and lever meshes.",
        CreateSwitchIcon(), v => selectedType = v));
    cardList.Children.Add(CreateTypeChangeCard(dialog, currentType,
        InteractorProjectType.ThumbSlider, "Thumb slider",
        "Linear slider with backplate and thumb meshes.",
        CreateSliderIcon(), v => selectedType = v));
    cardList.Children.Add(CreateTypeChangeCard(dialog, currentType,
        InteractorProjectType.PushButton, "Push button",
        "Momentary button with push animation scaffold.",
        CreateButtonIcon(), v => selectedType = v));
    cardList.Children.Add(CreateTypeChangeCard(dialog, currentType,
        InteractorProjectType.IndicatorLight, "Indicator light",
        "LED indicator with bezel, dome, and emitter rig.",
        CreateIndicatorIcon(), v => selectedType = v));

    var scrollViewer = new ScrollViewer
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = cardList
    };
    Grid.SetRow(scrollViewer, 3);
    root.Children.Add(scrollViewer);

    // Row 4: Cancel button (ghost style)
    var cancelButton = new Button
    {
        Content = "Cancel",
        HorizontalAlignment = HorizontalAlignment.Center,
        MinWidth = 100,
        Padding = new Thickness(16, 8),
        Background = Brushes.Transparent,
        BorderBrush = BrushFromHex(BorderDefaultHex),
        BorderThickness = new Thickness(1),
        Foreground = BrushFromHex(TextSecondaryHex),
        Margin = new Thickness(0, 14, 0, 0),
        CornerRadius = new CornerRadius(6)
    };
    cancelButton.Click += (_, _) => dialog.Close();
    Grid.SetRow(cancelButton, 4);
    root.Children.Add(cancelButton);

    dialog.Content = root;
    await dialog.ShowDialog(this);
    return selectedType;
}
```

### Step 3: Create the Card Builder

This is nearly identical to the launcher's `CreateTypeCard`, but with an `isCurrent` disabled state:

```csharp
private static Border CreateTypeChangeCard(
    Window dialog,
    InteractorProjectType currentType,
    InteractorProjectType targetType,
    string title,
    string description,
    Control icon,
    Action<InteractorProjectType> onSelected)
{
    bool isCurrent = currentType == targetType;

    var card = new Border
    {
        Background = BrushFromHex(isCurrent ? Surface2Hex : Surface1Hex),
        BorderBrush = BrushFromHex(isCurrent ? AccentHex : BorderDefaultHex),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(16, 14),
        Cursor = isCurrent ? Cursor.Default : new Cursor(StandardCursorType.Hand),
        Opacity = isCurrent ? 0.55 : 1.0
    };

    var grid = new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        ColumnSpacing = 16
    };

    var iconBorder = new Border
    {
        Width = 44,
        Height = 44,
        CornerRadius = new CornerRadius(10),
        Background = BrushFromHex(Surface2Hex),
        BorderBrush = BrushFromHex(BorderSubtleHex),
        BorderThickness = new Thickness(1),
        Child = icon,
        VerticalAlignment = VerticalAlignment.Center
    };
    Grid.SetColumn(iconBorder, 0);
    grid.Children.Add(iconBorder);

    var textPanel = new StackPanel
    {
        Spacing = 3,
        VerticalAlignment = VerticalAlignment.Center
    };
    textPanel.Children.Add(new TextBlock
    {
        Text = isCurrent ? $"{title}  (current)" : title,
        FontSize = 14,
        FontWeight = FontWeight.SemiBold,
        Foreground = BrushFromHex(TextPrimaryHex)
    });
    textPanel.Children.Add(new TextBlock
    {
        Text = description,
        FontSize = 12,
        Foreground = BrushFromHex(TextSecondaryHex),
        TextWrapping = TextWrapping.Wrap
    });
    Grid.SetColumn(textPanel, 1);
    grid.Children.Add(textPanel);

    card.Child = grid;

    if (!isCurrent)
    {
        card.PointerEntered += (_, _) =>
        {
            card.Background = BrushFromHex(Surface3Hex);
            card.BorderBrush = BrushFromHex(BorderStrongHex);
        };
        card.PointerExited += (_, _) =>
        {
            card.Background = BrushFromHex(Surface1Hex);
            card.BorderBrush = BrushFromHex(BorderDefaultHex);
        };
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
            onSelected(targetType);
            dialog.Close();
            e.Handled = true;
        };
    }

    return card;
}
```

### Step 4: Icon Factory Methods

Copy the 5 icon factory methods from `ProjectLauncherWindow.axaml.cs` into this partial class (or into a shared static helper class if you prefer). They are:
- `CreateKnobIcon()`
- `CreateSwitchIcon()`
- `CreateSliderIcon()`
- `CreateButtonIcon()`
- `CreateIndicatorIcon()`

Also copy `CreateIconCanvas()` and `WrapIcon()` helpers.

**Make sure to add the using aliases** at the top of the file:
```csharp
using ShapeEllipse = Avalonia.Controls.Shapes.Ellipse;
using ShapePath = Avalonia.Controls.Shapes.Path;
using ShapeRectangle = Avalonia.Controls.Shapes.Rectangle;
```

### Step 5: Remove Old Helpers

Delete `CreateProjectTypePickerButton()` — it's fully replaced by `CreateTypeChangeCard()`.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, open a project, click "Change type..." in the toolbar:
- Dialog should look identical to the launcher's type picker but with the current type dimmed.
- Clicking a non-current card switches type and closes.
- Cancel button closes without changes.
- Token-based colors throughout — no raw `Brushes.Gray`.

---

## Subphase 11B: Confirm Dialog Redesign

### What to Do

Restyle `ShowProjectTypeConfirmDialogAsync()` to use design tokens and the same card-based dialog pattern as the launcher's error dialog.

### Step 1: Rewrite the Confirm Dialog

```csharp
private async Task<bool> ShowProjectTypeConfirmDialogAsync(string title, string message, string confirmText)
{
    bool confirmed = false;
    var dialog = new Window
    {
        Title = title,
        Width = 520,
        Height = 260,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = BrushFromHex(Surface0Hex)
    };

    var confirmButton = new Button
    {
        Content = confirmText,
        MinWidth = 120,
        Background = BrushFromHex(AccentSubtleHex),
        BorderBrush = BrushFromHex(AccentHex),
        BorderThickness = new Thickness(1),
        Foreground = BrushFromHex(TextPrimaryHex),
        Padding = new Thickness(14, 8),
        CornerRadius = new CornerRadius(6)
    };
    confirmButton.Click += (_, _) =>
    {
        confirmed = true;
        dialog.Close();
    };

    var cancelButton = new Button
    {
        Content = "Cancel",
        MinWidth = 90,
        Background = Brushes.Transparent,
        BorderBrush = BrushFromHex(BorderDefaultHex),
        BorderThickness = new Thickness(1),
        Foreground = BrushFromHex(TextSecondaryHex),
        Padding = new Thickness(14, 8),
        CornerRadius = new CornerRadius(6)
    };
    cancelButton.Click += (_, _) => dialog.Close();

    dialog.Content = new Border
    {
        Margin = new Thickness(24),
        Padding = new Thickness(20),
        Background = BrushFromHex(Surface1Hex),
        BorderBrush = BrushFromHex(BorderSubtleHex),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Child = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = BrushFromHex(TextPrimaryHex)
                },
                new TextBlock
                {
                    Text = message,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = BrushFromHex(TextSecondaryHex),
                    [Grid.RowProperty] = 1
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancelButton, confirmButton },
                    [Grid.RowProperty] = 2
                }
            }
        }
    };

    await dialog.ShowDialog(this);
    return confirmed;
}
```

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Open a RotaryKnob project, click "Change type...", select FlipSwitch:
- A styled confirm dialog should appear with Surface1 card, AccentSubtle confirm button, ghost Cancel.
- Clicking "Change Type" proceeds. Clicking "Cancel" aborts.

---

## Subphase 11C: Inspector Panel Consistency Pass

### What to Do

Normalize the 4 non-knob inspector sections in `MainWindow.axaml` for visual consistency. This is **NOT a redesign** — it's a polish pass to fix inconsistencies.

### Changes to Apply

#### 1. Standardize Expander Headers (naming consistency)

| Current | Change To |
|---------|-----------|
| `"Slider assembly · Preview"` | `"Slider assembly"` |
| `"Toggle · Preview"` | `"Toggle assembly"` |
| `"Push button · Preview"` | `"Push button assembly"` |
| `"Indicator light"` | `"Indicator light assembly"` (already consistent) |

**Reasoning**: "· Preview" suffix is confusing — it implies a preview pane exists inside the expander, but there isn't one. Drop it for clarity.

#### 2. Add Section Tags Where Missing

**Slider "Geometry · Advanced"** currently has no section tags. Add them:

```xml
<TextBlock Classes="section-tag" Text="BACKPLATE"/>
<!-- ...backplate width, height, thickness... -->

<TextBlock Classes="section-tag" Text="THUMB"/>
<!-- ...thumb width, height, depth... -->
```

**Push Button** is too sparse for section tags (only 1 control). Leave as-is.

#### 3. Add Hint Text to Slider Setup

The Slider Setup section has no hint text explaining what it does. Add one after the first Expander opens, matching the Indicator Light pattern:

```xml
<TextBlock Text="Backplate and thumb mesh configuration." Classes="hint"/>
```

#### 4. Add Hint Text to Toggle Setup

Same pattern:
```xml
<TextBlock Text="Base and lever mesh configuration with state control." Classes="hint"/>
```

#### 5. Standardize "Refresh library" Button Position

Both Slider and Toggle have a `DockPanel` with "Refresh library" button and "Mesh sources" section title. The DockPanel pattern is visually awkward. Replace with a cleaner layout:

```xml
<TextBlock Classes="section-tag" Text="MESH SOURCES"/>
<Grid ColumnDefinitions="120,*,Auto" ColumnSpacing="8">
    <TextBlock Grid.Column="0" Classes="param-label" Text="Backplate mesh"/>
    <ComboBox Grid.Column="1" x:Name="SliderBackplateMeshCombo"/>
    <Button Grid.Column="2" x:Name="RefreshSliderLibraryButton"
            Content="↻" ToolTip.Tip="Refresh mesh library"
            MinWidth="28" Padding="4,4"/>
</Grid>
```

**Wait** — this changes the Grid column count from 2 to 3, which means the handler code that doesn't reference grid columns will still work (it only references `x:Name`). But verify the button `x:Name` stays the same: `RefreshSliderLibraryButton` / `RefreshToggleLibraryButton`.

**Alternative (safer)**: Keep the DockPanel but add a section-tag above it and style the button more compactly:

```xml
<TextBlock Classes="section-tag" Text="MESH SOURCES"/>
<DockPanel LastChildFill="True">
    <Button x:Name="RefreshSliderLibraryButton"
            Content="↻"
            ToolTip.Tip="Refresh mesh library"
            DockPanel.Dock="Right"
            Margin="8,0,0,0"
            MinWidth="32"
            Padding="6,4"/>
    <Grid ColumnDefinitions="120,*" ColumnSpacing="8">
        <TextBlock Grid.Column="0" Classes="param-label" Text="Backplate mesh"/>
        <ComboBox Grid.Column="1" x:Name="SliderBackplateMeshCombo"/>
    </Grid>
</DockPanel>
```

Use this safer alternative. Apply to both Slider and Toggle. Remove the old "Mesh sources" `section-title` TextBlock and replace with the `section-tag` above the DockPanel.

#### 6. Push Button Polish

The Push Button has a stub hint "Procedural push-button assembly controls." — reword to be more helpful:

```xml
<TextBlock Text="Press amount controls the button depression depth. More geometry controls coming soon." Classes="hint"/>
```

#### 7. Indicator Light: Expand "Lat segments" / "Long segments" Labels

These abbreviations are unclear. Expand them:

| Current | Change To |
|---------|-----------|
| `"Lat segments"` | `"Latitude segments"` |
| `"Long segments"` | `"Longitude segments"` |

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Open projects of each type and verify:
- All inspector sections have consistent header naming.
- Section tags appear where needed.
- Hint text provides context in Setup sections.
- "Refresh library" buttons are compact and positioned consistently.
- No existing x:Name values have changed.
- All handler wiring still works (click buttons, change values, verify viewport updates).

---

## File Touchpoints

### Modified Files

| File | Subphases | Changes |
|------|-----------|---------|
| `MainWindow.ProjectTypeCommands.cs` | 11A, 11B | Full rewrite of type change dialog + confirm dialog, add hex constants, add icon factories, remove old button helper |
| `MainWindow.axaml` (lines ~441–900, ~1223–1519) | 11C | Inspector section header renames, section tags, hint text, refresh button layout, label expansions |

### New Files

None.

### Untouched Files

| File | Reason |
|------|--------|
| `App.axaml` | Use existing tokens, do not modify |
| `App.axaml.cs` | Startup flow unchanged |
| `ProjectLauncherWindow.*` | Already polished in Phase 10 |
| `MainWindow.SliderAssemblyHandlers.cs` | Event handler logic unchanged |
| `MainWindow.ToggleAssemblyHandlers.cs` | Event handler logic unchanged |
| `MainWindow.PushButtonAssemblyHandlers.cs` | Event handler logic unchanged |
| `MainWindow.IndicatorLightHandlers.cs` | Event handler logic unchanged |
| All `*Catalog.cs` files | Data loading unchanged |
| All Core / Rendering code | No backend changes |

---

## Appendix: Icon Factory Methods

Copy these verbatim from `ProjectLauncherWindow.axaml.cs` (they are `private static` and need to be duplicated or extracted to a shared helper):

```csharp
private static Canvas CreateIconCanvas() => new() { Width = 16, Height = 16 };

private static Viewbox WrapIcon(Canvas canvas) => new() { Width = 24, Height = 24, Child = canvas };

private static Viewbox CreateKnobIcon()
{
    var canvas = CreateIconCanvas();
    var outline = new ShapeEllipse { Width = 13, Height = 13, Stroke = BrushFromHex(TextSecondaryHex), StrokeThickness = 1.4 };
    Canvas.SetLeft(outline, 1.5); Canvas.SetTop(outline, 1.5);
    canvas.Children.Add(outline);
    canvas.Children.Add(new ShapePath { Data = Geometry.Parse("M8,2 L8,5.5"), Stroke = BrushFromHex(AccentHex), StrokeThickness = 2, StrokeLineCap = PenLineCap.Round });
    return WrapIcon(canvas);
}

// ... CreateSwitchIcon(), CreateSliderIcon(), CreateButtonIcon(), CreateIndicatorIcon()
// Copy from ProjectLauncherWindow.axaml.cs lines 446-563
```

## Appendix: Color Token Quick Reference

```csharp
private const string Surface0Hex = "#0F1317";
private const string Surface1Hex = "#141820";
private const string Surface2Hex = "#1A1F28";
private const string Surface3Hex = "#222830";
private const string BorderSubtleHex = "#252C35";
private const string BorderDefaultHex = "#2E3640";
private const string BorderStrongHex = "#3A4450";
private const string TextPrimaryHex = "#E2EAF2";
private const string TextSecondaryHex = "#A8B4C0";
private const string TextTertiaryHex = "#707C88";
private const string AccentHex = "#4A90B8";
private const string AccentSubtleHex = "#2A4A60";
```
