using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private const double BrushDockMargin = 12d;

        private void WireBrushDockDrag()
        {
            if (_brushDockDragHandle != null)
            {
                _brushDockDragHandle.PointerPressed += BrushDockDragHandle_PointerPressed;
                _brushDockDragHandle.PointerMoved += BrushDockDragHandle_PointerMoved;
                _brushDockDragHandle.PointerReleased += BrushDockDragHandle_PointerReleased;
                _brushDockDragHandle.PointerCaptureLost += BrushDockDragHandle_PointerCaptureLost;
            }

            if (_viewportHostBorder != null)
            {
                _viewportHostBorder.SizeChanged += (_, _) => ClampBrushDockToViewport();
            }

            // Set default position after first layout
            if (_brushDockPopup != null)
            {
                _brushDockPopup.Opened += (_, _) =>
                {
                    if (!_brushDockDefaultPositionSet)
                    {
                        SetBrushDockDefaultPosition();
                        _brushDockDefaultPositionSet = true;
                    }
                };
            }
        }

        public void ToggleBrushDockVisibility()
        {
            if (GetModelNode() == null)
            {
                return;
            }

            _brushDockHiddenByUser = !_brushDockHiddenByUser;
            UpdateBrushQuickToolbarState();
        }

        private void BrushDockDragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_brushDockDragHandle == null ||
                _brushDockPopup == null ||
                !e.GetCurrentPoint(_brushDockDragHandle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _isBrushDockDragging = true;
            // Track position relative to the main window for consistent delta calculation
            _brushDockDragStart = e.GetPosition(this);
            e.Pointer.Capture(_brushDockDragHandle);
            e.Handled = true;
        }

        private void BrushDockDragHandle_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isBrushDockDragging || _brushDockPopup == null)
            {
                return;
            }

            Point current = e.GetPosition(this);
            double dx = current.X - _brushDockDragStart.X;
            double dy = current.Y - _brushDockDragStart.Y;
            _brushDockDragStart = current;

            double newOffsetX = _brushDockPopup.HorizontalOffset + dx;
            double newOffsetY = _brushDockPopup.VerticalOffset + dy;

            (newOffsetX, newOffsetY) = ClampPopupOffset(newOffsetX, newOffsetY);

            _brushDockPopup.HorizontalOffset = newOffsetX;
            _brushDockPopup.VerticalOffset = newOffsetY;
            e.Handled = true;
        }

        private void BrushDockDragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isBrushDockDragging)
            {
                return;
            }

            _isBrushDockDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void BrushDockDragHandle_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isBrushDockDragging = false;
        }

        private void SetBrushDockDefaultPosition()
        {
            if (_brushDockPopup == null || _viewportHostBorder == null || _viewportBrushDock == null)
            {
                return;
            }

            double viewportH = _viewportHostBorder.Bounds.Height;
            double dockH = _viewportBrushDock.Bounds.Height;

            if (viewportH <= 0)
            {
                return;
            }

            // Default: bottom-left, inset by margin
            // Popup anchor is TopLeft of ViewportHostBorder, gravity is BottomRight
            // So offset (12, 12) puts it at top-left + 12px inset
            // For bottom-left: Y = viewportH - dockH - margin
            double defaultX = BrushDockMargin;
            double defaultY = Math.Max(BrushDockMargin, viewportH - dockH - BrushDockMargin);

            _brushDockPopup.HorizontalOffset = defaultX;
            _brushDockPopup.VerticalOffset = defaultY;
        }

        private (double x, double y) ClampPopupOffset(double x, double y)
        {
            if (_viewportHostBorder == null || _viewportBrushDock == null)
            {
                return (x, y);
            }

            double viewportW = _viewportHostBorder.Bounds.Width;
            double viewportH = _viewportHostBorder.Bounds.Height;
            double dockW = _viewportBrushDock.Bounds.Width;
            double dockH = _viewportBrushDock.Bounds.Height;

            if (viewportW <= 0 || viewportH <= 0)
            {
                return (x, y);
            }

            double maxX = Math.Max(BrushDockMargin, viewportW - dockW - BrushDockMargin);
            double maxY = Math.Max(BrushDockMargin, viewportH - dockH - BrushDockMargin);

            x = Math.Clamp(x, BrushDockMargin, maxX);
            y = Math.Clamp(y, BrushDockMargin, maxY);

            return (x, y);
        }

        private void ClampBrushDockToViewport()
        {
            if (_brushDockPopup == null)
            {
                return;
            }

            (double clampedX, double clampedY) = ClampPopupOffset(
                _brushDockPopup.HorizontalOffset,
                _brushDockPopup.VerticalOffset);

            _brushDockPopup.HorizontalOffset = clampedX;
            _brushDockPopup.VerticalOffset = clampedY;
        }

        private void EnsureBrushDockDefaultPosition()
        {
            if (_brushDockDefaultPositionSet)
            {
                return;
            }

            if (_brushDockPopup != null && _viewportHostBorder != null &&
                _viewportHostBorder.Bounds.Height > 0)
            {
                SetBrushDockDefaultPosition();
                _brushDockDefaultPositionSet = true;
            }
        }
    }
}
