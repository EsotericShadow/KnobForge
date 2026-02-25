using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using KnobForge.Core;
using KnobForge.Core.Scene;
using KnobForge.Rendering.GPU;

namespace KnobForge.App.Controls
{
    public sealed partial class MetalViewport
    {
        private void ClearMeshResources()
        {
            ReplaceMeshResources(ref _meshResources, null);
            ReplaceMeshResources(ref _collarResources, null);
            ReplaceMeshResources(ref _sliderBackplateResources, null);
            ReplaceMeshResources(ref _sliderThumbResources, null);
            ReplaceMeshResources(ref _toggleBaseResources, null);
            ReplaceMeshResources(ref _toggleLeverResources, null);
            ReplaceMeshResources(ref _toggleSleeveResources, null);
            ReplaceMeshResources(ref _pushButtonBaseResources, null);
            ReplaceMeshResources(ref _pushButtonCapResources, null);
            ReplaceMeshResources(ref _indicatorBaseResources, null);
            ReplaceMeshResources(ref _indicatorHousingResources, null);
            ReplaceMeshResources(ref _indicatorLensResources, null);
            ReplaceMeshResources(ref _indicatorReflectorResources, null);
            ReplaceMeshResources(ref _indicatorEmitterResources, null);
            ReplaceMeshResources(ref _indicatorAuraResources, null);
            _paintPickMapDirty = true;
        }

        private void ReplaceMeshResources(ref MetalMeshGpuResources? target, MetalMeshGpuResources? replacement)
        {
            if (ReferenceEquals(target, replacement))
            {
                return;
            }

            target?.Dispose();
            target = replacement;
            _paintPickMapDirty = true;
        }

        private void RefreshMeshResources(KnobProject? project, ModelNode? modelNode)
        {
            if (_context is null || project is null || modelNode is null)
            {
                ClearMeshResources();
                _collarShapeKey = default;
                _sliderAssemblyShapeKey = default;
                _toggleAssemblyShapeKey = default;
                _pushButtonAssemblyShapeKey = default;
                _indicatorAssemblyShapeKey = default;
                return;
            }

            CollarNode? collarNode = modelNode.Children.OfType<CollarNode>().FirstOrDefault();
            CollarShapeKey nextCollarKey = BuildCollarShapeKey(modelNode, collarNode);
            bool collarEnabled =
                project.ProjectType == InteractorProjectType.RotaryKnob &&
                collarNode is { Enabled: true } &&
                collarNode.Preset != CollarPreset.None;
            bool collarShapeChanged = !nextCollarKey.Equals(_collarShapeKey);
            if (!collarEnabled)
            {
                ReplaceMeshResources(ref _collarResources, null);
                _collarShapeKey = default;
            }
            else if (collarShapeChanged || _collarResources == null)
            {
                _collarShapeKey = nextCollarKey;
                MetalMeshGpuResources? nextCollarResources = null;
                CollarMesh? collarMesh = CollarMeshBuilder.TryBuildFromProject(project);
                if (collarMesh is null || collarMesh.Vertices.Length == 0 || collarMesh.Indices.Length == 0)
                {
                    Console.WriteLine(
                        $"[MetalViewport] Collar mesh build failed. enabled={collarEnabled}, preset={collarNode?.Preset}, pathSegments={collarNode?.PathSegments ?? 0}, crossSegments={collarNode?.CrossSegments ?? 0}, importPath={collarNode?.ImportedMeshPath ?? "<none>"}");
                }
                else
                {
                    nextCollarResources = CreateGpuResources(collarMesh.Vertices, collarMesh.Indices, collarMesh.ReferenceRadius);
                }

                ReplaceMeshResources(ref _collarResources, nextCollarResources);
            }

            SliderAssemblyConfig sliderConfig = SliderAssemblyMeshBuilder.ResolveConfig(project);
            SliderAssemblyShapeKey nextSliderKey = BuildSliderAssemblyShapeKey(sliderConfig);
            bool sliderEnabled = sliderConfig.Enabled;
            bool sliderShapeChanged = !nextSliderKey.Equals(_sliderAssemblyShapeKey);
            if (!sliderEnabled)
            {
                ReplaceMeshResources(ref _sliderBackplateResources, null);
                ReplaceMeshResources(ref _sliderThumbResources, null);
                _sliderAssemblyShapeKey = default;
            }
            else if (sliderShapeChanged || _sliderBackplateResources == null || _sliderThumbResources == null)
            {
                _sliderAssemblyShapeKey = nextSliderKey;
                SliderPartMesh backplateMesh = SliderAssemblyMeshBuilder.BuildBackplateMesh(sliderConfig);
                SliderPartMesh thumbMesh = SliderAssemblyMeshBuilder.BuildThumbMesh(sliderConfig);
                MetalMeshGpuResources? nextBackplateResources = null;
                MetalMeshGpuResources? nextThumbResources = null;
                if (backplateMesh.Vertices.Length > 0 && backplateMesh.Indices.Length > 0)
                {
                    nextBackplateResources = CreateGpuResources(backplateMesh.Vertices, backplateMesh.Indices, backplateMesh.ReferenceRadius);
                }

                if (thumbMesh.Vertices.Length > 0 && thumbMesh.Indices.Length > 0)
                {
                    nextThumbResources = CreateGpuResources(thumbMesh.Vertices, thumbMesh.Indices, thumbMesh.ReferenceRadius);
                }

                ReplaceMeshResources(ref _sliderBackplateResources, nextBackplateResources);
                ReplaceMeshResources(ref _sliderThumbResources, nextThumbResources);
            }

            ToggleAssemblyConfig toggleConfig = ToggleAssemblyMeshBuilder.ResolveConfig(project);
            ToggleAssemblyShapeKey nextToggleKey = BuildToggleAssemblyShapeKey(toggleConfig);
            bool toggleEnabled = toggleConfig.Enabled;
            bool toggleShapeChanged = !nextToggleKey.Equals(_toggleAssemblyShapeKey);
            if (!toggleEnabled)
            {
                ReplaceMeshResources(ref _toggleBaseResources, null);
                ReplaceMeshResources(ref _toggleLeverResources, null);
                ReplaceMeshResources(ref _toggleSleeveResources, null);
                _toggleAssemblyShapeKey = default;
            }
            else if (toggleShapeChanged || _toggleBaseResources == null || _toggleLeverResources == null || _toggleSleeveResources == null)
            {
                _toggleAssemblyShapeKey = nextToggleKey;
                TogglePartMesh baseMesh = ToggleAssemblyMeshBuilder.BuildBaseMesh(toggleConfig);
                TogglePartMesh leverMesh = ToggleAssemblyMeshBuilder.BuildLeverMesh(toggleConfig);
                TogglePartMesh sleeveMesh = ToggleAssemblyMeshBuilder.BuildSleeveMesh(toggleConfig);
                MetalMeshGpuResources? nextBaseResources = null;
                MetalMeshGpuResources? nextLeverResources = null;
                MetalMeshGpuResources? nextSleeveResources = null;
                if (baseMesh.Vertices.Length > 0 && baseMesh.Indices.Length > 0)
                {
                    nextBaseResources = CreateGpuResources(baseMesh.Vertices, baseMesh.Indices, baseMesh.ReferenceRadius);
                }

                if (leverMesh.Vertices.Length > 0 && leverMesh.Indices.Length > 0)
                {
                    nextLeverResources = CreateGpuResources(leverMesh.Vertices, leverMesh.Indices, leverMesh.ReferenceRadius);
                }

                if (sleeveMesh.Vertices.Length > 0 && sleeveMesh.Indices.Length > 0)
                {
                    nextSleeveResources = CreateGpuResources(sleeveMesh.Vertices, sleeveMesh.Indices, sleeveMesh.ReferenceRadius);
                }

                ReplaceMeshResources(ref _toggleBaseResources, nextBaseResources);
                ReplaceMeshResources(ref _toggleLeverResources, nextLeverResources);
                ReplaceMeshResources(ref _toggleSleeveResources, nextSleeveResources);
            }

            PushButtonAssemblyConfig pushButtonConfig = PushButtonAssemblyMeshBuilder.ResolveConfig(project);
            PushButtonAssemblyShapeKey nextPushButtonKey = BuildPushButtonAssemblyShapeKey(pushButtonConfig);
            bool pushButtonEnabled = pushButtonConfig.Enabled;
            bool pushButtonShapeChanged = !nextPushButtonKey.Equals(_pushButtonAssemblyShapeKey);
            if (!pushButtonEnabled)
            {
                ReplaceMeshResources(ref _pushButtonBaseResources, null);
                ReplaceMeshResources(ref _pushButtonCapResources, null);
                _pushButtonAssemblyShapeKey = default;
            }
            else if (pushButtonShapeChanged || _pushButtonBaseResources == null || _pushButtonCapResources == null)
            {
                _pushButtonAssemblyShapeKey = nextPushButtonKey;
                PushButtonPartMesh baseMesh = PushButtonAssemblyMeshBuilder.BuildBaseMesh(pushButtonConfig);
                PushButtonPartMesh capMesh = PushButtonAssemblyMeshBuilder.BuildCapMesh(pushButtonConfig);
                MetalMeshGpuResources? nextBaseResources = null;
                MetalMeshGpuResources? nextCapResources = null;
                if (baseMesh.Vertices.Length > 0 && baseMesh.Indices.Length > 0)
                {
                    nextBaseResources = CreateGpuResources(baseMesh.Vertices, baseMesh.Indices, baseMesh.ReferenceRadius);
                }

                if (capMesh.Vertices.Length > 0 && capMesh.Indices.Length > 0)
                {
                    nextCapResources = CreateGpuResources(capMesh.Vertices, capMesh.Indices, capMesh.ReferenceRadius);
                }

                ReplaceMeshResources(ref _pushButtonBaseResources, nextBaseResources);
                ReplaceMeshResources(ref _pushButtonCapResources, nextCapResources);
            }

            IndicatorAssemblyConfig indicatorConfig = IndicatorAssemblyMeshBuilder.ResolveConfig(project);
            IndicatorAssemblyShapeKey nextIndicatorKey = BuildIndicatorAssemblyShapeKey(indicatorConfig);
            bool indicatorEnabled = indicatorConfig.Enabled;
            bool indicatorShapeChanged = !nextIndicatorKey.Equals(_indicatorAssemblyShapeKey);
            if (!indicatorEnabled)
            {
                ReplaceMeshResources(ref _indicatorBaseResources, null);
                ReplaceMeshResources(ref _indicatorHousingResources, null);
                ReplaceMeshResources(ref _indicatorLensResources, null);
                ReplaceMeshResources(ref _indicatorReflectorResources, null);
                ReplaceMeshResources(ref _indicatorEmitterResources, null);
                ReplaceMeshResources(ref _indicatorAuraResources, null);
                _indicatorAssemblyShapeKey = default;
            }
            else if (indicatorShapeChanged ||
                _indicatorBaseResources == null ||
                _indicatorHousingResources == null ||
                _indicatorLensResources == null ||
                _indicatorReflectorResources == null ||
                _indicatorEmitterResources == null ||
                _indicatorAuraResources == null)
            {
                _indicatorAssemblyShapeKey = nextIndicatorKey;
                IndicatorPartMesh baseMesh = IndicatorAssemblyMeshBuilder.BuildBaseMesh(indicatorConfig);
                IndicatorPartMesh housingMesh = IndicatorAssemblyMeshBuilder.BuildHousingMesh(indicatorConfig);
                IndicatorPartMesh lensMesh = IndicatorAssemblyMeshBuilder.BuildLensMesh(indicatorConfig);
                IndicatorPartMesh reflectorMesh = IndicatorAssemblyMeshBuilder.BuildReflectorMesh(indicatorConfig);
                IndicatorPartMesh emitterMesh = IndicatorAssemblyMeshBuilder.BuildEmitterCoreMesh(indicatorConfig);
                IndicatorPartMesh auraMesh = IndicatorAssemblyMeshBuilder.BuildAuraMesh(indicatorConfig);
                MetalMeshGpuResources? nextBaseResources = null;
                MetalMeshGpuResources? nextHousingResources = null;
                MetalMeshGpuResources? nextLensResources = null;
                MetalMeshGpuResources? nextReflectorResources = null;
                MetalMeshGpuResources? nextEmitterResources = null;
                MetalMeshGpuResources? nextAuraResources = null;

                if (baseMesh.Vertices.Length > 0 && baseMesh.Indices.Length > 0)
                {
                    nextBaseResources = CreateGpuResources(baseMesh.Vertices, baseMesh.Indices, baseMesh.ReferenceRadius);
                }

                if (housingMesh.Vertices.Length > 0 && housingMesh.Indices.Length > 0)
                {
                    nextHousingResources = CreateGpuResources(housingMesh.Vertices, housingMesh.Indices, housingMesh.ReferenceRadius);
                }

                if (lensMesh.Vertices.Length > 0 && lensMesh.Indices.Length > 0)
                {
                    nextLensResources = CreateGpuResources(lensMesh.Vertices, lensMesh.Indices, lensMesh.ReferenceRadius);
                }

                if (reflectorMesh.Vertices.Length > 0 && reflectorMesh.Indices.Length > 0)
                {
                    nextReflectorResources = CreateGpuResources(reflectorMesh.Vertices, reflectorMesh.Indices, reflectorMesh.ReferenceRadius);
                }

                if (emitterMesh.Vertices.Length > 0 && emitterMesh.Indices.Length > 0)
                {
                    nextEmitterResources = CreateGpuResources(emitterMesh.Vertices, emitterMesh.Indices, emitterMesh.ReferenceRadius);
                }

                if (auraMesh.Vertices.Length > 0 && auraMesh.Indices.Length > 0)
                {
                    nextAuraResources = CreateGpuResources(auraMesh.Vertices, auraMesh.Indices, auraMesh.ReferenceRadius);
                }

                ReplaceMeshResources(ref _indicatorBaseResources, nextBaseResources);
                ReplaceMeshResources(ref _indicatorHousingResources, nextHousingResources);
                ReplaceMeshResources(ref _indicatorLensResources, nextLensResources);
                ReplaceMeshResources(ref _indicatorReflectorResources, nextReflectorResources);
                ReplaceMeshResources(ref _indicatorEmitterResources, nextEmitterResources);
                ReplaceMeshResources(ref _indicatorAuraResources, nextAuraResources);
            }

            if (project.ProjectType != InteractorProjectType.RotaryKnob)
            {
                ReplaceMeshResources(ref _meshResources, null);
                _meshShapeKey = default;
                ReleaseSpiralNormalTexture();
                _spiralNormalMapKey = default;
                return;
            }

            MeshShapeKey nextKey = new(
                MathF.Round(modelNode.Radius, 3),
                MathF.Round(modelNode.Height, 3),
                MathF.Round(modelNode.Bevel, 3),
                MathF.Round(modelNode.TopRadiusScale, 3),
                modelNode.RadialSegments,
                MathF.Round(modelNode.CrownProfile, 4),
                MathF.Round(modelNode.BevelCurve, 4),
                MathF.Round(modelNode.BodyTaper, 4),
                MathF.Round(modelNode.BodyBulge, 4),
                MathF.Round(modelNode.SpiralRidgeHeight, 3),
                MathF.Round(modelNode.SpiralRidgeWidth, 3),
                MathF.Round(modelNode.SpiralRidgeHeightVariance, 3),
                MathF.Round(modelNode.SpiralRidgeWidthVariance, 3),
                MathF.Round(modelNode.SpiralHeightVarianceThreshold, 3),
                MathF.Round(modelNode.SpiralWidthVarianceThreshold, 3),
                MathF.Round(modelNode.SpiralTurns, 3),
                (int)modelNode.GripType,
                MathF.Round(modelNode.GripStart, 4),
                MathF.Round(modelNode.GripHeight, 4),
                MathF.Round(modelNode.GripDensity, 3),
                MathF.Round(modelNode.GripPitch, 3),
                MathF.Round(modelNode.GripDepth, 3),
                MathF.Round(modelNode.GripWidth, 4),
                MathF.Round(modelNode.GripSharpness, 3),
                modelNode.IndicatorEnabled ? 1 : 0,
                (int)modelNode.IndicatorShape,
                (int)modelNode.IndicatorRelief,
                (int)modelNode.IndicatorProfile,
                MathF.Round(modelNode.IndicatorWidthRatio, 4),
                MathF.Round(modelNode.IndicatorLengthRatioTop, 4),
                MathF.Round(modelNode.IndicatorPositionRatio, 4),
                MathF.Round(modelNode.IndicatorThicknessRatio, 4),
                MathF.Round(modelNode.IndicatorRoundness, 4),
                modelNode.IndicatorCadWallsEnabled ? 1 : 0);

            if (_meshResources != null && nextKey.Equals(_meshShapeKey))
            {
                EnsureSpiralNormalTexture(modelNode, _meshResources.ReferenceRadius);
                return;
            }

            MetalMesh? mesh = MetalMeshBuilder.TryBuildFromProject(project);
            if (mesh is null || mesh.Vertices.Length == 0 || mesh.Indices.Length == 0)
            {
                ReplaceMeshResources(ref _meshResources, null);
                _meshShapeKey = default;
                return;
            }

            MetalMeshGpuResources? nextMeshResources = CreateGpuResources(mesh.Vertices, mesh.Indices, mesh.ReferenceRadius);
            ReplaceMeshResources(ref _meshResources, nextMeshResources);
            if (_meshResources == null)
            {
                _meshShapeKey = default;
                return;
            }

            _meshShapeKey = nextKey;
            EnsureSpiralNormalTexture(modelNode, mesh.ReferenceRadius);
        }

        private MetalMeshGpuResources? CreateGpuResources(MetalVertex[] vertices, uint[] indices, float referenceRadius)
        {
            if (_context is null)
            {
                return null;
            }

            IMTLBuffer vertexBuffer = _context.CreateBuffer<MetalVertex>(vertices);
            IMTLBuffer indexBuffer = _context.CreateBuffer<uint>(indices);
            if (vertexBuffer.Handle == IntPtr.Zero || indexBuffer.Handle == IntPtr.Zero)
            {
                vertexBuffer.Dispose();
                indexBuffer.Dispose();
                return null;
            }

            var positions = new Vector3[vertices.Length];
            Vector3 boundsMin = new(float.MaxValue);
            Vector3 boundsMax = new(float.MinValue);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i].Position;
                positions[i] = p;
                boundsMin = Vector3.Min(boundsMin, p);
                boundsMax = Vector3.Max(boundsMax, p);
            }

            uint[] indicesCopy = indices.ToArray();
            CpuTriangleBvh bvh = CpuTriangleBvh.Build(positions, indicesCopy);

            return new MetalMeshGpuResources
            {
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                IndexCount = indices.Length,
                IndexType = MTLIndexType.UInt32,
                ReferenceRadius = referenceRadius,
                Positions = positions,
                Indices = indicesCopy,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                Bvh = bvh
            };
        }

        private static bool IsRenderableMesh(MetalMeshGpuResources? mesh)
        {
            return mesh is not null &&
                mesh.VertexBuffer.Handle != IntPtr.Zero &&
                mesh.IndexBuffer.Handle != IntPtr.Zero &&
                mesh.IndexCount > 0;
        }

        private static float IncludeReferenceRadius(float current, MetalMeshGpuResources? mesh)
        {
            if (!IsRenderableMesh(mesh))
            {
                return current;
            }

            return MathF.Max(current, mesh!.ReferenceRadius);
        }

        private static bool IsImportedCollarPreset(CollarNode? collarNode)
        {
            return collarNode is not null && CollarNode.IsImportedMeshPreset(collarNode.Preset);
        }

        private static string ResolveImportedMeshPath(CollarNode collarNode)
        {
            return CollarNode.ResolveImportedMeshPath(collarNode.Preset, collarNode.ImportedMeshPath);
        }

        private static CollarShapeKey BuildCollarShapeKey(ModelNode modelNode, CollarNode? collarNode)
        {
            if (collarNode is null)
            {
                return default;
            }

            string importedMeshPath = ResolveImportedMeshPath(collarNode);
            long importedFileTicks = 0;
            if (!string.IsNullOrWhiteSpace(importedMeshPath) && File.Exists(importedMeshPath))
            {
                importedFileTicks = File.GetLastWriteTimeUtc(importedMeshPath).Ticks;
            }

            return new CollarShapeKey(
                collarNode.Enabled ? 1 : 0,
                (int)collarNode.Preset,
                MathF.Round(modelNode.Radius, 3),
                MathF.Round(modelNode.Height, 3),
                MathF.Round(collarNode.InnerRadiusRatio, 4),
                MathF.Round(collarNode.GapToKnobRatio, 4),
                MathF.Round(collarNode.ElevationRatio, 4),
                MathF.Round(collarNode.OverallRotationRadians, 4),
                MathF.Round(collarNode.BiteAngleRadians, 4),
                MathF.Round(collarNode.BodyRadiusRatio, 4),
                MathF.Round(collarNode.BodyEllipseYScale, 4),
                MathF.Round(collarNode.NeckTaper, 4),
                MathF.Round(collarNode.TailTaper, 4),
                MathF.Round(collarNode.MassBias, 4),
                MathF.Round(collarNode.TailUnderlap, 4),
                MathF.Round(collarNode.HeadScale, 4),
                MathF.Round(collarNode.JawBulge, 4),
                collarNode.UvSeamFollowBite ? 1 : 0,
                MathF.Round(collarNode.UvSeamOffset, 4),
                collarNode.PathSegments,
                collarNode.CrossSegments,
                MathF.Round(collarNode.ImportedScale, 4),
                MathF.Round(collarNode.ImportedBodyLengthScale, 4),
                MathF.Round(collarNode.ImportedBodyThicknessScale, 4),
                MathF.Round(collarNode.ImportedHeadLengthScale, 4),
                MathF.Round(collarNode.ImportedHeadThicknessScale, 4),
                MathF.Round(collarNode.ImportedRotationRadians, 4),
                collarNode.ImportedMirrorX ? 1 : 0,
                collarNode.ImportedMirrorY ? 1 : 0,
                collarNode.ImportedMirrorZ ? 1 : 0,
                MathF.Round(collarNode.ImportedOffsetXRatio, 4),
                MathF.Round(collarNode.ImportedOffsetYRatio, 4),
                MathF.Round(collarNode.ImportedInflateRatio, 4),
                importedMeshPath,
                importedFileTicks);
        }

        private static SliderAssemblyShapeKey BuildSliderAssemblyShapeKey(in SliderAssemblyConfig config)
        {
            if (!config.Enabled)
            {
                return default;
            }

            return new SliderAssemblyShapeKey(
                Enabled: 1,
                BackplateWidth: MathF.Round(config.BackplateWidth, 3),
                BackplateHeight: MathF.Round(config.BackplateHeight, 3),
                BackplateThickness: MathF.Round(config.BackplateThickness, 3),
                ThumbWidth: MathF.Round(config.ThumbWidth, 3),
                ThumbHeight: MathF.Round(config.ThumbHeight, 3),
                ThumbDepth: MathF.Round(config.ThumbDepth, 3),
                ThumbPositionNormalized: MathF.Round(config.ThumbPositionNormalized, 4),
                BackplateImportedMeshPath: config.BackplateImportedMeshPath ?? string.Empty,
                BackplateImportedMeshTicks: config.BackplateImportedMeshTicks,
                ThumbImportedMeshPath: config.ThumbImportedMeshPath ?? string.Empty,
                ThumbImportedMeshTicks: config.ThumbImportedMeshTicks);
        }

        private static ToggleAssemblyShapeKey BuildToggleAssemblyShapeKey(in ToggleAssemblyConfig config)
        {
            if (!config.Enabled)
            {
                return default;
            }

            return new ToggleAssemblyShapeKey(
                Enabled: 1,
                StateCount: config.StateCount,
                StateIndex: config.StateIndex,
                LeverAngleDeg: MathF.Round(config.LeverAngleDeg, 3),
                PlateWidth: MathF.Round(config.PlateWidth, 3),
                PlateHeight: MathF.Round(config.PlateHeight, 3),
                PlateThickness: MathF.Round(config.PlateThickness, 3),
                PlateOffsetY: MathF.Round(config.PlateOffsetY, 3),
                PlateOffsetZ: MathF.Round(config.PlateOffsetZ, 3),
                BushingRadius: MathF.Round(config.BushingRadius, 3),
                BushingHeight: MathF.Round(config.BushingHeight, 3),
                BushingSides: config.BushingSides,
                LowerBushingShape: (int)config.LowerBushingShape,
                UpperBushingShape: (int)config.UpperBushingShape,
                LowerBushingRadiusScale: MathF.Round(config.LowerBushingRadiusScale, 3),
                LowerBushingHeightRatio: MathF.Round(config.LowerBushingHeightRatio, 3),
                UpperBushingRadiusScale: MathF.Round(config.UpperBushingRadiusScale, 3),
                UpperBushingHeightRatio: MathF.Round(config.UpperBushingHeightRatio, 3),
                UpperBushingKnurlAmount: MathF.Round(config.UpperBushingKnurlAmount, 4),
                UpperBushingKnurlDensity: config.UpperBushingKnurlDensity,
                UpperBushingKnurlDepth: MathF.Round(config.UpperBushingKnurlDepth, 4),
                PivotHousingRadius: MathF.Round(config.PivotHousingRadius, 3),
                PivotHousingDepth: MathF.Round(config.PivotHousingDepth, 3),
                PivotHousingBevel: MathF.Round(config.PivotHousingBevel, 3),
                PivotBallRadius: MathF.Round(config.PivotBallRadius, 3),
                PivotClearance: MathF.Round(config.PivotClearance, 3),
                LeverLength: MathF.Round(config.LeverLength, 3),
                LeverBottomRadius: MathF.Round(config.LeverBottomRadius, 3),
                LeverTopRadius: MathF.Round(config.LeverTopRadius, 3),
                LeverSides: config.LeverSides,
                LeverPivotOffset: MathF.Round(config.LeverPivotOffset, 3),
                TipRadius: MathF.Round(config.TipRadius, 3),
                TipLatitudeSegments: config.TipLatitudeSegments,
                TipLongitudeSegments: config.TipLongitudeSegments,
                TipSleeveEnabled: config.TipSleeveEnabled ? 1 : 0,
                TipSleeveLength: MathF.Round(config.TipSleeveLength, 3),
                TipSleeveThickness: MathF.Round(config.TipSleeveThickness, 3),
                TipSleeveOuterRadius: MathF.Round(config.TipSleeveOuterRadius, 3),
                TipSleeveCoverage: MathF.Round(config.TipSleeveCoverage, 3),
                TipSleeveSides: config.TipSleeveSides,
                TipSleeveStyle: (int)config.TipSleeveStyle,
                TipSleeveTipStyle: (int)config.TipSleeveTipStyle,
                TipSleevePatternCount: config.TipSleevePatternCount,
                TipSleevePatternDepth: MathF.Round(config.TipSleevePatternDepth, 4),
                TipSleeveTipAmount: MathF.Round(config.TipSleeveTipAmount, 4),
                BaseImportedMeshPath: config.BaseImportedMeshPath ?? string.Empty,
                BaseImportedMeshTicks: config.BaseImportedMeshTicks,
                LeverImportedMeshPath: config.LeverImportedMeshPath ?? string.Empty,
                LeverImportedMeshTicks: config.LeverImportedMeshTicks);
        }

        private static PushButtonAssemblyShapeKey BuildPushButtonAssemblyShapeKey(in PushButtonAssemblyConfig config)
        {
            if (!config.Enabled)
            {
                return default;
            }

            return new PushButtonAssemblyShapeKey(
                Enabled: 1,
                PlateWidth: MathF.Round(config.PlateWidth, 3),
                PlateHeight: MathF.Round(config.PlateHeight, 3),
                PlateThickness: MathF.Round(config.PlateThickness, 3),
                BezelRadius: MathF.Round(config.BezelRadius, 3),
                BezelHeight: MathF.Round(config.BezelHeight, 3),
                CapRadius: MathF.Round(config.CapRadius, 3),
                CapHeight: MathF.Round(config.CapHeight, 3),
                PressDepth: MathF.Round(config.PressDepth, 3));
        }

        private static IndicatorAssemblyShapeKey BuildIndicatorAssemblyShapeKey(in IndicatorAssemblyConfig config)
        {
            if (!config.Enabled)
            {
                return default;
            }

            return new IndicatorAssemblyShapeKey(
                Enabled: 1,
                BaseWidth: MathF.Round(config.BaseWidth, 3),
                BaseHeight: MathF.Round(config.BaseHeight, 3),
                BaseThickness: MathF.Round(config.BaseThickness, 3),
                HousingRadius: MathF.Round(config.HousingRadius, 3),
                HousingHeight: MathF.Round(config.HousingHeight, 3),
                LensRadius: MathF.Round(config.LensRadius, 3),
                LensHeight: MathF.Round(config.LensHeight, 3),
                ReflectorBaseRadius: MathF.Round(config.ReflectorBaseRadius, 3),
                ReflectorTopRadius: MathF.Round(config.ReflectorTopRadius, 3),
                ReflectorDepth: MathF.Round(config.ReflectorDepth, 3),
                EmitterRadius: MathF.Round(config.EmitterRadius, 3),
                EmitterSpread: MathF.Round(config.EmitterSpread, 3),
                EmitterDepth: MathF.Round(config.EmitterDepth, 3),
                EmitterCount: config.EmitterCount,
                RadialSegments: config.RadialSegments,
                LensLatitudeSegments: config.LensLatitudeSegments,
                LensLongitudeSegments: config.LensLongitudeSegments);
        }

        private GpuUniforms BuildUniforms(
            KnobProject? project,
            ModelNode? modelNode,
            float referenceRadius,
            Size viewportDip,
            double? dynamicLightAnimationTimeSeconds = null)
        {
            float renderScale = GetRenderScale();
            float viewportWidthPx = MathF.Max(1f, (float)viewportDip.Width * renderScale);
            float viewportHeightPx = MathF.Max(1f, (float)viewportDip.Height * renderScale);
            return BuildUniformsForPixels(
                project,
                modelNode,
                referenceRadius,
                viewportWidthPx,
                viewportHeightPx,
                dynamicLightAnimationTimeSeconds);
        }

        private GpuUniforms BuildUniformsForPixels(
            KnobProject? project,
            ModelNode? modelNode,
            float referenceRadius,
            float viewportWidthPx,
            float viewportHeightPx,
            double? dynamicLightAnimationTimeSeconds = null)
        {
            GetCameraBasis(out Vector3 right, out Vector3 up, out Vector3 forward);

            float scaleX = (2f * _zoom) / MathF.Max(1f, viewportWidthPx);
            float scaleY = (2f * _zoom) / MathF.Max(1f, viewportHeightPx);
            float scaleZ = scaleX;
            float offsetX = (2f * _panPx.X) / MathF.Max(1f, viewportWidthPx);
            float offsetY = (-2f * _panPx.Y) / MathF.Max(1f, viewportHeightPx);

            float radius = MathF.Max(1f, referenceRadius);
            Vector3 cameraPos = -forward * (radius * 6f);

            MaterialNode? materialNode = modelNode?.Children.OfType<MaterialNode>().FirstOrDefault();
            Vector3 baseColor = materialNode?.BaseColor ?? new Vector3(0.55f, 0.16f, 0.16f);
            float metallic = Math.Clamp(materialNode?.Metallic ?? 0f, 0f, 1f);
            float roughness = Math.Clamp(materialNode?.Roughness ?? 0.5f, 0.04f, 1f);
            float pearlescence = Math.Clamp(materialNode?.Pearlescence ?? 0f, 0f, 1f);
            float rustAmount = Math.Clamp(materialNode?.RustAmount ?? 0f, 0f, 1f);
            float wearAmount = Math.Clamp(materialNode?.WearAmount ?? 0f, 0f, 1f);
            float gunkAmount = Math.Clamp(materialNode?.GunkAmount ?? 0f, 0f, 1f);
            float diffuseStrength = materialNode?.DiffuseStrength ?? 1f;
            float specularStrength = materialNode?.SpecularStrength ?? 1f;
            float brushStrength = Math.Clamp(materialNode?.RadialBrushStrength ?? 0f, 0f, 1f);
            float brushDensity = MathF.Max(1f, materialNode?.RadialBrushDensity ?? 56f);
            float surfaceCharacter = Math.Clamp(materialNode?.SurfaceCharacter ?? 0f, 0f, 1f);
            bool partMaterialsEnabled = materialNode?.PartMaterialsEnabled ?? false;
            Vector3 topBaseColor = materialNode?.TopBaseColor ?? baseColor;
            Vector3 bevelBaseColor = materialNode?.BevelBaseColor ?? baseColor;
            Vector3 sideBaseColor = materialNode?.SideBaseColor ?? baseColor;
            float topMetallic = partMaterialsEnabled
                ? Math.Clamp(materialNode?.TopMetallic ?? metallic, 0f, 1f)
                : metallic;
            float bevelMetallic = partMaterialsEnabled
                ? Math.Clamp(materialNode?.BevelMetallic ?? metallic, 0f, 1f)
                : metallic;
            float sideMetallic = partMaterialsEnabled
                ? Math.Clamp(materialNode?.SideMetallic ?? metallic, 0f, 1f)
                : metallic;
            float topRoughness = partMaterialsEnabled
                ? Math.Clamp(materialNode?.TopRoughness ?? roughness, 0.04f, 1f)
                : roughness;
            float bevelRoughness = partMaterialsEnabled
                ? Math.Clamp(materialNode?.BevelRoughness ?? roughness, 0.04f, 1f)
                : roughness;
            float sideRoughness = partMaterialsEnabled
                ? Math.Clamp(materialNode?.SideRoughness ?? roughness, 0.04f, 1f)
                : roughness;
            float indicatorEnabled = modelNode?.IndicatorEnabled == true ? 1f : 0f;
            float indicatorShape = (float)(modelNode?.IndicatorShape ?? IndicatorShape.Bar);
            float indicatorWidth = modelNode?.IndicatorWidthRatio ?? 0.06f;
            float indicatorLength = modelNode?.IndicatorLengthRatioTop ?? 0.28f;
            float indicatorPosition = modelNode?.IndicatorPositionRatio ?? 0.46f;
            float indicatorRoundness = modelNode?.IndicatorRoundness ?? 0f;
            IndicatorProfile indicatorProfileEnum = modelNode?.IndicatorProfile ?? IndicatorProfile.Straight;
            float indicatorProfile = (float)indicatorProfileEnum;
            float indicatorCadWallsEnabled = modelNode?.IndicatorCadWallsEnabled == true ? 1f : 0f;
            if (indicatorProfileEnum == IndicatorProfile.Straight)
            {
                indicatorRoundness = 0f;
            }
            Vector3 indicatorColor = modelNode?.IndicatorColor ?? new Vector3(0.97f, 0.96f, 0.92f);
            float indicatorColorBlend = modelNode?.IndicatorColorBlend ?? 1f;
            float turns = MathF.Max(1f, modelNode?.SpiralTurns ?? 220f);

            float modelRotationRadians = modelNode?.RotationRadians ?? 0f;
            float modelCos = MathF.Cos(modelRotationRadians);
            float modelSin = MathF.Sin(modelRotationRadians);
            float topScale = Math.Clamp(modelNode?.TopRadiusScale ?? 0.86f, 0.30f, 1.30f);
            float knobBaseRadius = MathF.Max(1f, modelNode?.Radius ?? radius);
            float knobTopRadius = knobBaseRadius * topScale;
            float spacingPx = (knobTopRadius / turns) * _zoom;
            float geometryKeep =
                project?.ProjectType == InteractorProjectType.RotaryKnob
                    ? SmoothStep(0.20f, 0.90f, spacingPx)
                    : 1f;
            float frontZ = (modelNode?.Height ?? (radius * 2f)) * 0.5f;

            GpuUniforms uniforms = default;
            uniforms.CameraPosAndReferenceRadius = new Vector4(cameraPos, radius);
            uniforms.RightAndScaleX = new Vector4(right, scaleX);
            uniforms.UpAndScaleY = new Vector4(up, scaleY);
            uniforms.ForwardAndScaleZ = new Vector4(forward, scaleZ);
            uniforms.ProjectionOffsetsAndLightCount = new Vector4(offsetX, offsetY, 0f, 0f);
            uniforms.DynamicLightParams = Vector4.Zero;
            uniforms.MaterialBaseColorAndMetallic = new Vector4(baseColor, metallic);
            uniforms.MaterialRoughnessDiffuseSpecMode = new Vector4(roughness, diffuseStrength, specularStrength, (float)(project?.Mode ?? LightingMode.Both));
            uniforms.MaterialPartTopColorAndMetallic = new Vector4(topBaseColor, topMetallic);
            uniforms.MaterialPartBevelColorAndMetallic = new Vector4(bevelBaseColor, bevelMetallic);
            uniforms.MaterialPartSideColorAndMetallic = new Vector4(sideBaseColor, sideMetallic);
            uniforms.MaterialPartRoughnessAndEnable = new Vector4(
                topRoughness,
                bevelRoughness,
                sideRoughness,
                partMaterialsEnabled ? 1f : 0f);
            uniforms.MaterialSurfaceBrushParams = new Vector4(brushStrength, brushDensity, surfaceCharacter, geometryKeep);
            uniforms.WeatherParams = new Vector4(rustAmount, wearAmount, gunkAmount, Math.Clamp(project?.BrushDarkness ?? 0.58f, 0f, 1f));
            Vector3 scratchExposeColor = project?.ScratchExposeColor ?? new Vector3(0.88f, 0.88f, 0.90f);
            float scratchExposeMetallic = Math.Clamp(project?.ScratchExposeMetallic ?? 0.92f, 0f, 1f);
            uniforms.ScratchExposeColorAndStrength = new Vector4(scratchExposeColor, scratchExposeMetallic);
            uniforms.AdvancedMaterialParams = new Vector4(
                Math.Clamp(project?.ScratchExposeRoughness ?? 0.20f, 0.04f, 1f),
                Math.Clamp(project?.ClearCoatAmount ?? 0f, 0f, 1f),
                Math.Clamp(project?.ClearCoatRoughness ?? 0.18f, 0.04f, 1f),
                (project?.AnisotropyAngleDegrees ?? 0f) * (MathF.PI / 180f));
            uniforms.IndicatorParams0 = new Vector4(indicatorEnabled, indicatorShape, indicatorWidth, indicatorLength);
            // Keep top-cap/indicator normalization stable even when scene bounds grow (e.g. collar enabled).
            uniforms.IndicatorParams1 = new Vector4(indicatorRoundness, indicatorPosition, knobTopRadius, pearlescence);
            uniforms.IndicatorColorAndBlend = new Vector4(indicatorColor, indicatorColorBlend);
            uniforms.IndicatorParams2 = new Vector4(indicatorProfile, indicatorCadWallsEnabled, 0f, 0f);
            if (project != null)
            {
                uniforms.MicroDetailParams = new Vector4(
                    project.SpiralNormalInfluenceEnabled ? 1f : 0f,
                    project.SpiralNormalLodFadeStart,
                    project.SpiralNormalLodFadeEnd,
                    project.SpiralRoughnessLodBoost);
            }
            else
            {
                uniforms.MicroDetailParams = new Vector4(1f, 0.55f, 2.4f, 0.20f);
            }

            if (project != null)
            {
                Vector3 envTop = project.EnvironmentTopColor;
                Vector3 envBottom = project.EnvironmentBottomColor;
                float envIntensity = MathF.Max(0f, project.EnvironmentIntensity);
                float envRoughMix = Math.Clamp(project.EnvironmentRoughnessMix, 0f, 1f);
                uniforms.EnvironmentTopColorAndIntensity = new Vector4(envTop, envIntensity);
                uniforms.EnvironmentBottomColorAndRoughnessMix = new Vector4(envBottom, envRoughMix);
            }
            else
            {
                uniforms.EnvironmentTopColorAndIntensity = new Vector4(0.12f, 0.12f, 0.13f, 1f);
                uniforms.EnvironmentBottomColorAndRoughnessMix = new Vector4(0.02f, 0.02f, 0.02f, 1f);
            }

            uniforms.ModelRotationCosSin = new Vector4(modelCos, modelSin, topScale, frontZ);
            uniforms.ShadowParams = Vector4.Zero;
            uniforms.ShadowColorAndOpacity = Vector4.Zero;
            uniforms.DebugBasisParams = new Vector4(
                (float)(project?.BasisDebug ?? BasisDebugMode.Off),
                Math.Clamp(project?.ScratchDepth ?? 0.30f, 0f, 1f),
                Math.Clamp(project?.PaintCoatMetallic ?? 0.02f, 0f, 1f),
                Math.Clamp(project?.PaintCoatRoughness ?? 0.56f, 0.04f, 1f));
            uniforms.LensMaterialParams0 = Vector4.Zero;
            uniforms.LensMaterialTintAndAbsorption = Vector4.Zero;
            uniforms.EnvironmentMapParams = Vector4.Zero;
            float lightEffectX = _orientation.InvertX ^ _lightEffectInvertX ? -1f : 1f;
            float lightEffectY = _orientation.InvertY ^ _lightEffectInvertY ? -1f : 1f;
            float lightEffectZ = _orientation.InvertZ ^ _lightEffectInvertZ ? -1f : 1f;
            uniforms.EnvironmentMapParams2 = new Vector4(lightEffectX, lightEffectY, lightEffectZ, 0f);
            uniforms.PostProcessParams = new Vector4(1f, 1.10f, 0.55f, 0.40f);
            uniforms.PostProcessParams2 = Vector4.Zero;
            uniforms.TonemapParams = new Vector4((float)TonemapOperator.Aces, 1f, 0f, 0f);

            if (project != null)
            {
                uniforms.EnvironmentMapParams = new Vector4(
                    Math.Clamp(project.EnvironmentHdriBlend, 0f, 1f),
                    0f,
                    project.EnvironmentHdriRotationDegrees * (MathF.PI / 180f),
                    0f);
                uniforms.PostProcessParams = new Vector4(
                    Math.Clamp(project.EnvironmentExposure, 0.10f, 4f),
                    Math.Clamp(project.EnvironmentBloomThreshold, 0f, 16f),
                    Math.Clamp(project.EnvironmentBloomKnee, 0.001f, 8f),
                    Math.Clamp(project.EnvironmentBloomStrength, 0f, 4f));
                uniforms.TonemapParams = new Vector4(
                    (float)project.ToneMappingOperator,
                    1f,
                    0f,
                    0f);

                int lightCount = Math.Min(project.Lights.Count, MaxGpuLights);
                uniforms.ProjectionOffsetsAndLightCount.Z = lightCount;

                for (int i = 0; i < lightCount; i++)
                {
                    KnobLight light = project.Lights[i];
                    Vector3 lightPos = ApplyLightOrientation(new Vector3(light.X, light.Y, light.Z));
                    Vector3 lightDir = ApplyLightOrientation(GetDirectionalVector(light));
                    if (lightDir.LengthSquared() > 1e-8f)
                    {
                        lightDir = Vector3.Normalize(lightDir);
                    }
                    else
                    {
                        lightDir = Vector3.UnitZ;
                    }

                    GpuLight packed = new()
                    {
                        PositionType = new Vector4(
                            lightPos,
                            light.Type == LightType.Directional ? 1f : 0f),
                        Direction = new Vector4(lightDir, 0f),
                        ColorIntensity = new Vector4(
                            light.Color.Red / 255f,
                            light.Color.Green / 255f,
                            light.Color.Blue / 255f,
                            MathF.Max(0f, light.Intensity)),
                        Params0 = new Vector4(
                            MathF.Max(0f, light.Falloff),
                            MathF.Max(0f, light.DiffuseBoost),
                            MathF.Max(0f, light.SpecularBoost),
                            MathF.Max(1f, light.SpecularPower))
                    };

                    SetGpuLight(ref uniforms, i, packed);
                }

                int dynamicLightCount = PackDynamicLights(ref uniforms, project, dynamicLightAnimationTimeSeconds);
                uniforms.DynamicLightParams.X = dynamicLightCount;
            }

            return uniforms;
        }

        private static GpuUniforms BuildCollarUniforms(in GpuUniforms baseUniforms, CollarNode collarNode)
        {
            GpuUniforms uniforms = baseUniforms;
            uniforms.MaterialBaseColorAndMetallic = new Vector4(collarNode.BaseColor, collarNode.Metallic);
            uniforms.MaterialRoughnessDiffuseSpecMode.X = collarNode.Roughness;
            uniforms.MaterialRoughnessDiffuseSpecMode.Y = 1f;
            uniforms.MaterialRoughnessDiffuseSpecMode.Z = 1f;
            uniforms.MaterialPartTopColorAndMetallic = uniforms.MaterialBaseColorAndMetallic;
            uniforms.MaterialPartBevelColorAndMetallic = uniforms.MaterialBaseColorAndMetallic;
            uniforms.MaterialPartSideColorAndMetallic = uniforms.MaterialBaseColorAndMetallic;
            uniforms.MaterialPartRoughnessAndEnable = new Vector4(collarNode.Roughness, collarNode.Roughness, collarNode.Roughness, 0f);
            uniforms.MaterialSurfaceBrushParams = new Vector4(0f, 56f, 0f, 1f);
            uniforms.WeatherParams = new Vector4(
                Math.Clamp(collarNode.RustAmount, 0f, 1f),
                Math.Clamp(collarNode.WearAmount, 0f, 1f),
                Math.Clamp(collarNode.GunkAmount, 0f, 1f),
                baseUniforms.WeatherParams.W);
            uniforms.IndicatorParams0 = Vector4.Zero;
            uniforms.IndicatorParams1 = new Vector4(0f, 0f, 0f, Math.Clamp(collarNode.Pearlescence, 0f, 1f));
            uniforms.IndicatorColorAndBlend = Vector4.Zero;
            uniforms.IndicatorParams2 = Vector4.Zero;
            uniforms.MicroDetailParams.X = 0f;
            uniforms.MicroDetailParams.W = 0f;
            uniforms.LensMaterialParams0 = Vector4.Zero;
            uniforms.LensMaterialTintAndAbsorption = Vector4.Zero;
            if (IsImportedCollarPreset(collarNode))
            {
                float envX = collarNode.ImportedMirrorX ? -1f : 1f;
                float envY = collarNode.ImportedMirrorY ? -1f : 1f;
                float envZ = collarNode.ImportedMirrorZ ? -1f : 1f;
                uniforms.EnvironmentMapParams2 = new Vector4(
                    uniforms.EnvironmentMapParams2.X * envX,
                    uniforms.EnvironmentMapParams2.Y * envY,
                    uniforms.EnvironmentMapParams2.Z * envZ,
                    uniforms.EnvironmentMapParams2.W);
            }
            return uniforms;
        }

        private static GpuUniforms BuildIndicatorLensUniforms(
            in GpuUniforms baseUniforms,
            Vector3 surfaceColor,
            float surfaceRoughness,
            float surfaceSpecularStrength,
            float transmission,
            float ior,
            float thickness,
            Vector3 tint,
            float absorption,
            Vector3 emissionColor,
            float emissionStrength)
        {
            GpuUniforms uniforms = BuildSliderPartUniforms(
                baseUniforms,
                surfaceColor,
                metallic: 0.00f,
                roughness: surfaceRoughness,
                pearlescence: 0.00f,
                diffuseStrength: 0.18f,
                specularStrength: surfaceSpecularStrength);
            uniforms.LensMaterialParams0 = new Vector4(
                1f,
                Math.Clamp(transmission, 0f, 1f),
                Math.Clamp(ior, 1f, 2.5f),
                Math.Clamp(thickness, 0f, 10f));
            uniforms.LensMaterialTintAndAbsorption = new Vector4(
                Math.Clamp(tint.X, 0f, 1f),
                Math.Clamp(tint.Y, 0f, 1f),
                Math.Clamp(tint.Z, 0f, 1f),
                Math.Clamp(absorption, 0f, 8f));
            uniforms.IndicatorColorAndBlend = new Vector4(
                Math.Clamp(emissionColor.X, 0f, 1f),
                Math.Clamp(emissionColor.Y, 0f, 1f),
                Math.Clamp(emissionColor.Z, 0f, 1f),
                Math.Clamp(emissionStrength, 0f, 192f));
            return uniforms;
        }

        private static GpuUniforms BuildIndicatorEmitterUniforms(
            in GpuUniforms baseUniforms,
            Vector3 emissionColor,
            float emissionStrength,
            float roughness = 0.12f)
        {
            GpuUniforms uniforms = BuildSliderPartUniforms(
                baseUniforms,
                baseColor: new Vector3(
                    Math.Clamp(emissionColor.X * 0.72f, 0f, 1f),
                    Math.Clamp(emissionColor.Y * 0.72f, 0f, 1f),
                    Math.Clamp(emissionColor.Z * 0.72f, 0f, 1f)),
                metallic: 0.02f,
                roughness: Math.Clamp(roughness, 0.04f, 1f),
                pearlescence: 0f,
                diffuseStrength: 1.15f,
                specularStrength: 0.40f);

            // Lighting mode 4 is reserved for emissive indicator emitter cores.
            uniforms.MaterialRoughnessDiffuseSpecMode.W = 4f;
            uniforms.IndicatorColorAndBlend = new Vector4(
                Math.Clamp(emissionColor.X, 0f, 1f),
                Math.Clamp(emissionColor.Y, 0f, 1f),
                Math.Clamp(emissionColor.Z, 0f, 1f),
                Math.Clamp(emissionStrength, 0f, 192f));
            return uniforms;
        }

        private static GpuUniforms BuildSliderPartUniforms(
            in GpuUniforms baseUniforms,
            Vector3 baseColor,
            float metallic,
            float roughness,
            float pearlescence,
            float diffuseStrength = 1f,
            float specularStrength = 1f,
            float rustAmount = 0f,
            float wearAmount = 0f,
            float gunkAmount = 0f,
            float surfaceBrushStrength = 0f,
            float surfaceBrushDensity = 56f,
            float surfaceCharacter = 0f,
            float anisotropyAngleRadians = 0f)
        {
            GpuUniforms uniforms = baseUniforms;
            Vector4 material = new(baseColor, Math.Clamp(metallic, 0f, 1f));
            float clampedRoughness = Math.Clamp(roughness, 0.04f, 1f);
            uniforms.MaterialBaseColorAndMetallic = material;
            uniforms.MaterialRoughnessDiffuseSpecMode.X = clampedRoughness;
            uniforms.MaterialRoughnessDiffuseSpecMode.Y = MathF.Max(0f, diffuseStrength);
            uniforms.MaterialRoughnessDiffuseSpecMode.Z = MathF.Max(0f, specularStrength);
            uniforms.MaterialPartTopColorAndMetallic = material;
            uniforms.MaterialPartBevelColorAndMetallic = material;
            uniforms.MaterialPartSideColorAndMetallic = material;
            uniforms.MaterialPartRoughnessAndEnable = new Vector4(clampedRoughness, clampedRoughness, clampedRoughness, 0f);
            uniforms.MaterialSurfaceBrushParams = new Vector4(
                Math.Clamp(surfaceBrushStrength, 0f, 1f),
                MathF.Max(1f, surfaceBrushDensity),
                Math.Clamp(surfaceCharacter, 0f, 1f),
                1f);
            uniforms.WeatherParams = new Vector4(
                Math.Clamp(rustAmount, 0f, 1f),
                Math.Clamp(wearAmount, 0f, 1f),
                Math.Clamp(gunkAmount, 0f, 1f),
                baseUniforms.WeatherParams.W);
            uniforms.AdvancedMaterialParams = new Vector4(
                uniforms.AdvancedMaterialParams.X,
                uniforms.AdvancedMaterialParams.Y,
                uniforms.AdvancedMaterialParams.Z,
                anisotropyAngleRadians);
            uniforms.IndicatorParams0 = Vector4.Zero;
            uniforms.IndicatorParams1 = new Vector4(0f, 0f, 0f, Math.Clamp(pearlescence, 0f, 1f));
            uniforms.IndicatorColorAndBlend = Vector4.Zero;
            uniforms.IndicatorParams2 = Vector4.Zero;
            uniforms.MicroDetailParams.X = 0f;
            uniforms.MicroDetailParams.W = 0f;
            return uniforms;
        }

        private static AssemblyPartMaterialPalette ResolveAssemblyPartMaterialPalette(MaterialNode? materialNode)
        {
            // Assembly materials follow the global material by default.
            // If part materials are enabled, map Side -> base/backplate and Top -> lever/thumb/cap.
            if (materialNode == null)
            {
                return new AssemblyPartMaterialPalette(
                    BaseColor: new Vector3(0.28f, 0.29f, 0.31f),
                    BaseMetallic: 0.82f,
                    BaseRoughness: 0.30f,
                    AccentColor: new Vector3(0.62f, 0.63f, 0.66f),
                    AccentMetallic: 0.94f,
                    AccentRoughness: 0.18f,
                    Pearlescence: 0.05f);
            }

            Vector3 globalColor = materialNode.BaseColor;
            float globalMetallic = Math.Clamp(materialNode.Metallic, 0f, 1f);
            float globalRoughness = Math.Clamp(materialNode.Roughness, 0.04f, 1f);
            float pearlescence = Math.Clamp(materialNode.Pearlescence, 0f, 1f);

            if (!materialNode.PartMaterialsEnabled)
            {
                return new AssemblyPartMaterialPalette(
                    BaseColor: globalColor,
                    BaseMetallic: globalMetallic,
                    BaseRoughness: globalRoughness,
                    AccentColor: globalColor,
                    AccentMetallic: globalMetallic,
                    AccentRoughness: globalRoughness,
                    Pearlescence: pearlescence);
            }

            return new AssemblyPartMaterialPalette(
                BaseColor: materialNode.SideBaseColor,
                BaseMetallic: Math.Clamp(materialNode.SideMetallic, 0f, 1f),
                BaseRoughness: Math.Clamp(materialNode.SideRoughness, 0.04f, 1f),
                AccentColor: materialNode.TopBaseColor,
                AccentMetallic: Math.Clamp(materialNode.TopMetallic, 0f, 1f),
                AccentRoughness: Math.Clamp(materialNode.TopRoughness, 0.04f, 1f),
                Pearlescence: pearlescence);
        }

        private readonly record struct AssemblyPartMaterialPalette(
            Vector3 BaseColor,
            float BaseMetallic,
            float BaseRoughness,
            Vector3 AccentColor,
            float AccentMetallic,
            float AccentRoughness,
            float Pearlescence);

        private static void LogCollarState(string pass, CollarNode? collarNode, MetalMeshGpuResources? collarResources)
        {
            _ = pass;
            _ = collarNode;
            _ = collarResources;
        }

        private Vector3 ApplyLightOrientation(Vector3 value)
        {
            if (_orientation.InvertX)
            {
                value.X = -value.X;
            }

            if (_orientation.InvertY)
            {
                value.Y = -value.Y;
            }

            if (_orientation.InvertZ)
            {
                value.Z = -value.Z;
            }

            return value;
        }

        private Vector3 ApplyGizmoDisplayOrientation(Vector3 value)
        {
            if (_gizmoInvertX)
            {
                value.X = -value.X;
            }

            if (_gizmoInvertY)
            {
                value.Y = -value.Y;
            }

            if (_gizmoInvertZ)
            {
                value.Z = -value.Z;
            }

            return value;
        }

        private static Vector3 GetDirectionalVector(KnobLight light)
        {
            float z = light.Z / 300f;
            Vector3 dir = new(MathF.Cos(light.DirectionRadians), MathF.Sin(light.DirectionRadians), z);
            if (dir.LengthSquared() < 1e-6f)
            {
                return Vector3.UnitZ;
            }

            return Vector3.Normalize(dir);
        }

        private static void SetGpuLight(ref GpuUniforms uniforms, int index, in GpuLight light)
        {
            switch (index)
            {
                case 0:
                    uniforms.Light0 = light;
                    break;
                case 1:
                    uniforms.Light1 = light;
                    break;
                case 2:
                    uniforms.Light2 = light;
                    break;
                case 3:
                    uniforms.Light3 = light;
                    break;
                case 4:
                    uniforms.Light4 = light;
                    break;
                case 5:
                    uniforms.Light5 = light;
                    break;
                case 6:
                    uniforms.Light6 = light;
                    break;
                case 7:
                    uniforms.Light7 = light;
                    break;
            }
        }

        private int PackDynamicLights(
            ref GpuUniforms uniforms,
            KnobProject project,
            double? animationTimeSecondsOverride = null)
        {
            // Indicator emitter rig is indicator-project-only. Do not bleed LED colors
            // into rotary/slider/switch/button scene lighting.
            if (project.ProjectType != InteractorProjectType.IndicatorLight)
            {
                return 0;
            }

            DynamicLightRig rig = project.DynamicLightRig;
            bool rigEnabled = rig.Enabled;
            if (!rigEnabled || rig.Sources.Count == 0)
            {
                return 0;
            }

            int maxActive = Math.Clamp(rig.MaxActiveLights, 1, MaxGpuLights);
            double animationTimeSeconds = animationTimeSecondsOverride
                ?? (Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
            int packedCount = 0;

            for (int sourceIndex = 0; sourceIndex < rig.Sources.Count && packedCount < maxActive; sourceIndex++)
            {
                DynamicLightSource source = rig.Sources[sourceIndex];
                if (!source.Enabled)
                {
                    continue;
                }

                float animatedIntensity = EvaluateDynamicLightIntensity(rig, source, sourceIndex, animationTimeSeconds);
                if (animatedIntensity <= 1e-5f)
                {
                    continue;
                }

                Vector3 lightPos = ApplyLightOrientation(new Vector3(source.X, source.Y, source.Z));
                GpuLight packed = new()
                {
                    PositionType = new Vector4(lightPos, 0f), // point light
                    Direction = new Vector4(Vector3.UnitZ, 0f),
                    ColorIntensity = new Vector4(
                        source.Color.Red / 255f,
                        source.Color.Green / 255f,
                        source.Color.Blue / 255f,
                        animatedIntensity),
                    Params0 = new Vector4(
                        MathF.Max(0f, source.Falloff),
                        1f, // diffuse boost
                        1f, // specular boost
                        MathF.Max(4f, source.Radius))
                };

                SetDynamicGpuLight(ref uniforms, packedCount, packed);
                packedCount++;
            }

            uniforms.DynamicLightParams = new Vector4(
                packedCount,
                (float)rig.AnimationMode,
                MathF.Max(0f, rig.AnimationSpeed),
                0f);
            return packedCount;
        }

        private static float EvaluateDynamicLightIntensity(
            DynamicLightRig rig,
            DynamicLightSource source,
            int sourceIndex,
            double timeSeconds)
        {
            float masterIntensity = ComputeIndicatorDynamicLightResponse(rig.MasterIntensity);
            float baseIntensity = MathF.Max(0f, source.Intensity) * masterIntensity;
            if (baseIntensity <= 1e-6f)
            {
                return 0f;
            }

            float speed = MathF.Max(0f, rig.AnimationSpeed);
            double t = timeSeconds * Math.Max(0.0001, speed);
            float modeMultiplier = 1f;
            float phaseOffsetRadians = source.AnimationPhaseOffsetDegrees * (MathF.PI / 180f);

            switch (rig.AnimationMode)
            {
                case DynamicLightAnimationMode.Pulse:
                {
                    float phase = (sourceIndex * 0.73f) + ((rig.FlickerSeed & 255) * 0.011f) + phaseOffsetRadians;
                    float wave = 0.5f + (0.5f * MathF.Sin((float)(t * Math.PI * 2.0) + phase));
                    modeMultiplier = 0.45f + (0.55f * wave);
                    break;
                }
                case DynamicLightAnimationMode.Flicker:
                {
                    float amount = Math.Clamp(rig.FlickerAmount, 0f, 1f);
                    float smoothing = Math.Clamp(rig.FlickerSmoothing, 0f, 1f);
                    float dropoutChance = Math.Clamp(rig.FlickerDropoutChance, 0f, 1f);
                    float cycles = (float)t;
                    float seeded = (rig.FlickerSeed & 4095) * 0.00087f;
                    float phaseA = phaseOffsetRadians + (sourceIndex * 0.79f) + seeded;
                    float phaseB = (phaseOffsetRadians * 1.47f) + (sourceIndex * 1.91f) + (seeded * 3.7f);
                    float phaseC = (phaseOffsetRadians * 0.63f) + (sourceIndex * 2.77f) + (seeded * 5.1f);

                    float wave1 = 0.5f + (0.5f * MathF.Sin((cycles * MathF.Tau) + phaseA));
                    float wave2 = 0.5f + (0.5f * MathF.Sin((cycles * MathF.Tau * 3f) + phaseB));
                    float wave3 = 0.5f + (0.5f * MathF.Sin((cycles * MathF.Tau * 7f) + phaseC));
                    float raw = (wave1 * 0.50f) + (wave2 * 0.30f) + (wave3 * 0.20f);

                    float shaped = smoothing + ((1f - smoothing) * raw);
                    float dropoutWave = 0.5f + (0.5f * MathF.Sin((cycles * MathF.Tau * 11f) + (phaseC * 1.3f)));
                    if (dropoutChance > 1e-5f && dropoutWave < dropoutChance)
                    {
                        float keep = dropoutWave / MathF.Max(1e-6f, dropoutChance);
                        shaped *= 0.2f + (0.8f * keep);
                    }

                    modeMultiplier = (1f - amount) + (amount * shaped);
                    break;
                }
                case DynamicLightAnimationMode.Custom:
                {
                    float phase = (sourceIndex * 1.17f) + ((rig.FlickerSeed & 1023) * 0.0039f) + phaseOffsetRadians;
                    float sine = 0.5f + (0.5f * MathF.Sin((float)(t * Math.PI * 2.0) + phase));
                    float triTime = (float)t + (sourceIndex * 0.19f) + (phaseOffsetRadians / (MathF.PI * 2f));
                    float tri = 1f - MathF.Abs((Wrap01(triTime) * 2f) - 1f);
                    modeMultiplier = 0.35f + (0.65f * ((sine * 0.65f) + (tri * 0.35f)));
                    break;
                }
                case DynamicLightAnimationMode.Steady:
                default:
                    modeMultiplier = 1f;
                    break;
            }

            return baseIntensity * MathF.Max(0f, modeMultiplier);
        }

        private static float ComputeIndicatorDynamicLightResponse(float sliderValue)
        {
            float value = MathF.Max(0f, sliderValue);
            if (value <= 1f)
            {
                return value;
            }

            // Keep point lights localized and controlled to avoid washing the base/housing.
            float over = value - 1f;
            return 1f + (over * 0.35f);
        }

        private static float ComputeIndicatorEmissiveResponse(float sliderValue)
        {
            // Keep low-end control linear, then aggressively boost upper range so max settings
            // can drive a visibly bright emitter through filmic tonemapping.
            float value = MathF.Max(0f, sliderValue);
            if (value <= 1f)
            {
                return value;
            }

            float over = value - 1f;
            return 1f + (over * (4f + (8f * over)));
        }

        private static float Wrap01(float value)
        {
            return value - MathF.Floor(value);
        }

        private static void SetDynamicGpuLight(ref GpuUniforms uniforms, int index, in GpuLight light)
        {
            switch (index)
            {
                case 0:
                    uniforms.DynamicLight0 = light;
                    break;
                case 1:
                    uniforms.DynamicLight1 = light;
                    break;
                case 2:
                    uniforms.DynamicLight2 = light;
                    break;
                case 3:
                    uniforms.DynamicLight3 = light;
                    break;
                case 4:
                    uniforms.DynamicLight4 = light;
                    break;
                case 5:
                    uniforms.DynamicLight5 = light;
                    break;
                case 6:
                    uniforms.DynamicLight6 = light;
                    break;
                case 7:
                    uniforms.DynamicLight7 = light;
                    break;
            }
        }

        private IntPtr EnsureUniformUploadScratchBuffer(int requiredSize, bool paintStamp)
        {
            if (requiredSize <= 0)
            {
                return IntPtr.Zero;
            }

            ref IntPtr targetBuffer = ref (paintStamp
                ? ref _paintStampUniformUploadScratch
                : ref _gpuUniformUploadScratch);
            ref int targetSize = ref (paintStamp
                ? ref _paintStampUniformUploadScratchSize
                : ref _gpuUniformUploadScratchSize);

            if (targetBuffer != IntPtr.Zero && targetSize >= requiredSize)
            {
                return targetBuffer;
            }

            if (targetBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(targetBuffer);
            }

            targetBuffer = Marshal.AllocHGlobal(requiredSize);
            targetSize = requiredSize;
            return targetBuffer;
        }

        private void ReleaseUniformUploadScratchBuffers()
        {
            if (_gpuUniformUploadScratch != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_gpuUniformUploadScratch);
                _gpuUniformUploadScratch = IntPtr.Zero;
                _gpuUniformUploadScratchSize = 0;
            }

            if (_paintStampUniformUploadScratch != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_paintStampUniformUploadScratch);
                _paintStampUniformUploadScratch = IntPtr.Zero;
                _paintStampUniformUploadScratchSize = 0;
            }
        }

        private void UploadUniforms(IntPtr encoderPtr, in GpuUniforms uniforms)
        {
            if (encoderPtr == IntPtr.Zero)
            {
                return;
            }

            int uniformSize = Marshal.SizeOf<GpuUniforms>();
            IntPtr uniformPtr = EnsureUniformUploadScratchBuffer(uniformSize, paintStamp: false);
            if (uniformPtr == IntPtr.Zero)
            {
                return;
            }

            Marshal.StructureToPtr(uniforms, uniformPtr, false);
            ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                encoderPtr,
                Selectors.SetVertexBytesLengthAtIndex,
                uniformPtr,
                (nuint)uniformSize,
                1);
            ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                encoderPtr,
                Selectors.SetFragmentBytesLengthAtIndex,
                uniformPtr,
                (nuint)uniformSize,
                1);
        }

        private static readonly Vector2[] ShadowSampleKernel =
        {
            new(0.0f, 0.0f),
            new(0.285f, -0.192f),
            new(-0.247f, 0.208f),
            new(0.118f, 0.326f),
            new(-0.332f, -0.087f),
            new(0.402f, 0.094f),
            new(-0.116f, -0.375f),
            new(0.046f, 0.462f),
            new(-0.463f, 0.041f),
            new(0.353f, -0.323f),
            new(-0.294f, -0.334f),
            new(0.214f, 0.452f),
            new(-0.027f, -0.497f),
            new(0.492f, -0.028f),
            new(-0.438f, 0.238f),
            new(0.165f, -0.468f)
        };

    }
}
