using System;
using SkiaSharp;

namespace KnobForge.Rendering;

public static class ViewportCameraFraming
{
    public static ViewportCameraState BuildViewportCameraState(
        float referenceRadius,
        int outputResolution,
        int renderResolution,
        float padding,
        float cameraDistanceScale,
        float yawDeg,
        float pitchDeg,
        ViewportCameraState? seedCameraState)
    {
        if (seedCameraState.HasValue)
        {
            ViewportCameraState state = seedCameraState.Value;
            float resolutionScale = renderResolution / (float)Math.Max(1, outputResolution);
            float zoom = Math.Clamp(state.Zoom * resolutionScale, 0.2f, 32f);
            SKPoint pan = new(state.PanPx.X * resolutionScale, state.PanPx.Y * resolutionScale);
            zoom = MathF.Min(zoom, ComputeSafeZoomForFrame(referenceRadius, renderResolution, padding * resolutionScale, pan));
            zoom = ApplyCameraDistanceScaleToZoom(zoom, cameraDistanceScale);
            return new ViewportCameraState(yawDeg, pitchDeg, zoom, pan);
        }

        float paddingPx = MathF.Max(0f, padding);
        float contentPixels = MathF.Max(1f, renderResolution - (paddingPx * 2f));
        float fallbackZoom = contentPixels / MathF.Max(1f, referenceRadius * 2f);
        fallbackZoom = ApplyCameraDistanceScaleToZoom(fallbackZoom, cameraDistanceScale);
        return new ViewportCameraState(yawDeg, pitchDeg, fallbackZoom, SKPoint.Empty);
    }

    public static float ApplyCameraDistanceScaleToZoom(float zoom, float cameraDistanceScale)
    {
        float safeDistanceScale = MathF.Max(0.0001f, cameraDistanceScale);
        return Math.Clamp(zoom / safeDistanceScale, 0.2f, 32f);
    }

    public static float ComputeSafeZoomForFrame(
        float referenceRadius,
        int renderResolution,
        float paddingPx,
        SKPoint panPx)
    {
        float radius = MathF.Max(1f, referenceRadius);
        float halfWidthAvailable = MathF.Max(1f, (renderResolution * 0.5f) - paddingPx - MathF.Abs(panPx.X));
        float halfHeightAvailable = MathF.Max(1f, (renderResolution * 0.5f) - paddingPx - MathF.Abs(panPx.Y));
        float halfSpan = MathF.Min(halfWidthAvailable, halfHeightAvailable);
        // Leave a small guard band so rotating protrusions do not clip during offscreen capture.
        return MathF.Max(0.2f, (halfSpan * 0.96f) / radius);
    }
}
