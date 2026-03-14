# Phase 9: Floating Brush Transport — Codex Implementation Prompt

## Your Role

You are implementing Phase 9 of the KnobForge Material Tool Transformation. Your job is to turn the existing `ViewportBrushDock` (a hidden, static toolbar) into a professional floating transport panel that lives inside the 3D viewport, is always visible when relevant, and can be dragged to any position within the viewport bounds. Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–8 are complete:
- **Phase 1**: UV infrastructure — vertex UVs flow through the pipeline.
- **Phase 2**: Texture map import — PBR textures on slots 4–7.
- **Phase 3**: Paint system upgrades — variable resolution paint masks, layer compositing.
- **Phase 4**: Multi-material support — per-SubMesh draw calls with per-material textures.
- **Phase 5**: Texture bake pipeline — CPU material evaluator exports PBR texture map PNGs.
- **Phase 6**: Inspector control overhaul — all 219 SpriteKnobSliders replaced with compact ValueInput controls.
- **Phase 7**: Node-based material graph — procedural texture generation via DAG.
- **Phase 8**: Inspector UI/UX overhaul — design token system, component theming, resizable sidebars, layout fixes.

## What Phase 9 Does

Transforms the `ViewportBrushDock` from a hidden, bottom-left-anchored overlay into a fully interactive floating transport that:

1. **Is visible by default** when a model is loaded (not gated behind brush-tab selection).
2. **Can be freely dragged** to any position within the viewport bounds.
3. **Stays within viewport bounds** — clamped so it never disappears off-edge.
4. **Has a drag handle** — a small grip region at the top that communicates "I am draggable."
5. **Can be toggled** with a keyboard shortcut (`B` key) and/or a viewport button.
6. **Remembers its position** within the current session (reset to default on app launch).

**Explicitly deferred** (do NOT implement):
- Docking/snapping to viewport edges or other panels.
- Persisting toolbar position to the project file.
- Resizing the toolbar.
- Custom icon rendering (keep existing SVG Path icons).

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify MetalViewport rendering code.** This phase is purely UI overlay logic.
2. **Do NOT modify the existing brush painting pipeline.** Button click handlers, channel/type selection, and paint layer logic must remain identical.
3. **Do NOT modify `App.axaml` design tokens.** Use the existing token system. If you need a new resource, add it to `App.axaml` following the existing naming conventions.
4. **Keep all existing `x:Name` values.** The code-behind in `MainWindow.BrushQuickToolbar.cs` wires to these names via `FindControl`. Renaming breaks the connection.
5. **Use `RenderTransform` with `TranslateTransform` for positioning** — not Canvas.Left/Top, not Margin manipulation. This is the cleanest Avalonia pattern for draggable elements inside a Grid overlay.
6. **The app must compile and run identically after each subphase.** Zero behavior regressions.

---

## Existing Architecture (Read Before Coding)

### Viewport Layout (MainWindow.axaml)

The viewport lives in column 2 of the main 5-column grid:

```xml
<Border Grid.Column="2" ClipToBounds="True">
    <Grid>
        <controls:MetalViewport x:Name="MetalViewport" IsVisible="True"/>

        <Border x:Name="ViewportOverlay"
                IsHitTestVisible="True"
                Focusable="True"
                Background="Transparent"/>

        <Border x:Name="ViewportBrushDock"
                HorizontalAlignment="Left"
                VerticalAlignment="Top"
                ...
                IsVisible="False">
            <!-- Horizontal StackPanel with 4 groups of buttons -->
        </Border>
    </Grid>
</Border>
```

**Layering order**: MetalViewport (bottom) → ViewportOverlay (transparent hit-test surface for pointer/key events) → ViewportBrushDock (top, floating).

The `ViewportOverlay` captures all pointer and keyboard events and forwards them to MetalViewport. The brush dock must intercept its own drag events **before** they reach the overlay.

### Current Visibility Logic (MainWindow.BrushQuickToolbar.cs)

```csharp
private void UpdateBrushQuickToolbarState()
{
    bool brushTabActive = ReferenceEquals(_inspectorTabControl?.SelectedItem, _brushTabItem);
    bool hasModel = GetModelNode() != null;

    if (_viewportBrushDock != null)
    {
        _viewportBrushDock.IsVisible = brushTabActive;
        _viewportBrushDock.IsEnabled = hasModel;
        _viewportBrushDock.Opacity = hasModel ? 1d : 0.55d;
    }
}
```

This method is called when the inspector `TabControl.SelectionChanged` fires and at initialization.

### Existing Pointer Event Forwarding (MainWindow.Initialization.cs)

```csharp
_viewportOverlay.AddHandler(InputElement.PointerPressedEvent, ViewportOverlay_PointerPressed, RoutingStrategies.Tunnel);
_viewportOverlay.AddHandler(InputElement.PointerMovedEvent, ViewportOverlay_PointerMoved, RoutingStrategies.Tunnel);
_viewportOverlay.AddHandler(InputElement.PointerReleasedEvent, ViewportOverlay_PointerReleased, RoutingStrategies.Tunnel);
_viewportOverlay.AddHandler(InputElement.KeyDownEvent, ViewportOverlay_KeyDown, RoutingStrategies.Tunnel);
```

These forward to `MetalViewport.HandlePointerPressedFromOverlay(...)` etc. The brush dock's drag events must NOT propagate to the viewport overlay.

### Existing Drag Delta Pattern (MetalViewport.InputAndBrush.cs)

The codebase already uses this pointer-tracking pattern for camera pan:

```csharp
// On PointerPressed: capture start position
_lastPointer = e.GetPosition(this);

// On PointerMoved: calculate delta, apply
Point current = e.GetPosition(this);
double dx = current.X - _lastPointer.X;
double dy = current.Y - _lastPointer.Y;
_lastPointer = current;
```

Phase 9 replicates this pattern for toolbar dragging.

### Design Token System (App.axaml)

All colors and styles use named resources. Relevant tokens:

| Token | Value | Use |
|-------|-------|-----|
| `Surface1` | `#141820` | Panel backgrounds |
| `Surface2` | `#1A1F28` | Card/inset backgrounds |
| `Surface3` | `#222830` | Hover/raised elements |
| `BorderSubtle` | `#252C35` | Dividers |
| `BorderDefault` | `#2E3640` | Control borders |
| `TextPrimary` | `#E2EAF2` | Primary text |
| `TextTertiary` | `#707C88` | Hints, section tags |
| `Accent` | `#4A90B8` | Active highlights |
| `AccentSubtle` | `#2A4A60` | Active backgrounds |

Use `{StaticResource Surface2Brush}` etc. — all have corresponding `SolidColorBrush` resources.

### Button State Logic (MainWindow.BrushQuickToolbar.cs)

```csharp
private static void ApplyQuickButtonState(Button? button, bool active)
{
    button.Opacity = active ? 1d : 0.75d;
    button.Background = active
        ? new SolidColorBrush(Color.Parse("#2A4A60"))  // AccentSubtle
        : new SolidColorBrush(Color.Parse("#1A1F28")); // Surface2
    button.BorderBrush = active
        ? new SolidColorBrush(Color.Parse("#4A90B8"))  // Accent
        : new SolidColorBrush(Color.Parse("#2E3640")); // BorderDefault
    button.BorderThickness = new Avalonia.Thickness(active ? 1.5 : 1);
}
```

Do not modify this method in Phase 9.

---

## Subphase 9A: Make the Toolbar Draggable

### What to Do

Add drag-to-reposition behavior to `ViewportBrushDock` using `TranslateTransform` and pointer event handlers. The toolbar gets a visible drag handle at the top.

### Step 1: Add TranslateTransform to the Brush Dock (MainWindow.axaml)

On the `ViewportBrushDock` Border element, add a `RenderTransform`:

```xml
<Border x:Name="ViewportBrushDock"
        HorizontalAlignment="Left"
        VerticalAlignment="Top"
        Margin="12"
        ...>
    <Border.RenderTransform>
        <TranslateTransform x:Name="BrushDockTranslate" X="0" Y="0"/>
    </Border.RenderTransform>
    ...
</Border>
```

**Change `VerticalAlignment` from `Bottom` to `Top`** — the default position will be top-left of the viewport with a 12px margin. The TranslateTransform offsets from there.

### Step 2: Add a Drag Handle Region

Insert a drag handle bar **above** the existing button content inside the dock. This is a small Border with a grip visual (three horizontal dots or lines):

```xml
<Border x:Name="ViewportBrushDock" ...>
    <Border.RenderTransform>
        <TranslateTransform x:Name="BrushDockTranslate" X="0" Y="0"/>
    </Border.RenderTransform>
    <StackPanel Spacing="4">
        <!-- Drag handle -->
        <Border x:Name="BrushDockDragHandle"
                Height="14"
                Background="Transparent"
                Cursor="Hand"
                HorizontalAlignment="Stretch"
                CornerRadius="4">
            <StackPanel Orientation="Horizontal"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center"
                        Spacing="3">
                <!-- Grip dots: 3 pairs of small circles -->
                <Ellipse Width="3" Height="3" Fill="{StaticResource TextTertiaryBrush}"/>
                <Ellipse Width="3" Height="3" Fill="{StaticResource TextTertiaryBrush}"/>
                <Ellipse Width="3" Height="3" Fill="{StaticResource TextTertiaryBrush}"/>
                <Ellipse Width="3" Height="3" Fill="{StaticResource TextTertiaryBrush}"/>
                <Ellipse Width="3" Height="3" Fill="{StaticResource TextTertiaryBrush}"/>
            </StackPanel>
        </Border>

        <!-- Existing button row (keep the current Horizontal StackPanel with all 4 groups) -->
        <StackPanel Orientation="Horizontal" Spacing="0">
            ... existing PAINT / CHANNEL / TOOL / ACTIONS groups ...
        </StackPanel>
    </StackPanel>
</Border>
```

The drag handle should get a hover state to communicate interactivity:

```xml
<!-- Add to App.axaml Styles section -->
<Style Selector="Border#BrushDockDragHandle:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface3Brush}"/>
</Style>
```

### Step 3: Create Drag Logic in Code-Behind

Create a new file: `MainWindow.BrushDockDrag.cs`

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private bool _isBrushDockDragging;
        private Point _brushDockDragStart;

        private void WireBrushDockDrag()
        {
            if (_brushDockDragHandle == null || _brushDockTranslate == null)
                return;

            _brushDockDragHandle.PointerPressed += BrushDockDragHandle_PointerPressed;
            _brushDockDragHandle.PointerMoved += BrushDockDragHandle_PointerMoved;
            _brushDockDragHandle.PointerReleased += BrushDockDragHandle_PointerReleased;
        }

        private void BrushDockDragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(_brushDockDragHandle).Properties.IsLeftButtonPressed)
                return;

            _isBrushDockDragging = true;
            _brushDockDragStart = e.GetPosition(this);
            e.Pointer.Capture(_brushDockDragHandle);
            e.Handled = true;  // Prevent event from reaching ViewportOverlay
        }

        private void BrushDockDragHandle_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isBrushDockDragging || _brushDockTranslate == null || _viewportBrushDock == null)
                return;

            Point current = e.GetPosition(this);
            double dx = current.X - _brushDockDragStart.X;
            double dy = current.Y - _brushDockDragStart.Y;
            _brushDockDragStart = current;

            double newX = _brushDockTranslate.X + dx;
            double newY = _brushDockTranslate.Y + dy;

            // Clamp to viewport bounds
            (newX, newY) = ClampBrushDockPosition(newX, newY);

            _brushDockTranslate.X = newX;
            _brushDockTranslate.Y = newY;

            e.Handled = true;
        }

        private void BrushDockDragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isBrushDockDragging)
                return;

            _isBrushDockDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private (double x, double y) ClampBrushDockPosition(double x, double y)
        {
            if (_viewportBrushDock == null)
                return (x, y);

            // Get the viewport container bounds
            // The brush dock's parent Grid is inside the viewport Border (Grid.Column="2")
            var viewportContainer = _viewportBrushDock.Parent as Avalonia.Controls.Panel;
            if (viewportContainer == null)
                return (x, y);

            double viewportW = viewportContainer.Bounds.Width;
            double viewportH = viewportContainer.Bounds.Height;
            double dockW = _viewportBrushDock.Bounds.Width;
            double dockH = _viewportBrushDock.Bounds.Height;
            double margin = 12; // Match the AXAML Margin value

            // The dock starts at (margin, margin) due to HorizontalAlignment=Left, VerticalAlignment=Top, Margin=12.
            // TranslateTransform offsets from that origin.
            // Clamp so the dock stays fully within the viewport.
            double maxX = viewportW - dockW - margin * 2;
            double maxY = viewportH - dockH - margin * 2;

            x = Math.Max(0, Math.Min(x, maxX));
            y = Math.Max(0, Math.Min(y, maxY));

            return (x, y);
        }
    }
}
```

### Step 4: Register Fields and Wire Up

In `MainWindow/MainWindow.cs`, add the field declarations alongside the existing brush quick toolbar fields:

```csharp
private Border? _brushDockDragHandle;
private TranslateTransform? _brushDockTranslate;
```

In `MainWindow.Initialization.cs`, add FindControl calls in the same region as the existing brush toolbar controls:

```csharp
_brushDockDragHandle = this.FindControl<Border>("BrushDockDragHandle");
_brushDockTranslate = this.FindControl<TranslateTransform>("BrushDockTranslate");
```

**Important**: `FindControl` may not resolve `TranslateTransform` by name since it's not a `Control`. If that's the case, resolve it from the dock's RenderTransform instead:

```csharp
_brushDockDragHandle = this.FindControl<Border>("BrushDockDragHandle");
_brushDockTranslate = _viewportBrushDock?.RenderTransform as TranslateTransform;
```

Then call `WireBrushDockDrag()` right after `WireBrushQuickToolbarButtons()` in the initialization sequence.

### Step 5: Prevent Drag Events from Reaching the Viewport

The brush dock sits above `ViewportOverlay` in the visual tree. When the user drags the handle, those pointer events must NOT propagate to the viewport's orbit/pan/paint handlers.

The `e.Handled = true` in the drag handlers takes care of this for the handle region. But the entire brush dock should also block pointer events from passing through to the overlay. The dock already has `IsHitTestVisible` defaulting to `true` since it's a Border with a non-transparent Background. Verify this — if the dock's button clicks are already working without triggering viewport actions, the hit-testing is correct.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Then run the app and verify:
- The brush dock appears in the viewport (see Subphase 9B for visibility changes).
- Pressing and dragging on the grip dots moves the entire toolbar.
- Releasing the mouse stops the drag.
- The toolbar cannot be dragged outside the viewport bounds.
- Clicking buttons inside the toolbar still works (does not trigger drag).
- Viewport orbit/pan/paint still works when clicking outside the toolbar.

---

## Subphase 9B: Visibility and Toggle Logic

### What to Do

Change the toolbar from "only visible when brush tab is active" to "visible whenever a model is loaded, toggleable with the `B` key."

### Step 1: Update Visibility Logic (MainWindow.BrushQuickToolbar.cs)

Replace the visibility logic in `UpdateBrushQuickToolbarState()`:

```csharp
// BEFORE:
_viewportBrushDock.IsVisible = brushTabActive;

// AFTER:
bool hasModel = GetModelNode() != null;
bool userHidden = _brushDockHiddenByUser;  // New field, see below
_viewportBrushDock.IsVisible = hasModel && !userHidden;
```

Add a new field to `MainWindow/MainWindow.cs`:

```csharp
private bool _brushDockHiddenByUser;
```

The toolbar is now visible whenever a model exists, regardless of which inspector tab is selected. The user can hide it with the `B` key.

### Step 2: Add Keyboard Toggle (MainWindow.BrushDockDrag.cs)

Add a method that the existing key handler can call:

```csharp
/// <summary>
/// Called from the viewport KeyDown handler when 'B' is pressed.
/// Toggles brush dock visibility.
/// </summary>
public void ToggleBrushDockVisibility()
{
    _brushDockHiddenByUser = !_brushDockHiddenByUser;
    UpdateBrushQuickToolbarState();
}
```

### Step 3: Wire the B Key (MetalViewport.InputAndBrush.cs)

In the existing `HandleKeyDownFromOverlay` method, add a case for the `B` key. The method currently handles `R` (reset camera) and arrow keys (pan). Add `B` in the same pattern:

```csharp
if (e.Key == Key.B)
{
    // This needs to call back to MainWindow — use an event or callback.
    // The cleanest approach is to raise an event that MainWindow subscribes to.
    OnBrushDockToggleRequested();
    e.Handled = true;
}
```

**Option A — Direct callback**: Add a public `Action? BrushDockToggleRequested` on MetalViewport. MainWindow subscribes in initialization:

```csharp
// In MetalViewport.InputAndBrush.cs:
public Action? BrushDockToggleRequested { get; set; }

private void OnBrushDockToggleRequested()
{
    BrushDockToggleRequested?.Invoke();
}

// In HandleKeyDownFromOverlay:
if (e.Key == Key.B)
{
    OnBrushDockToggleRequested();
    e.Handled = true;
}
```

```csharp
// In MainWindow.Initialization.cs, after MetalViewport is resolved:
_metalViewport.BrushDockToggleRequested = ToggleBrushDockVisibility;
```

**Option B — Handle in ViewportOverlay KeyDown directly**: If the existing `ViewportOverlay_KeyDown` handler in MainWindow already has the KeyEventArgs, just add the `B` check there instead of routing through MetalViewport. Choose whichever approach matches the existing codebase conventions better.

Preferred: **Option B** if `ViewportOverlay_KeyDown` exists in MainWindow and has access to `ToggleBrushDockVisibility()`. Otherwise use Option A.

### Step 4: Visual Feedback for Hidden State

When the user presses `B` to hide the toolbar, provide no additional UI — the toolbar simply disappears. Pressing `B` again brings it back. The toolbar reappears at the same position it was in before hiding (the TranslateTransform values persist).

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Then run the app and verify:
- Load a model → toolbar appears in the viewport.
- Switch inspector tabs → toolbar remains visible.
- Press `B` → toolbar hides.
- Press `B` again → toolbar reappears at the same position.
- Close the model → toolbar hides (no model loaded).
- Load another model → toolbar reappears.

---

## Subphase 9C: Default Position and Polish

### What to Do

Set a sensible default position (bottom-left of viewport, inset from edges), add subtle visual polish to the drag handle, and ensure the toolbar feels integrated with the viewport.

### Step 1: Set Default Position on Load

In the initialization sequence, after the viewport has been measured (use a `LayoutUpdated` callback or defer with `Dispatcher.UIThread.Post`), set the default TranslateTransform to position the dock at the bottom-left:

```csharp
private void SetBrushDockDefaultPosition()
{
    if (_viewportBrushDock == null || _brushDockTranslate == null)
        return;

    var viewportContainer = _viewportBrushDock.Parent as Avalonia.Controls.Panel;
    if (viewportContainer == null || viewportContainer.Bounds.Height <= 0)
        return;

    double viewportH = viewportContainer.Bounds.Height;
    double dockH = _viewportBrushDock.Bounds.Height;
    double margin = 12;

    // Position at bottom-left: X stays at 0 (already left-aligned with margin),
    // Y moves down to bottom minus dock height minus margins
    _brushDockTranslate.X = 0;
    _brushDockTranslate.Y = Math.Max(0, viewportH - dockH - margin * 2);
}
```

Call this once after the first layout pass. Use `_viewportBrushDock.LayoutUpdated` with a one-shot flag:

```csharp
private bool _brushDockDefaultPositionSet;

// In initialization:
if (_viewportBrushDock != null)
{
    _viewportBrushDock.LayoutUpdated += (_, _) =>
    {
        if (!_brushDockDefaultPositionSet && _viewportBrushDock.Bounds.Height > 0)
        {
            _brushDockDefaultPositionSet = true;
            SetBrushDockDefaultPosition();
        }
    };
}
```

### Step 2: Re-Clamp on Viewport Resize

When the window is resized, the toolbar might end up outside the new viewport bounds. Subscribe to the viewport container's `SizeChanged` event:

```csharp
// In initialization, after resolving the viewport container:
var viewportContainer = _viewportBrushDock?.Parent as Avalonia.Controls.Panel;
if (viewportContainer != null)
{
    viewportContainer.SizeChanged += (_, _) =>
    {
        if (_brushDockTranslate != null)
        {
            var (clampedX, clampedY) = ClampBrushDockPosition(
                _brushDockTranslate.X, _brushDockTranslate.Y);
            _brushDockTranslate.X = clampedX;
            _brushDockTranslate.Y = clampedY;
        }
    };
}
```

### Step 3: Drag Handle Hover Polish

Add these styles to `App.axaml` for the drag handle visual feedback:

```xml
<!-- Brush dock drag handle -->
<Style Selector="Border#BrushDockDragHandle">
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Background" Value="Transparent"/>
</Style>
<Style Selector="Border#BrushDockDragHandle:pointerover">
    <Setter Property="Background" Value="{StaticResource Surface3Brush}"/>
</Style>
```

### Step 4: Subtle Drop Shadow (Optional Enhancement)

If Avalonia 11.x supports `BoxShadow` on the target platform, add a subtle shadow to the dock to reinforce that it floats above the viewport:

```xml
<Border x:Name="ViewportBrushDock"
        BoxShadow="0 2 8 0 #40000000"
        ...>
```

If `BoxShadow` is not available or causes issues, skip this — the existing border is sufficient.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Then run the app and verify:
- On first load with a model, the toolbar appears at the **bottom-left** of the viewport.
- Drag the toolbar to top-right, then resize the window smaller → toolbar clamps inward.
- Drag handle shows `Surface3` background on hover.
- The toolbar has a subtle floating appearance.

---

## File Touchpoints

### New Files

| File | Purpose |
|------|---------|
| `KnobForge.App/Views/MainWindow.BrushDockDrag.cs` | Drag logic, position clamping, toggle, default position |

### Modified Files

| File | Subphases | Changes |
|------|-----------|---------|
| `KnobForge.App/Views/MainWindow.axaml` | 9A | Add `TranslateTransform`, drag handle, restructure dock content into outer `StackPanel Spacing="4"` with handle + existing button row |
| `KnobForge.App/App.axaml` | 9C | Add `Border#BrushDockDragHandle` hover style |
| `KnobForge.App/Views/MainWindow/MainWindow.cs` | 9A, 9B | Add fields: `_brushDockDragHandle`, `_brushDockTranslate`, `_isBrushDockDragging`, `_brushDockHiddenByUser`, `_brushDockDefaultPositionSet` |
| `KnobForge.App/Views/MainWindow.Initialization.cs` | 9A, 9B, 9C | Add FindControl for drag handle, resolve TranslateTransform, call `WireBrushDockDrag()`, wire B-key toggle, wire default position + resize clamp |
| `KnobForge.App/Views/MainWindow.BrushQuickToolbar.cs` | 9B | Change visibility from `brushTabActive` to `hasModel && !userHidden` |
| `KnobForge.App/Controls/MetalViewport/MetalViewport.InputAndBrush.cs` | 9B | Add `B` key handling (if Option A chosen) |

### Untouched Files

All Core, Rendering, and other App files remain unchanged. This phase modifies only the viewport overlay and brush toolbar UI layer.

---

## Reference: Existing Button Names (Do Not Rename)

These `x:Name` values are wired in `MainWindow.BrushQuickToolbar.cs` — preserve them exactly:

| Name | Group | Purpose |
|------|-------|---------|
| `BrushQuickToggleButton` | Paint | Master paint on/off |
| `BrushQuickColorButton` | Channel | Color channel |
| `BrushQuickScratchButton` | Channel | Scratch channel |
| `BrushQuickEraseButton` | Channel | Erase channel |
| `BrushQuickRustButton` | Channel | Rust channel |
| `BrushQuickWearButton` | Channel | Wear channel |
| `BrushQuickGunkButton` | Channel | Gunk channel |
| `BrushQuickSprayButton` | Tool | Spray brush type |
| `BrushQuickStrokeButton` | Tool | Stroke brush type |
| `BrushQuickNeedleButton` | Tool | Needle abrasion |
| `BrushQuickScuffButton` | Tool | Scuff abrasion |
| `BrushQuickAddLayerButton` | Actions | Add paint layer |
| `BrushQuickClearMaskButton` | Actions | Clear paint mask |

---

## Reference: PaintChannel and BrushType Enums

```csharp
public enum PaintChannel
{
    Rust = 0, Wear = 1, Gunk = 2, Scratch = 3,
    Erase = 4, Color = 5, Roughness = 6, Metallic = 7
}

public enum PaintBrushType
{
    Spray, Stroke, Circle, Square, Splat
}

public enum ScratchAbrasionType
{
    Needle, Chisel, Burr, Scuff
}
```

---

## Design Principles (for Codex Reference)

This toolbar follows the conventions of professional DCC floating transports:

- **Substance Painter**: Floating tool shelf with drag handle, stays within canvas, channel/tool groups separated by dividers.
- **Photoshop**: Floating panels with title bar grip, clamped to application bounds, `Tab` key hides all panels.
- **Blender**: T-panel tools with toggle shortcut (`T`), floating popups stay within 3D viewport bounds.
- **darktable**: Floating shortcuts panel with 50/30/10% contrast hierarchy.

The grip-dot drag handle at the top is the universal affordance for "this panel can be moved." The `B` key toggle follows Blender's convention of single-letter viewport shortcuts.
