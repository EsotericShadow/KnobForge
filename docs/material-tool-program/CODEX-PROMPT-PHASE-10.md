# Phase 10: Project Launcher Overhaul — Codex Implementation Prompt

## Your Role

You are implementing Phase 10 of the KnobForge Material Tool Transformation. Your job is to turn the functional-but-bare `ProjectLauncherWindow` into a polished, professional project hub that feels like it belongs alongside Unity Hub, DaVinci Resolve's Project Manager, and Unreal's Project Browser. Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–9 are complete. The app has a mature design token system defined in `App.axaml` (Phase 8) with surface elevations, border tiers, text hierarchy, accent color, and spacing tokens. **This phase must use those tokens everywhere — no raw hex colors.**

## What Phase 10 Does

Redesigns the `ProjectLauncherWindow` from a basic 3-row layout (header / listbox / status bar) into a professional project hub with:

1. **Branded hero header** with the app name, version, and a prominent "New Project" action.
2. **Rich project cards** with large thumbnails, proper hover/selection states, and clear typography hierarchy.
3. **Beautiful empty state** when no projects exist — an inviting call to action, not a bare text line.
4. **Professional "New Project" type picker** — a styled modal with SVG icons for each project type, not raw code-behind buttons.
5. **Design token integration** — every color, font size, and spacing references the existing token system.
6. **Consistent with MainWindow chrome** — the launcher should feel like the same app.

**Explicitly deferred** (do NOT implement):
- Project search/filter (future feature).
- Project deletion or renaming from the launcher.
- Drag-and-drop to open `.knob` files.
- Grid view toggle (list-only is fine).
- Animated transitions.

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify `App.axaml` design tokens.** Use the existing token system. You may add **new** styles scoped to the launcher (using `Window.Styles` or `Window.Resources` inside `ProjectLauncherWindow.axaml`) but do not change any existing global resources.
2. **Do NOT modify `App.axaml.cs`.** The startup flow (`LaunchRequested` event, `BuildMainWindow`, etc.) stays identical.
3. **Do NOT modify any Core or Rendering code.** This is purely a UI change to the launcher window.
4. **Do NOT change the `ProjectLauncherResult` class or `ProjectCard` inner class public API.** The data flow between launcher and App must remain compatible.
5. **Do NOT change `KnobProjectFileStore` or the `.knob` file format.**
6. **Keep all existing `x:Name` values.** Code-behind resolves controls by name via `FindControl`.
7. **The app must compile and run identically after each subphase.**

---

## Existing Architecture (Read Before Coding)

### ProjectLauncherWindow.axaml — Current Structure

```
Window (1020×680, Title="KnobForge Projects")
└─ Grid RowDefinitions="Auto,*,Auto"
   ├─ Row 0: Header — title + subtitle + 3 action buttons (New, Open Selected, Browse)
   ├─ Row 1: Body — ListBox with project cards (thumbnail 122×122 + name/date/path)
   └─ Row 2: Footer — status text
```

**Problems to fix:**
- All colors are hardcoded hex values (`#11161A`, `#0D1216`, `#2A3138`, `#E6EEF5`, `#A9B4BF`, `#708294`, `#182129`, `#33414F`, `#10161C99`).
- No hover state on project cards — selection is the only visual feedback.
- Thumbnail placeholder shows a static "Preview" text over a tinted overlay even when a real thumbnail exists.
- The empty state is a single line of text in the status bar.
- The "New Project" dialog is built entirely in code-behind with no visual design.
- Buttons are unstyled Fluent defaults — no accent color for the primary action.
- No branding or visual identity — it's a generic dark window.

### ProjectLauncherWindow.axaml.cs — Code-Behind

Key methods (preserve these, modify only their visual output):
- `ReloadProjects()` — populates `_projectCards` from `KnobProjectFileStore.GetLauncherEntries()`.
- `ShowProjectTypePickerAsync()` — builds a modal Window in code with 5 project type buttons. **This entire method gets redesigned in Phase 10.**
- `CreateProjectTypeButton(...)` — helper that creates styled buttons for the type picker.
- `OnProjectListDoubleTapped(...)` — fires `LaunchRequested` on double-click.
- `UpdateSelectionActions()` — enables/disables "Open Selected" button.

### Data Model

```csharp
public sealed class KnobProjectLauncherEntry
{
    public string FilePath { get; set; }
    public string DisplayName { get; set; }
    public DateTime SavedUtc { get; set; }
    public string? ThumbnailPngBase64 { get; set; }
}
```

```csharp
public enum InteractorProjectType
{
    RotaryKnob = 0,
    FlipSwitch = 1,
    ThumbSlider = 2,
    PushButton = 3,
    IndicatorLight = 4
}
```

### Design Token System (App.axaml)

All tokens are defined as `Color` + `SolidColorBrush` pairs:

| Token | Hex | Purpose |
|-------|-----|---------|
| `Surface0` / `Surface0Brush` | `#0F1317` | Deepest background, chrome |
| `Surface1` / `Surface1Brush` | `#141820` | Panel backgrounds |
| `Surface2` / `Surface2Brush` | `#1A1F28` | Card/inset backgrounds |
| `Surface3` / `Surface3Brush` | `#222830` | Hover/raised elements |
| `BorderSubtle` / `BorderSubtleBrush` | `#252C35` | Section dividers |
| `BorderDefault` / `BorderDefaultBrush` | `#2E3640` | Control borders |
| `BorderStrong` / `BorderStrongBrush` | `#3A4450` | Focused/active borders |
| `TextPrimary` / `TextPrimaryBrush` | `#E2EAF2` | Headings, primary content |
| `TextSecondary` / `TextSecondaryBrush` | `#A8B4C0` | Labels, descriptions |
| `TextTertiary` / `TextTertiaryBrush` | `#707C88` | Hints, paths, timestamps |
| `Accent` / `AccentBrush` | `#4A90B8` | Primary accent (blue) |
| `AccentSubtle` / `AccentSubtleBrush` | `#2A4A60` | Accent background |

Spacing tokens (as `Thickness`):

| Token | Value |
|-------|-------|
| `SpaceXs` | `0,2,0,2` |
| `SpaceSm` | `0,4,0,4` |
| `SpaceMd` | `0,8,0,8` |
| `SpaceLg` | `0,12,0,12` |

Text styles (as classes defined in `Application.Styles`):

| Class | FontSize | FontWeight | Foreground |
|-------|----------|------------|------------|
| `section-title` | 11 | SemiBold | TextTertiary |
| `subtitle` | 12 | Normal | TextSecondary |
| `hint` | 11 | Normal | TextTertiary |

---

## Design Reference: Professional Project Launchers

Study these patterns (do not copy, but follow the conventions):

**Unity Hub**: Left sidebar with "New Project" CTA at top, project list with large thumbnails on the right, each card shows name + path + last modified + editor version. Hover lifts the card slightly. Selection highlights with accent border.

**DaVinci Resolve Project Manager**: Grid of project thumbnails with name below, dark background, selected project gets a bright border. "New Project" is a prominent button.

**Unreal Project Browser**: Two-column layout — left has categories/templates, right shows project cards. Cards have large preview images with title overlay at bottom.

**Figma Recent Files**: Clean card grid, large thumbnails, name + timestamp below, hover shows a subtle border + shadow.

**Common patterns across all of them:**
- The primary CTA ("New Project" / "Create") is visually prominent — larger, accent-colored, or icon-accompanied.
- Project cards are visually rich — thumbnails dominate, metadata is secondary.
- Hover state is distinct from selection state.
- Empty states are inviting, not error-like.
- The launcher feels like a branded experience, not a file picker.

---

## Subphase 10A: Token Integration and Window Chrome

### What to Do

Replace all hardcoded hex colors in `ProjectLauncherWindow.axaml` with design token references, and set the window background to match the app chrome.

### Step 1: Reference Tokens via StaticResource

The launcher window does not currently import the App-level resources, but because it's part of the same `Application`, all resources defined in `App.axaml` `Application.Resources` are available via `{StaticResource}`.

**Replace every hardcoded color**:

| Old Hex | Replace With |
|---------|-------------|
| `#11161A` (header/footer bg) | `{StaticResource Surface0Brush}` |
| `#0D1216` (body bg) | `{StaticResource Surface0Brush}` |
| `#2A3138` (borders) | `{StaticResource BorderSubtleBrush}` |
| `#E6EEF5` (primary text) | `{StaticResource TextPrimaryBrush}` |
| `#A9B4BF` (secondary text) | `{StaticResource TextSecondaryBrush}` |
| `#708294` (tertiary text/paths) | `{StaticResource TextTertiaryBrush}` |
| `#182129` (thumbnail bg) | `{StaticResource Surface2Brush}` |
| `#33414F` (thumbnail border) | `{StaticResource BorderDefaultBrush}` |
| `#10161C99` (thumbnail overlay) | Remove or replace with a subtle overlay using Surface0 at ~40% opacity |

### Step 2: Set Window Background

Add to the Window element:

```xml
<Window ...
        Background="{StaticResource Surface0Brush}">
```

This ensures the window chrome matches the main app. On macOS, the title bar will pick up the dark background automatically via `ExtendClientAreaToDecorationsHint` (optional — only add if it works without breaking the title bar buttons).

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, verify:
- All colors now come from tokens.
- Visual appearance is similar but now consistent with MainWindow's palette.
- No raw hex values remain in the AXAML (except inside Window.Resources if you define launcher-specific colors).

---

## Subphase 10B: Hero Header Redesign

### What to Do

Transform the header from a basic title + buttons row into a branded hero area with visual hierarchy and a prominent primary action.

### Step 1: Restructure the Header Layout

Replace the current header Border contents with a more spacious, branded layout:

```xml
<Border Grid.Row="0"
        Background="{StaticResource Surface1Brush}"
        BorderBrush="{StaticResource BorderSubtleBrush}"
        BorderThickness="0,0,0,1"
        Padding="24,20">
    <Grid ColumnDefinitions="*,Auto">
        <!-- Branding -->
        <StackPanel Grid.Column="0" Spacing="4">
            <StackPanel Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
                <!-- App icon: a simple knob silhouette -->
                <Border Width="36" Height="36" CornerRadius="8"
                        Background="{StaticResource AccentSubtleBrush}"
                        BorderBrush="{StaticResource AccentBrush}"
                        BorderThickness="1">
                    <Viewbox Width="20" Height="20" Margin="8">
                        <Canvas Width="16" Height="16">
                            <!-- Simple knob icon: circle with pointer -->
                            <Ellipse Width="14" Height="14" Canvas.Left="1" Canvas.Top="1"
                                     Stroke="{StaticResource TextPrimaryBrush}" StrokeThickness="1.5"/>
                            <Path Data="M8,2 L8,5"
                                  Stroke="{StaticResource AccentBrush}" StrokeThickness="2"
                                  StrokeLineCap="Round"/>
                        </Canvas>
                    </Viewbox>
                </Border>
                <StackPanel Spacing="0">
                    <TextBlock Text="KnobForge"
                               FontSize="22"
                               FontWeight="Bold"
                               Foreground="{StaticResource TextPrimaryBrush}"/>
                    <TextBlock Text="Skeuomorphic Knob Renderer"
                               FontSize="11"
                               Foreground="{StaticResource TextTertiaryBrush}"/>
                </StackPanel>
            </StackPanel>
        </StackPanel>

        <!-- Actions -->
        <StackPanel Grid.Column="1"
                    Orientation="Horizontal"
                    Spacing="8"
                    VerticalAlignment="Center">
            <Button x:Name="BrowseProjectButton"
                    Content="Browse..."
                    MinWidth="92"
                    Padding="12,8"/>
            <Button x:Name="OpenSelectedProjectButton"
                    Content="Open Selected"
                    MinWidth="118"
                    Padding="12,8"/>
            <Button x:Name="NewProjectButton"
                    MinWidth="128"
                    Padding="14,8">
                <StackPanel Orientation="Horizontal" Spacing="6">
                    <Viewbox Width="14" Height="14">
                        <Canvas Width="16" Height="16">
                            <Path Data="M8,2 L8,14 M2,8 L14,8"
                                  Stroke="{StaticResource TextPrimaryBrush}"
                                  StrokeThickness="2" StrokeLineCap="Round"/>
                        </Canvas>
                    </Viewbox>
                    <TextBlock Text="New Project"
                               VerticalAlignment="Center"
                               FontWeight="SemiBold"/>
                </StackPanel>
            </Button>
        </StackPanel>
    </Grid>
</Border>
```

### Step 2: Style the Primary Action Button

Add a launcher-local style in `Window.Styles` to give the "New Project" button accent treatment:

```xml
<Window.Styles>
    <Style Selector="Button#NewProjectButton">
        <Setter Property="Background" Value="{StaticResource AccentSubtleBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    </Style>
    <Style Selector="Button#NewProjectButton:pointerover">
        <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
    </Style>
</Window.Styles>
```

**Button order change**: "New Project" moves to the far right (most prominent position). "Browse" moves to the far left (least common action). This follows Fitts's Law — the primary action is at the natural scan-end of the header.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, verify:
- Header has clear branding with knob icon.
- "New Project" button is visually prominent (accent background).
- Buttons are properly spaced and sized.
- The header feels more spacious and branded than before.

---

## Subphase 10C: Project Card Redesign

### What to Do

Redesign the project list items from basic text rows into rich, hoverable cards with better visual hierarchy.

### Step 1: Redesign the DataTemplate

Replace the existing `ListBox.ItemTemplate` with a richer card layout:

```xml
<ListBox.ItemTemplate>
    <DataTemplate>
        <Border x:Name="ProjectCardBorder"
                Background="{StaticResource Surface1Brush}"
                BorderBrush="{StaticResource BorderSubtleBrush}"
                BorderThickness="1"
                CornerRadius="8"
                Padding="12"
                Margin="0,0,0,8"
                Cursor="Hand">
            <Grid ColumnDefinitions="140,*" ColumnSpacing="16">
                <!-- Thumbnail -->
                <Border Grid.Column="0"
                        Width="140"
                        Height="140"
                        CornerRadius="6"
                        ClipToBounds="True"
                        Background="{StaticResource Surface2Brush}"
                        BorderBrush="{StaticResource BorderDefaultBrush}"
                        BorderThickness="1">
                    <Panel>
                        <Image Source="{Binding Thumbnail}"
                               Stretch="UniformToFill"/>
                        <!-- Placeholder only shows when no thumbnail -->
                        <StackPanel HorizontalAlignment="Center"
                                    VerticalAlignment="Center"
                                    Spacing="4"
                                    IsVisible="{Binding Thumbnail, Converter={x:Static ObjectConverters.IsNull}}">
                            <Viewbox Width="28" Height="28">
                                <Canvas Width="16" Height="16">
                                    <Ellipse Width="14" Height="14" Canvas.Left="1" Canvas.Top="1"
                                             Stroke="{StaticResource TextTertiaryBrush}" StrokeThickness="1.2"/>
                                    <Path Data="M8,2 L8,5"
                                          Stroke="{StaticResource TextTertiaryBrush}" StrokeThickness="1.5"
                                          StrokeLineCap="Round"/>
                                </Canvas>
                            </Viewbox>
                            <TextBlock Text="No Preview"
                                       FontSize="10"
                                       Foreground="{StaticResource TextTertiaryBrush}"
                                       HorizontalAlignment="Center"/>
                        </StackPanel>
                    </Panel>
                </Border>

                <!-- Project Info -->
                <StackPanel Grid.Column="1"
                            VerticalAlignment="Center"
                            Spacing="6">
                    <TextBlock Text="{Binding DisplayName}"
                               FontSize="17"
                               FontWeight="SemiBold"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               TextTrimming="CharacterEllipsis"/>
                    <TextBlock Text="{Binding SavedDisplay}"
                               FontSize="12"
                               Foreground="{StaticResource TextSecondaryBrush}"/>
                    <TextBlock Text="{Binding FilePath}"
                               FontSize="11"
                               Foreground="{StaticResource TextTertiaryBrush}"
                               TextTrimming="CharacterEllipsis"
                               Opacity="0.8"/>
                </StackPanel>
            </Grid>
        </Border>
    </DataTemplate>
</ListBox.ItemTemplate>
```

### Step 2: Add Card Hover and Selection States

Add these styles to the launcher's `Window.Styles`:

```xml
<!-- Card hover -->
<Style Selector="ListBox#ProjectListBox ListBoxItem:pointerover Border#ProjectCardBorder">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
</Style>

<!-- Card selected -->
<Style Selector="ListBox#ProjectListBox ListBoxItem:selected Border#ProjectCardBorder">
    <Setter Property="Background" Value="{StaticResource Surface2Brush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="1.5"/>
</Style>

<!-- Strip default ListBoxItem selection chrome -->
<Style Selector="ListBox#ProjectListBox ListBoxItem">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Padding" Value="4,0"/>
</Style>
<Style Selector="ListBox#ProjectListBox ListBoxItem:selected">
    <Setter Property="Background" Value="Transparent"/>
</Style>
<Style Selector="ListBox#ProjectListBox ListBoxItem:pointerover">
    <Setter Property="Background" Value="Transparent"/>
</Style>
<Style Selector="ListBox#ProjectListBox ListBoxItem:selected:pointerover">
    <Setter Property="Background" Value="Transparent"/>
</Style>
```

**Important**: The default FluentTheme `ListBoxItem` has its own selection background (a blue/purple tint). The styles above override it to `Transparent` so that the inner `Border#ProjectCardBorder` controls all visual feedback instead. This is the standard pattern for custom card layouts in Avalonia ListBoxes.

### Step 3: Style the ListBox Container

Update the ListBox itself for cleaner integration:

```xml
<ListBox x:Name="ProjectListBox"
         Background="Transparent"
         BorderThickness="0"
         Foreground="{StaticResource TextPrimaryBrush}"
         Padding="4">
```

Remove the existing `BorderBrush`, `BorderThickness`, `CornerRadius`, and `Background` from the ListBox (the outer `Border` in Row 1 provides the background).

The body Border becomes:

```xml
<Border Grid.Row="1"
        Background="{StaticResource Surface0Brush}"
        Padding="20,12">
```

### Step 4: Thumbnail Placeholder Logic

The current template shows a "Preview" text with a tinted overlay **on top of the real thumbnail** — this means even when a thumbnail exists, there's an overlay dimming it. Fix this:

- Remove the tinted `Border Background="#10161C99"` overlay.
- Remove the static "Preview" TextBlock.
- Instead, use the `IsVisible` binding shown in Step 1 so the placeholder knob icon + "No Preview" only appear when `Thumbnail` is null.

**Note on IsNull converter**: Avalonia's built-in `ObjectConverters.IsNull` should work for this. If it doesn't compile, use a simple IValueConverter in code-behind or just always show the Image (it will be empty/invisible when Thumbnail is null).

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, verify:
- Project cards have rounded corners, proper spacing, and token-based colors.
- Hovering a card changes its background and border.
- Selecting a card gives it an accent-colored border.
- No default ListBoxItem blue selection chrome is visible.
- Thumbnails display cleanly without a dimming overlay.
- "No Preview" placeholder appears only when there's no thumbnail.

---

## Subphase 10D: Empty State Design

### What to Do

When no projects exist, show an inviting empty state in the body area instead of relying on the status bar text.

### Step 1: Add an Empty State Overlay

Inside the body `Border` (Row 1), add a panel that sits on top of the ListBox and shows when `_projectCards.Count == 0`:

```xml
<Border Grid.Row="1" Background="{StaticResource Surface0Brush}" Padding="20,12">
    <Grid>
        <ListBox x:Name="ProjectListBox" .../>

        <!-- Empty state -->
        <StackPanel x:Name="EmptyStatePanel"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center"
                    Spacing="16"
                    IsVisible="False">
            <!-- Large knob icon -->
            <Border Width="72" Height="72" CornerRadius="16"
                    Background="{StaticResource Surface2Brush}"
                    BorderBrush="{StaticResource BorderDefaultBrush}"
                    BorderThickness="1"
                    HorizontalAlignment="Center">
                <Viewbox Width="36" Height="36" Margin="18">
                    <Canvas Width="16" Height="16">
                        <Ellipse Width="14" Height="14" Canvas.Left="1" Canvas.Top="1"
                                 Stroke="{StaticResource TextTertiaryBrush}" StrokeThickness="1.5"/>
                        <Path Data="M8,2 L8,5"
                              Stroke="{StaticResource AccentBrush}" StrokeThickness="2"
                              StrokeLineCap="Round"/>
                    </Canvas>
                </Viewbox>
            </Border>
            <TextBlock Text="No projects yet"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{StaticResource TextPrimaryBrush}"
                       HorizontalAlignment="Center"/>
            <TextBlock Text="Create a new project to start rendering knobs, switches, sliders, and more."
                       FontSize="13"
                       Foreground="{StaticResource TextSecondaryBrush}"
                       HorizontalAlignment="Center"
                       TextAlignment="Center"
                       MaxWidth="360"/>
            <Button x:Name="EmptyStateNewProjectButton"
                    MinWidth="160"
                    Padding="16,10"
                    HorizontalAlignment="Center"
                    Margin="0,8,0,0">
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <Viewbox Width="14" Height="14">
                        <Canvas Width="16" Height="16">
                            <Path Data="M8,2 L8,14 M2,8 L14,8"
                                  Stroke="{StaticResource TextPrimaryBrush}"
                                  StrokeThickness="2" StrokeLineCap="Round"/>
                        </Canvas>
                    </Viewbox>
                    <TextBlock Text="Create Your First Project"
                               FontWeight="SemiBold"
                               VerticalAlignment="Center"/>
                </StackPanel>
            </Button>
        </StackPanel>
    </Grid>
</Border>
```

### Step 2: Wire Empty State in Code-Behind

In `ProjectLauncherWindow.axaml.cs`, add the field:

```csharp
private readonly StackPanel _emptyStatePanel;
private readonly Button _emptyStateNewProjectButton;
```

Resolve via FindControl in the constructor. Wire the button click:

```csharp
_emptyStateNewProjectButton.Click += OnNewProjectButtonClicked;
```

Update `ReloadProjects()` to toggle visibility:

```csharp
bool hasProjects = _projectCards.Count > 0;
_projectListBox.IsVisible = hasProjects;
_emptyStatePanel.IsVisible = !hasProjects;

_statusTextBlock.Text = hasProjects
    ? $"{_projectCards.Count} project{(_projectCards.Count == 1 ? "" : "s")}"
    : "";
```

### Step 3: Style the Empty State Button

Add to `Window.Styles`:

```xml
<Style Selector="Button#EmptyStateNewProjectButton">
    <Setter Property="Background" Value="{StaticResource AccentSubtleBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
</Style>
<Style Selector="Button#EmptyStateNewProjectButton:pointerover">
    <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
</Style>
```

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app with no saved projects, verify:
- The empty state panel is centered and inviting.
- The "Create Your First Project" button opens the type picker.
- When projects exist, the empty state is hidden and the list shows.

---

## Subphase 10E: Professional "New Project" Type Picker

### What to Do

Replace the current code-behind-built `ShowProjectTypePickerAsync()` dialog with a professionally styled modal featuring SVG icons for each project type.

### Step 1: Redesign ShowProjectTypePickerAsync

Rewrite the method to produce a visually rich dialog. The dialog should have:
- A title area with "Choose Project Type" heading.
- A **grid of type cards** (not a vertical list of buttons) — each card has an icon, title, and description.
- Cards respond to hover with `Surface3` background.
- Clicking a card selects the type and closes the dialog.
- A "Cancel" link (not a button) at the bottom.

Use a 3-column layout for the 5 types (2 rows: 3 + 2), or a single-column list if that looks better. The key improvement is that each type gets a distinctive SVG icon.

**Icons for each project type** (simple, recognizable silhouettes):

| Type | Icon Description | Suggested Path Data |
|------|-----------------|---------------------|
| Rotary Knob | Circle with pointer line | `Ellipse` + vertical `Path` from center to top |
| Flip Switch | Vertical toggle with lever | Two rectangles: base + angled lever |
| Thumb Slider | Horizontal track with thumb | Horizontal line + rectangle on it |
| Push Button | Circle/rounded rectangle depressed | Two concentric rounded rectangles |
| Indicator Light | Circle with radial glow | Filled circle + outer ring |

### Step 2: Build the Dialog Layout in Code-Behind

Since this dialog is generated programmatically, structure it as:

```csharp
private async Task<InteractorProjectType?> ShowProjectTypePickerAsync()
{
    InteractorProjectType? selectedType = null;

    var dialog = new Window
    {
        Title = "New Project",
        Width = 540,
        Height = 480,
        MinWidth = 500,
        MinHeight = 440,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = new SolidColorBrush(Color.Parse("#0F1317")) // Surface0
    };

    var root = new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 20
    };

    // Header
    root.Children.Add(new TextBlock
    {
        Text = "Choose Project Type",
        FontSize = 20,
        FontWeight = FontWeight.SemiBold,
        Foreground = new SolidColorBrush(Color.Parse("#E2EAF2")) // TextPrimary
    });
    root.Children.Add(new TextBlock
    {
        Text = "Select the type of audio plugin control you want to create.",
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.Parse("#A8B4C0")) // TextSecondary
    });

    // Type cards (vertical list — cleaner than grid for 5 items)
    var cardList = new StackPanel { Spacing = 6 };

    cardList.Children.Add(CreateTypeCard(dialog, "Rotary Knob",
        "Encoder and knob-focused workflow with rotation animation.",
        CreateKnobIcon(),
        InteractorProjectType.RotaryKnob, v => selectedType = v));
    cardList.Children.Add(CreateTypeCard(dialog, "Flip Switch",
        "Toggle switch with base and lever meshes.",
        CreateSwitchIcon(),
        InteractorProjectType.FlipSwitch, v => selectedType = v));
    cardList.Children.Add(CreateTypeCard(dialog, "Thumb Slider",
        "Linear slider with backplate and thumb meshes.",
        CreateSliderIcon(),
        InteractorProjectType.ThumbSlider, v => selectedType = v));
    cardList.Children.Add(CreateTypeCard(dialog, "Push Button",
        "Momentary button with push animation scaffold.",
        CreateButtonIcon(),
        InteractorProjectType.PushButton, v => selectedType = v));
    cardList.Children.Add(CreateTypeCard(dialog, "Indicator Light",
        "LED indicator with bezel, dome, and emitter rig.",
        CreateIndicatorIcon(),
        InteractorProjectType.IndicatorLight, v => selectedType = v));

    root.Children.Add(cardList);

    // Cancel
    var cancelText = new TextBlock
    {
        Text = "Cancel",
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.Parse("#707C88")), // TextTertiary
        HorizontalAlignment = HorizontalAlignment.Center,
        Cursor = new Cursor(StandardCursorType.Hand),
        Margin = new Thickness(0, 8, 0, 0)
    };
    cancelText.PointerPressed += (_, _) => dialog.Close();
    root.Children.Add(cancelText);

    dialog.Content = root;
    await dialog.ShowDialog(this);
    return selectedType;
}
```

### Step 3: Create Type Card Builder

```csharp
private static Border CreateTypeCard(
    Window dialog,
    string title,
    string description,
    Control icon,
    InteractorProjectType projectType,
    Action<InteractorProjectType> onSelected)
{
    var card = new Border
    {
        Background = new SolidColorBrush(Color.Parse("#141820")),   // Surface1
        BorderBrush = new SolidColorBrush(Color.Parse("#2E3640")),  // BorderDefault
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(14, 12),
        Cursor = new Cursor(StandardCursorType.Hand)
    };

    var grid = new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
    };

    // Icon container
    var iconBorder = new Border
    {
        Width = 40,
        Height = 40,
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(Color.Parse("#1A1F28")),  // Surface2
        Margin = new Thickness(0, 0, 14, 0),
        Child = icon,
        VerticalAlignment = VerticalAlignment.Center
    };
    Grid.SetColumn(iconBorder, 0);
    grid.Children.Add(iconBorder);

    // Text
    var textPanel = new StackPanel
    {
        Spacing = 2,
        VerticalAlignment = VerticalAlignment.Center
    };
    textPanel.Children.Add(new TextBlock
    {
        Text = title,
        FontSize = 14,
        FontWeight = FontWeight.SemiBold,
        Foreground = new SolidColorBrush(Color.Parse("#E2EAF2"))   // TextPrimary
    });
    textPanel.Children.Add(new TextBlock
    {
        Text = description,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.Parse("#A8B4C0"))   // TextSecondary
    });
    Grid.SetColumn(textPanel, 1);
    grid.Children.Add(textPanel);

    card.Child = grid;

    // Hover
    card.PointerEntered += (_, _) =>
    {
        card.Background = new SolidColorBrush(Color.Parse("#222830"));    // Surface3
        card.BorderBrush = new SolidColorBrush(Color.Parse("#3A4450"));   // BorderStrong
    };
    card.PointerExited += (_, _) =>
    {
        card.Background = new SolidColorBrush(Color.Parse("#141820"));    // Surface1
        card.BorderBrush = new SolidColorBrush(Color.Parse("#2E3640"));   // BorderDefault
    };

    // Click
    card.PointerPressed += (_, _) =>
    {
        onSelected(projectType);
        dialog.Close();
    };

    return card;
}
```

### Step 4: Create SVG Icon Methods

Create simple icon factory methods that return a `Viewbox` with `Canvas` + `Path`/`Ellipse` elements:

```csharp
private static Viewbox CreateKnobIcon()
{
    // Rotary knob: circle with pointer
    var canvas = new Canvas { Width = 16, Height = 16 };
    canvas.Children.Add(new Avalonia.Controls.Shapes.Ellipse
    {
        Width = 13, Height = 13,
        [Canvas.LeftProperty] = 1.5, [Canvas.TopProperty] = 1.5,
        Stroke = new SolidColorBrush(Color.Parse("#A8B4C0")),
        StrokeThickness = 1.4
    });
    canvas.Children.Add(new Avalonia.Controls.Shapes.Path
    {
        Data = Avalonia.Media.Geometry.Parse("M8,2 L8,5.5"),
        Stroke = new SolidColorBrush(Color.Parse("#4A90B8")),
        StrokeThickness = 2,
        StrokeLineCap = PenLineCap.Round
    });
    return new Viewbox { Width = 22, Height = 22, Child = canvas };
}
```

Implement similar methods for each type: `CreateSwitchIcon()`, `CreateSliderIcon()`, `CreateButtonIcon()`, `CreateIndicatorIcon()`. Use the same color palette — `TextSecondary` for outlines, `Accent` for active/highlighted parts.

**Note**: Since these are code-behind-generated (not AXAML), you must create `Ellipse`, `Path`, `Rectangle` etc. programmatically. Reference the correct Avalonia shapes namespace:
```csharp
using Avalonia.Controls.Shapes;
```

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, click "New Project", verify:
- The dialog looks professional with card-based type selection.
- Each card has a distinct icon, title, and description.
- Hover changes background and border colors.
- Clicking a card selects the type and closes the dialog.
- Cancel text at bottom closes without selection.

---

## Subphase 10F: Footer Polish and Final Touches

### What to Do

Refine the status bar footer and add any remaining polish.

### Step 1: Minimal Footer

The footer should be subtle — just a quiet status line:

```xml
<Border Grid.Row="2"
        Background="{StaticResource Surface1Brush}"
        BorderBrush="{StaticResource BorderSubtleBrush}"
        BorderThickness="0,1,0,0"
        Padding="24,8">
    <Grid ColumnDefinitions="*,Auto">
        <TextBlock x:Name="LauncherStatusTextBlock"
                   Grid.Column="0"
                   FontSize="11"
                   Foreground="{StaticResource TextTertiaryBrush}"
                   VerticalAlignment="Center"/>
        <TextBlock Grid.Column="1"
                   FontSize="11"
                   Foreground="{StaticResource TextTertiaryBrush}"
                   VerticalAlignment="Center"
                   Text="Double-click a project to open"/>
    </Grid>
</Border>
```

### Step 2: Final AXAML Audit

Scan the entire `ProjectLauncherWindow.axaml` and verify:
- Zero hardcoded hex values outside `Window.Resources` (if any were defined).
- All text uses `TextPrimaryBrush`, `TextSecondaryBrush`, or `TextTertiaryBrush`.
- All backgrounds use `Surface0Brush`, `Surface1Brush`, or `Surface2Brush`.
- All borders use `BorderSubtleBrush`, `BorderDefaultBrush`, or `BorderStrongBrush`.
- The accent color is used only for the primary action button and selected card border.

### Step 3: Final Code-Behind Audit

Scan `ProjectLauncherWindow.axaml.cs` and verify:
- All `Color.Parse` calls in the type picker dialog use the correct token hex values (with comments labeling the token name).
- No color is used that doesn't exist in the token system.
- The `CreateProjectTypeButton` helper can be removed (replaced by `CreateTypeCard`).

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Final visual check:
- Launch with saved projects → header + rich card list + subtle footer.
- Launch with no projects → header + centered empty state + footer.
- Click "New Project" → polished type picker with icons.
- Select a type → main window opens correctly.
- Open an existing project → main window opens and loads correctly.
- Double-click a project → same behavior.
- Browse for a `.knob` file → file picker works as before.

---

## File Touchpoints

### Modified Files

| File | Subphases | Changes |
|------|-----------|---------|
| `KnobForge.App/Views/ProjectLauncherWindow.axaml` | 10A, 10B, 10C, 10D, 10F | Complete AXAML redesign: token colors, hero header, card template, empty state, footer, Window.Styles |
| `KnobForge.App/Views/ProjectLauncherWindow.axaml.cs` | 10D, 10E | New fields for empty state, rewritten `ShowProjectTypePickerAsync` + `CreateTypeCard` + icon factories, updated `ReloadProjects` visibility logic |

### New Files

None.

### Untouched Files

| File | Reason |
|------|--------|
| `App.axaml` | Use existing tokens, do not modify |
| `App.axaml.cs` | Startup flow unchanged |
| `KnobProjectFileStore.cs` | Data layer unchanged |
| All Core / Rendering code | No backend changes |
| `MainWindow.axaml` + code-behind | Not touched by this phase |

---

## Appendix: Color Token Quick Reference

For the code-behind `Color.Parse` calls in the type picker dialog (since AXAML resources aren't directly accessible in a dynamically-created Window):

```csharp
// Surface levels
const string Surface0 = "#0F1317";
const string Surface1 = "#141820";
const string Surface2 = "#1A1F28";
const string Surface3 = "#222830";

// Borders
const string BorderSubtle  = "#252C35";
const string BorderDefault = "#2E3640";
const string BorderStrong  = "#3A4450";

// Text
const string TextPrimary   = "#E2EAF2";
const string TextSecondary = "#A8B4C0";
const string TextTertiary  = "#707C88";

// Accent
const string Accent       = "#4A90B8";
const string AccentSubtle = "#2A4A60";
```

**Recommendation**: Define these as `private const string` fields in the class so they're named and documented, not scattered as magic strings through the dialog-building code.
