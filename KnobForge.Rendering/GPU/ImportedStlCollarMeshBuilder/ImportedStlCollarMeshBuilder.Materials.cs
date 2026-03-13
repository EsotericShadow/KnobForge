using KnobForge.Core.Scene;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace KnobForge.Rendering.GPU;

public static partial class ImportedStlCollarMeshBuilder
{
    public static bool TryBuildMaterialNodesFromPath(string path, out MaterialNode[] materials)
    {
        materials = Array.Empty<MaterialNode>();
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path) ||
            !TryReadImportedMesh(path, out ImportedMeshData meshData) ||
            meshData.Materials is not { Count: > 0 } materialDefs)
        {
            return false;
        }

        string tempDirectory = GetEmbeddedTextureCacheDirectory(path);
        Directory.CreateDirectory(tempDirectory);

        materials = materialDefs
            .Select((materialDef, materialIndex) => BuildMaterialNode(materialDef, materialIndex, meshData, tempDirectory))
            .ToArray();
        return materials.Length > 0;
    }

    private static MaterialNode BuildMaterialNode(
        GlbMaterialDef materialDef,
        int materialIndex,
        ImportedMeshData meshData,
        string tempDirectory)
    {
        var materialNode = new MaterialNode(string.IsNullOrWhiteSpace(materialDef.Name) ? $"Material {materialIndex}" : materialDef.Name)
        {
            BaseColor = materialDef.BaseColor,
            Metallic = Math.Clamp(materialDef.Metallic, 0f, 1f),
            Roughness = Math.Clamp(materialDef.Roughness, 0.04f, 1f),
            PartMaterialsEnabled = false,
            AlbedoMapPath = TryCreateTextureFilePath(meshData, materialDef.BaseColorTextureIndex, tempDirectory, $"material_{materialIndex}_albedo"),
            NormalMapPath = TryCreateTextureFilePath(meshData, materialDef.NormalTextureIndex, tempDirectory, $"material_{materialIndex}_normal")
        };

        if (TryCreateMetallicRoughnessTextureFilePaths(
                meshData,
                materialDef.MetallicRoughnessTextureIndex,
                tempDirectory,
                $"material_{materialIndex}_metalrough",
                out string? roughnessMapPath,
                out string? metallicMapPath))
        {
            materialNode.RoughnessMapPath = roughnessMapPath;
            materialNode.MetallicMapPath = metallicMapPath;
        }

        return materialNode;
    }

    private static string GetEmbeddedTextureCacheDirectory(string glbPath)
    {
        long fileTicks;
        try
        {
            fileTicks = File.GetLastWriteTimeUtc(glbPath).Ticks;
        }
        catch
        {
            fileTicks = 0;
        }

        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{Path.GetFullPath(glbPath)}|{fileTicks}"));
        string hash = Convert.ToHexString(hashBytes).ToLowerInvariant()[..16];
        return Path.Combine(Path.GetTempPath(), "KnobForge", "embedded_textures", hash);
    }

    private static string? TryCreateTextureFilePath(
        ImportedMeshData meshData,
        int? textureIndex,
        string tempDirectory,
        string fileStem)
    {
        if (!TryGetEmbeddedImageBytes(meshData, textureIndex, out byte[] imageBytes, out _))
        {
            return null;
        }

        string outputPath = Path.Combine(tempDirectory, fileStem + ".png");
        return TryWriteBitmapPng(imageBytes, outputPath)
            ? outputPath
            : null;
    }

    private static bool TryCreateMetallicRoughnessTextureFilePaths(
        ImportedMeshData meshData,
        int? textureIndex,
        string tempDirectory,
        string fileStem,
        out string? roughnessPath,
        out string? metallicPath)
    {
        roughnessPath = null;
        metallicPath = null;
        if (!TryGetEmbeddedImageBytes(meshData, textureIndex, out byte[] imageBytes, out _) ||
            !TryDecodeBitmap(imageBytes, out SKBitmap bitmap))
        {
            return false;
        }

        using (bitmap)
        {
            using var roughnessBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var metallicBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor source = bitmap.GetPixel(x, y);
                    roughnessBitmap.SetPixel(x, y, new SKColor(source.Green, source.Green, source.Green, byte.MaxValue));
                    metallicBitmap.SetPixel(x, y, new SKColor(source.Blue, source.Blue, source.Blue, byte.MaxValue));
                }
            }

            string roughnessOutputPath = Path.Combine(tempDirectory, fileStem + "_roughness.png");
            string metallicOutputPath = Path.Combine(tempDirectory, fileStem + "_metallic.png");
            if (!TryWriteBitmapPng(roughnessBitmap, roughnessOutputPath) ||
                !TryWriteBitmapPng(metallicBitmap, metallicOutputPath))
            {
                return false;
            }

            roughnessPath = roughnessOutputPath;
            metallicPath = metallicOutputPath;
            return true;
        }
    }

    private static bool TryGetEmbeddedImageBytes(
        ImportedMeshData meshData,
        int? textureIndex,
        out byte[] imageBytes,
        out int imageIndex)
    {
        imageBytes = Array.Empty<byte>();
        imageIndex = -1;
        if (textureIndex is not int resolvedTextureIndex ||
            resolvedTextureIndex < 0 ||
            meshData.TextureImageIndices is not { Count: > 0 } textureImageIndices ||
            meshData.EmbeddedImages is not { Count: > 0 } embeddedImages ||
            resolvedTextureIndex >= textureImageIndices.Count)
        {
            return false;
        }

        imageIndex = textureImageIndices[resolvedTextureIndex];
        if (imageIndex < 0 || imageIndex >= embeddedImages.Count)
        {
            imageIndex = -1;
            return false;
        }

        imageBytes = embeddedImages[imageIndex];
        return imageBytes.Length > 0;
    }

    private static bool TryWriteBitmapPng(byte[] imageBytes, string outputPath)
    {
        if (!TryDecodeBitmap(imageBytes, out SKBitmap bitmap))
        {
            return false;
        }

        using (bitmap)
        {
            return TryWriteBitmapPng(bitmap, outputPath);
        }
    }

    private static bool TryWriteBitmapPng(SKBitmap bitmap, string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                return true;
            }

            using var converted = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            if (!bitmap.CopyTo(converted))
            {
                return false;
            }

            using var image = SKImage.FromBitmap(converted);
            using SKData? encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null)
            {
                return false;
            }

            File.WriteAllBytes(outputPath, encoded.ToArray());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeBitmap(byte[] imageBytes, out SKBitmap bitmap)
    {
        bitmap = null!;
        try
        {
            SKBitmap? decoded = SKBitmap.Decode(imageBytes);
            if (decoded == null)
            {
                return false;
            }

            bitmap = decoded;
            return true;
        }
        catch
        {
            bitmap?.Dispose();
            bitmap = null!;
            return false;
        }
    }
}
