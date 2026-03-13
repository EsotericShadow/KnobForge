# Phase 6: Inspector Control Overhaul (Choroboros Dev Panel)

## Phase Identity

- **Phase**: 6 of 7
- **Name**: Inspector Control Overhaul
- **Depends on**: None (independent of Phases 1-5 pipeline; can execute whenever)
- **Unlocks**: Nothing — standalone UI polish phase
- **Risk**: Low — purely UI layer, no GPU pipeline or data model changes
- **Milestone**: M6 — All inspector knobs and value inputs replaced with Choroboros-style compact numeric controls

## Why This Phase Exists

KnobForge's inspector currently uses 219 `SpriteKnobSlider` controls — custom sprite-sheet-animated knob dials inheriting from Avalonia `Slider`. Each one is paired with a `TextBlock` readout, and 28 of them have an additional precision `TextBox` input. This is three separate control types for one purpose: editing a number.

The sprite knobs look good but are space-inefficient (64px diameter minimum), don't expose the numeric value inline, and require a separate text field for precision entry. The replacement is a purpose-built compact numeric input — the Choroboros Dev Panel `ValueInput` — which combines a right-aligned text editor, stacked up/down arrow buttons, mouse drag, scroll wheel, and modifier key fine/coarse control into a single row control. This is the same control design used in the user's JUCE audio plugin UI toolkit.

## Design Specification (Ported from JUCE)

The new control is called `ValueInput` (Avalonia `UserControl`). It replaces the JUCE `LockableFloatPropertyComponent` minus the lock button/state (removed by design).

### Layout

```
┌─────────────────────┬──┐
│  right-aligned text  │▲ │
│  editor (editable)   │──│
│                      │▼ │
└─────────────────────┴──┘
```

- **Left**: Editable text field, right-aligned, displays value with configurable decimal places (default 3)
- **Right**: Narrow column (8-10px wide) split vertically into up arrow (top) and down arrow (bottom)
- Rounded rectangle container with border, matching the hacker/dark theme aesthetic

### Interaction Model

1. **Text field**: Editable, right-aligned. Enter/Return commits the typed value. Escape reverts to last committed value and defocuses. Value is parsed, sanitized, clamped, and optionally snapped to step.

2. **Up/Down arrow buttons**: Click = one step. Hold = autorepeat (420ms initial delay, then 110ms repeat, accelerating to 45ms). Modifier keys: Shift or Cmd/Ctrl = finer (0.2× / 0.25×), Alt = coarser (5×). Multipliers stack.

3. **Mouse drag on text field**: Vertical drag — up = increase, down = decrease. Delta Y pixels / 6 = step count. Moves < 2px ignored (dead zone to allow click-to-edit). Non-fine mode applies acceleration: multiplier = clamp(1.0 + abs(deltaY) / 220.0, 1.0, 6.0). Same modifier keys as arrows.

4. **Mouse wheel on text field**: Smooth (trackpad) = deltaY × 16 steps. Discrete wheel = deltaY × 4, minimum 1 step. Same modifier keys.

### Value Semantics

- `Minimum`, `Maximum`, `Step`, `SkewFactor` properties
- `Value` property (double, two-way bindable via `AvaloniaProperty`)
- Sanitization: value is normalized to 0-1 via `NormalisableRange(min, max, step, skew)`, then converted back and snapped to step grid
- Display format: configurable `DecimalPlaces` property (default 3)
- All user input goes through `ApplyValue → Sanitize → Clamp → Snap → Update` pipeline

### Visual Style

- Background: dark field color (theme-consistent)
- Text: right-aligned, monospace-friendly font matching current inspector
- Border: 1px rounded rectangle, 2.5px radius
- Arrow buttons: chevron paths drawn in code (no images), highlight on hover/press
- Divider lines: vertical between text and arrow column, horizontal between up and down arrows

## Subphases

### Subphase 6A: Create ValueInput Control

#### Project 6A.1: Core Control Implementation

**Task 6A.1.1: Create ValueInput UserControl**
- New file: `KnobForge.App/Controls/ValueInput.cs`
- Inherits from `Avalonia.Controls.UserControl`
- Styled properties:
  - `Value` (double, default 0.0) — two-way bindable
  - `Minimum` (double, default 0.0)
  - `Maximum` (double, default 1.0)
  - `Step` (double, default 0.01) — increment size
  - `SkewFactor` (double, default 1.0) — NormalisableRange skew
  - `DecimalPlaces` (int, default 3) — display format
  - `IsReadOnly` (bool, default false)
- Internal composition: `Border` > `Grid` (two columns: `*` + `Auto`) > `TextBox` in col 0, `StackPanel` (up/down buttons) in col 1
- All construction in code-behind (no AXAML template file needed — keeps it self-contained)

**Task 6A.1.2: Value sanitization pipeline**
- Private method: `double SanitizeValue(double raw)`
- Normalizes to 0-1 using skew, converts back, snaps to step, clamps to min/max
- All external value changes go through this pipeline

**Task 6A.1.3: Text editor behavior**
- TextBox is right-aligned, single-line
- On Enter/Return: parse text, apply through sanitize pipeline, update Value property
- On Escape: revert text to current Value, release focus
- On focus lost: same as Enter (commit)
- Refresh display text when Value changes programmatically (but not while user is editing)

**Task 6A.1.4: Step arrow buttons**
- Two buttons stacked vertically in the right column
- Custom drawn chevron arrows (override `Render` or use `DrawingPresenter` with `PathGeometry`)
- Click: nudge Value by ±1 step × modifier multiplier
- Autorepeat: 420ms initial, 110ms sustain, 45ms fast — use Avalonia `DispatcherTimer` for repeat
- Cursor: pointing hand on hover

**Task 6A.1.5: Mouse drag on text area**
- Override `PointerPressed` / `PointerMoved` / `PointerReleased` on the text field area
- On press: record screen Y and current value; set `isDragging = false`
- On move: if delta Y > 2px, enter drag mode (suppress text selection)
- Drag delta: `(startScreenY - currentScreenY) / 6.0` steps
- Acceleration (non-fine mode): `clamp(1.0 + abs(deltaY) / 220.0, 1.0, 6.0)`
- Modifier multiplier: Shift = 0.2×, Ctrl/Cmd = 0.25×, Alt = 5.0× (stack multiplicatively)
- Apply: `dragStartValue + stepDelta × Step`

**Task 6A.1.6: Mouse wheel**
- Override `PointerWheelChanged` on the control
- Smooth scroll (trackpad): `deltaY × 16` steps
- Discrete scroll: `deltaY × 4` steps, minimum 1
- Same modifier multiplier
- Apply through sanitize pipeline

**Task 6A.1.7: Painting / visual rendering**
- Override `Render` for the container:
  - Fill rounded rectangle (dark field bg)
  - Stroke rounded rectangle (border)
  - Vertical divider line between text and arrow column
  - Horizontal divider line between up and down arrows
- Arrow buttons paint chevron paths with correct hit/hover/press states
- Colors: pull from a static theme helper (or hardcode dark-theme constants matching existing hacker aesthetic)

#### Project 6A.2: Styling and Theme Integration

**Task 6A.2.1: Add ValueInput style to App.axaml**
- Define a `<Style Selector="controls|ValueInput">` block
- Set default font, margin, height to match inspector row height
- Ensure it works within the existing `StackPanel`-based inspector layout

**Task 6A.2.2: Test control in isolation**
- Add a single ValueInput to a test location in the inspector
- Verify all 4 interaction modes: text edit, arrow buttons, drag, scroll
- Verify modifier keys work correctly
- Verify value clamping and step snapping
- Remove test control after verification

---

### Subphase 6B: Replace SpriteKnobSliders

This is the bulk migration. There are 219 `SpriteKnobSlider` instances, each paired with a value `TextBlock` readout, and 28 also paired with a precision `TextBox`.

#### Project 6B.1: Systematic Replacement

**Task 6B.1.1: Replace AXAML declarations**
- In `MainWindow.axaml`: replace every `<controls:SpriteKnobSlider x:Name="FooSlider" Minimum="X" Maximum="Y"/>` with `<controls:ValueInput x:Name="FooSlider" Minimum="X" Maximum="Y" Step="Z"/>`
- Determine appropriate `Step` for each control based on its range and the format string used in its readout (e.g., "0.000" → Step=0.001, "0.0" → Step=0.1, "0" → Step=1)
- Remove the associated `<TextBlock x:Name="FooValueText"/>` readout — the ValueInput shows the value inline
- Remove the associated precision `<TextBox x:Name="FooInputTextBox"/>` where present — the ValueInput IS the text input

**Task 6B.1.2: Update field declarations**
- In MainWindow partial classes: change field types from `SpriteKnobSlider` to `ValueInput`
- Remove the `TextBlock` readout fields (e.g., `_fooValueText`)
- Remove the precision `TextBox` fields (e.g., `_fooInputTextBox`)

**Task 6B.1.3: Update event handler wiring**
- ValueInput exposes a `Value` styled property — use `PropertyChanged` the same way
- The handler pattern stays the same: `if (e.Property != ValueInput.ValueProperty) return;`
- Replace `_fooSlider.Value` reads with `_fooSlider.Value` — same API if ValueInput exposes Value as a double property

**Task 6B.1.4: Remove readout update code**
- Delete all `_fooValueText.Text = $"..."` readout assignments in the various `Update*Readouts` methods
- These are spread across multiple partial class files:
  - `MainWindow.EnvironmentShadowReadouts.cs`
  - `MainWindow.ShapeReadouts.cs`
  - And other `*Readouts.cs` files
- The ValueInput's text field replaces all readout functionality

**Task 6B.1.5: Remove precision control wiring**
- Delete `MainWindow.PrecisionControls.cs` (or gut its contents)
- Remove all `WirePrecisionTextEntry(...)` calls from `InitializePrecisionControls()`
- Remove all `UpdatePrecisionControlEntryText(...)` calls
- The ValueInput inherently supports precision entry — no separate TextBox needed

**Task 6B.1.6: Handle special format cases**
- Some readouts use units: `$"{value:0.0} deg"`, `$"{value:0} px"`, etc.
- ValueInput can have a `Suffix` property (optional) for inline unit display
- Or: set `DecimalPlaces` appropriately and let the label carry the unit

#### Project 6B.2: Batch-by-Section Migration Strategy

Given 219 controls, replace them section by section to catch regressions early:

1. **Lighting section** (~15 controls) — replace, build, verify
2. **Model/Shape section** (~25 controls) — replace, build, verify
3. **Material section** (~20 controls) — replace, build, verify
4. **Paint/Brush section** (~20 controls) — replace, build, verify
5. **Collar section** (~15 controls) — replace, build, verify
6. **Environment/Shadow section** (~10 controls) — replace, build, verify
7. **Export/Post-processing section** (~15 controls) — replace, build, verify
8. **Remaining sections** — replace, build, verify
9. Final pass: search for any remaining SpriteKnobSlider references

---

### Subphase 6C: Cleanup

#### Project 6C.1: Remove Legacy Control

**Task 6C.1.1: Delete SpriteKnobSlider.cs**
- File: `KnobForge.App/Controls/SpriteKnobSlider.cs`
- Only after all 219 instances are replaced and verified
- Remove from project/build if needed

**Task 6C.1.2: Remove sprite sheet assets**
- Delete the knob sprite sheet PNG(s) from `Assets/`
- Update any resource references

**Task 6C.1.3: Remove SpriteKnobSlider style from App.axaml**
- Delete the `<Style Selector="controls|SpriteKnobSlider">` block and its ControlTemplate

**Task 6C.1.4: Clean up readout partial classes**
- Delete or empty readout files that now have no purpose
- Remove unused `using` statements across MainWindow partials
- Remove unused field declarations

**Task 6C.1.5: Clean up PrecisionControls**
- Delete `MainWindow.PrecisionControls.cs` if fully emptied
- Remove `InitializePrecisionControls()` call from initialization chain

---

## Verification Checklist (Phase 6 Complete)

- [ ] ValueInput control implements all 4 interaction modes (text, arrows, drag, scroll)
- [ ] Modifier keys work: Shift (0.2×), Ctrl/Cmd (0.25×), Alt (5×), stacking
- [ ] Arrow autorepeat timing matches spec (420ms / 110ms / 45ms)
- [ ] Drag dead zone (< 2px) prevents accidental drags when clicking to edit text
- [ ] Value sanitization: skew, step snap, clamp all function correctly
- [ ] All 219 SpriteKnobSlider instances replaced with ValueInput
- [ ] All 219 TextBlock readouts removed (value shown inline in ValueInput)
- [ ] All 28 precision TextBox inputs removed (ValueInput provides text editing)
- [ ] Every inspector control still reads/writes the correct project property
- [ ] SpriteKnobSlider.cs and sprite sheet assets deleted
- [ ] PrecisionControls code removed
- [ ] No remaining references to SpriteKnobSlider in the codebase
- [ ] Build succeeds with zero SpriteKnobSlider references
- [ ] Visual regression check: adjust every property section, confirm values apply correctly to the render

## Performance Note

The current SpriteKnobSlider loads and crops a 2048×1664px sprite sheet image per control instance. 219 instances means significant texture memory and bitmap cropping overhead. The new ValueInput is pure vector rendering (paths + text) — expect measurable improvement in inspector load time and memory usage.

## Files Touched

| File | Nature of Change |
|------|-----------------|
| `MainWindow.axaml` | Replace 219 SpriteKnobSlider + readout TextBlocks with ValueInput |
| `MainWindow.Initialization.cs` | Remove precision control wiring |
| `MainWindow.PrecisionControls.cs` | Delete entirely |
| `MainWindow.EnvironmentShadowReadouts.cs` | Remove readout text updates |
| `MainWindow.ShapeReadouts.cs` | Remove readout text updates |
| All `*Handlers.cs` partials | Update PropertyChanged checks to ValueInput.ValueProperty |
| `App.axaml` | Remove SpriteKnobSlider style, add ValueInput style |

## New Files

| File | Purpose |
|------|---------|
| `KnobForge.App/Controls/ValueInput.cs` | Choroboros Dev Panel value input control |

## Deleted Files

| File | Reason |
|------|--------|
| `KnobForge.App/Controls/SpriteKnobSlider.cs` | Replaced by ValueInput |
| `KnobForge.App/Views/MainWindow.PrecisionControls.cs` | Functionality absorbed into ValueInput |
| `Assets/green_channel_strip_over_right_spritesheet.png` | No longer needed |
