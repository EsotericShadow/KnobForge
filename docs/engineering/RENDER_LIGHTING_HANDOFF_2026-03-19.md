# Render / Lighting Handoff

Date: 2026-03-19

Branch: `main`

Current HEAD: `3bb3dc2` (`Fix macOS launcher project opens via fresh-process handoff`)

## Purpose

This handoff is for the next agent taking over environment polish work:

- bloom quality and shaping
- environment reflections
- indicator light radiation / spill
- projected light or shadow contribution onto a floor / receiver plane
- floor reflections if needed

The launcher crash triage is no longer the active task. The last launcher-path fix is verified working on macOS 26.3.1 arm64: opening a saved project from the launcher did not crash after `3bb3dc2`.

## Repo State

The worktree is very dirty. Do not broad-reset or “clean up” unrelated files.

Relevant dirty files already in the tree include:

- `KnobForge.App/Controls/MetalViewport.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.CameraAndOrientation.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.MeshAndUniforms.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.OffscreenRender.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.ProjectTypesAndBvh.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.RuntimeAndGizmos.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.Shadows.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.StateAndPaintLayers.cs`
- `KnobForge.App/Views/MainWindow.EnvironmentShadowReadouts.cs`
- `KnobForge.App/Views/MainWindow.Initialization.cs`
- `KnobForge.App/Views/MainWindow.MainWindow.SceneAndInspector.cs`
- `KnobForge.App/Views/MainWindow.axaml`
- `KnobForge.Core/KnobProject.cs`
- `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs`
- `KnobForge.App/Views/MainWindow.DebugAxes.cs` (currently untracked)

That means the next agent should diff specific files before assuming current behavior.

## Current Render Architecture

### 1. UI controls

Environment, bloom, reflection, HDRI, and shadow controls are exposed in:

- `KnobForge.App/Views/MainWindow.axaml`
- `KnobForge.App/Views/MainWindow.EnvironmentShadowReadouts.cs`
- `KnobForge.App/Views/MainWindow.Initialization.cs`
- `KnobForge.App/Views/MainWindow/MainWindow.SceneAndInspector.cs`

Key UI sections:

- Bloom controls: `KnobForge.App/Views/MainWindow.axaml:2311`
- Reflection controls: `KnobForge.App/Views/MainWindow.axaml:2375`
- Shadow controls: `KnobForge.App/Views/MainWindow.axaml:2432`

UI wiring to project state happens in:

- `KnobForge.App/Views/MainWindow.EnvironmentShadowReadouts.cs:14`
- `KnobForge.App/Views/MainWindow.Initialization.cs:1138`

### 2. Project state

Persistent knobs for bloom / reflection / HDRI / shadows live in:

- `KnobForge.Core/KnobProject.cs:373`

Notable state already exists for:

- `EnvironmentBloomStrength`
- `EnvironmentBloomThreshold`
- `EnvironmentBloomKnee`
- `BloomRadius`
- `BloomTintR/G/B`
- `GlareRotationDegrees`
- `BloomCompositeIntensity`
- `ReflectionStrength`
- `ReflectionFresnelBias`
- `ClearCoatReflectionStrength`
- `ReflectionOnlyPreview`
- `EnvironmentHdriPath`
- `EnvironmentHdriBlend`
- `EnvironmentHdriRotationDegrees`
- `ShadowsEnabled`
- `ShadowMode`
- `ShadowStrength`
- `ShadowSoftness`
- `ShadowDistance`
- `ShadowScale`
- `ShadowQuality`
- `ShadowGray`
- `ShadowDiffuseInfluence`

These values are already captured in undo / inspector snapshot / reference profile code, so they should survive project save/load.

### 3. Metal viewport path

Main real-time rendering path is centered in:

- `KnobForge.App/Controls/MetalViewport.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.MeshAndUniforms.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.Shadows.cs`
- `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs`

Uniform packing for environment, reflection, bloom, and HDRI lives in:

- `KnobForge.App/Controls/MetalViewport/MetalViewport.MeshAndUniforms.cs:820`

HDRI texture loading lives in:

- `KnobForge.App/Controls/MetalViewport/MetalViewport.RuntimeAndGizmos.cs:536`

Bloom execution in the real-time viewport lives in:

- `KnobForge.App/Controls/MetalViewport.cs:1752`
- `KnobForge.App/Controls/MetalViewport.cs:1833`

Current bloom implementation is:

- fullscreen extract pass
- fullscreen blur pass or additive multi-direction blur
- fullscreen composite pass

This is screen-space post-process bloom, not geometry-aware light spill.

### 4. Shader-side reflection / environment lighting

Main PBR/environment reflection code lives in:

- `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs:897`
- `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs:1022`

Important current behavior:

- reflections are environment-driven, not planar scene reflections
- optional HDRI is sampled through `EvaluateEnvironmentLighting`
- reflection strength, Fresnel bias, and clear-coat reflection strength are already hooked up
- `ReflectionOnlyPreview` already exists for debugging reflection contribution

### 5. Indicator emissive / dynamic light path

Indicator emissive and aura uniforms are built in:

- `KnobForge.App/Controls/MetalViewport.cs:1103`
- `KnobForge.App/Controls/MetalViewport.cs:1125`

Dynamic light rig types live in:

- `KnobForge.Core/DynamicLightRig.cs:15`

This suggests the codebase already has a concept of local emitters and emissive glow, but not yet a dedicated “light landing on floor” receiver path.

### 6. Shadow path

Projected and direct-shadow logic is in:

- `KnobForge.App/Controls/MetalViewport/MetalViewport.Shadows.cs:383`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.Shadows.cs:464`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.Shadows.cs:551`

Current behavior:

- shadow contribution is derived from scene lights
- weighted / dominant / selected light modes already exist
- a direct shadow-map visibility path exists in the shader
- there is already some “projected shadow” behavior onto visible receivers

But there is no dedicated floor receiver plane or floor-material shading path in the current real-time viewport.

## What Exists Already

The next agent should not re-implement these from scratch:

- bloom UI, project properties, and render passes
- reflection UI, project properties, and shader wiring
- HDRI loading and environment-map blending
- clear-coat reflection controls
- reflection-only preview
- projected shadow controls and direct shadow visibility
- indicator emissive lens / emitter / aura uniforms
- dynamic light rig data model

## What Is Missing For The User’s Goal

The user specifically wants more convincing:

- bloom
- reflections
- light radiation / spill
- projection onto the floor

The missing pieces appear to be:

### 1. No dedicated floor receiver

There is no obvious floor / ground plane render object in the viewport path.

A search for floor/ground receiver code did not find a real floor mesh or plane-render path. The current shadow system appears to operate on existing scene geometry rather than a dedicated external receiver.

### 2. No planar floor reflection system

Current reflections are environment reflections. They are not mirror or planar scene reflections from a receiver surface.

If the user wants “reflections on the floor,” that is not the same feature as the current environment reflection controls.

### 3. No explicit emissive light spill pass

Current bloom is a post-process and current indicator emission is a material/emitter effect. Neither one guarantees visible “light landing on the floor.”

To get convincing floor radiation, a receiver term needs to be rendered somehow:

- projected decal-like light spill
- additive floor-light pass
- local-light contribution on a floor plane
- or a more expensive volumetric/participating-media approximation

## Safest Next Implementation Order

This is the lowest-risk order in the current dirty tree.

### Phase A: add a floor receiver plane

Add an explicit optional floor plane to the viewport render path first.

Why:

- it gives shadows and light spill somewhere to land
- it separates “receiver rendering” from the existing object-only shading
- it is the cleanest foundation for both floor projection and floor reflections

Recommended first version:

- simple quad plane under the asset
- neutral material with tunable roughness / reflectivity
- render only in viewport for now
- no export dependency on the first pass

### Phase B: land shadows on the floor plane

Once the floor plane exists, reuse current shadow direction / weight resolution to shade the plane.

Why:

- current shadow logic already computes usable 2D offset / softness / weight information
- this is the easiest way to give the user the “projection on the floor” feeling immediately

### Phase C: add emissive spill from indicator emitters

Use the existing emitter color/intensity and dynamic light rig concepts to drive a floor spill term.

Recommended first version:

- fake projected radial/elliptical spill on the floor plane
- tint from emitter color
- intensity scaled by emitter strength and distance
- optional softness / spread / anisotropy control

This is much cheaper and more controllable than trying to jump directly to volumetrics.

### Phase D: add floor reflections

Only after the floor plane exists.

Recommended first version:

- environment-only reflection on the floor plane
- separate floor reflectivity and roughness controls

Possible later version:

- planar reflected scene pass or cheap mirrored-scene approximation

Do not start with a full planar reflection system unless the simple floor receiver + env reflection still looks inadequate.

### Phase E: tune bloom after spill exists

Bloom should be tuned after light spill exists, not before.

Why:

- if the scene still lacks a receiver/spill term, bloom tuning alone will just make highlights smear more
- the user is asking for “radiation / projection onto the floor,” which is not solved by stronger bloom

## Specific Recommendations For The Next Agent

1. Start by proving there is no floor receiver in the real-time viewport path.

2. Add the floor receiver in a tightly scoped way inside the Metal viewport only.

3. Reuse existing project-level shadow controls first, before inventing new ones.

4. Treat emissive spill as an artistic receiver pass, not a physically complete GI system.

5. Keep preview/export parity in mind:
   `KnobForge.Rendering/PreviewRenderer/PreviewRenderer.Shading.cs:295` has its own environment reflection model, separate from the real-time Metal shader path.

6. Do not destabilize the launcher fix:
   `3bb3dc2` fixed the macOS launcher crash by using a fresh-process project handoff.

## Good Files To Open First

- `KnobForge.App/Controls/MetalViewport.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.MeshAndUniforms.cs`
- `KnobForge.App/Controls/MetalViewport/MetalViewport.Shadows.cs`
- `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs`
- `KnobForge.App/Views/MainWindow.axaml`
- `KnobForge.App/Views/MainWindow.EnvironmentShadowReadouts.cs`
- `KnobForge.Core/KnobProject.cs`
- `KnobForge.Rendering/PreviewRenderer/PreviewRenderer.Shading.cs`

## Useful Commands

Build:

```bash
dotnet build /Users/main/Desktop/KnobForge/KnobForge.App/KnobForge.App.csproj -c Release -m:1 -nodeReuse:false
```

Launch installed bundle:

```bash
open /Users/main/Applications/Monozukuri.app
```

Direct-open control path:

```bash
cd /Users/main/Desktop/KnobForge/KnobForge.App/bin/Release/net8.0
./KnobForge.App "/Users/main/Desktop/KnobForge/Projects/red_width_dragon_knob.knob"
```

## Bottom Line

The rendering stack already has environment bloom, environment reflections, HDRI blending, direct/projected shadows, and indicator emissive shading.

What it does not yet clearly have is a dedicated floor receiver that can accept:

- cast shadow projection
- emissive light spill / radiation
- floor-specific reflections

That is the cleanest next target.
