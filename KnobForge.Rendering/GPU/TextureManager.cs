using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace KnobForge.Rendering.GPU;

public enum TextureMapType
{
    Albedo,
    Normal,
    Roughness,
    Metallic
}

public sealed class TextureManager : IDisposable
{
    private const nuint Rgba8UnormPixelFormat = 70; // MTLPixelFormatRGBA8Unorm
    private const nuint TextureUsageShaderRead = 1; // MTLTextureUsageShaderRead
    private const int MaxTextureDimension = 4096;

    private readonly IntPtr _metalDevice;
    private readonly Dictionary<string, CachedTexture> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public IntPtr FallbackAlbedo { get; }
    public IntPtr FallbackNormal { get; }
    public IntPtr FallbackRoughness { get; }
    public IntPtr FallbackMetallic { get; }

    public TextureManager(IntPtr metalDevice)
    {
        _metalDevice = metalDevice;
        FallbackAlbedo = CreateTextureFromRgba8(new byte[] { 255, 255, 255, 255 }, 1, 1);
        FallbackNormal = CreateTextureFromRgba8(new byte[] { 128, 128, 255, 255 }, 1, 1);
        FallbackRoughness = CreateTextureFromRgba8(new byte[] { 128, 128, 128, 255 }, 1, 1);
        FallbackMetallic = CreateTextureFromRgba8(new byte[] { 255, 255, 255, 255 }, 1, 1);
    }

    public IntPtr GetOrLoadTexture(string? filePath, TextureMapType mapType)
    {
        ThrowIfDisposed();

        if (_metalDevice == IntPtr.Zero)
        {
            return GetFallback(mapType);
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return GetFallback(mapType);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return GetFallback(mapType);
        }

        if (!File.Exists(fullPath) || !IsSupportedTexturePath(fullPath))
        {
            return GetFallback(mapType);
        }

        long writeTicks;
        try
        {
            writeTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
        }
        catch
        {
            return GetFallback(mapType);
        }

        if (_cache.TryGetValue(fullPath, out CachedTexture cached))
        {
            if (cached.WriteTicks == writeTicks && cached.Texture != IntPtr.Zero)
            {
                return cached.Texture;
            }

            ReleaseTexture(cached.Texture);
            _cache.Remove(fullPath);
        }

        IntPtr texture = LoadTexture(fullPath);
        if (texture == IntPtr.Zero)
        {
            return GetFallback(mapType);
        }

        _cache[fullPath] = new CachedTexture(texture, writeTicks);
        return texture;
    }

    public void InvalidatePath(string? filePath)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return;
        }

        if (_cache.TryGetValue(fullPath, out CachedTexture cached))
        {
            ReleaseTexture(cached.Texture);
            _cache.Remove(fullPath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (CachedTexture cached in _cache.Values.ToArray())
        {
            ReleaseTexture(cached.Texture);
        }

        _cache.Clear();
        ReleaseTexture(FallbackAlbedo);
        ReleaseTexture(FallbackNormal);
        ReleaseTexture(FallbackRoughness);
        ReleaseTexture(FallbackMetallic);
        GC.SuppressFinalize(this);
    }

    private static bool IsSupportedTexturePath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private IntPtr GetFallback(TextureMapType mapType)
    {
        return mapType switch
        {
            TextureMapType.Albedo => FallbackAlbedo,
            TextureMapType.Normal => FallbackNormal,
            TextureMapType.Roughness => FallbackRoughness,
            TextureMapType.Metallic => FallbackMetallic,
            _ => FallbackAlbedo
        };
    }

    private IntPtr LoadTexture(string fullPath)
    {
        byte[]? pixelBytes = LoadImageToRgba8(fullPath, out int width, out int height);
        if (pixelBytes is null || width <= 0 || height <= 0)
        {
            return IntPtr.Zero;
        }

        return CreateTextureFromRgba8(pixelBytes, width, height);
    }

    private byte[]? LoadImageToRgba8(string filePath, out int width, out int height)
    {
        width = 0;
        height = 0;

        using SKBitmap? bitmap = SKBitmap.Decode(filePath);
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

        width = activeBitmap.Width;
        height = activeBitmap.Height;
        byte[] bytes = activeBitmap.GetPixelSpan().ToArray();
        resized?.Dispose();
        return bytes;
    }

    private IntPtr CreateTextureFromRgba8(byte[] pixelBytes, int width, int height)
    {
        if (_metalDevice == IntPtr.Zero || pixelBytes.Length == 0 || width <= 0 || height <= 0)
        {
            return IntPtr.Zero;
        }

        IntPtr descriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
            ObjCClasses.MTLTextureDescriptor,
            Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
            Rgba8UnormPixelFormat,
            (nuint)width,
            (nuint)height,
            true);
        if (descriptor == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        ObjC.Void_objc_msgSend_UInt(descriptor, Selectors.SetUsage, TextureUsageShaderRead);
        IntPtr texture = ObjC.IntPtr_objc_msgSend_IntPtr(_metalDevice, Selectors.NewTextureWithDescriptor, descriptor);
        if (texture == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        GCHandle pinned = GCHandle.Alloc(pixelBytes, GCHandleType.Pinned);
        try
        {
            MTLRegion region = new(
                new MTLOrigin(0, 0, 0),
                new MTLSize((nuint)width, (nuint)height, 1));
            ObjC.Void_objc_msgSend_MTLRegion_UInt_IntPtr_UInt(
                texture,
                Selectors.ReplaceRegionMipmapLevelWithBytesBytesPerRow,
                region,
                0,
                pinned.AddrOfPinnedObject(),
                (nuint)(width * 4));
        }
        finally
        {
            pinned.Free();
        }

        IntPtr commandBuffer = MetalRendererContext.Instance.CreateCommandBuffer().Handle;
        if (commandBuffer != IntPtr.Zero)
        {
            IntPtr blitEncoder = ObjC.IntPtr_objc_msgSend(commandBuffer, Selectors.BlitCommandEncoder);
            if (blitEncoder != IntPtr.Zero)
            {
                try
                {
                    ObjC.Void_objc_msgSend_IntPtr(blitEncoder, Selectors.GenerateMipmapsForTexture, texture);
                }
                finally
                {
                    ObjC.Void_objc_msgSend(blitEncoder, Selectors.EndEncoding);
                }
            }

            ObjC.Void_objc_msgSend(commandBuffer, Selectors.Commit);
            ObjC.Void_objc_msgSend(commandBuffer, Selectors.WaitUntilCompleted);
        }

        return texture;
    }

    private static void ReleaseTexture(IntPtr texture)
    {
        if (texture != IntPtr.Zero)
        {
            ObjC.Void_objc_msgSend(texture, Selectors.Release);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TextureManager));
        }
    }

    private readonly record struct CachedTexture(IntPtr Texture, long WriteTicks);

    private static class ObjCClasses
    {
        public static readonly IntPtr MTLTextureDescriptor = ObjC.objc_getClass("MTLTextureDescriptor");
    }

    private static class Selectors
    {
        public static readonly IntPtr Texture2DDescriptorWithPixelFormatWidthHeightMipmapped =
            ObjC.sel_registerName("texture2DDescriptorWithPixelFormat:width:height:mipmapped:");
        public static readonly IntPtr SetUsage = ObjC.sel_registerName("setUsage:");
        public static readonly IntPtr NewTextureWithDescriptor = ObjC.sel_registerName("newTextureWithDescriptor:");
        public static readonly IntPtr ReplaceRegionMipmapLevelWithBytesBytesPerRow =
            ObjC.sel_registerName("replaceRegion:mipmapLevel:withBytes:bytesPerRow:");
        public static readonly IntPtr BlitCommandEncoder = ObjC.sel_registerName("blitCommandEncoder");
        public static readonly IntPtr GenerateMipmapsForTexture = ObjC.sel_registerName("generateMipmapsForTexture:");
        public static readonly IntPtr EndEncoding = ObjC.sel_registerName("endEncoding");
        public static readonly IntPtr Commit = ObjC.sel_registerName("commit");
        public static readonly IntPtr WaitUntilCompleted = ObjC.sel_registerName("waitUntilCompleted");
        public static readonly IntPtr Release = ObjC.sel_registerName("release");
    }

    private static class ObjC
    {
        [DllImport("/usr/lib/libobjc.A.dylib")]
        public static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.A.dylib")]
        public static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
            IntPtr receiver,
            IntPtr selector,
            nuint arg1,
            nuint arg2,
            nuint arg3,
            bool arg4);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void Void_objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void Void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void Void_objc_msgSend_UInt(IntPtr receiver, IntPtr selector, nuint arg1);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void Void_objc_msgSend_MTLRegion_UInt_IntPtr_UInt(
            IntPtr receiver,
            IntPtr selector,
            MTLRegion arg1,
            nuint arg2,
            IntPtr arg3,
            nuint arg4);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MTLOrigin
    {
        public readonly nuint X;
        public readonly nuint Y;
        public readonly nuint Z;

        public MTLOrigin(nuint x, nuint y, nuint z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MTLSize
    {
        public readonly nuint Width;
        public readonly nuint Height;
        public readonly nuint Depth;

        public MTLSize(nuint width, nuint height, nuint depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MTLRegion
    {
        public readonly MTLOrigin Origin;
        public readonly MTLSize Size;

        public MTLRegion(MTLOrigin origin, MTLSize size)
        {
            Origin = origin;
            Size = size;
        }
    }
}
