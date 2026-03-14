using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KnobForge.Rendering.GPU;

public static class MeshDiskCache
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MZCM");
    private const int Version = 1;
    private const string CacheFileExtension = ".mzcm";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Caches", "Monozukuri", "MeshCache");

    public static bool TryLoad(string shapeKeyHash, out MetalVertex[] vertices, out uint[] indices, out float referenceRadius)
    {
        vertices = Array.Empty<MetalVertex>();
        indices = Array.Empty<uint>();
        referenceRadius = 0f;

        string cachePath = GetCachePath(shapeKeyHash);
        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(cachePath);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            {
                return false;
            }

            int version = reader.ReadInt32();
            if (version != Version)
            {
                return false;
            }

            int vertexCount = reader.ReadInt32();
            int indexCount = reader.ReadInt32();
            referenceRadius = reader.ReadSingle();
            if (vertexCount < 0 || indexCount < 0)
            {
                return false;
            }

            vertices = new MetalVertex[vertexCount];
            indices = new uint[indexCount];
            reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(vertices.AsSpan()));
            reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(indices.AsSpan()));
            return true;
        }
        catch
        {
            vertices = Array.Empty<MetalVertex>();
            indices = Array.Empty<uint>();
            referenceRadius = 0f;
            return false;
        }
    }

    public static void Save(string shapeKeyHash, MetalVertex[] vertices, uint[] indices, float referenceRadius)
    {
        if (vertices.Length == 0 || indices.Length == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(CacheDirectory);
            string cachePath = GetCachePath(shapeKeyHash);
            string tempPath = cachePath + ".tmp";

            using (FileStream stream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(vertices.Length);
                writer.Write(indices.Length);
                writer.Write(referenceRadius);
                writer.Write(MemoryMarshal.AsBytes(vertices.AsSpan()));
                writer.Write(MemoryMarshal.AsBytes(indices.AsSpan()));
            }

            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch
        {
            // Cache failures are non-fatal.
        }
    }

    public static string ComputeHash<T>(T shapeKey) where T : struct
    {
        string serialized = shapeKey.ToString() ?? typeof(T).FullName ?? typeof(T).Name;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(hash);
    }

    public static void EvictStale(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                return;
            }

            DateTime cutoffUtc = DateTime.UtcNow - maxAge;
            foreach (string cachePath in Directory.EnumerateFiles(CacheDirectory, "*" + CacheFileExtension))
            {
                try
                {
                    DateTime lastWriteUtc = File.GetLastWriteTimeUtc(cachePath);
                    if (lastWriteUtc < cutoffUtc)
                    {
                        File.Delete(cachePath);
                    }
                }
                catch
                {
                    // Ignore per-file eviction failures.
                }
            }
        }
        catch
        {
            // Ignore cache eviction failures.
        }
    }

    private static string GetCachePath(string shapeKeyHash)
    {
        string safeHash = string.IsNullOrWhiteSpace(shapeKeyHash) ? "UNKNOWN" : shapeKeyHash.Trim();
        return Path.Combine(CacheDirectory, safeHash + CacheFileExtension);
    }
}
