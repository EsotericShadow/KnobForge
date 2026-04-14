using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using KnobForge.Core;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using KnobForge.Rendering.GPU;
using SkiaSharp;

namespace KnobForge.Rendering;

public sealed partial class KnobExporter
{
        private static float ApplyCameraDistanceScaleToZoom(float zoom, float cameraDistanceScale)
        {
            return ViewportCameraFraming.ApplyCameraDistanceScaleToZoom(zoom, cameraDistanceScale);
        }

        private static Camera BuildExportCamera(
            float referenceRadius,
            KnobExportSettings settings,
            int outputResolution,
            int renderResolution,
            OrientationDebug orientation,
            ViewportCameraState? cameraState)
        {
            if (cameraState.HasValue)
            {
                ViewportCameraState state = cameraState.Value;
                float yaw = state.OrbitYawDeg * (MathF.PI / 180f);
                float pitch = Math.Clamp(state.OrbitPitchDeg, -85f, 85f) * (MathF.PI / 180f);
                Vector3 forward = Vector3.Normalize(new Vector3(
                    MathF.Sin(yaw) * MathF.Cos(pitch),
                    MathF.Sin(pitch),
                    -MathF.Cos(yaw) * MathF.Cos(pitch)));

                Vector3 worldUp = Vector3.UnitY;
                Vector3 right = Vector3.Cross(worldUp, forward);
                if (right.LengthSquared() < 1e-6f)
                {
                    right = Vector3.UnitX;
                }
                else
                {
                    right = Vector3.Normalize(right);
                }

                Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

                if (orientation.InvertX)
                {
                    right *= -1f;
                }

                if (orientation.InvertY)
                {
                    up *= -1f;
                }

                if (orientation.InvertZ)
                {
                    forward *= -1f;
                }

                if (orientation.FlipCamera180)
                {
                    forward = -forward;
                    right = -right;
                }

                float distance = MathF.Max(1f, referenceRadius) * 6f * MathF.Max(0.0001f, settings.CameraDistanceScale);
                Vector3 position = -forward * distance;
                float resolutionScale = renderResolution / (float)Math.Max(1, outputResolution);
                float zoom = Math.Clamp(state.Zoom * resolutionScale, 0.2f, 32f);
                SKPoint pan = new(state.PanPx.X * resolutionScale, state.PanPx.Y * resolutionScale);
                zoom = MathF.Min(zoom, ComputeSafeZoomForFrame(referenceRadius, renderResolution, settings.Padding * resolutionScale, pan));
                zoom = ApplyCameraDistanceScaleToZoom(zoom, settings.CameraDistanceScale);
                return new Camera(position, forward, right, up, zoom, pan);
            }

            // Fallback when launched without a live viewport state.
            Vector3 fallbackForward = new(0f, 0f, 1f);
            Vector3 fallbackWorldUp = new(0f, 1f, 0f);
            Vector3 fallbackRight = Vector3.Normalize(Vector3.Cross(fallbackWorldUp, fallbackForward));
            Vector3 fallbackUp = Vector3.Normalize(Vector3.Cross(fallbackForward, fallbackRight));
            float fallbackDistance = settings.CameraDistanceScale * MathF.Max(1f, referenceRadius);
            Vector3 fallbackPosition = -fallbackForward * fallbackDistance;
            float padding = MathF.Max(0f, settings.Padding);
            float contentPixels = MathF.Max(1f, renderResolution - (padding * 2f));
            float fallbackZoom = contentPixels / MathF.Max(1f, referenceRadius * 2f);
            fallbackZoom = ApplyCameraDistanceScaleToZoom(fallbackZoom, settings.CameraDistanceScale);
            return new Camera(fallbackPosition, fallbackForward, fallbackRight, fallbackUp, fallbackZoom, SKPoint.Empty);
        }

        private static ViewportCameraState BuildExportViewportCameraState(
            float referenceRadius,
            KnobExportSettings settings,
            int outputResolution,
            int renderResolution,
            ViewportCameraState? cameraState)
        {
            float yaw = cameraState?.OrbitYawDeg ?? 30f;
            float pitch = cameraState?.OrbitPitchDeg ?? -20f;
            return ViewportCameraFraming.BuildViewportCameraState(
                referenceRadius,
                outputResolution,
                renderResolution,
                settings.Padding,
                settings.CameraDistanceScale,
                yaw,
                pitch,
                cameraState);
        }

        private static float ComputeSafeZoomForFrame(
            float referenceRadius,
            int renderResolution,
            float paddingPx,
            SKPoint panPx)
        {
            return ViewportCameraFraming.ComputeSafeZoomForFrame(referenceRadius, renderResolution, paddingPx, panPx);
        }

        private float GetSceneReferenceRadius()
        {
            if (_referenceRadiusOverride.HasValue)
            {
                return MathF.Max(1f, _referenceRadiusOverride.Value);
            }

            float maxReferenceRadius = 1f;
            ModelNode? modelNode = _project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            maxReferenceRadius = MathF.Max(maxReferenceRadius, modelNode?.Radius ?? 1f);

            MetalMesh? mesh = MetalMeshBuilder.TryBuildFromProject(_project);
            if (mesh != null)
            {
                maxReferenceRadius = MathF.Max(maxReferenceRadius, mesh.ReferenceRadius);
            }

            CollarMesh? collarMesh = CollarMeshBuilder.TryBuildFromProject(_project);
            if (collarMesh != null)
            {
                maxReferenceRadius = MathF.Max(maxReferenceRadius, collarMesh.ReferenceRadius);
            }

            return maxReferenceRadius;
        }
}
