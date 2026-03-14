# Phase 13: Reflections, Environment IBL & Shaped Bloom

## Your Role

You are implementing Phase 13 of the Monozukuri Material Tool Transformation. This phase upgrades the reflection and post-processing pipeline to produce physically-based environment reflections that respond to surface roughness, adds a BRDF integration LUT for energy-correct specular, introduces shaped bloom kernels for cinematic glare, and bundles HDR environment presets for out-of-the-box studio lighting.

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–12 are complete. The rendering pipeline uses Metal via `MetalRendererContext`, with per-assembly mesh builders generating `MetalVertex[]` + `uint[]` arrays uploaded to GPU buffers. The Metal shader source lives in `MetalPipelineManager.Shaders.cs` as the C# string constant `FallbackShaderSource`. All shader modifications happen by editing this string.

**Current rendering pipeline state:**
- **BRDF**: Anisotropic Beckmann NDF with Schlick-GGX geometry term and Schlick Fresnel (F0 floor at 0.04–0.08). Clear coat uses isotropic GGX.
- **Environment lighting**: Procedural gradient (hemisphere blend + horizon band + sky hotspot) with optional equirectangular HDR map blended via `envMapBlend`. Single-sample lookup at the mirror reflection direction.
- **Environment map upload**: Mipmaps ARE generated via `MTLBlitCommandEncoder.GenerateMipmapsForTexture` when loading equirectangular maps. The shader sampler already uses `mip_filter::linear` — but `EvaluateEnvironmentLighting` does NOT pass a `level()` parameter, so it always samples at the finest mip.
- **Bloom pipeline**: 3-stage (extract → 5-tap Gaussian blur → composite). Two blur passes: horizontal then vertical. Isotropic only — no directional star/streak kernels.
- **Tone mapping**: ACES Fitted (mode 0) and AGX-Like (mode 1).
- **Post-process uniforms**: `PostProcessParams` (exposure, bloomThreshold, bloomKnee, bloomStrength) and `PostProcessParams2` (texelX, texelY, blurDirX, blurDirY).

## What Phase 13 Does

### Four Subphases (execute in order):

1. **13A — Roughness-Based Environment Mip Sampling**: Modify the shader's `EvaluateEnvironmentLighting` to accept a roughness parameter and sample the environment map at `level(roughness * maxMipLevel)`, producing blurred reflections on rough surfaces and sharp reflections on smooth ones.

2. **13B — Split-Sum BRDF Integration LUT**: Bake a 256×256 RG16Float lookup texture at startup, keyed on (NdotV, roughness). Use it in the fragment shader to replace the current `fresnelView` approximation in the environment specular term with the proper split-sum result.

3. **13C — Shaped Bloom Kernels**: Add a `BloomKernelShape` enum (Soft, Star4, Star6, AnamorphicStreak) with per-shape directional blur pass configurations, so bloom can produce star/streak glare patterns.

4. **13D — Bundled Environment Presets**: Ship 4 procedural gradient presets (Studio, Rack, Showroom, Dark) that users can select without loading an HDRI file. Presets define top/bottom colors, intensity, and roughness mix.

**Explicitly deferred** (do NOT implement):
- Compute-shader-based mip chain generation (Metal's blit encoder `generateMipmaps` is sufficient).
- Screen-space reflections (SSR) — out of scope for a single-object renderer.
- Diffuse irradiance convolution (the procedural gradient handles this adequately).
- Bloom downscale chain (progressive downsample cascade) — future optimization.

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify `App.axaml` or `App.axaml.cs`.** Design tokens and startup flow stay identical.
2. **Do NOT modify geometry or mesh builders.** This phase is shader + pipeline only.
3. **All existing `GpuUniforms` fields must be preserved.** Add new fields at the END of the struct (both C# and Metal sides). Alignment must match.
4. **The app must compile and run identically after each subphase.** New features must default to values that produce the same visual output as before (backward compatibility).
5. **All new enums must be in `KnobForge.Core`** (same namespace as existing enums).
6. **All new project properties must be in `KnobProject.cs`** with sensible defaults that match current behavior.
7. **Do NOT add new Metal shader function names** unless strictly necessary (13C adds none — it reuses `fragment_bloom_blur` with different direction vectors). 13B adds one compute kernel.
8. **The `FallbackShaderSource` string must remain a single valid Metal shader.** Test compilation by running the app.

---

## Existing Architecture (Read Before Coding)

### Shader Source Location

All Metal shader code is in a single C# string constant:

```
KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs
└── private const string FallbackShaderSource = @"...";
```

### Key Shader Functions and Line References

| Function | Lines | Purpose |
|---|---|---|
| `EvaluateEnvironmentColor` | 88–96 | Procedural gradient (hemisphere + horizon + hotspot) |
| `EvaluateEnvironmentLighting` | 144–167 | Gradient + equirectangular map blend. **13A modifies this.** |
| `fragment_main` (env reflection block) | 911–933 | Environment specular + diffuse accumulation. **13A & 13B modify this.** |
| `fragment_main` (clear coat env) | 934–939 | Clear coat environment term. **13A applies here too.** |
| `fragment_main` (lens refraction) | 971–989 | Lens transmission env lookups. **13A applies to refracted env.** |
| `fragment_bloom_extract` | 1060–1074 | Luminance threshold extraction. |
| `fragment_bloom_blur` | 1076–1092 | 5-tap Gaussian blur with direction vector. **13C reuses this.** |
| `fragment_bloom_composite` | 1094–1117 | Additive bloom blend onto source. |

### GpuUniforms (C# Side)

The C# `GpuUniforms` struct is defined in `MetalViewport.ProjectTypesAndBvh.cs`. Fields are `Vector4` packed, uploaded via a single buffer binding (buffer index 1). The Metal `GpuUniforms` struct in the shader must match exactly.

### Bloom Pass Dispatch (MetalViewport.cs)

The bloom pipeline in `MetalViewport.cs` runs two blur passes:
1. Horizontal: `PostProcessParams2 = (1/w, 1/h, 1.0, 0.0)` → renders into `_bloomBlurTexture`
2. Vertical: `PostProcessParams2 = (1/w, 1/h, 0.0, 1.0)` → renders into `_bloomExtractTexture`
Then composite blends bloom onto the main color buffer.

### Environment Map Upload (MetalViewport.RuntimeAndGizmos.cs)

`EnsureEnvironmentMapTexture()` loads an equirectangular HDRI via SkiaSharp, creates a `MTLTexture2DDescriptor` with `mipmapped: true`, uploads pixel data, and calls `blitEncoder.GenerateMipmapsForTexture(texture)`. **Mipmaps already exist on the GPU.** The max mip level count is `floor(log2(max(width, height))) + 1`.

---

## Subphase 13A — Roughness-Based Environment Mip Sampling

### Goal

Make environment reflections respond to surface roughness: smooth surfaces (roughness → 0) get sharp reflections from the finest mip; rough surfaces (roughness → 1) get blurred reflections from coarser mips. This is the single highest-impact visual improvement in Phase 13.

### Step 1: Add `envMapMaxMipLevel` to GpuUniforms

**C# side** — `MetalViewport.ProjectTypesAndBvh.cs`:
Add a new float field to the C# `GpuUniforms` struct. Pack it into an existing or new `Vector4`. The cleanest approach:

Add a new `Vector4 environmentMapParams3` at the end of the struct (after `dynamicLights`). Use `.x` for `envMapMaxMipLevel`, `.y`/`.z`/`.w` reserved for 13B/13C/13D.

**Metal side** — `MetalPipelineManager.Shaders.cs`:
Add matching `float4 environmentMapParams3;` after `dynamicLights[MAX_LIGHTS]` in the `GpuUniforms` struct.

### Step 2: Compute and Upload Max Mip Level

In `EnsureEnvironmentMapTexture()` or in the per-frame uniform upload, compute:
```csharp
float maxMipLevel = MathF.Floor(MathF.Log2(MathF.Max(envTextureWidth, envTextureHeight)));
```
Store this in `uniforms.EnvironmentMapParams3.X`.

When no environment map is loaded, set to 0 (the gradient fallback doesn't use mips).

### Step 3: Modify `EvaluateEnvironmentLighting` Shader Function

Current signature (line 144):
```metal
static inline float3 EvaluateEnvironmentLighting(
    texture2d<float> environmentMap,
    float3 direction,
    float3 envBottom,
    float3 envTop,
    float envMapBlend,
    bool envMapAvailable,
    float envMapRotationRadians,
    float3 envOrientation)
```

**New signature** — add two parameters:
```metal
static inline float3 EvaluateEnvironmentLighting(
    texture2d<float> environmentMap,
    float3 direction,
    float3 envBottom,
    float3 envTop,
    float envMapBlend,
    bool envMapAvailable,
    float envMapRotationRadians,
    float3 envOrientation,
    float roughness,         // NEW
    float maxMipLevel)       // NEW
```

**Change the sampling line** (currently line 165):
```metal
// OLD:
float3 sampled = environmentMap.sample(envSampler, float2(u, v)).xyz;

// NEW:
float mipLevel = roughness * maxMipLevel;
float3 sampled = environmentMap.sample(envSampler, float2(u, v), level(mipLevel)).xyz;
```

When `maxMipLevel` is 0 (no map loaded), the `level(0)` call samples the base mip — identical to current behavior. Backward compatible.

### Step 4: Update All Call Sites

There are **four** call sites for `EvaluateEnvironmentLighting` in `fragment_main`:

1. **Main environment reflection** (line 913):
   ```metal
   float3 envColor = EvaluateEnvironmentLighting(
       environmentMap, envDir, envBottom, envTop,
       envMapBlend, envMapAvailable, envMapRotationRadians, envOrientation,
       roughness, maxMipLevel);  // ADD
   ```

2. **Lens refraction narrow** (line 971):
   ```metal
   float3 refractedEnv = EvaluateEnvironmentLighting(
       environmentMap, refractedDir, envBottom, envTop,
       envMapBlend, envMapAvailable, envMapRotationRadians, envOrientation,
       roughness * 0.5, maxMipLevel);  // ADD — refracted ray is sharper
   ```

3. **Lens refraction wide** (line 981):
   ```metal
   float3 refractedEnvWide = EvaluateEnvironmentLighting(
       environmentMap, refractedDirWide, envBottom, envTop,
       envMapBlend, envMapAvailable, envMapRotationRadians, envOrientation,
       clamp(roughness + 0.25, 0.0, 1.0), maxMipLevel);  // ADD — wide cone is blurrier
   ```

4. **Clear coat** does NOT call `EvaluateEnvironmentLighting` directly — it reuses `envColor` from call site 1. But clear coat should use its own roughness. **Add a second env lookup** for clear coat:
   ```metal
   // After line 934, replace:
   // accum += envColor * clearCoatFresnelView * envIntensity * clearCoatAmount * clearCoatEnvEnergy;
   // With:
   float3 clearCoatEnvColor = EvaluateEnvironmentLighting(
       environmentMap, R, envBottom, envTop,
       envMapBlend, envMapAvailable, envMapRotationRadians, envOrientation,
       clearCoatRoughness, maxMipLevel);
   accum += clearCoatEnvColor * clearCoatFresnelView * envIntensity * clearCoatAmount * clearCoatEnvEnergy;
   ```

### Step 5: Remove Scalar `roughEnergy` Approximation

The current line 929:
```metal
float roughEnergy = mix(1.12, 0.45, roughness * envRoughMix);
```
This was a hand-tuned scalar to fake roughness darkening. Now that mip sampling handles this physically, **replace** it:
```metal
float roughEnergy = mix(1.0, 0.65, roughness * envRoughMix);
```
Keep a mild roughness attenuation (energy conservation isn't perfect with box-filtered mips) but reduce the range from (1.12→0.45) to (1.0→0.65) since mip blur now handles most of the visual softening.

### Verification

- Smooth metallic knob (roughness 0.04): sharp environment reflections matching current output.
- Rough matte knob (roughness 0.85): soft/blurred reflections instead of the current dim-but-sharp reflections.
- No environment map loaded: identical to before (gradient fallback, maxMipLevel=0).
- Toggle lens (frosted glass): refracted environment should be softer than clear glass.
- App compiles and runs without assertion failures.

---

## Subphase 13B — Split-Sum BRDF Integration LUT

### Goal

Replace the current `envSpecular = envColor * fresnelView * specTint * envSpecWeight` approximation with the physically-correct split-sum approach: `envSpecular = envColor * (F0 * brdfLut.r + brdfLut.g) * specTint`. This corrects energy at grazing angles and for rough metals.

### Step 1: Create `BrdfLutGenerator.cs`

**File**: `KnobForge.Rendering/GPU/BrdfLutGenerator.cs`

This class bakes a 256×256 RG16Float texture at app startup using a CPU-side importance-sampled integration. It runs once and caches the result.

```csharp
namespace KnobForge.Rendering.GPU;

public static class BrdfLutGenerator
{
    public const int LutSize = 256;

    /// <summary>
    /// Returns a float[] of length LutSize*LutSize*2 containing (scale, bias) pairs
    /// in row-major order. Row = roughness (0 at top, 1 at bottom), Col = NdotV (0 at left, 1 at right).
    /// </summary>
    public static float[] Generate()
    {
        var data = new float[LutSize * LutSize * 2];
        for (int y = 0; y < LutSize; y++)
        {
            float roughness = (y + 0.5f) / LutSize;
            float alpha = roughness * roughness;
            for (int x = 0; x < LutSize; x++)
            {
                float NdotV = MathF.Max((x + 0.5f) / LutSize, 1e-4f);
                var (scale, bias) = IntegrateBrdf(NdotV, alpha);
                int idx = (y * LutSize + x) * 2;
                data[idx] = scale;
                data[idx + 1] = bias;
            }
        }
        return data;
    }

    private static (float scale, float bias) IntegrateBrdf(float NdotV, float alpha)
    {
        // View vector in tangent space.
        float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - NdotV * NdotV));
        var V = new System.Numerics.Vector3(sinTheta, 0f, NdotV);
        var N = new System.Numerics.Vector3(0f, 0f, 1f);

        float scale = 0f, bias = 0f;
        const int SampleCount = 1024;
        float alphaSq = alpha * alpha;

        for (int i = 0; i < SampleCount; i++)
        {
            // Hammersley sequence.
            float xi1 = (float)i / SampleCount;
            float xi2 = RadicalInverseVdC((uint)i);

            // GGX importance sampling.
            float phi = 2f * MathF.PI * xi1;
            float cosTheta = MathF.Sqrt(MathF.Max(0f, (1f - xi2) / (1f + (alphaSq - 1f) * xi2)));
            float sinThetaH = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));
            var H = new System.Numerics.Vector3(
                MathF.Cos(phi) * sinThetaH,
                MathF.Sin(phi) * sinThetaH,
                cosTheta);

            float VdotH = MathF.Max(System.Numerics.Vector3.Dot(V, H), 0f);
            var L = 2f * VdotH * H - V;
            float NdotL = MathF.Max(L.Z, 0f);
            float NdotH = MathF.Max(H.Z, 0f);

            if (NdotL > 0f)
            {
                float G = SmithGGX(NdotV, NdotL, alpha);
                float Gvis = G * VdotH / MathF.Max(NdotH * NdotV, 1e-7f);
                float Fc = MathF.Pow(1f - VdotH, 5f);
                scale += (1f - Fc) * Gvis;
                bias += Fc * Gvis;
            }
        }

        return (scale / SampleCount, bias / SampleCount);
    }

    private static float SmithGGX(float NdotV, float NdotL, float alpha)
    {
        float k = alpha / 2f;
        float gv = NdotV / (NdotV * (1f - k) + k);
        float gl = NdotL / (NdotL * (1f - k) + k);
        return gv * gl;
    }

    private static float RadicalInverseVdC(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f; // / 0x100000000
    }
}
```

### Step 2: Create and Upload the LUT Texture

In `MetalViewport.RuntimeAndGizmos.cs` (or wherever environment textures are managed):

Add a field:
```csharp
private IntPtr _brdfLutTexture;
private bool _brdfLutReady;
```

Add an `EnsureBrdfLutTexture()` method that:
1. Calls `BrdfLutGenerator.Generate()` once.
2. Creates a 256×256 `MTLTexture` with pixel format `rg16Float` (no mipmaps needed).
3. Uploads the float data (convert each float to Half via `System.Half`).
4. Sets `_brdfLutReady = true`.

Call this from the rendering setup path (same place `EnsureEnvironmentMapTexture` is called), so it runs once at startup.

### Step 3: Bind the LUT Texture

Bind the BRDF LUT to a new texture slot in `fragment_main`. Currently textures 0–8 are used:
- 0: spiralNormalMap
- 1: paintMask
- 2: paintColor
- 3: environmentMap
- 4: albedoMap
- 5: normalMap
- 6: roughnessMap
- 7: metallicMap
- 8: paintMask2

**Use texture slot 9** for the BRDF LUT.

**Metal side** — add to `fragment_main` signature:
```metal
texture2d<float> brdfLut [[texture(9)]]
```

**C# side** — in the render pass where `fragment_main` textures are bound, add:
```csharp
SetFragmentTexture(_brdfLutTexture, 9);
```

### Step 4: Sample the LUT in the Fragment Shader

In `fragment_main`, after the light loop and before the environment reflection block (around line 906), add:
```metal
// BRDF integration LUT lookup.
float2 brdfUV = float2(NdotV, roughness);
constexpr sampler brdfSampler(filter::linear, address::clamp_to_edge);
float2 brdfScale = brdfLut.sample(brdfSampler, brdfUV).rg;
```

### Step 5: Replace Environment Specular Calculation

**Current** (line 925):
```metal
float3 envSpecular = envColor * fresnelView * specTint * envSpecWeight;
```

**New**:
```metal
float3 envSpecular = envColor * (F0 * brdfScale.x + brdfScale.y) * specTint * envSpecWeight;
```

This replaces the view-angle-only `fresnelView` with the roughness-aware BRDF integration. For smooth metals, `brdfScale ≈ (1.0, 0.0)` so `F0 * 1.0` gives strong reflections. For rough surfaces, the bias term correctly adds energy at grazing angles.

### Step 6: Fallback When LUT Not Available

If the BRDF LUT texture is not yet ready (during the first frame or if creation fails), bind a 1×1 white texture or set `brdfScale = float2(1.0, 0.0)` via a uniform flag. This preserves the old `fresnelView`-based behavior as fallback.

Add a flag to `environmentMapParams3.y`:
- `1.0` = BRDF LUT is bound and valid
- `0.0` = fallback to `fresnelView`

In the shader:
```metal
bool useBrdfLut = uniforms.environmentMapParams3.y > 0.5;
float3 envF;
if (useBrdfLut)
{
    float2 brdfUV = float2(NdotV, roughness);
    float2 brdfScale = brdfLut.sample(brdfSampler, brdfUV).rg;
    envF = F0 * brdfScale.x + brdfScale.y;
}
else
{
    envF = fresnelView;
}
float3 envSpecular = envColor * envF * specTint * envSpecWeight;
```

### Verification

- Smooth chrome knob: reflections nearly identical to before (brdfScale ≈ (1, 0), close to fresnelView).
- Rough dark metal: improved grazing angle brightness, less energy loss than before.
- Clear coat on rough body: clear coat uses `fresnelView` (no change — coat is separate), body underneath uses BRDF LUT.
- Performance: single texture sample per fragment — negligible cost.
- App compiles and runs without assertion failures.

---

## Subphase 13C — Shaped Bloom Kernels

### Goal

Add bloom kernel shape options so that specular highlights can produce star, streak, or soft glare patterns instead of the current isotropic Gaussian blob.

### Step 1: Add `BloomKernelShape` Enum

**File**: `KnobForge.Core/BloomKernelShape.cs`

```csharp
namespace KnobForge.Core;

public enum BloomKernelShape
{
    Soft = 0,      // Current behavior — 2-pass H+V Gaussian
    Star4 = 1,     // 4-point star (4 directional passes at 0°, 45°, 90°, 135°)
    Star6 = 2,     // 6-point star (6 directional passes at 0°, 30°, 60°, 90°, 120°, 150°)
    AnamorphicStreak = 3  // Horizontal-dominant streak (wide H + narrow V)
}
```

### Step 2: Add Project Property

**File**: `KnobProject.cs`

```csharp
public BloomKernelShape BloomKernelShape { get; set; } = BloomKernelShape.Soft;
```

Default `Soft` = identical to current behavior.

### Step 3: Implement Multi-Directional Blur Passes

The existing `fragment_bloom_blur` already accepts an arbitrary direction vector via `PostProcessParams2.zw`. No shader changes needed — all the logic is in the C# dispatch.

**In `MetalViewport.cs`**, replace the current 2-pass blur dispatch with a shape-aware dispatch. Create a helper method:

```csharp
private static (float dirX, float dirY)[] GetBloomDirections(BloomKernelShape shape)
{
    return shape switch
    {
        BloomKernelShape.Star4 => new[]
        {
            (1.0f, 0.0f),     // 0°
            (0.0f, 1.0f),     // 90°
            (0.707f, 0.707f), // 45°
            (-0.707f, 0.707f) // 135°
        },
        BloomKernelShape.Star6 => new[]
        {
            (1.0f, 0.0f),       // 0°
            (0.5f, 0.866f),     // 60°
            (-0.5f, 0.866f),    // 120°
            (0.0f, 1.0f),       // 90°
            (0.866f, 0.5f),     // 30°
            (-0.866f, 0.5f)     // 150°
        },
        BloomKernelShape.AnamorphicStreak => new[]
        {
            (1.0f, 0.0f),   // Horizontal (long)
            (1.0f, 0.0f),   // Horizontal again (double pass for extra spread)
            (0.0f, 1.0f)    // Vertical (short, single pass)
        },
        _ => new[]  // Soft — current behavior
        {
            (1.0f, 0.0f),
            (0.0f, 1.0f)
        }
    };
}
```

### Step 4: Modify Bloom Dispatch Loop

Replace the current two hardcoded blur passes with:

```csharp
var directions = GetBloomDirections(project?.BloomKernelShape ?? BloomKernelShape.Soft);

// For star shapes, we need to accumulate results additively.
// Strategy: Run extract → for each direction pair, blur H then V → accumulate into result.
// For Soft (2 passes): identical to current — blur ping-pongs between two textures.
// For Star4/Star6: each directional pass blurs from extract into blur texture,
//   then accumulate blur texture into a result texture with additive blending.
//   Final result = average of all directional passes.

if (directions.Length == 2)
{
    // Current Soft path — no changes needed.
    // Pass 1: extract → blur (horizontal)
    // Pass 2: blur → extract (vertical) — reuses extract as ping-pong target
    // Composite reads from extract.
}
else
{
    // Multi-directional: for each direction, run one blur pass from
    // _bloomExtractTexture into _bloomBlurTexture, then additively
    // composite _bloomBlurTexture into the accumulation target.
    // After all directions, scale by 1/numDirections.

    // Use postProcessParams2.zw for direction, existing blur shader handles it.
    for (int d = 0; d < directions.Length; d++)
    {
        uniforms.PostProcessParams2 = new Vector4(
            1f / MathF.Max(1f, _bloomTextureWidth),
            1f / MathF.Max(1f, _bloomTextureHeight),
            directions[d].dirX,
            directions[d].dirY);
        RenderBloomBlurPass(commandBuffer, _bloomBlurTexture, _bloomExtractTexture, uniforms);
        // Accumulate _bloomBlurTexture into result...
    }
}
```

**IMPORTANT**: For the multi-directional case, you need a way to additively accumulate multiple blur results. Options:

**Option A (simpler — recommended)**: Run all directional passes sequentially, ping-ponging between two textures, where each pass reads the *original* extract texture and writes an additively blended result. This requires a third bloom texture (`_bloomAccumTexture`) or reusing the additive blend pipeline that already exists (`MetalPipelineManager` has an `Additive` pipeline variant).

**Option B**: For each direction, blur from extract → blur texture, then blit/add blur texture onto an accumulation texture using the existing additive pipeline. After all directions, divide by the direction count (encode as a uniform).

Choose Option A or B based on what's simplest with the existing pipeline infrastructure. The key constraint: the existing `fragment_bloom_blur` shader doesn't need modification — it already handles arbitrary direction vectors.

### Step 5: Add Bloom Accumulation Texture (if needed)

If the multi-directional dispatch requires a third bloom texture, add `_bloomAccumTexture` alongside the existing `_bloomExtractTexture` and `_bloomBlurTexture`. Create it with the same dimensions and pixel format.

### Step 6: Normalize Multi-Directional Bloom

After accumulating all directional blur results, the accumulated brightness will be proportional to the number of directions. To maintain consistent bloom intensity across shapes:

For Star4 (4 directions): multiply final bloom by `0.25` (or `2.0/numDirections` if you want stars brighter than soft).
For Star6 (6 directions): multiply by `1.0/6.0 * 3.0` = `0.5` (stars should be roughly 2x the intensity of a single pass for visual impact).
For AnamorphicStreak: the two horizontal passes naturally double horizontal intensity, which is the desired anamorphic look. Normalize vertical to match.

Encode the normalization factor in `PostProcessParams` or a new uniform field.

### Step 7: Inspector UI Integration (Minimal)

Add a `BloomKernelShape` dropdown in the post-processing section of the inspector panel. Follow the same ComboBox pattern used for `ExportFilterPreset` or `TonemapMode`. Bind to `project.BloomKernelShape`.

### Verification

- `Soft`: identical bloom to before — 2-pass Gaussian.
- `Star4`: bright specular highlights produce a 4-pointed star pattern.
- `Star6`: bright specular highlights produce a 6-pointed star pattern.
- `AnamorphicStreak`: bright specular highlights produce a horizontal streak.
- Bloom intensity is visually consistent across shapes (soft and star look similarly bright from a distance).
- Export pipeline produces correct bloom shape in rendered filmstrips.
- App compiles and runs without assertion failures.

---

## Subphase 13D — Bundled Environment Presets

### Goal

Provide 4 curated procedural gradient presets so users get good-looking studio lighting without needing to load an HDRI file.

### Step 1: Add `EnvironmentPreset` Enum

**File**: `KnobForge.Core/EnvironmentPreset.cs`

```csharp
namespace KnobForge.Core;

public enum EnvironmentPreset
{
    Custom = 0,      // User-defined colors (current behavior, default)
    Studio = 1,      // Clean neutral studio with warm key light
    Rack = 2,        // Dark rack-mount environment with cool side fill
    Showroom = 3,    // Bright showroom with vivid reflections
    Dark = 4         // Moody dark environment for dramatic contrast
}
```

### Step 2: Define Preset Values

**File**: `KnobForge.Core/EnvironmentPresets.cs`

```csharp
using System.Numerics;

namespace KnobForge.Core;

public readonly record struct EnvironmentPresetDefinition(
    EnvironmentPreset Preset,
    string DisplayName,
    Vector3 TopColor,
    Vector3 BottomColor,
    float Intensity,
    float RoughnessMix);

public static class EnvironmentPresets
{
    private static readonly EnvironmentPresetDefinition[] Definitions = new[]
    {
        new EnvironmentPresetDefinition(
            EnvironmentPreset.Studio,
            "Studio",
            new Vector3(0.85f, 0.82f, 0.78f),   // Warm white top
            new Vector3(0.18f, 0.17f, 0.20f),    // Cool dark floor
            1.20f,
            0.75f),
        new EnvironmentPresetDefinition(
            EnvironmentPreset.Rack,
            "Rack",
            new Vector3(0.22f, 0.24f, 0.30f),    // Cool blue-grey top
            new Vector3(0.05f, 0.04f, 0.06f),    // Very dark bottom
            0.85f,
            0.60f),
        new EnvironmentPresetDefinition(
            EnvironmentPreset.Showroom,
            "Showroom",
            new Vector3(1.00f, 0.97f, 0.92f),    // Bright warm white
            new Vector3(0.35f, 0.32f, 0.28f),    // Warm mid-tone floor
            1.50f,
            0.90f),
        new EnvironmentPresetDefinition(
            EnvironmentPreset.Dark,
            "Dark",
            new Vector3(0.10f, 0.10f, 0.12f),    // Near-black top
            new Vector3(0.03f, 0.02f, 0.02f),    // Deep shadow bottom
            0.55f,
            0.40f)
    };

    public static IReadOnlyList<EnvironmentPresetDefinition> All => Definitions;

    public static EnvironmentPresetDefinition? Get(EnvironmentPreset preset)
    {
        foreach (var def in Definitions)
        {
            if (def.Preset == preset)
                return def;
        }
        return null;
    }
}
```

### Step 3: Add Project Property

**File**: `KnobProject.cs`

```csharp
public EnvironmentPreset EnvironmentPreset { get; set; } = EnvironmentPreset.Custom;
```

Default `Custom` = current user-defined colors unchanged.

### Step 4: Apply Preset When Selected

In the code that uploads environment uniforms to the GPU (look for where `environmentTopColorAndIntensity` and `environmentBottomColorAndRoughnessMix` are set — likely in `MetalViewport.cs` or `MetalViewport.ProjectTypesAndBvh.cs`):

```csharp
if (project.EnvironmentPreset != EnvironmentPreset.Custom)
{
    var preset = EnvironmentPresets.Get(project.EnvironmentPreset);
    if (preset.HasValue)
    {
        var p = preset.Value;
        uniforms.EnvironmentTopColorAndIntensity = new Vector4(p.TopColor, p.Intensity);
        uniforms.EnvironmentBottomColorAndRoughnessMix = new Vector4(p.BottomColor, p.RoughnessMix);
    }
}
// else: use the user's custom values as currently done.
```

### Step 5: Inspector UI Integration

Add an `EnvironmentPreset` dropdown in the environment section of the inspector panel. When the user selects a preset other than Custom:
- Apply the preset colors and intensity.
- Optionally grey out (but don't disable) the manual color pickers so users can see the preset values.
- If the user modifies any environment color/intensity manually, auto-switch back to `Custom`.

Follow the same ComboBox pattern used for other inspector dropdowns.

### Step 6: Preset Thumbnail Preview (Optional, Low Priority)

If time permits, render a small preview swatch for each preset in the dropdown. This is NOT required for 13D to be complete — it's a polish item.

### Verification

- `Custom` preset: environment colors and intensity identical to current behavior.
- `Studio` preset: neutral, well-lit appearance with warm reflections on metallic surfaces.
- `Rack` preset: dark, moody appearance with cool blue reflections.
- `Showroom` preset: bright, vivid appearance with strong reflections.
- `Dark` preset: dramatic contrast with minimal ambient light.
- Switching presets updates the viewport immediately.
- Saving and loading a project preserves the selected preset.
- App compiles and runs without assertion failures.

---

## File Touchpoint Table

| Subphase | File | Action |
|---|---|---|
| 13A | `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs` | Modify `EvaluateEnvironmentLighting` signature + sampling; update all 4 call sites; adjust `roughEnergy`; add `environmentMapParams3` to Metal `GpuUniforms` |
| 13A | `KnobForge.App/Controls/MetalViewport/MetalViewport.ProjectTypesAndBvh.cs` | Add `EnvironmentMapParams3` to C# `GpuUniforms` struct |
| 13A | `KnobForge.App/Controls/MetalViewport/MetalViewport.RuntimeAndGizmos.cs` | Compute and upload `envMapMaxMipLevel` |
| 13B | `KnobForge.Rendering/GPU/BrdfLutGenerator.cs` | **NEW** — CPU-side BRDF integration LUT baker |
| 13B | `KnobForge.App/Controls/MetalViewport/MetalViewport.RuntimeAndGizmos.cs` | Create + upload BRDF LUT texture (texture slot 9) |
| 13B | `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs` | Add `brdfLut` texture parameter to `fragment_main`; sample LUT; replace `fresnelView` in env specular |
| 13B | `KnobForge.App/Controls/MetalViewport/MetalViewport.ProjectTypesAndBvh.cs` | Add `brdfLutAvailable` flag to `environmentMapParams3.y` |
| 13C | `KnobForge.Core/BloomKernelShape.cs` | **NEW** — enum definition |
| 13C | `KnobForge.Core/KnobProject.cs` | Add `BloomKernelShape` property |
| 13C | `KnobForge.App/Controls/MetalViewport.cs` | Multi-directional bloom blur dispatch; optional `_bloomAccumTexture` |
| 13C | Inspector AXAML (post-process section) | Add BloomKernelShape ComboBox |
| 13D | `KnobForge.Core/EnvironmentPreset.cs` | **NEW** — enum definition |
| 13D | `KnobForge.Core/EnvironmentPresets.cs` | **NEW** — preset definitions + lookup |
| 13D | `KnobForge.Core/KnobProject.cs` | Add `EnvironmentPreset` property |
| 13D | `KnobForge.App/Controls/MetalViewport.cs` (or uniform upload site) | Apply preset overrides to environment uniforms |
| 13D | Inspector AXAML (environment section) | Add EnvironmentPreset ComboBox |

---

## Appendix A: GpuUniforms Extension Plan

The new `float4 environmentMapParams3` field uses:
- `.x` = `envMapMaxMipLevel` (13A)
- `.y` = `brdfLutAvailable` flag — 1.0 if LUT texture is bound (13B)
- `.z` = reserved (future)
- `.w` = reserved (future)

This field goes at the end of the Metal `GpuUniforms` struct (after `dynamicLights[MAX_LIGHTS]`) and at the matching position in the C# struct. Ensure 16-byte alignment.

## Appendix B: Texture Slot Map (After Phase 13)

| Slot | Texture | Used By |
|---|---|---|
| 0 | spiralNormalMap | fragment_main |
| 1 | paintMask | vertex_main, fragment_main |
| 2 | paintColor | fragment_main |
| 3 | environmentMap | fragment_main |
| 4 | albedoMap | fragment_main |
| 5 | normalMap | fragment_main |
| 6 | roughnessMap | fragment_main |
| 7 | metallicMap | fragment_main |
| 8 | paintMask2 | fragment_main |
| 9 | brdfLut | fragment_main (**NEW** — 13B) |

## Appendix C: Performance Notes

- **13A** adds zero new texture samples for the gradient fallback path. When an environment map is loaded, sampling at an explicit mip level is the same cost as the current implicit level-0 sample. The clear coat path adds one extra env sample per fragment when clear coat > 0.
- **13B** adds one RG16F texture sample per fragment. At 256×256, the LUT fits in L1 cache on all Apple GPUs.
- **13C** Star4 = 4 blur passes instead of 2; Star6 = 6 passes instead of 2. Bloom textures are typically 1/4 to 1/2 resolution, so additional passes are cheap. AnamorphicStreak = 3 passes.
- **13D** is zero-cost at runtime — it only changes uniform values.

## Appendix D: Backward Compatibility Defaults

| Setting | Default Value | Matches Current Behavior |
|---|---|---|
| `envMapMaxMipLevel` | 0 when no map loaded | Yes — gradient unaffected |
| `brdfLutAvailable` | 0 until LUT created | Yes — falls back to `fresnelView` |
| `BloomKernelShape` | `Soft` | Yes — 2-pass H+V Gaussian |
| `EnvironmentPreset` | `Custom` | Yes — user colors unchanged |
