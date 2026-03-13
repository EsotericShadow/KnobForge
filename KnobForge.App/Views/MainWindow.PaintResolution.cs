using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private static readonly int[] SupportedPaintMaskResolutions = { 512, 1024, 2048, 4096 };

        private bool _paintResolutionUiInitialized;

        private void InitializePaintResolutionUx()
        {
            if (_paintResolutionUiInitialized || _paintMaskResolutionCombo == null)
            {
                return;
            }

            _paintResolutionUiInitialized = true;
            _paintMaskResolutionCombo.ItemsSource = SupportedPaintMaskResolutions;
            _paintMaskResolutionCombo.SelectionChanged += OnPaintMaskResolutionSelectionChanged;
            UpdatePaintResolutionUi();
        }

        private void UpdatePaintResolutionUi()
        {
            if (_paintMaskResolutionCombo != null)
            {
                int currentSize = _project.PaintMaskSize;
                if (_paintMaskResolutionCombo.SelectedItem is not int selectedSize || selectedSize != currentSize)
                {
                    _paintMaskResolutionCombo.SelectedItem = currentSize;
                }
            }

            if (_paintMaskResolutionMemoryText != null)
            {
                int layerCount = Math.Max(1, _metalViewport?.GetPaintLayers().Count ?? 1);
                double perMaskMb = GetPaintMaskMegabytes(_project.PaintMaskSize);
                _paintMaskResolutionMemoryText.Text =
                    $"{layerCount} layers x {_project.PaintMaskSize}px = ~{perMaskMb * layerCount:0} MB mask data";
            }
        }

        private async void OnPaintMaskResolutionSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingUi || _paintMaskResolutionCombo == null)
            {
                return;
            }

            if (_paintMaskResolutionCombo.SelectedItem is not int requestedSize)
            {
                UpdatePaintResolutionUi();
                return;
            }

            int currentSize = _project.PaintMaskSize;
            if (requestedSize == currentSize)
            {
                UpdatePaintResolutionUi();
                return;
            }

            bool confirmed = await ShowPaintMaskResolutionConfirmDialogAsync(currentSize, requestedSize);
            if (!confirmed)
            {
                WithUiRefreshSuppressed(() => _paintMaskResolutionCombo.SelectedItem = currentSize);
                UpdatePaintResolutionUi();
                return;
            }

            _project.SetPaintMaskResolution(requestedSize);
            if (_metalViewport != null)
            {
                _metalViewport.DiscardPendingPaintStamps();
                _metalViewport.RequestClearPaintColorTexture();
                _metalViewport.ResetPaintStateForMaskResize();
                _metalViewport.InvalidateGpu();
            }

            UpdatePaintResolutionUi();
            NotifyRenderOnly();
            InitializeUndoRedoHistory(resetStacks: true);
        }

        private async Task<bool> ShowPaintMaskResolutionConfirmDialogAsync(int currentSize, int nextSize)
        {
            bool confirmed = false;
            var dialog = new Window
            {
                Title = "Change Paint Mask Resolution",
                Width = 560,
                Height = 260,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var confirmButton = new Button
            {
                Content = "Change Resolution",
                MinWidth = 132
            };
            confirmButton.Click += (_, _) =>
            {
                confirmed = true;
                dialog.Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 90
            };
            cancelButton.Click += (_, _) => dialog.Close();

            dialog.Content = new Grid
            {
                Margin = new Thickness(16),
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"Changing the paint mask from {currentSize}px to {nextSize}px clears all paint data.",
                                TextWrapping = TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = $"Current mask memory: {FormatPaintMaskMemory(currentSize)}",
                                Foreground = Brush.Parse("#A9B4BF")
                            },
                            new TextBlock
                            {
                                Text = $"New mask memory: {FormatPaintMaskMemory(nextSize)}",
                                Foreground = Brush.Parse("#A9B4BF")
                            }
                        }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, confirmButton },
                        [Grid.RowProperty] = 1
                    }
                }
            };

            await dialog.ShowDialog(this);
            return confirmed;
        }

        private static string FormatPaintMaskMemory(int size)
        {
            return $"~{GetPaintMaskMegabytes(size):0} MB";
        }

        private static double GetPaintMaskMegabytes(int size)
        {
            double bytes = size * size * 4d;
            return bytes / (1024d * 1024d);
        }
    }
}
