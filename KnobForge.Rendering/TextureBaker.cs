using KnobForge.Core;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using SkiaSharp;
using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading;

namespace KnobForge.Rendering;

public sealed class TextureBaker
{
    private const float MinRoughness = 0.04f;
    private const int MaxTextureDimension = 4096;

    public BakeResult Bake(
        KnobProject project,
        MaterialNode material,
        TextureBakeSettings settings,
        IProgress<float>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(settings);

        var result = new BakeResult
        {
            Resolution = settings.Resolution
        };

        try
        {
            ValidateSettings(settings);
            ct.ThrowIfCancellationRequested();

            Directory.CreateDirectory(settings.OutputFolder);

            BakeSnapshot snapshot = CaptureSnapshot(project, material);
            int resolution = settings.Resolution;
            int pixelCount = checked(resolution * resolution);

            byte[]? albedoBuffer = settings.BakeAlbedo ? new byte[checked(pixelCount * 4)] : null;
            byte[]? roughnessBuffer = settings.BakeRoughness ? new byte[checked(pixelCount * 4)] : null;
            byte[]? metallicBuffer = settings.BakeMetallic ? new byte[checked(pixelCount * 4)] : null;
            byte[]? normalBuffer = settings.BakeNormal ? new byte[checked(pixelCount * 4)] : null;

            for (int y = 0; y < resolution; y++)
            {
                if ((y & 7) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report((float)y / resolution);
                }

                float v = (y + 0.5f) / resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;
                    Vector4 paintSample = SampleBilinear(snapshot.PaintMask, snapshot.PaintMaskSize, u, v);
                    Vector4 colorPaintSample = SampleBilinear(snapshot.PaintColor, snapshot.PaintMaskSize, u, v);
                    Vector4 paintMask2Sample = SampleBilinear(snapshot.PaintMask2, snapshot.PaintMaskSize, u, v);

                    Vector3? textureAlbedo = snapshot.AlbedoTexture is null
                        ? null
                        : SampleRgb(snapshot.AlbedoTexture, u, v);
                    float? textureRoughness = snapshot.RoughnessTexture is null
                        ? null
                        : Clamp(SampleRgb(snapshot.RoughnessTexture, u, v).X, MinRoughness, 1f);
                    float? textureMetallic = snapshot.MetallicTexture is null
                        ? null
                        : Clamp(SampleRgb(snapshot.MetallicTexture, u, v).X, 0f, 1f);

                    EvaluateMaterialAtTexel(
                        u,
                        v,
                        snapshot.BaseColor,
                        snapshot.Roughness,
                        snapshot.Metallic,
                        paintSample,
                        colorPaintSample,
                        paintMask2Sample,
                        textureAlbedo,
                        textureRoughness,
                        textureMetallic,
                        snapshot.RustAmount,
                        snapshot.WearAmount,
                        snapshot.GunkAmount,
                        snapshot.BrushDarkness,
                        snapshot.PaintCoatRoughness,
                        snapshot.PaintCoatMetallic,
                        snapshot.ScratchExposeColor,
                        snapshot.ScratchExposeRoughness,
                        snapshot.ScratchExposeMetallic,
                        out Vector3 finalAlbedo,
                        out float finalRoughness,
                        out float finalMetallic);

                    int dst = ((y * resolution) + x) * 4;
                    if (albedoBuffer is not null)
                    {
                        WriteRgb(albedoBuffer, dst, finalAlbedo);
                    }

                    if (roughnessBuffer is not null)
                    {
                        WriteGrayscale(roughnessBuffer, dst, finalRoughness);
                    }

                    if (metallicBuffer is not null)
                    {
                        WriteGrayscale(metallicBuffer, dst, finalMetallic);
                    }

                    if (normalBuffer is not null)
                    {
                        Vector3 bakedNormal = ComposeNormalAtTexel(snapshot, u, v, resolution);
                        Vector3 encodedNormal = new(
                            Clamp((bakedNormal.X * 0.5f) + 0.5f, 0f, 1f),
                            Clamp((bakedNormal.Y * 0.5f) + 0.5f, 0f, 1f),
                            Clamp((bakedNormal.Z * 0.5f) + 0.5f, 0f, 1f));
                        WriteRgb(normalBuffer, dst, encodedNormal);
                    }
                }
            }

            progress?.Report(1f);

            string sanitizedBaseName = SanitizeFileNamePart(settings.BaseName, "bake");
            if (albedoBuffer is not null)
            {
                result.AlbedoPath = WritePng(
                    Path.Combine(settings.OutputFolder, $"{sanitizedBaseName}_albedo.png"),
                    albedoBuffer,
                    resolution);
            }

            if (normalBuffer is not null)
            {
                result.NormalPath = WritePng(
                    Path.Combine(settings.OutputFolder, $"{sanitizedBaseName}_normal.png"),
                    normalBuffer,
                    resolution);
            }

            if (roughnessBuffer is not null)
            {
                result.RoughnessPath = WritePng(
                    Path.Combine(settings.OutputFolder, $"{sanitizedBaseName}_roughness.png"),
                    roughnessBuffer,
                    resolution);
            }

            if (metallicBuffer is not null)
            {
                result.MetallicPath = WritePng(
                    Path.Combine(settings.OutputFolder, $"{sanitizedBaseName}_metallic.png"),
                    metallicBuffer,
                    resolution);
            }

            result.MetadataPath = WriteMetadata(settings, result);
            result.Success = true;
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    private static void ValidateSettings(TextureBakeSettings settings)
    {
        if (settings.Resolution is not (256 or 512 or 1024 or 2048 or 4096))
        {
            throw new ArgumentOutOfRangeException(nameof(settings.Resolution), "Bake resolution must be 256, 512, 1024, 2048, or 4096.");
        }

        if (!settings.BakeAlbedo && !settings.BakeNormal && !settings.BakeRoughness && !settings.BakeMetallic)
        {
            throw new InvalidOperationException("Select at least one texture map to bake.");
        }

        if (string.IsNullOrWhiteSpace(settings.OutputFolder))
        {
            throw new InvalidOperationException("Texture bake output folder is required.");
        }
    }

    private static BakeSnapshot CaptureSnapshot(KnobProject project, MaterialNode material)
    {
        return new BakeSnapshot(
            project.PaintMaskSize,
            (byte[])project.GetPaintMaskRgba8().Clone(),
            (byte[])project.GetPaintColorRgba8().Clone(),
            (byte[])project.GetPaintMask2Rgba8().Clone(),
            Clamp01(material.BaseColor),
            Clamp(material.Roughness, MinRoughness, 1f),
            Clamp(material.Metallic, 0f, 1f),
            Clamp(material.RustAmount, 0f, 1f),
            Clamp(material.WearAmount, 0f, 1f),
            Clamp(material.GunkAmount, 0f, 1f),
            Clamp(project.BrushDarkness, 0f, 1f),
            Clamp(project.PaintCoatRoughness, MinRoughness, 1f),
            Clamp(project.PaintCoatMetallic, 0f, 1f),
            Clamp01(project.ScratchExposeColor),
            Clamp(project.ScratchExposeRoughness, MinRoughness, 1f),
            Clamp(project.ScratchExposeMetallic, 0f, 1f),
            Clamp(material.NormalMapStrength, 0f, 2f),
            LoadTexture(material.AlbedoMapPath),
            LoadTexture(material.NormalMapPath),
            LoadTexture(material.RoughnessMapPath),
            LoadTexture(material.MetallicMapPath));
    }

    private static void EvaluateMaterialAtTexel(
        float u,
        float v,
        Vector3 baseMaterialColor,
        float baseMaterialRoughness,
        float baseMaterialMetallic,
        Vector4 paintSample,
        Vector4 colorPaintSample,
        Vector4 paintMask2Sample,
        Vector3? textureAlbedo,
        float? textureRoughness,
        float? textureMetallic,
        float rustAmount,
        float wearAmount,
        float gunkAmount,
        float brushDarkness,
        float paintCoatRoughness,
        float paintCoatMetallic,
        Vector3 scratchExposeColor,
        float scratchExposeRoughness,
        float scratchExposeMetallic,
        out Vector3 finalAlbedo,
        out float finalRoughness,
        out float finalMetallic)
    {
        Vector3 baseColor = textureAlbedo is { } sampledAlbedo ? Clamp01(sampledAlbedo) : Clamp01(baseMaterialColor);
        float roughness = textureRoughness is { } sampledRoughness
            ? Clamp(sampledRoughness, MinRoughness, 1f)
            : Clamp(baseMaterialRoughness, MinRoughness, 1f);
        float metallic = textureMetallic is { } sampledMetallic
            ? Clamp(sampledMetallic, 0f, 1f)
            : Clamp(baseMaterialMetallic, 0f, 1f);

        float colorPaintMask = Clamp(colorPaintSample.W, 0f, 1f);
        Vector3 colorPaintBase = Vector3.Zero;
        if (colorPaintMask > 1e-5f)
        {
            colorPaintBase = Clamp01(new Vector3(
                colorPaintSample.X / colorPaintMask,
                colorPaintSample.Y / colorPaintMask,
                colorPaintSample.Z / colorPaintMask));
        }

        baseColor = Vector3.Lerp(baseColor, colorPaintBase, colorPaintMask);
        float paintCoatBlend = SmoothStep(0f, 0.85f, colorPaintMask);
        roughness = Lerp(roughness, paintCoatRoughness, paintCoatBlend);
        metallic = Lerp(metallic, paintCoatMetallic, paintCoatBlend);

        float darknessGain = Lerp(0.45f, 1.45f, brushDarkness);
        float rustRaw = Clamp(paintSample.X, 0f, 1f);
        float wearRaw = Clamp(paintSample.Y, 0f, 1f);
        float gunkRaw = Clamp(paintSample.Z, 0f, 1f);
        float scratchRaw = Clamp(paintSample.W, 0f, 1f);

        float rustNoiseA = ValueNoise2((u * 192f) + 11.3f, (v * 217f) + 6.7f);
        float rustNoiseB = ValueNoise2((u * 67f) + 41.1f, (v * 59f) + 13.5f);
        float rustSplotch = SmoothStep(0.32f, 0.90f, (rustNoiseA * 0.72f) + (rustNoiseB * 0.58f));

        float rustStrength = Lerp(0.30f, 1.00f, rustAmount);
        float wearStrength = Lerp(0.15f, 0.70f, wearAmount);
        float gunkStrength = Lerp(0.35f, 1.20f, gunkAmount);
        float scratchStrength = Lerp(0.30f, 1.00f, wearAmount);

        float rustMask = Clamp(rustRaw * rustSplotch * darknessGain * rustStrength, 0f, 1f);
        float wearMask = Clamp(wearRaw * Lerp(0.30f, 0.80f, brushDarkness) * wearStrength, 0f, 1f);
        float gunkMask = Clamp(gunkRaw * Lerp(0.55f, 1.65f, brushDarkness) * gunkStrength, 0f, 1f);
        float scratchMask = Clamp(scratchRaw * Lerp(0.45f, 1.00f, brushDarkness) * scratchStrength, 0f, 1f);

        float rustHue = ValueNoise2((u * 103f) + 3.1f, (v * 97f) + 17.2f);
        Vector3 rustDark = new(0.23f, 0.08f, 0.04f);
        Vector3 rustMid = new(0.46f, 0.17f, 0.07f);
        Vector3 rustOrange = new(0.71f, 0.29f, 0.09f);
        Vector3 rustColor = Vector3.Lerp(
            Vector3.Lerp(rustDark, rustMid, Clamp(rustHue * 1.25f, 0f, 1f)),
            rustOrange,
            Clamp((rustHue - 0.35f) / 0.65f, 0f, 1f));
        Vector3 gunkColor = new(0.02f, 0.02f, 0.018f);
        Vector3 wearColor = Vector3.Lerp(baseColor, new Vector3(0.80f, 0.79f, 0.76f), 0.45f);

        baseColor = Vector3.Lerp(baseColor, rustColor, Clamp(rustMask * 0.88f, 0f, 1f));
        baseColor = Vector3.Lerp(baseColor, gunkColor, Clamp(gunkMask * 0.96f, 0f, 1f));
        baseColor = Vector3.Lerp(baseColor, wearColor, Clamp(wearMask * 0.24f, 0f, 1f));
        float grimeDarken = Clamp((rustMask * 0.18f + gunkMask * 0.55f) * (0.25f + (0.75f * brushDarkness)), 0f, 0.85f);
        baseColor *= 1f - grimeDarken;
        float scratchReveal = Clamp(scratchMask, 0f, 1f);
        baseColor = Vector3.Lerp(baseColor, scratchExposeColor, scratchReveal);

        roughness = Clamp(roughness + (rustMask * 0.34f) + (gunkMask * 0.62f) - (wearMask * 0.05f), MinRoughness, 1f);
        metallic = Clamp(metallic - (rustMask * 0.62f) - (gunkMask * 0.30f), 0f, 1f);
        roughness = Lerp(roughness, scratchExposeRoughness, scratchReveal);
        metallic = Lerp(metallic, scratchExposeMetallic, scratchReveal);

        float roughnessTarget = Clamp(paintMask2Sample.X, 0f, 1f);
        float metallicTarget = Clamp(paintMask2Sample.Y, 0f, 1f);
        float roughnessAlpha = Clamp(paintMask2Sample.Z, 0f, 1f);
        float metallicAlpha = Clamp(paintMask2Sample.W, 0f, 1f);
        roughness = Lerp(roughness, roughnessTarget, roughnessAlpha);
        metallic = Lerp(metallic, metallicTarget, metallicAlpha);

        finalAlbedo = Clamp01(baseColor);
        finalRoughness = Clamp(roughness, MinRoughness, 1f);
        finalMetallic = Clamp(metallic, 0f, 1f);
    }

    private static Vector3 ComposeNormalAtTexel(BakeSnapshot snapshot, float u, float v, int resolution)
    {
        Vector3 tangentNormal = snapshot.NormalTexture is null
            ? new Vector3(0f, 0f, 1f)
            : DecodeNormalSample(SampleRgb(snapshot.NormalTexture, u, v), snapshot.NormalMapStrength);

        float texel = 1f / Math.Max(1, resolution);
        float scratchCenter = ComputeScratchMask(snapshot, u, v);
        if (scratchCenter <= 1e-5f)
        {
            return NormalizeSafe(tangentNormal, new Vector3(0f, 0f, 1f));
        }

        float scratchLeft = ComputeScratchMask(snapshot, u - texel, v);
        float scratchRight = ComputeScratchMask(snapshot, u + texel, v);
        float scratchUp = ComputeScratchMask(snapshot, u, v - texel);
        float scratchDown = ComputeScratchMask(snapshot, u, v + texel);

        float dx = scratchRight - scratchLeft;
        float dy = scratchDown - scratchUp;
        float bumpScale = 0.45f * scratchCenter;

        Vector3 bumped = new(
            tangentNormal.X + (dx * bumpScale),
            tangentNormal.Y - (dy * bumpScale),
            MathF.Max(0.01f, tangentNormal.Z));
        return NormalizeSafe(bumped, new Vector3(0f, 0f, 1f));
    }

    private static float ComputeScratchMask(BakeSnapshot snapshot, float u, float v)
    {
        Vector4 paintSample = SampleBilinear(snapshot.PaintMask, snapshot.PaintMaskSize, u, v);
        float scratchRaw = Clamp(paintSample.W, 0f, 1f);
        float scratchStrength = Lerp(0.30f, 1.00f, snapshot.WearAmount);
        return Clamp(scratchRaw * Lerp(0.45f, 1.00f, snapshot.BrushDarkness) * scratchStrength, 0f, 1f);
    }

    private static Vector3 DecodeNormalSample(Vector3 encodedNormal, float strength)
    {
        Vector3 tangentNormal = new(
            (encodedNormal.X * 2f) - 1f,
            (encodedNormal.Y * 2f) - 1f,
            (encodedNormal.Z * 2f) - 1f);
        tangentNormal.X *= strength;
        tangentNormal.Y *= strength;
        return NormalizeSafe(tangentNormal, new Vector3(0f, 0f, 1f));
    }

    private static LoadedTexture? LoadTexture(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return null;
        }

        if (!File.Exists(fullPath))
        {
            return null;
        }

        using SKBitmap? bitmap = SKBitmap.Decode(fullPath);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return null;
        }

        using SKBitmap? converted = bitmap.Copy(SKColorType.Rgba8888);
        if (converted is null)
        {
            return null;
        }

        SKBitmap activeBitmap = converted;
        SKBitmap? resized = null;
        if (activeBitmap.Width > MaxTextureDimension || activeBitmap.Height > MaxTextureDimension)
        {
            float scale = Math.Min((float)MaxTextureDimension / activeBitmap.Width, (float)MaxTextureDimension / activeBitmap.Height);
            int newWidth = Math.Max(1, (int)MathF.Round(activeBitmap.Width * scale));
            int newHeight = Math.Max(1, (int)MathF.Round(activeBitmap.Height * scale));
            resized = activeBitmap.Resize(
                new SKImageInfo(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized is null)
            {
                return null;
            }

            activeBitmap = resized;
        }

        try
        {
            return new LoadedTexture(activeBitmap.GetPixelSpan().ToArray(), activeBitmap.Width, activeBitmap.Height);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static Vector4 SampleBilinear(byte[] rgba8, int size, float u, float v)
    {
        if (rgba8.Length == 0 || size <= 0)
        {
            return Vector4.Zero;
        }

        float x = (Clamp(u, 0f, 1f) * size) - 0.5f;
        float y = (Clamp(v, 0f, 1f) * size) - 0.5f;
        int x0 = Clamp((int)MathF.Floor(x), 0, size - 1);
        int y0 = Clamp((int)MathF.Floor(y), 0, size - 1);
        int x1 = Clamp(x0 + 1, 0, size - 1);
        int y1 = Clamp(y0 + 1, 0, size - 1);
        float tx = Clamp(x - MathF.Floor(x), 0f, 1f);
        float ty = Clamp(y - MathF.Floor(y), 0f, 1f);

        Vector4 n00 = ReadPixel(rgba8, size, x0, y0);
        Vector4 n10 = ReadPixel(rgba8, size, x1, y0);
        Vector4 n01 = ReadPixel(rgba8, size, x0, y1);
        Vector4 n11 = ReadPixel(rgba8, size, x1, y1);
        Vector4 nx0 = Vector4.Lerp(n00, n10, tx);
        Vector4 nx1 = Vector4.Lerp(n01, n11, tx);
        return Vector4.Lerp(nx0, nx1, ty);
    }

    private static Vector3 SampleRgb(LoadedTexture texture, float u, float v)
    {
        Vector4 sampled = SampleBilinear(texture.Pixels, texture.Width, texture.Height, u, v);
        return new Vector3(sampled.X, sampled.Y, sampled.Z);
    }

    private static Vector4 SampleBilinear(byte[] rgba8, int width, int height, float u, float v)
    {
        if (rgba8.Length == 0 || width <= 0 || height <= 0)
        {
            return Vector4.Zero;
        }

        float x = (Clamp(u, 0f, 1f) * width) - 0.5f;
        float y = (Clamp(v, 0f, 1f) * height) - 0.5f;
        int x0 = Clamp((int)MathF.Floor(x), 0, width - 1);
        int y0 = Clamp((int)MathF.Floor(y), 0, height - 1);
        int x1 = Clamp(x0 + 1, 0, width - 1);
        int y1 = Clamp(y0 + 1, 0, height - 1);
        float tx = Clamp(x - MathF.Floor(x), 0f, 1f);
        float ty = Clamp(y - MathF.Floor(y), 0f, 1f);

        Vector4 n00 = ReadPixel(rgba8, width, height, x0, y0);
        Vector4 n10 = ReadPixel(rgba8, width, height, x1, y0);
        Vector4 n01 = ReadPixel(rgba8, width, height, x0, y1);
        Vector4 n11 = ReadPixel(rgba8, width, height, x1, y1);
        Vector4 nx0 = Vector4.Lerp(n00, n10, tx);
        Vector4 nx1 = Vector4.Lerp(n01, n11, tx);
        return Vector4.Lerp(nx0, nx1, ty);
    }

    private static Vector4 ReadPixel(byte[] rgba8, int size, int x, int y)
    {
        int idx = ((y * size) + x) * 4;
        return new Vector4(
            rgba8[idx + 0] / 255f,
            rgba8[idx + 1] / 255f,
            rgba8[idx + 2] / 255f,
            rgba8[idx + 3] / 255f);
    }

    private static Vector4 ReadPixel(byte[] rgba8, int width, int height, int x, int y)
    {
        int clampedX = Clamp(x, 0, width - 1);
        int clampedY = Clamp(y, 0, height - 1);
        int idx = ((clampedY * width) + clampedX) * 4;
        return new Vector4(
            rgba8[idx + 0] / 255f,
            rgba8[idx + 1] / 255f,
            rgba8[idx + 2] / 255f,
            rgba8[idx + 3] / 255f);
    }

    private static void WriteRgb(byte[] destination, int offset, Vector3 color)
    {
        destination[offset + 0] = ToByte(color.X);
        destination[offset + 1] = ToByte(color.Y);
        destination[offset + 2] = ToByte(color.Z);
        destination[offset + 3] = 255;
    }

    private static void WriteGrayscale(byte[] destination, int offset, float value)
    {
        byte channel = ToByte(value);
        destination[offset + 0] = channel;
        destination[offset + 1] = channel;
        destination[offset + 2] = channel;
        destination[offset + 3] = 255;
    }

    private static string WritePng(string outputPath, byte[] rgba8, int resolution)
    {
        using var bitmap = new SKBitmap(resolution, resolution, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        rgba8.CopyTo(bitmap.GetPixelSpan());
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
        return outputPath;
    }

    private static string WriteMetadata(TextureBakeSettings settings, BakeResult result)
    {
        string baseName = SanitizeFileNamePart(settings.BaseName, "bake");
        string outputPath = Path.Combine(settings.OutputFolder, $"{baseName}_material.json");
        var metadata = new
        {
            version = 1,
            resolution = settings.Resolution,
            maps = new
            {
                albedo = result.AlbedoPath is null ? null : Path.GetFileName(result.AlbedoPath),
                normal = result.NormalPath is null ? null : Path.GetFileName(result.NormalPath),
                roughness = result.RoughnessPath is null ? null : Path.GetFileName(result.RoughnessPath),
                metallic = result.MetallicPath is null ? null : Path.GetFileName(result.MetallicPath)
            },
            workflow = "metallic-roughness",
            normalSpace = "tangent",
            source = "KnobForge"
        };
        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(outputPath, json);
        return outputPath;
    }

    private static string SanitizeFileNamePart(string? value, string fallback)
    {
        string text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            text = text.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(Clamp(value, 0f, 1f) * 255f), 0, 255);
    }

    private static Vector3 Clamp01(Vector3 value)
    {
        return new Vector3(
            Clamp(value.X, 0f, 1f),
            Clamp(value.Y, 0f, 1f),
            Clamp(value.Z, 0f, 1f));
    }

    private static float Fract(float value)
    {
        return value - MathF.Floor(value);
    }

    private static float Hash21(float x, float y)
    {
        float px = Fract(x * 123.34f);
        float py = Fract(y * 456.21f);
        float d = (px * (px + 45.32f)) + (py * (py + 45.32f));
        px += d;
        py += d;
        return Fract(px * py);
    }

    private static float ValueNoise2(float x, float y)
    {
        float ix = MathF.Floor(x);
        float iy = MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;
        float a = Hash21(ix, iy);
        float b = Hash21(ix + 1f, iy);
        float c = Hash21(ix, iy + 1f);
        float d = Hash21(ix + 1f, iy + 1f);
        float ux = fx * fx * (3f - (2f * fx));
        float uy = fy * fy * (3f - (2f * fy));
        float ab = Lerp(a, b, ux);
        float cd = Lerp(c, d, ux);
        return Lerp(ab, cd, uy);
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        if (MathF.Abs(edge1 - edge0) <= 1e-8f)
        {
            return x < edge0 ? 0f : 1f;
        }

        float t = Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Clamp(value, min, max);
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }

    private static Vector3 NormalizeSafe(Vector3 value, Vector3 fallback)
    {
        float lenSq = value.LengthSquared();
        if (lenSq <= 1e-8f)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lenSq);
    }

    private sealed record LoadedTexture(byte[] Pixels, int Width, int Height);

    private sealed record BakeSnapshot(
        int PaintMaskSize,
        byte[] PaintMask,
        byte[] PaintColor,
        byte[] PaintMask2,
        Vector3 BaseColor,
        float Roughness,
        float Metallic,
        float RustAmount,
        float WearAmount,
        float GunkAmount,
        float BrushDarkness,
        float PaintCoatRoughness,
        float PaintCoatMetallic,
        Vector3 ScratchExposeColor,
        float ScratchExposeRoughness,
        float ScratchExposeMetallic,
        float NormalMapStrength,
        LoadedTexture? AlbedoTexture,
        LoadedTexture? NormalTexture,
        LoadedTexture? RoughnessTexture,
        LoadedTexture? MetallicTexture);
}
