using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using KnobForge.Core;
using KnobForge.Core.Scene;


namespace KnobForge.Rendering.GPU;

public static partial class ImportedStlCollarMeshBuilder
{
    private static bool TryReadBinaryGlb(string path, out ImportedMeshData meshData)
    {
        meshData = new ImportedMeshData();
        var positions = new List<Vector3>();
        var indices = new List<uint>();
        var normals = new List<Vector3>();
        var texcoords = new List<Vector2>();
        var subMeshes = new List<SubMesh>();
        bool haveNormalsForAllPrimitives = true;
        bool haveTexcoordsForAllPrimitives = true;

        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(path);
        }
        catch
        {
            return false;
        }

        if (fileBytes.Length < 20)
        {
            return false;
        }

        using var stream = new MemoryStream(fileBytes, writable: false);
        using var reader = new BinaryReader(stream);

        if (reader.ReadUInt32() != GlbMagic)
        {
            return false;
        }

        if (reader.ReadUInt32() != 2u)
        {
            return false;
        }

        uint declaredLength = reader.ReadUInt32();
        if (declaredLength < 20u || declaredLength > fileBytes.Length)
        {
            return false;
        }

        string? jsonChunkText = null;
        byte[]? binaryChunk = null;
        while ((stream.Position + 8) <= declaredLength)
        {
            uint chunkLength = reader.ReadUInt32();
            uint chunkType = reader.ReadUInt32();
            if (chunkLength > int.MaxValue || (stream.Position + chunkLength) > declaredLength)
            {
                return false;
            }

            byte[] chunkData = reader.ReadBytes((int)chunkLength);
            if (chunkData.Length != (int)chunkLength)
            {
                return false;
            }

            if (chunkType == GlbJsonChunkType)
            {
                jsonChunkText = Encoding.UTF8.GetString(chunkData)
                    .TrimEnd('\0', '\t', '\r', '\n', ' ');
            }
            else if (chunkType == GlbBinChunkType && binaryChunk is null)
            {
                binaryChunk = chunkData;
            }
        }

        if (string.IsNullOrWhiteSpace(jsonChunkText) || binaryChunk is null)
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(jsonChunkText);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("meshes", out JsonElement meshesElement) ||
            meshesElement.ValueKind != JsonValueKind.Array ||
            meshesElement.GetArrayLength() == 0)
        {
            return false;
        }

        if (!root.TryGetProperty("accessors", out JsonElement accessorsElement) ||
            accessorsElement.ValueKind != JsonValueKind.Array ||
            accessorsElement.GetArrayLength() == 0)
        {
            return false;
        }

        if (!root.TryGetProperty("bufferViews", out JsonElement bufferViewsElement) ||
            bufferViewsElement.ValueKind != JsonValueKind.Array ||
            bufferViewsElement.GetArrayLength() == 0)
        {
            return false;
        }

        List<int>? textureImageIndices = ParseTextureImageIndices(root);
        List<GlbMaterialDef>? materialDefs = ParseGlbMaterialDefs(root);
        List<byte[]>? embeddedImages = ExtractEmbeddedImages(root, bufferViewsElement, binaryChunk);

        foreach (JsonElement mesh in meshesElement.EnumerateArray())
        {
            if (!mesh.TryGetProperty("primitives", out JsonElement primitivesElement) ||
                primitivesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement primitive in primitivesElement.EnumerateArray())
            {
                int primitiveMaterialIndex = 0;
                if (primitive.TryGetProperty("material", out JsonElement materialElement) &&
                    materialElement.TryGetInt32(out int parsedMaterialIndex))
                {
                    primitiveMaterialIndex = parsedMaterialIndex;
                }

                int mode = 4; // TRIANGLES
                if (primitive.TryGetProperty("mode", out JsonElement modeElement) &&
                    modeElement.ValueKind == JsonValueKind.Number &&
                    modeElement.TryGetInt32(out int parsedMode))
                {
                    mode = parsedMode;
                }

                if (mode != 4)
                {
                    continue;
                }

                if (!primitive.TryGetProperty("attributes", out JsonElement attributesElement) ||
                    attributesElement.ValueKind != JsonValueKind.Object ||
                    !attributesElement.TryGetProperty("POSITION", out JsonElement positionAccessorElement) ||
                    !positionAccessorElement.TryGetInt32(out int positionAccessorIndex))
                {
                    continue;
                }

                if (!TryReadAccessorVector3(
                        accessorsElement,
                        bufferViewsElement,
                        binaryChunk,
                        positionAccessorIndex,
                        out Vector3[] primitivePositions) ||
                    primitivePositions.Length == 0)
                {
                    continue;
                }

                Vector3[]? primitiveNormals = null;
                if (attributesElement.TryGetProperty("NORMAL", out JsonElement normalAccessorElement) &&
                    normalAccessorElement.TryGetInt32(out int normalAccessorIndex) &&
                    TryReadAccessorVector3(
                        accessorsElement,
                        bufferViewsElement,
                        binaryChunk,
                        normalAccessorIndex,
                        out Vector3[] readNormals) &&
                    readNormals.Length == primitivePositions.Length)
                {
                    primitiveNormals = readNormals;
                }
                else
                {
                    haveNormalsForAllPrimitives = false;
                }

                Vector2[]? primitiveTexcoords = null;
                if (attributesElement.TryGetProperty("TEXCOORD_0", out JsonElement texcoordAccessorElement) &&
                    texcoordAccessorElement.TryGetInt32(out int texcoordAccessorIndex) &&
                    TryReadAccessorVector2(
                        accessorsElement,
                        bufferViewsElement,
                        binaryChunk,
                        texcoordAccessorIndex,
                        out Vector2[] readTexcoords) &&
                    readTexcoords.Length == primitivePositions.Length)
                {
                    primitiveTexcoords = readTexcoords;
                }
                else
                {
                    haveTexcoordsForAllPrimitives = false;
                }

                int baseVertex = positions.Count;
                for (int i = 0; i < primitivePositions.Length; i++)
                {
                    positions.Add(primitivePositions[i]);
                    if (primitiveNormals is not null)
                    {
                        normals.Add(primitiveNormals[i]);
                    }

                    if (primitiveTexcoords is not null)
                    {
                        texcoords.Add(primitiveTexcoords[i]);
                    }
                }

                int primitiveIndexOffset = indices.Count;
                if (primitive.TryGetProperty("indices", out JsonElement indicesAccessorElement) &&
                    indicesAccessorElement.TryGetInt32(out int indicesAccessorIndex) &&
                    TryReadAccessorIndices(
                        accessorsElement,
                        bufferViewsElement,
                        binaryChunk,
                        indicesAccessorIndex,
                        out uint[] primitiveIndices) &&
                    primitiveIndices.Length >= 3)
                {
                    for (int i = 0; i + 2 < primitiveIndices.Length; i += 3)
                    {
                        uint i0 = primitiveIndices[i + 0];
                        uint i1 = primitiveIndices[i + 1];
                        uint i2 = primitiveIndices[i + 2];
                        if (i0 >= primitivePositions.Length ||
                            i1 >= primitivePositions.Length ||
                            i2 >= primitivePositions.Length ||
                            i0 == i1 || i1 == i2 || i2 == i0)
                        {
                            continue;
                        }

                        indices.Add((uint)(baseVertex + (int)i0));
                        indices.Add((uint)(baseVertex + (int)i1));
                        indices.Add((uint)(baseVertex + (int)i2));
                    }
                }
                else
                {
                    for (int i = 0; i + 2 < primitivePositions.Length; i += 3)
                    {
                        indices.Add((uint)(baseVertex + i + 0));
                        indices.Add((uint)(baseVertex + i + 1));
                        indices.Add((uint)(baseVertex + i + 2));
                    }
                }

                int primitiveIndexCount = indices.Count - primitiveIndexOffset;
                if (primitiveIndexCount > 0)
                {
                    subMeshes.Add(new SubMesh
                    {
                        IndexOffset = primitiveIndexOffset,
                        IndexCount = primitiveIndexCount,
                        MaterialIndex = primitiveMaterialIndex
                    });
                }
            }
        }

        if (positions.Count == 0 || indices.Count < 3)
        {
            return false;
        }

        meshData = new ImportedMeshData
        {
            Positions = positions,
            Indices = indices,
            Normals = haveNormalsForAllPrimitives && normals.Count == positions.Count ? normals : null,
            Texcoords = haveTexcoordsForAllPrimitives && texcoords.Count == positions.Count ? texcoords : null,
            SubMeshes = subMeshes.Count > 0 ? subMeshes : null,
            Materials = materialDefs is { Count: > 0 } ? materialDefs : null,
            EmbeddedImages = embeddedImages is { Count: > 0 } ? embeddedImages : null,
            TextureImageIndices = textureImageIndices is { Count: > 0 } ? textureImageIndices : null
        };
        return true;
    }

    private static List<GlbMaterialDef>? ParseGlbMaterialDefs(JsonElement root)
    {
        if (!root.TryGetProperty("materials", out JsonElement materialsElement) ||
            materialsElement.ValueKind != JsonValueKind.Array ||
            materialsElement.GetArrayLength() == 0)
        {
            return null;
        }

        var materialDefs = new List<GlbMaterialDef>(materialsElement.GetArrayLength());
        int materialIndex = 0;
        foreach (JsonElement materialElement in materialsElement.EnumerateArray())
        {
            string name = $"Material {materialIndex}";
            if (materialElement.TryGetProperty("name", out JsonElement nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                string? parsedName = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(parsedName))
                {
                    name = parsedName;
                }
            }

            Vector3 baseColor = Vector3.One;
            float metallic = 1.0f;
            float roughness = 1.0f;
            int? baseColorTextureIndex = null;
            int? metallicRoughnessTextureIndex = null;
            if (materialElement.TryGetProperty("pbrMetallicRoughness", out JsonElement pbrElement) &&
                pbrElement.ValueKind == JsonValueKind.Object)
            {
                if (pbrElement.TryGetProperty("baseColorFactor", out JsonElement baseColorFactorElement) &&
                    baseColorFactorElement.ValueKind == JsonValueKind.Array)
                {
                    baseColor = ReadColorFactorRgb(baseColorFactorElement, Vector3.One);
                }

                if (pbrElement.TryGetProperty("metallicFactor", out JsonElement metallicFactorElement) &&
                    metallicFactorElement.TryGetSingle(out float parsedMetallic))
                {
                    metallic = parsedMetallic;
                }

                if (pbrElement.TryGetProperty("roughnessFactor", out JsonElement roughnessFactorElement) &&
                    roughnessFactorElement.TryGetSingle(out float parsedRoughness))
                {
                    roughness = parsedRoughness;
                }

                if (pbrElement.TryGetProperty("baseColorTexture", out JsonElement baseColorTextureElement) &&
                    baseColorTextureElement.ValueKind == JsonValueKind.Object &&
                    baseColorTextureElement.TryGetProperty("index", out JsonElement baseColorTextureIndexElement) &&
                    baseColorTextureIndexElement.TryGetInt32(out int parsedBaseColorTextureIndex))
                {
                    baseColorTextureIndex = parsedBaseColorTextureIndex;
                }

                if (pbrElement.TryGetProperty("metallicRoughnessTexture", out JsonElement metallicRoughnessTextureElement) &&
                    metallicRoughnessTextureElement.ValueKind == JsonValueKind.Object &&
                    metallicRoughnessTextureElement.TryGetProperty("index", out JsonElement metallicRoughnessTextureIndexElement) &&
                    metallicRoughnessTextureIndexElement.TryGetInt32(out int parsedMetallicRoughnessTextureIndex))
                {
                    metallicRoughnessTextureIndex = parsedMetallicRoughnessTextureIndex;
                }
            }

            int? normalTextureIndex = null;
            if (materialElement.TryGetProperty("normalTexture", out JsonElement normalTextureElement) &&
                normalTextureElement.ValueKind == JsonValueKind.Object &&
                normalTextureElement.TryGetProperty("index", out JsonElement normalTextureIndexElement) &&
                normalTextureIndexElement.TryGetInt32(out int parsedNormalTextureIndex))
            {
                normalTextureIndex = parsedNormalTextureIndex;
            }

            materialDefs.Add(new GlbMaterialDef
            {
                Name = name,
                BaseColor = baseColor,
                Metallic = metallic,
                Roughness = roughness,
                BaseColorTextureIndex = baseColorTextureIndex,
                NormalTextureIndex = normalTextureIndex,
                MetallicRoughnessTextureIndex = metallicRoughnessTextureIndex
            });
            materialIndex++;
        }

        return materialDefs;
    }

    private static List<int>? ParseTextureImageIndices(JsonElement root)
    {
        if (!root.TryGetProperty("textures", out JsonElement texturesElement) ||
            texturesElement.ValueKind != JsonValueKind.Array ||
            texturesElement.GetArrayLength() == 0)
        {
            return null;
        }

        var textureImageIndices = new List<int>(texturesElement.GetArrayLength());
        foreach (JsonElement textureElement in texturesElement.EnumerateArray())
        {
            int sourceImageIndex = -1;
            if (textureElement.ValueKind == JsonValueKind.Object &&
                textureElement.TryGetProperty("source", out JsonElement sourceElement) &&
                sourceElement.TryGetInt32(out int parsedSourceImageIndex))
            {
                sourceImageIndex = parsedSourceImageIndex;
            }

            textureImageIndices.Add(sourceImageIndex);
        }

        return textureImageIndices;
    }

    private static List<byte[]>? ExtractEmbeddedImages(
        JsonElement root,
        JsonElement bufferViewsElement,
        byte[] binaryChunk)
    {
        if (!root.TryGetProperty("images", out JsonElement imagesElement) ||
            imagesElement.ValueKind != JsonValueKind.Array ||
            imagesElement.GetArrayLength() == 0)
        {
            return null;
        }

        var embeddedImages = new List<byte[]>(imagesElement.GetArrayLength());
        foreach (JsonElement imageElement in imagesElement.EnumerateArray())
        {
            byte[] imageBytes = Array.Empty<byte>();
            if (imageElement.ValueKind == JsonValueKind.Object &&
                imageElement.TryGetProperty("bufferView", out JsonElement bufferViewIndexElement) &&
                bufferViewIndexElement.TryGetInt32(out int bufferViewIndex) &&
                TryExtractBufferViewBytes(bufferViewsElement, binaryChunk, bufferViewIndex, out byte[] extractedBytes))
            {
                imageBytes = extractedBytes;
            }

            embeddedImages.Add(imageBytes);
        }

        return embeddedImages;
    }

    private static bool TryExtractBufferViewBytes(
        JsonElement bufferViewsElement,
        byte[] binaryChunk,
        int bufferViewIndex,
        out byte[] extractedBytes)
    {
        extractedBytes = Array.Empty<byte>();
        if (!TryGetArrayElement(bufferViewsElement, bufferViewIndex, out JsonElement bufferViewElement) ||
            bufferViewElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (bufferViewElement.TryGetProperty("buffer", out JsonElement bufferIndexElement) &&
            bufferIndexElement.TryGetInt32(out int bufferIndex) &&
            bufferIndex != 0)
        {
            return false;
        }

        int byteOffset = 0;
        if (bufferViewElement.TryGetProperty("byteOffset", out JsonElement byteOffsetElement) &&
            byteOffsetElement.TryGetInt32(out int parsedByteOffset))
        {
            byteOffset = parsedByteOffset;
        }

        if (!bufferViewElement.TryGetProperty("byteLength", out JsonElement byteLengthElement) ||
            !byteLengthElement.TryGetInt32(out int byteLength) ||
            byteLength <= 0)
        {
            return false;
        }

        if (byteOffset < 0 ||
            byteLength < 0 ||
            (byteOffset + byteLength) > binaryChunk.Length)
        {
            return false;
        }

        extractedBytes = binaryChunk.AsSpan(byteOffset, byteLength).ToArray();
        return true;
    }

    private static Vector3 ReadColorFactorRgb(JsonElement factorArray, Vector3 fallback)
    {
        if (factorArray.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        float[] values = new float[3];
        for (int i = 0; i < values.Length; i++)
        {
            if (!TryGetArrayElement(factorArray, i, out JsonElement valueElement) ||
                !valueElement.TryGetSingle(out values[i]))
            {
                return fallback;
            }
        }

        return new Vector3(values[0], values[1], values[2]);
    }

    private static bool TryReadAccessorVector3(
        JsonElement accessorsElement,
        JsonElement bufferViewsElement,
        byte[] bufferBytes,
        int accessorIndex,
        out Vector3[] vectors)
    {
        vectors = Array.Empty<Vector3>();
        if (!TryResolveAccessorView(accessorsElement, bufferViewsElement, bufferBytes.Length, accessorIndex, out AccessorView view))
        {
            return false;
        }

        if (!string.Equals(view.Type, "VEC3", StringComparison.Ordinal) ||
            view.ComponentType != 5126)
        {
            return false;
        }

        int stride = view.ByteStride > 0 ? view.ByteStride : 12;
        if (stride < 12)
        {
            return false;
        }

        long lastVectorStart = view.DataOffset + ((long)(view.Count - 1) * stride);
        long accessorEnd = view.DataOffset + view.ByteLength;
        if (lastVectorStart < 0 ||
            (lastVectorStart + 12) > bufferBytes.Length ||
            (lastVectorStart + 12) > accessorEnd)
        {
            return false;
        }

        vectors = new Vector3[view.Count];
        for (int i = 0; i < view.Count; i++)
        {
            int offset = view.DataOffset + (i * stride);
            float x = BitConverter.ToSingle(bufferBytes, offset + 0);
            float y = BitConverter.ToSingle(bufferBytes, offset + 4);
            float z = BitConverter.ToSingle(bufferBytes, offset + 8);
            vectors[i] = new Vector3(x, y, z);
        }

        return true;
    }

    private static bool TryReadAccessorVector2(
        JsonElement accessorsElement,
        JsonElement bufferViewsElement,
        byte[] bufferBytes,
        int accessorIndex,
        out Vector2[] vectors)
    {
        vectors = Array.Empty<Vector2>();
        if (!TryResolveAccessorView(accessorsElement, bufferViewsElement, bufferBytes.Length, accessorIndex, out AccessorView view))
        {
            return false;
        }

        if (!string.Equals(view.Type, "VEC2", StringComparison.Ordinal) ||
            view.ComponentType != 5126)
        {
            return false;
        }

        int stride = view.ByteStride > 0 ? view.ByteStride : 8;
        if (stride < 8)
        {
            return false;
        }

        long lastVectorStart = view.DataOffset + ((long)(view.Count - 1) * stride);
        long accessorEnd = view.DataOffset + view.ByteLength;
        if (lastVectorStart < 0 ||
            (lastVectorStart + 8) > bufferBytes.Length ||
            (lastVectorStart + 8) > accessorEnd)
        {
            return false;
        }

        vectors = new Vector2[view.Count];
        for (int i = 0; i < view.Count; i++)
        {
            int offset = view.DataOffset + (i * stride);
            float x = BitConverter.ToSingle(bufferBytes, offset + 0);
            float y = BitConverter.ToSingle(bufferBytes, offset + 4);
            vectors[i] = new Vector2(x, y);
        }

        return true;
    }

    private static bool TryReadAccessorIndices(
        JsonElement accessorsElement,
        JsonElement bufferViewsElement,
        byte[] bufferBytes,
        int accessorIndex,
        out uint[] values)
    {
        values = Array.Empty<uint>();
        if (!TryResolveAccessorView(accessorsElement, bufferViewsElement, bufferBytes.Length, accessorIndex, out AccessorView view))
        {
            return false;
        }

        if (!string.Equals(view.Type, "SCALAR", StringComparison.Ordinal))
        {
            return false;
        }

        int componentSize = view.ComponentType switch
        {
            5121 => 1, // UNSIGNED_BYTE
            5123 => 2, // UNSIGNED_SHORT
            5125 => 4, // UNSIGNED_INT
            _ => 0
        };
        if (componentSize == 0)
        {
            return false;
        }

        int stride = view.ByteStride > 0 ? view.ByteStride : componentSize;
        if (stride < componentSize)
        {
            return false;
        }

        long lastValueStart = view.DataOffset + ((long)(view.Count - 1) * stride);
        long accessorEnd = view.DataOffset + view.ByteLength;
        if (lastValueStart < 0 ||
            (lastValueStart + componentSize) > bufferBytes.Length ||
            (lastValueStart + componentSize) > accessorEnd)
        {
            return false;
        }

        values = new uint[view.Count];
        for (int i = 0; i < view.Count; i++)
        {
            int offset = view.DataOffset + (i * stride);
            values[i] = view.ComponentType switch
            {
                5121 => bufferBytes[offset],
                5123 => BitConverter.ToUInt16(bufferBytes, offset),
                5125 => BitConverter.ToUInt32(bufferBytes, offset),
                _ => 0u
            };
        }

        return true;
    }

    private static bool TryResolveAccessorView(
        JsonElement accessorsElement,
        JsonElement bufferViewsElement,
        int bufferLength,
        int accessorIndex,
        out AccessorView view)
    {
        view = default;
        if (!TryGetArrayElement(accessorsElement, accessorIndex, out JsonElement accessorElement) ||
            accessorElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Sparse accessors are uncommon for static meshes; keep importer strict.
        if (accessorElement.TryGetProperty("sparse", out _))
        {
            return false;
        }

        if (!accessorElement.TryGetProperty("bufferView", out JsonElement accessorBufferViewElement) ||
            !accessorBufferViewElement.TryGetInt32(out int bufferViewIndex) ||
            !TryGetArrayElement(bufferViewsElement, bufferViewIndex, out JsonElement bufferViewElement) ||
            bufferViewElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!accessorElement.TryGetProperty("count", out JsonElement countElement) ||
            !countElement.TryGetInt32(out int count) ||
            count <= 0)
        {
            return false;
        }

        if (!accessorElement.TryGetProperty("componentType", out JsonElement componentTypeElement) ||
            !componentTypeElement.TryGetInt32(out int componentType))
        {
            return false;
        }

        if (!accessorElement.TryGetProperty("type", out JsonElement typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        if (bufferViewElement.TryGetProperty("buffer", out JsonElement bufferIndexElement) &&
            bufferIndexElement.TryGetInt32(out int bufferIndex) &&
            bufferIndex != 0)
        {
            // GLB uses the first (and usually only) binary buffer chunk.
            return false;
        }

        int bufferViewOffset = 0;
        if (bufferViewElement.TryGetProperty("byteOffset", out JsonElement bufferViewOffsetElement) &&
            bufferViewOffsetElement.TryGetInt32(out int parsedBufferViewOffset))
        {
            bufferViewOffset = parsedBufferViewOffset;
        }

        if (!bufferViewElement.TryGetProperty("byteLength", out JsonElement bufferViewLengthElement) ||
            !bufferViewLengthElement.TryGetInt32(out int bufferViewLength) ||
            bufferViewLength <= 0)
        {
            return false;
        }

        int accessorOffset = 0;
        if (accessorElement.TryGetProperty("byteOffset", out JsonElement accessorOffsetElement) &&
            accessorOffsetElement.TryGetInt32(out int parsedAccessorOffset))
        {
            accessorOffset = parsedAccessorOffset;
        }

        int byteStride = 0;
        if (bufferViewElement.TryGetProperty("byteStride", out JsonElement byteStrideElement) &&
            byteStrideElement.TryGetInt32(out int parsedByteStride))
        {
            byteStride = parsedByteStride;
        }

        int dataOffset = bufferViewOffset + accessorOffset;
        if (dataOffset < 0 || dataOffset >= bufferLength)
        {
            return false;
        }

        if (dataOffset > (bufferViewOffset + bufferViewLength))
        {
            return false;
        }

        view = new AccessorView(
            DataOffset: dataOffset,
            Count: count,
            ComponentType: componentType,
            Type: type,
            ByteStride: byteStride,
            ByteLength: bufferViewLength - accessorOffset);
        return view.ByteLength > 0;
    }

    private static bool TryGetArrayElement(JsonElement arrayElement, int index, out JsonElement value)
    {
        value = default;
        if (arrayElement.ValueKind != JsonValueKind.Array || index < 0 || index >= arrayElement.GetArrayLength())
        {
            return false;
        }

        value = arrayElement[index];
        return true;
    }
}
