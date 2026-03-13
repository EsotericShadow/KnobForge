# Codex Implementation Prompt — Phase 2: Texture Map Import

## Your Role

You are implementing Phase 2 of the KnobForge Material Tool Transformation. Your job is to add PBR texture map support: users assign albedo, normal, roughness, and metallic maps to materials, and see them rendered in real-time. Work incrementally — complete each subphase, verify it compiles, then move to the next.

## CRITICAL LESSON FROM PHASE 1

Phase 1 introduced a UV regression because the implementor mixed up two coordinate spaces. **KnobForge has two separate UV systems that must NEVER be conflated:**

1. **Paint UVs** — legacy planar projection: `localXY / (referenceRadius * 2.0) + 0.5`. Used for paint mask sampling, scratch carve, and weathering. These are computed at runtime in the shader from world position. **DO NOT TOUCH THIS.**

2. **Material UVs** — `inVertex.texcoord`. These are proper vertex UVs computed at mesh build time. Phase 2 texture maps sample using these UVs.

The fragment shader already has both. Paint uses `paintUv` (computed from `localXY`). Material sampling uses `inVertex.texcoord` (from vertex attribute). When you add texture map sampling, use `inVertex.texcoord`. Never touch the paint UV path.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only). Metal interop is raw P/Invoke (no managed wrapper). Shaders are inline C# strings. SkiaSharp is used for image loading. System.Text.Json for serialization.

## What You're Building

Users will be able to assign 4 PBR texture maps to any material:
- **Albedo** (RGB color) — replaces the flat `baseColor`
- **Normal** (tangent-space) — perturbs surface normals for detail
- **Roughness** (grayscale) — replaces the scalar `roughness` value
- **Metallic** (grayscale) — replaces the scalar `metallic` value

These texture maps are sampled in the fragment shader BEFORE the weathering/paint pass, so paint and weathering apply on top of textured surfaces.

## Current State (Verified Post-Phase 1)

### Fragment Shader Signature — 4 texture slots used (DO NOT RENUMBER)
```metal
fragment float4 fragment_main(
    VertexOut inVertex [[stage_in]],
    constant GpuUniforms& uniforms [[buffer(1)]],
    texture2d<float> spiralNormalMap [[texture(0)]],
    texture2d<float> paintMask [[texture(1)]],
    texture2d<float> paintColor [[texture(2)]],
    texture2d<float> environmentMap [[texture(3)]])
```
You will ADD texture slots 4–7 to this signature. Do not change slots 0–3.

### VertexOut — already has texcoord from Phase 1
```metal
struct VertexOut
{
    float4 position [[position]];
    float3 worldPos;
    float3 worldNormal;
    float4 worldTangentSign;
    float2 texcoord;
};
```
The TBN data is already flowing: `worldTangentSign.xyz` = tangent, `worldTangentSign.w` = handedness sign. You need this for normal mapping.

### GpuUniforms (shader-side) — 35 named float4 fields + lights
```metal
struct GpuUniforms
{
    float4 cameraPosAndReferenceRadius;
    float4 rightAndScaleX;
    float4 upAndScaleY;
    float4 forwardAndScaleZ;
    float4 projectionOffsetsAndLightCount;
    float4 materialBaseColorAndMetallic;
    float4 materialRoughnessDiffuseSpecMode;
    float4 materialPartTopColorAndMetallic;
    float4 materialPartBevelColorAndMetallic;
    float4 materialPartSideColorAndMetallic;
    float4 materialPartRoughnessAndEnable;
    float4 materialSurfaceBrushParams;
    float4 weatherParams;
    float4 scratchExposeColorAndStrength;
    float4 advancedMaterialParams;
    float4 indicatorParams0;
    float4 indicatorParams1;
    float4 indicatorColorAndBlend;
    float4 indicatorParams2;
    float4 microDetailParams;
    float4 environmentTopColorAndIntensity;
    float4 environmentBottomColorAndRoughnessMix;
    float4 modelRotationCosSin;
    float4 shadowParams;
    float4 shadowColorAndOpacity;
    float4 debugBasisParams;
    float4 lensMaterialParams0;
    float4 lensMaterialTintAndAbsorption;
    float4 environmentMapParams;
    float4 environmentMapParams2;
    float4 postProcessParams;
    float4 postProcessParams2;
    float4 tonemapParams;
    GpuLight lights[MAX_LIGHTS];
    float4 dynamicLightParams;
    GpuLight dynamicLights[MAX_LIGHTS];
};
```
You will add new fields **at the end, before `GpuLight lights[MAX_LIGHTS]`**. The C# struct must match exactly.

### GpuUniforms (C# side) — in MetalViewport.ProjectTypesAndBvh.cs
```csharp
[StructLayout(LayoutKind.Sequential)]
private struct GpuUniforms
{
    // ... 33 named Vector4 fields ...
    public Vector4 TonemapParams;
    public GpuLight Light0; // through Light7
    public Vector4 DynamicLightParams;
    public GpuLight DynamicLight0; // through DynamicLight7
}
```
New fields go after `TonemapParams` and before `Light0`.

### Texture Binding — TWO render paths bind textures
Both must be updated:

**1. Offscreen export path** — `MetalViewport.OffscreenRender.cs` ~line 499:
```csharp
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex, _spiralNormalTexture, 0);
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex, _paintMaskTexture, 1);
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex, _paintColorTexture, 2);
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex, _environmentMapTexture, 3);
```

**2. Live viewport path** — `MetalViewport.cs` ~line 1035:
```csharp
// Same pattern, same 4 slots
```

After both blocks, you must add bindings for slots 4–7 (or fallback textures when no map is assigned).

### Metal Texture Creation Pattern — follow this exactly
From `EnsurePaintMaskTexture` in `MetalViewport.PaintResources.cs`:

```csharp
// 1. Create descriptor
IntPtr descriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
    ObjCClasses.MTLTextureDescriptor,
    Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
    PaintMaskPixelFormat,  // use RGBA8Unorm = 70
    (nuint)width,
    (nuint)height,
    true);  // mipmapped

ObjC.Void_objc_msgSend_UInt(descriptor, Selectors.SetUsage, PaintMaskTextureUsage);

// 2. Create texture
IntPtr texture = ObjC.IntPtr_objc_msgSend_IntPtr(
    _context.Device.Handle,
    Selectors.NewTextureWithDescriptor,
    descriptor);

// 3. Upload pixel data
GCHandle pinned = GCHandle.Alloc(pixelBytes, GCHandleType.Pinned);
try
{
    MTLRegion region = new MTLRegion(
        new MTLOrigin(0, 0, 0),
        new MTLSize((nuint)width, (nuint)height, 1));
    ObjC.Void_objc_msgSend_MTLRegion_UInt_IntPtr_UInt(
        texture,
        Selectors.ReplaceRegionMipmapLevelWithBytesBytesPerRow,
        region, 0,
        pinned.AddrOfPinnedObject(),
        (nuint)(width * 4));
}
finally { pinned.Free(); }

// 4. Generate mipmaps
IntPtr commandBuffer = _context.CreateCommandBuffer().Handle;
IntPtr blitEncoder = ObjC.IntPtr_objc_msgSend(commandBuffer, Selectors.BlitCommandEncoder);
ObjC.Void_objc_msgSend_IntPtr(blitEncoder, Selectors.GenerateMipmapsForTexture, texture);
ObjC.Void_objc_msgSend(blitEncoder, Selectors.EndEncoding);
ObjC.Void_objc_msgSend(commandBuffer, Selectors.Commit);
ObjC.Void_objc_msgSend(commandBuffer, Selectors.WaitUntilCompleted);
```

### MaterialNode — current properties (NO texture fields yet)
```csharp
// Scene/MaterialNode.cs — flat property bag
// Properties: BaseColor, TopBaseColor, BevelBaseColor, SideBaseColor,
//   PartMaterialsEnabled, TopMetallic, TopRoughness, BevelMetallic, BevelRoughness,
//   SideMetallic, SideRoughness, Metallic, Roughness, Pearlescence,
//   RustAmount, WearAmount, GunkAmount, RadialBrushStrength, RadialBrushDensity,
//   SurfaceCharacter, SpecularPower, DiffuseStrength, SpecularStrength
// NO texture path properties. NO texture references.
```

### Paint UV Path — the code that MUST NOT CHANGE
```metal
// Lines ~601 in fragment_main (already fixed in Phase 1):
float2 paintUv = localXY / max(referenceRadius * 2.0, 1e-4) + 0.5;
```
This is the paint mask sampling UV. It uses a legacy planar projection from world-space XY. **Do not change this line. Do not replace it with inVertex.texcoord. Do not touch the paint sampling block.**

### SkiaSharp Format Support (Verified)
- **Supported decode**: PNG, JPEG, WebP, GIF, BMP, ICO
- **NOT supported**: TIFF (despite what the plan doc says), EXR, HDR, TGA
- Use `SKBitmap.Decode(filePath)` then convert to RGBA8 via `bitmap.Copy(SKColorType.Rgba8888)`

## Execution Order

### SUBPHASE 2A: Data Model (do this FIRST)

**Step 1: Add texture path properties to MaterialNode**

File: `KnobForge.Core/Scene/MaterialNode.cs`

Add these nullable string properties:
```csharp
public string? AlbedoMapPath { get; set; }
public string? NormalMapPath { get; set; }
public string? RoughnessMapPath { get; set; }
public string? MetallicMapPath { get; set; }

public float NormalMapStrength { get; set; } = 1.0f;

// Convenience (not serialized, computed)
public bool HasAlbedoMap => !string.IsNullOrEmpty(AlbedoMapPath);
public bool HasNormalMap => !string.IsNullOrEmpty(NormalMapPath);
public bool HasRoughnessMap => !string.IsNullOrEmpty(RoughnessMapPath);
public bool HasMetallicMap => !string.IsNullOrEmpty(MetallicMapPath);
```

Follow the existing property pattern in the file. These are simple auto-properties — no special clamping needed for paths.

**Step 2: Verify serialization**

System.Text.Json will naturally serialize/deserialize nullable strings as null when missing. Load an old .knob file and verify it doesn't crash — the missing properties will default to null. Build and test.

### SUBPHASE 2B: Texture Loading Infrastructure

**Step 3: Create TextureManager class**

New file: `KnobForge.Rendering/GPU/TextureManager.cs`

This class manages the lifecycle of Material texture Metal handles. It should:
- Accept a file path → decode with SkiaSharp → create Metal texture → cache it
- Return a Metal texture IntPtr (or IntPtr.Zero if loading fails)
- Provide fallback textures: 1x1 white (albedo default), 1x1 flat normal (128,128,255,255), 1x1 mid-gray (roughness default), 1x1 white (metallic default)
- Track file modification time for cache invalidation
- Dispose all textures on cleanup

```csharp
public class TextureManager : IDisposable
{
    // Takes the Metal device handle for texture creation
    public TextureManager(IntPtr metalDevice) { ... }

    // Returns cached Metal texture, or loads and caches it. Returns fallback on failure.
    public IntPtr GetOrLoadTexture(string? filePath, TextureMapType mapType) { ... }

    // Call when a path changes to force reload next frame
    public void InvalidatePath(string? filePath) { ... }

    // Fallback textures (created once)
    public IntPtr FallbackAlbedo { get; }
    public IntPtr FallbackNormal { get; }
    public IntPtr FallbackRoughness { get; }
    public IntPtr FallbackMetallic { get; }

    public void Dispose() { ... }
}

public enum TextureMapType { Albedo, Normal, Roughness, Metallic }
```

**IMPORTANT:** The TextureManager needs access to the same Metal interop pattern used in `MetalViewport.PaintResources.cs`. It needs the `ObjC` helper, `Selectors`, `ObjCClasses`, `MTLRegion`, `MTLOrigin`, `MTLSize` types. Check where these are defined and ensure TextureManager can access them. If they're internal to the App project, you may need to either:
- Put TextureManager in the App project alongside MetalViewport, or
- Pass a texture-creation delegate/interface from MetalViewport to TextureManager

Choose whichever approach is cleanest given the existing project structure.

**Step 4: Implement SkiaSharp loading**

```csharp
private byte[]? LoadImageToRgba8(string filePath, out int width, out int height)
{
    width = height = 0;
    using var bitmap = SKBitmap.Decode(filePath);
    if (bitmap == null) return null;

    // Clamp to 4096 max
    int maxDim = 4096;
    if (bitmap.Width > maxDim || bitmap.Height > maxDim)
    {
        float scale = Math.Min((float)maxDim / bitmap.Width, (float)maxDim / bitmap.Height);
        int newW = (int)(bitmap.Width * scale);
        int newH = (int)(bitmap.Height * scale);
        using var resized = bitmap.Resize(new SKImageInfo(newW, newH, SKColorType.Rgba8888, SKAlphaType.Unpremul), SKFilterQuality.High);
        if (resized == null) return null;
        width = resized.Width;
        height = resized.Height;
        return resized.GetPixelSpan().ToArray();
    }

    // Convert to RGBA8
    using var converted = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    if (!bitmap.CopyTo(converted)) return null;
    width = converted.Width;
    height = converted.Height;
    return converted.GetPixelSpan().ToArray();
}
```

**Step 5: Create fallback textures**

Create 1x1 textures at initialization:
- Albedo fallback: `[255, 255, 255, 255]` (white — multiplies with existing baseColor)
- Normal fallback: `[128, 128, 255, 255]` (flat up-facing tangent-space normal)
- Roughness fallback: `[128, 128, 128, 255]` (mid-gray — 0.5 roughness, effectively neutral)
- Metallic fallback: `[255, 255, 255, 255]` (white — uses existing metallic scalar)

Build and verify.

### SUBPHASE 2C: GPU Pipeline Integration

**Step 6: Extend GpuUniforms — BOTH shader and C# structs**

Add to the shader GpuUniforms struct (AFTER `tonemapParams`, BEFORE `lights`):
```metal
float4 textureMapFlags;    // x=hasAlbedo, y=hasNormal, z=hasRoughness, w=hasMetallic
float4 textureMapParams;   // x=normalStrength, yzw=reserved
```

Add matching fields to the C# GpuUniforms struct (AFTER `TonemapParams`, BEFORE `Light0`):
```csharp
public Vector4 TextureMapFlags;
public Vector4 TextureMapParams;
```

**THE C# AND SHADER STRUCTS MUST MATCH EXACTLY IN ORDER AND SIZE.** Count the fields. If you add 2 float4s to the shader, add 2 Vector4s to the C# struct in the same position.

**Step 7: Add texture slots 4–7 to fragment shader**

Update the fragment_main signature:
```metal
fragment float4 fragment_main(
    VertexOut inVertex [[stage_in]],
    constant GpuUniforms& uniforms [[buffer(1)]],
    texture2d<float> spiralNormalMap [[texture(0)]],
    texture2d<float> paintMask [[texture(1)]],
    texture2d<float> paintColor [[texture(2)]],
    texture2d<float> environmentMap [[texture(3)]],
    texture2d<float> albedoMap [[texture(4)]],
    texture2d<float> normalMap [[texture(5)]],
    texture2d<float> roughnessMap [[texture(6)]],
    texture2d<float> metallicMap [[texture(7)]])
```

**Step 8: Add texture map sampling in fragment shader**

Insert texture sampling AFTER the `baseColor` / `metallic` / `roughness` scalar setup, and BEFORE the paint/weathering block. The key insertion point is where `baseColor` is first established from uniforms, and before `paintUv` is computed.

```metal
// === TEXTURE MAP SAMPLING (material UVs, not paint UVs) ===
float2 matUV = inVertex.texcoord;
constexpr sampler texSampler(filter::linear, mip_filter::linear, address::repeat);

if (uniforms.textureMapFlags.x > 0.5) {
    float4 albedoSample = albedoMap.sample(texSampler, matUV);
    baseColor = albedoSample.rgb;
}

if (uniforms.textureMapFlags.z > 0.5) {
    float roughSample = roughnessMap.sample(texSampler, matUV).r;
    roughness = clamp(roughSample, 0.04, 1.0);
}

if (uniforms.textureMapFlags.w > 0.5) {
    float metalSample = metallicMap.sample(texSampler, matUV).r;
    metallic = clamp(metalSample, 0.0, 1.0);
}

if (uniforms.textureMapFlags.y > 0.5) {
    float3 T = normalize(inVertex.worldTangentSign.xyz);
    float3 N = normal;
    float3 B = cross(N, T) * inVertex.worldTangentSign.w;
    float3x3 TBN = float3x3(T, B, N);

    float3 normalSample = normalMap.sample(texSampler, matUV).rgb;
    normalSample = normalSample * 2.0 - 1.0;  // decode [0,1] -> [-1,1]
    float nStrength = uniforms.textureMapParams.x;
    normalSample.xy *= nStrength;
    normalSample = normalize(normalSample);
    normal = normalize(TBN * normalSample);
}
```

**IMPORTANT ORDER:** The normal map modifies `normal`. The `normal` variable is used later for lighting. Make sure the normal map is applied before any lighting calculations but after the normal is first established.

**Step 9: Bind textures in BOTH render paths**

In `MetalViewport.OffscreenRender.cs` (after the existing 4 texture binds):
```csharp
// Bind material texture maps (slots 4-7)
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
    _textureManager?.GetOrLoadTexture(materialNode?.AlbedoMapPath, TextureMapType.Albedo) ?? _textureManager?.FallbackAlbedo ?? IntPtr.Zero, 4);
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
    _textureManager?.GetOrLoadTexture(materialNode?.NormalMapPath, TextureMapType.Normal) ?? _textureManager?.FallbackNormal ?? IntPtr.Zero, 5);
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
    _textureManager?.GetOrLoadTexture(materialNode?.RoughnessMapPath, TextureMapType.Roughness) ?? _textureManager?.FallbackRoughness ?? IntPtr.Zero, 6);
ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
    _textureManager?.GetOrLoadTexture(materialNode?.MetallicMapPath, TextureMapType.Metallic) ?? _textureManager?.FallbackMetallic ?? IntPtr.Zero, 7);
```

Do the same in `MetalViewport.cs` (the live viewport path, ~line 1050).

**Step 10: Upload texture flags in uniform building**

Find where `uniforms.MaterialBaseColorAndMetallic` is set (in the uniform building method). After the existing material uniform uploads, add:

```csharp
uniforms.TextureMapFlags = new Vector4(
    materialNode?.HasAlbedoMap == true ? 1f : 0f,
    materialNode?.HasNormalMap == true ? 1f : 0f,
    materialNode?.HasRoughnessMap == true ? 1f : 0f,
    materialNode?.HasMetallicMap == true ? 1f : 0f);
uniforms.TextureMapParams = new Vector4(
    materialNode?.NormalMapStrength ?? 1f, 0f, 0f, 0f);
```

**Step 11: Build and verify**

The app must compile. With no texture maps assigned, rendering must be identical to Phase 1 output (the texture flags will all be 0, so sampling is skipped). If anything looks different, a uniform offset is wrong — check the struct field ordering.

### SUBPHASE 2D: Inspector UI

**Step 12: Add texture map UI to the material inspector**

Find the material inspector panel (look for where BaseColor, Metallic, Roughness sliders are created in the UI). Add a new section below the existing material controls:

For each map type (Albedo, Normal, Roughness, Metallic):
- A label with the map name
- A TextBlock showing the current file path (or "None")
- A "Browse..." Button that opens `StorageProvider.OpenFilePickerAsync` with image file filter
- A "Clear" Button that sets the path to null

On browse: set `materialNode.AlbedoMapPath = selectedFile.Path.LocalPath` (or equivalent), then invalidate the TextureManager cache for the old path.

**Step 13: Add normal map strength slider**

Only visible when a normal map is assigned. Range 0.0–2.0, default 1.0.

**Step 14: Build and test end-to-end**

Build. Launch. Open a knob project. Browse to assign an albedo texture. The viewport should show the texture mapped onto the knob's top face. Assign a normal map. Bumps should appear. Assign roughness/metallic maps. Surface should change accordingly. Clear all maps — rendering returns to flat scalar materials.

## Critical Constraints

1. **DO NOT TOUCH PAINT UV PATH.** The paint system uses its own legacy UV space. Texture maps use `inVertex.texcoord`. These are separate. This was the #1 lesson from Phase 1.
2. **STRUCT ALIGNMENT.** The C# `GpuUniforms` and MSL `GpuUniforms` must have fields in exactly the same order and count. A single missing or extra field will shift all subsequent data and produce garbage rendering.
3. **FALLBACK TEXTURES ALWAYS BOUND.** Even when no texture is assigned, a 1x1 fallback must be bound to slots 4–7. Never bind IntPtr.Zero to a texture slot — it will crash or produce undefined behavior.
4. **DO NOT CHANGE EXISTING TEXTURE SLOTS.** Slots 0–3 are spiralNormalMap, paintMask, paintColor, environmentMap. Do not renumber, remove, or reorder them.
5. **Supported image formats:** PNG, JPEG, WebP, BMP only. SkiaSharp does NOT support TIFF, EXR, HDR, or TGA. Do not claim TIFF support in the UI file filter.
6. **Normal map green channel:** Default to OpenGL convention (Y-up). Most texture sources (Poly Haven, ambientCG) use this convention. Consider adding a "Flip Y" toggle later but don't block Phase 2 on it.

## What NOT to Do

- Do not add tiling/offset controls yet (simplify to 1:1 mapping for initial implementation — add tiling in a follow-up)
- Do not add per-part texture assignment (Top/Bevel/Side — that's Phase 4 territory)
- Do not touch the paint mask resolution (Phase 3)
- Do not add texture preview thumbnails in the first pass (optional polish)
- Do not add drag-and-drop texture assignment
- Do not change the export pipeline format (spritesheets stay as-is)
- Do not add new NuGet packages

## After Phase 2 Is Complete

Update `docs/material-tool-program/00-PROGRAM.md` — change Phase 2 status from "Not started" to "Complete". Then read `docs/material-tool-program/03-PHASE-3-PAINT-UPGRADES.md` for the next phase.
