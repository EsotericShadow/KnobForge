using System;
using System.Collections.Generic;
using KnobForge.Core.Export;
using SkiaSharp;

namespace KnobForge.Rendering
{
    public static class ExportViewpointResolver
    {
        public static ExportViewpoint[] ResolveViewpoints(KnobExportSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var configured = new List<ExportViewpoint>();
            bool hasExplicitViewpointConfig = settings.ExportViewpoints != null && settings.ExportViewpoints.Count > 0;
            if (settings.ExportViewpoints != null)
            {
                for (int i = 0; i < settings.ExportViewpoints.Count; i++)
                {
                    ExportViewpoint? source = settings.ExportViewpoints[i];
                    if (source == null || !source.Enabled)
                    {
                        continue;
                    }

                    configured.Add(Clone(source));
                }
            }

            ExportViewpoint[] raw;
            if (hasExplicitViewpointConfig)
            {
                raw = configured.Count > 0
                    ? configured.ToArray()
                    : CreateLegacyViewpoints(false, 0f, 0f);
            }
            else
            {
                raw = CreateLegacyViewpoints(
                    settings.ExportOrbitVariants,
                    settings.OrbitVariantYawOffsetDeg,
                    settings.OrbitVariantPitchOffsetDeg);
            }

            return NormalizeAndDedupe(raw);
        }

        public static ExportViewpoint[] CreateLegacyViewpoints(
            bool includeOrbitVariants,
            float yawOffsetDeg,
            float pitchOffsetDeg)
        {
            float yaw = MathF.Abs(yawOffsetDeg);
            float pitch = MathF.Abs(pitchOffsetDeg);
            var variants = new List<ExportViewpoint>(5)
            {
                new ExportViewpoint
                {
                    Name = "Primary",
                    FileTag = string.Empty,
                    Order = 0,
                    Enabled = true,
                    YawOffsetDeg = 0f,
                    PitchOffsetDeg = 0f
                }
            };

            if (includeOrbitVariants)
            {
                variants.Add(new ExportViewpoint
                {
                    Name = "Under Left",
                    FileTag = "under_left",
                    Order = 1,
                    Enabled = true,
                    YawOffsetDeg = -yaw,
                    PitchOffsetDeg = pitch
                });
                variants.Add(new ExportViewpoint
                {
                    Name = "Under Right",
                    FileTag = "under_right",
                    Order = 2,
                    Enabled = true,
                    YawOffsetDeg = yaw,
                    PitchOffsetDeg = pitch
                });
                variants.Add(new ExportViewpoint
                {
                    Name = "Over Left",
                    FileTag = "over_left",
                    Order = 3,
                    Enabled = true,
                    YawOffsetDeg = -yaw,
                    PitchOffsetDeg = -pitch
                });
                variants.Add(new ExportViewpoint
                {
                    Name = "Over Right",
                    FileTag = "over_right",
                    Order = 4,
                    Enabled = true,
                    YawOffsetDeg = yaw,
                    PitchOffsetDeg = -pitch
                });
            }

            return variants.ToArray();
        }

        public static ViewportCameraState ApplyViewpoint(
            ViewportCameraState baseState,
            ExportViewpoint viewpoint)
        {
            if (viewpoint == null)
            {
                return baseState;
            }

            float yaw = viewpoint.UseAbsoluteCamera
                ? viewpoint.OrbitYawDeg
                : baseState.OrbitYawDeg + viewpoint.YawOffsetDeg;
            float pitch = viewpoint.UseAbsoluteCamera
                ? viewpoint.OrbitPitchDeg
                : baseState.OrbitPitchDeg + viewpoint.PitchOffsetDeg;
            pitch = Math.Clamp(pitch, -85f, 85f);

            float zoom = viewpoint.OverrideZoom ? viewpoint.Zoom : baseState.Zoom;
            zoom = Math.Clamp(zoom, 0.2f, 32f);

            SKPoint pan = viewpoint.OverridePan
                ? new SKPoint(viewpoint.PanXPx, viewpoint.PanYPx)
                : baseState.PanPx;

            return new ViewportCameraState(yaw, pitch, zoom, pan);
        }

        private static ExportViewpoint[] NormalizeAndDedupe(ExportViewpoint[] raw)
        {
            var indexed = new List<(ExportViewpoint View, int Index)>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                ExportViewpoint source = raw[i];
                if (source == null || !source.Enabled)
                {
                    continue;
                }

                indexed.Add((Clone(source), i));
            }

            if (indexed.Count == 0)
            {
                indexed.Add((CreateLegacyViewpoints(false, 0f, 0f)[0], 0));
            }

            indexed.Sort((a, b) =>
            {
                int byOrder = a.View.Order.CompareTo(b.View.Order);
                return byOrder != 0 ? byOrder : a.Index.CompareTo(b.Index);
            });

            var dedupeByTag = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dedupeByCamera = new HashSet<(int, int, int, int, int, int, int, int)>();
            var result = new List<ExportViewpoint>(indexed.Count);

            for (int i = 0; i < indexed.Count; i++)
            {
                ExportViewpoint view = indexed[i].View;
                view.Name = string.IsNullOrWhiteSpace(view.Name) ? $"View {i + 1}" : view.Name.Trim();
                view.FileTag = NormalizeFileTag(view.FileTag);
                if (i > 0 && string.IsNullOrWhiteSpace(view.FileTag))
                {
                    view.FileTag = $"view_{i + 1}";
                }

                if (!dedupeByTag.Add(view.FileTag))
                {
                    continue;
                }

                var cameraKey = BuildCameraKey(view);
                if (!dedupeByCamera.Add(cameraKey))
                {
                    continue;
                }

                result.Add(view);
            }

            if (result.Count == 0)
            {
                result.Add(CreateLegacyViewpoints(false, 0f, 0f)[0]);
            }

            return result.ToArray();
        }

        private static (int, int, int, int, int, int, int, int) BuildCameraKey(ExportViewpoint view)
        {
            return (
                Quantize(view.UseAbsoluteCamera ? view.OrbitYawDeg : view.YawOffsetDeg),
                Quantize(view.UseAbsoluteCamera ? view.OrbitPitchDeg : view.PitchOffsetDeg),
                view.UseAbsoluteCamera ? 1 : 0,
                view.OverrideZoom ? Quantize(view.Zoom) : int.MinValue,
                view.OverridePan ? Quantize(view.PanXPx) : int.MinValue,
                view.OverridePan ? Quantize(view.PanYPx) : int.MinValue,
                view.OverrideZoom ? 1 : 0,
                view.OverridePan ? 1 : 0);
        }

        private static int Quantize(float value)
        {
            return (int)MathF.Round(value * 1000f);
        }

        private static string NormalizeFileTag(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            Span<char> buffer = stackalloc char[trimmed.Length];
            int write = 0;
            bool wroteUnderscore = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (char.IsLetterOrDigit(c))
                {
                    buffer[write++] = char.ToLowerInvariant(c);
                    wroteUnderscore = false;
                }
                else if (!wroteUnderscore)
                {
                    buffer[write++] = '_';
                    wroteUnderscore = true;
                }
            }

            string normalized = new(buffer[..write]);
            return normalized.Trim('_');
        }

        private static ExportViewpoint Clone(ExportViewpoint source)
        {
            return new ExportViewpoint
            {
                Name = source.Name,
                FileTag = source.FileTag,
                Enabled = source.Enabled,
                Order = source.Order,
                YawOffsetDeg = source.YawOffsetDeg,
                PitchOffsetDeg = source.PitchOffsetDeg,
                UseAbsoluteCamera = source.UseAbsoluteCamera,
                OrbitYawDeg = source.OrbitYawDeg,
                OrbitPitchDeg = source.OrbitPitchDeg,
                OverrideZoom = source.OverrideZoom,
                Zoom = source.Zoom,
                OverridePan = source.OverridePan,
                PanXPx = source.PanXPx,
                PanYPx = source.PanYPx
            };
        }
    }
}
