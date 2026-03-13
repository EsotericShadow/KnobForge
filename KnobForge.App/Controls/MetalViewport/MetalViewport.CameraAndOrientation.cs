using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using SkiaSharp;

namespace KnobForge.App.Controls
{
    public sealed partial class MetalViewport
    {
        private bool TryScreenToScene(SKPoint screenPoint, out SKPoint scenePoint)
        {
            GetScreenCenterPx(out float centerX, out float centerY);
            GetCameraBasis(out Vector3 right, out Vector3 up, out _);

            float m00 = _zoom * right.X;
            float m01 = _zoom * right.Y;
            float m10 = _zoom * up.X;
            float m11 = _zoom * up.Y;
            float det = m00 * m11 - m01 * m10;
            if (MathF.Abs(det) < 1e-5f)
            {
                scenePoint = SKPoint.Empty;
                return false;
            }

            float dx = screenPoint.X - centerX;
            float dy = screenPoint.Y - centerY;
            float sx = (dx * m11 - m01 * dy) / det;
            float sy = (m00 * dy - dx * m10) / det;
            scenePoint = new SKPoint(sx, sy);
            return true;
        }

        private void GetCameraBasis(out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            float yaw = DegreesToRadians(_orbitYawDeg);
            float pitch = DegreesToRadians(_orbitPitchDeg);
            forward = Vector3.Normalize(new Vector3(
                MathF.Sin(yaw) * MathF.Cos(pitch),
                MathF.Sin(pitch),
                -MathF.Cos(yaw) * MathF.Cos(pitch)));

            Vector3 worldUp = Vector3.UnitY;
            right = Vector3.Cross(worldUp, forward);
            if (right.LengthSquared() < 1e-6f)
            {
                right = Vector3.UnitX;
            }
            else
            {
                right = Vector3.Normalize(right);
            }

            up = Vector3.Normalize(Vector3.Cross(forward, right));

            if (_orientation.InvertX)
            {
                right *= -1f;
            }

            if (_orientation.InvertY)
            {
                up *= -1f;
            }

            if (_orientation.InvertZ)
            {
                forward *= -1f;
            }

            if (_orientation.FlipCamera180)
            {
                forward = -forward;
                right = -right;
            }
        }

        private bool ResolveFrontFacingClockwise(Vector3 right, Vector3 up, Vector3 forward)
        {
            // In this renderer we project manually using camera right/up vectors.
            // If the basis is mirrored, front-face winding must be flipped to keep outward culling stable.
            float handedness = Vector3.Dot(Vector3.Cross(right, up), forward);
            return handedness < 0f;
        }

        private static SKPoint ProjectSceneOffset(SKPoint scene, float zoom, Vector3 right, Vector3 up)
        {
            float x = zoom * (scene.X * right.X + scene.Y * right.Y);
            float y = -zoom * (scene.X * up.X + scene.Y * up.Y);
            return new SKPoint(x, y);
        }

        private void ShowOrientationContextMenu(Point pointerDip)
        {
            _debugContextMenu?.Close();

            CollarNode? collarNode = GetDebugCollarNode();
            bool hasImportedCollar = IsImportedCollarPreset(collarNode);

            var cameraMenu = CreateSubmenu(
                "Camera Basis",
                CreateToggleMenuItem("Invert X", _orientation.InvertX, () => _orientation.InvertX = !_orientation.InvertX),
                CreateToggleMenuItem("Invert Y", _orientation.InvertY, () => _orientation.InvertY = !_orientation.InvertY),
                CreateToggleMenuItem("Invert Z", _orientation.InvertZ, () => _orientation.InvertZ = !_orientation.InvertZ),
                new Separator(),
                CreateToggleMenuItem("Flip Camera 180°", _orientation.FlipCamera180, () => _orientation.FlipCamera180 = !_orientation.FlipCamera180));

            var gizmoMenu = CreateSubmenu(
                "Gizmo Overlay",
                CreateToggleMenuItem("Invert X", _gizmoInvertX, () => _gizmoInvertX = !_gizmoInvertX),
                CreateToggleMenuItem("Invert Y", _gizmoInvertY, () => _gizmoInvertY = !_gizmoInvertY),
                CreateToggleMenuItem("Invert Z", _gizmoInvertZ, () => _gizmoInvertZ = !_gizmoInvertZ));

            var brushMenu = CreateSubmenu(
                "Brush / Paint Mapping",
                CreateToggleMenuItem("Invert X", _brushInvertX, () => _brushInvertX = !_brushInvertX),
                CreateToggleMenuItem("Invert Y", _brushInvertY, () => _brushInvertY = !_brushInvertY),
                CreateToggleMenuItem("Invert Z (Depth)", _brushInvertZ, () => _brushInvertZ = !_brushInvertZ),
                new Separator(),
                CreateActionMenuItem("Mirror Saved Paint X", () => MirrorPaintHistoryUvs(mirrorX: true, mirrorY: false), invalidateGpu: false),
                CreateActionMenuItem("Mirror Saved Paint Y", () => MirrorPaintHistoryUvs(mirrorX: false, mirrorY: true), invalidateGpu: false),
                CreateActionMenuItem("Mirror Saved Paint X+Y", () => MirrorPaintHistoryUvs(mirrorX: true, mirrorY: true), invalidateGpu: false));

            var lightEffectsMenu = CreateSubmenu(
                "Light Effects / Env Lookup",
                CreateToggleMenuItem("Invert X", _lightEffectInvertX, () => _lightEffectInvertX = !_lightEffectInvertX),
                CreateToggleMenuItem("Invert Y", _lightEffectInvertY, () => _lightEffectInvertY = !_lightEffectInvertY),
                CreateToggleMenuItem("Invert Z", _lightEffectInvertZ, () => _lightEffectInvertZ = !_lightEffectInvertZ));

            var bloomMenu = CreateSubmenu(
                "Bloom / Post-Process",
                CreateToggleMenuItem("Composite Invert X", _bloomCompositeInvertX, () => _bloomCompositeInvertX = !_bloomCompositeInvertX),
                CreateToggleMenuItem("Composite Invert Y", _bloomCompositeInvertY, () => _bloomCompositeInvertY = !_bloomCompositeInvertY));

            var collarMenu = CreateSubmenu(
                "Collar Mesh / Compensation",
                CreateReadOnlyToggleMenuItem(
                    hasImportedCollar ? "Project Mirror X" : "Project Mirror X (No Imported Collar)",
                    hasImportedCollar && collarNode!.ImportedMirrorX),
                CreateReadOnlyToggleMenuItem(
                    hasImportedCollar ? "Project Mirror Y" : "Project Mirror Y (No Imported Collar)",
                    hasImportedCollar && collarNode!.ImportedMirrorY),
                CreateReadOnlyToggleMenuItem(
                    hasImportedCollar ? "Project Mirror Z" : "Project Mirror Z (No Imported Collar)",
                    hasImportedCollar && collarNode!.ImportedMirrorZ),
                new Separator(),
                CreateToggleMenuItem("Compensation Flip X", _collarCompensationInvertX, () => _collarCompensationInvertX = !_collarCompensationInvertX),
                CreateToggleMenuItem("Compensation Flip Y", _collarCompensationInvertY, () => _collarCompensationInvertY = !_collarCompensationInvertY),
                CreateToggleMenuItem("Compensation Flip Z", _collarCompensationInvertZ, () => _collarCompensationInvertZ = !_collarCompensationInvertZ),
                new Separator(),
                CreateReadOnlyToggleMenuItem("Invert Collar Orbit (Imported Mesh) [Locked Off]", false),
                CreateToggleMenuItem(
                    "Invert Front-Face Winding (Imported Mesh Collar)",
                    _invertImportedStlFrontFaceWinding,
                    () => _invertImportedStlFrontFaceWinding = !_invertImportedStlFrontFaceWinding));

            var geometryMenu = CreateSubmenu(
                "Geometry / Winding",
                CreateToggleMenuItem(
                    "Invert Front-Face Winding (Knob)",
                    _invertKnobFrontFaceWinding,
                    () => _invertKnobFrontFaceWinding = !_invertKnobFrontFaceWinding));

            var printItem = CreateActionMenuItem("Print Debug State", PrintOrientation);
            var resetItem = CreateActionMenuItem("Reset Orientation / Debug Axes", ResetOrientationDebugState, invalidateGpu: true);

            _debugContextMenu = new ContextMenu
            {
                Items =
                {
                    cameraMenu,
                    gizmoMenu,
                    brushMenu,
                    lightEffectsMenu,
                    bloomMenu,
                    collarMenu,
                    geometryMenu,
                    new Separator(),
                    printItem,
                    resetItem
                },
                Placement = PlacementMode.Pointer,
                PlacementRect = new Rect(pointerDip, new Size(1, 1))
            };

            _debugContextMenu.Open(this);
        }

        private void PrintOrientation()
        {
            Console.WriteLine("---- Orientation Debug ----");
            Console.WriteLine($"InvertX: {_orientation.InvertX}");
            Console.WriteLine($"InvertY: {_orientation.InvertY}");
            Console.WriteLine($"InvertZ: {_orientation.InvertZ}");
            Console.WriteLine($"GizmoInvertX: {_gizmoInvertX}");
            Console.WriteLine($"GizmoInvertY: {_gizmoInvertY}");
            Console.WriteLine($"GizmoInvertZ: {_gizmoInvertZ}");
            Console.WriteLine($"BrushInvertX: {_brushInvertX}");
            Console.WriteLine($"BrushInvertY: {_brushInvertY}");
            Console.WriteLine($"BrushInvertZ: {_brushInvertZ}");
            Console.WriteLine($"LightEffectInvertX: {_lightEffectInvertX}");
            Console.WriteLine($"LightEffectInvertY: {_lightEffectInvertY}");
            Console.WriteLine($"LightEffectInvertZ: {_lightEffectInvertZ}");
            Console.WriteLine($"CollarCompensationInvertX: {_collarCompensationInvertX}");
            Console.WriteLine($"CollarCompensationInvertY: {_collarCompensationInvertY}");
            Console.WriteLine($"CollarCompensationInvertZ: {_collarCompensationInvertZ}");
            Console.WriteLine($"BloomCompositeInvertX: {_bloomCompositeInvertX}");
            Console.WriteLine($"BloomCompositeInvertY: {_bloomCompositeInvertY}");
            Console.WriteLine($"FlipCamera180: {_orientation.FlipCamera180}");
            Console.WriteLine($"InvertImportedCollarOrbit: {_invertImportedCollarOrbit}");
            Console.WriteLine($"InvertKnobFrontFaceWinding: {_invertKnobFrontFaceWinding}");
            Console.WriteLine($"InvertImportedStlFrontFaceWinding: {_invertImportedStlFrontFaceWinding}");
            CollarNode? collarNode = GetDebugCollarNode();
            if (IsImportedCollarPreset(collarNode) && collarNode is not null)
            {
                Console.WriteLine($"ProjectCollarMirrorX: {collarNode.ImportedMirrorX}");
                Console.WriteLine($"ProjectCollarMirrorY: {collarNode.ImportedMirrorY}");
                Console.WriteLine($"ProjectCollarMirrorZ: {collarNode.ImportedMirrorZ}");
            }

            Console.WriteLine("---------------------------");
        }

        private void ResetOrientationDebugState()
        {
            _orientation = new OrientationDebug
            {
                InvertX = true,
                InvertY = false,
                InvertZ = true,
                FlipCamera180 = true
            };
            _gizmoInvertX = false;
            _gizmoInvertY = false;
            _gizmoInvertZ = false;
            _brushInvertX = false;
            _brushInvertY = true;
            _brushInvertZ = false;
            _lightEffectInvertX = true;
            _lightEffectInvertY = true;
            _lightEffectInvertZ = false;
            _collarCompensationInvertX = false;
            _collarCompensationInvertY = false;
            _collarCompensationInvertZ = false;
            _bloomCompositeInvertX = false;
            _bloomCompositeInvertY = false;
            _invertImportedCollarOrbit = false;
            _invertKnobFrontFaceWinding = true;
            _invertImportedStlFrontFaceWinding = true;
            PrintOrientation();
        }

        private MenuItem CreateToggleMenuItem(string header, bool isChecked, Action onToggle)
        {
            var item = new MenuItem
            {
                Header = header,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = isChecked
            };
            item.Click += (_, _) =>
            {
                onToggle();
                PrintOrientation();
                InvalidateGpu();
            };
            return item;
        }

        private static MenuItem CreateReadOnlyToggleMenuItem(string header, bool isChecked)
        {
            return new MenuItem
            {
                Header = header,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = isChecked,
                IsEnabled = false
            };
        }

        private static MenuItem CreateSubmenu(string header, params object[] items)
        {
            var item = new MenuItem
            {
                Header = header
            };

            foreach (object child in items)
            {
                item.Items.Add(child);
            }

            return item;
        }

        private MenuItem CreateActionMenuItem(string header, Action onClick, bool invalidateGpu = false)
        {
            var item = new MenuItem
            {
                Header = header
            };
            item.Click += (_, _) =>
            {
                onClick();
                if (invalidateGpu)
                {
                    InvalidateGpu();
                }
            };
            return item;
        }

        private CollarNode? GetDebugCollarNode()
        {
            ModelNode? modelNode = _project?.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            return modelNode?.Children.OfType<CollarNode>().FirstOrDefault();
        }

        private float GetRenderScale()
        {
            return (float)(TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);
        }

        private float GetViewportWidthPx()
        {
            return (float)(Bounds.Width * GetRenderScale());
        }

        private float GetViewportHeightPx()
        {
            return (float)(Bounds.Height * GetRenderScale());
        }

        private void GetScreenCenterPx(out float centerX, out float centerY)
        {
            centerX = GetViewportWidthPx() * 0.5f + _panPx.X;
            centerY = GetViewportHeightPx() * 0.5f + _panPx.Y;
        }

        private SKPoint DipToScreen(Point dipPoint)
        {
            float scale = GetRenderScale();
            return new SKPoint((float)dipPoint.X * scale, (float)dipPoint.Y * scale);
        }

        private static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180f);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            if (edge1 <= edge0)
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - (2f * t));
        }

        private static IntPtr ToNSString(string value)
        {
            IntPtr utf8Ptr = Marshal.StringToHGlobalAnsi(value);
            try
            {
                return ObjC.IntPtr_objc_msgSend_IntPtr(ObjCClasses.NSString, Selectors.StringWithUTF8String, utf8Ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(utf8Ptr);
            }
        }

        private static string DescribeNSError(IntPtr error)
        {
            if (error == IntPtr.Zero)
            {
                return "unknown error";
            }

            IntPtr description = ObjC.IntPtr_objc_msgSend(error, Selectors.LocalizedDescription);
            if (description == IntPtr.Zero)
            {
                return $"NSError(0x{error.ToInt64():X})";
            }

            IntPtr utf8 = ObjC.IntPtr_objc_msgSend(description, Selectors.UTF8String);
            if (utf8 == IntPtr.Zero)
            {
                return $"NSError(0x{error.ToInt64():X})";
            }

            return Marshal.PtrToStringAnsi(utf8) ?? $"NSError(0x{error.ToInt64():X})";
        }

        private static void LogPaintStampError(string message)
        {
            Console.Error.WriteLine($"[MetalViewport] {message}");
        }
    }
}
