# Indicator Lens Material Checklist

## Goal
Bring indicator lens shading closer to PBR best practices for translucent plastics/glass while keeping the current Metal pipeline stable.

## Phase 1: Data Model and Persistence
- [x] Add lens transmission control (`0..1`)
- [x] Add lens IOR control (`1.0..2.5`)
- [x] Add lens thickness control (`0..10`)
- [x] Add lens tint color (`RGB 0..1`)
- [x] Add lens absorption control (`0..8`)
- [x] Set sane defaults in indicator assembly defaults
- [x] Include new fields in undo/save/load snapshot paths

## Phase 2: UI Wiring (Inspector)
- [x] Add new controls under `Indicator Light Assembly > Lens Material`
- [x] Wire controls into `OnIndicatorLightSettingsChanged`
- [x] Push UI values into `KnobProject`
- [x] Pull `KnobProject` values back into UI on refresh
- [x] Add readouts for each new control

## Phase 3: Renderer Wiring
- [x] Extend GPU uniform payload with lens material params
- [x] Initialize defaults for non-lens parts
- [x] Add dedicated lens-uniform builder path
- [x] Use lens params for indicator lens part render uniforms

## Phase 4: Shader Behavior
- [x] Add lens params to Metal shader uniform struct
- [x] Apply Schlick Fresnel from IOR
- [x] Apply Beer-Lambert-style absorption tint by thickness
- [x] Blend transmitted component with existing PBR accumulation by transmission

## Phase 5: Verification
- [x] Solution build clean
- [x] Regression suite clean
- [x] Visual tuning pass with presets (Clear / Frosted / Saturated LED)
- [x] Add optional lens roughness/specular controls
- [x] Interactive 1:1 indicator preview available in Render Settings for framing/timing checks before export
- [ ] Add documentation screenshots for lens-material presets

## Lens Preset Values
- Clear Lens: Transmission `0.88`, IOR `1.49`, Thickness `1.00`, Absorption `1.20`, Roughness `0.14`, Specular `1.25`, Tint `(0.78, 0.92, 0.84)`
- Frosted: Transmission `0.62`, IOR `1.47`, Thickness `1.80`, Absorption `1.60`, Roughness `0.36`, Specular `1.05`, Tint `(0.86, 0.92, 0.90)`
- Saturated LED: Transmission `0.94`, IOR `1.50`, Thickness `1.20`, Absorption `2.10`, Roughness `0.10`, Specular `1.35`, Tint `(0.58, 0.92, 0.68)`

## Preset Source of Truth
- Preset constants are centralized in `/Users/main/Desktop/KnobForge/KnobForge.Core/IndicatorLensMaterialPresets.cs`.
- UI preset buttons and indicator default seeding both resolve values from that file to prevent drift.

## Remaining
- Capture comparison screenshots and add before/after examples in docs.

## Screenshot TODO (Phase 5 closeout)
- Capture inspector + viewport for Clear preset and save to `/Users/main/Desktop/KnobForge/docs/images/indicator-lens-clear.png`
- Capture inspector + viewport for Frosted preset and save to `/Users/main/Desktop/KnobForge/docs/images/indicator-lens-frosted.png`
- Capture inspector + viewport for Saturated preset and save to `/Users/main/Desktop/KnobForge/docs/images/indicator-lens-saturated.png`
