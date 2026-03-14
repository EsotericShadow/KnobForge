using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System;
using System.Threading.Tasks;
using ShapeEllipse = Avalonia.Controls.Shapes.Ellipse;
using ShapePath = Avalonia.Controls.Shapes.Path;
using ShapeRectangle = Avalonia.Controls.Shapes.Rectangle;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private const string Surface0Hex = "#0F1317";
        private const string Surface1Hex = "#141820";
        private const string Surface2Hex = "#1A1F28";
        private const string Surface3Hex = "#222830";
        private const string BorderSubtleHex = "#252C35";
        private const string BorderDefaultHex = "#2E3640";
        private const string BorderStrongHex = "#3A4450";
        private const string TextPrimaryHex = "#E2EAF2";
        private const string TextSecondaryHex = "#A8B4C0";
        private const string AccentHex = "#4A90B8";
        private const string AccentSubtleHex = "#2A4A60";

        private async void ChangeProjectTypeFromMenu()
        {
            InteractorProjectType? selectedType = await ShowProjectTypeChangePickerAsync();
            if (!selectedType.HasValue || selectedType.Value == _project.ProjectType)
            {
                return;
            }

            InteractorProjectType targetType = selectedType.Value;
            if (!await ConfirmProjectTypeChangeAsync(targetType))
            {
                return;
            }

            bool selectedNodeWasCollar = _project.SelectedNode is CollarNode;
            _project.ApplyInteractorProjectTypeDefaults(targetType);
            _project.EnsureSelection();

            if (selectedNodeWasCollar && targetType != InteractorProjectType.RotaryKnob)
            {
                _project.SetSelectedNode(_project.EnsureModelNode());
            }

            NotifyProjectStateChanged(
                tabPolicy: InspectorRefreshTabPolicy.FollowSceneSelection,
                syncSelectionFromInspectorContext: false);
        }

        private async Task<InteractorProjectType?> ShowProjectTypeChangePickerAsync()
        {
            InteractorProjectType currentType = _project.ProjectType;
            InteractorProjectType? selectedType = null;

            var dialog = new Window
            {
                Title = "Change Project Type",
                Width = 580,
                Height = 560,
                MinWidth = 540,
                MinHeight = 500,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushFromHex(Surface0Hex)
            };

            var root = new Grid
            {
                Margin = new Thickness(28, 24, 28, 20),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto")
            };

            var titleBlock = new TextBlock
            {
                Text = "Change Project Type",
                FontSize = 22,
                FontWeight = FontWeight.SemiBold,
                Foreground = BrushFromHex(TextPrimaryHex)
            };
            Grid.SetRow(titleBlock, 0);
            root.Children.Add(titleBlock);

            var subtitleBlock = new TextBlock
            {
                Text = $"Current: {GetProjectTypeDisplayName(currentType)}",
                FontSize = 12,
                Foreground = BrushFromHex(TextSecondaryHex),
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(subtitleBlock, 1);
            root.Children.Add(subtitleBlock);

            var separator = new Border
            {
                Height = 1,
                Background = BrushFromHex(BorderSubtleHex),
                Margin = new Thickness(0, 16, 0, 16)
            };
            Grid.SetRow(separator, 2);
            root.Children.Add(separator);

            var cardList = new StackPanel { Spacing = 10 };
            cardList.Children.Add(CreateTypeChangeCard(
                dialog,
                currentType,
                InteractorProjectType.RotaryKnob,
                "Rotary knob",
                "Encoder and knob-focused workflow with rotation animation.",
                CreateKnobIcon(),
                value => selectedType = value));
            cardList.Children.Add(CreateTypeChangeCard(
                dialog,
                currentType,
                InteractorProjectType.FlipSwitch,
                "Flip switch",
                "Toggle switch workflow with base and lever meshes.",
                CreateSwitchIcon(),
                value => selectedType = value));
            cardList.Children.Add(CreateTypeChangeCard(
                dialog,
                currentType,
                InteractorProjectType.ThumbSlider,
                "Thumb slider",
                "Linear slider with backplate and thumb meshes.",
                CreateSliderIcon(),
                value => selectedType = value));
            cardList.Children.Add(CreateTypeChangeCard(
                dialog,
                currentType,
                InteractorProjectType.PushButton,
                "Push button",
                "Momentary button with push animation scaffold.",
                CreateButtonIcon(),
                value => selectedType = value));
            cardList.Children.Add(CreateTypeChangeCard(
                dialog,
                currentType,
                InteractorProjectType.IndicatorLight,
                "Indicator light",
                "LED indicator with bezel, dome, and emitter rig.",
                CreateIndicatorIcon(),
                value => selectedType = value));

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = cardList
            };
            Grid.SetRow(scrollViewer, 3);
            root.Children.Add(scrollViewer);

            var cancelButton = new Button
            {
                Content = "Cancel",
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 100,
                Padding = new Thickness(16, 8),
                Background = Brushes.Transparent,
                BorderBrush = BrushFromHex(BorderDefaultHex),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromHex(TextSecondaryHex),
                Margin = new Thickness(0, 14, 0, 0),
                CornerRadius = new CornerRadius(6)
            };
            cancelButton.Click += (_, _) => dialog.Close();
            Grid.SetRow(cancelButton, 4);
            root.Children.Add(cancelButton);

            dialog.Content = root;
            await dialog.ShowDialog(this);
            return selectedType;
        }

        private static Border CreateTypeChangeCard(
            Window dialog,
            InteractorProjectType currentType,
            InteractorProjectType targetType,
            string title,
            string description,
            Control icon,
            Action<InteractorProjectType> onSelected)
        {
            bool isCurrent = currentType == targetType;

            var card = new Border
            {
                Background = BrushFromHex(isCurrent ? Surface2Hex : Surface1Hex),
                BorderBrush = BrushFromHex(isCurrent ? AccentHex : BorderDefaultHex),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14),
                Cursor = isCurrent ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand),
                Opacity = isCurrent ? 0.55 : 1.0
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 16
            };

            var iconBorder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(10),
                Background = BrushFromHex(Surface2Hex),
                BorderBrush = BrushFromHex(BorderSubtleHex),
                BorderThickness = new Thickness(1),
                Child = icon,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            var textPanel = new StackPanel
            {
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center
            };
            textPanel.Children.Add(new TextBlock
            {
                Text = isCurrent ? $"{title} (current)" : title,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = BrushFromHex(TextPrimaryHex)
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = BrushFromHex(TextSecondaryHex),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            card.Child = grid;

            if (!isCurrent)
            {
                card.PointerEntered += (_, _) =>
                {
                    card.Background = BrushFromHex(Surface3Hex);
                    card.BorderBrush = BrushFromHex(BorderStrongHex);
                };
                card.PointerExited += (_, _) =>
                {
                    card.Background = BrushFromHex(Surface1Hex);
                    card.BorderBrush = BrushFromHex(BorderDefaultHex);
                };
                card.PointerPressed += (_, e) =>
                {
                    if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
                    {
                        return;
                    }

                    onSelected(targetType);
                    dialog.Close();
                    e.Handled = true;
                };
            }

            return card;
        }

        private async Task<bool> ConfirmProjectTypeChangeAsync(InteractorProjectType targetType)
        {
            bool prunesCollar =
                _project.ProjectType == InteractorProjectType.RotaryKnob &&
                targetType != InteractorProjectType.RotaryKnob &&
                GetCollarNode() != null;

            if (!prunesCollar)
            {
                return true;
            }

            return await ShowProjectTypeConfirmDialogAsync(
                title: "Confirm Project Type Change",
                message:
                    "Switching away from Rotary Knob removes the collar node from the scene for this project type. " +
                    "You can revert with Undo (Cmd+Z). Continue?",
                confirmText: "Change Type");
        }

        private async Task<bool> ShowProjectTypeConfirmDialogAsync(string title, string message, string confirmText)
        {
            bool confirmed = false;
            var dialog = new Window
            {
                Title = title,
                Width = 520,
                Height = 260,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushFromHex(Surface0Hex)
            };

            var confirmButton = new Button
            {
                Content = confirmText,
                MinWidth = 120,
                Background = BrushFromHex(AccentSubtleHex),
                BorderBrush = BrushFromHex(AccentHex),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromHex(TextPrimaryHex),
                Padding = new Thickness(14, 8),
                CornerRadius = new CornerRadius(6)
            };
            confirmButton.Click += (_, _) =>
            {
                confirmed = true;
                dialog.Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 90,
                Background = Brushes.Transparent,
                BorderBrush = BrushFromHex(BorderDefaultHex),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromHex(TextSecondaryHex),
                Padding = new Thickness(14, 8),
                CornerRadius = new CornerRadius(6)
            };
            cancelButton.Click += (_, _) => dialog.Close();

            dialog.Content = new Border
            {
                Margin = new Thickness(24),
                Padding = new Thickness(20),
                Background = BrushFromHex(Surface1Hex),
                BorderBrush = BrushFromHex(BorderSubtleHex),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    RowSpacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 18,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = BrushFromHex(TextPrimaryHex)
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = BrushFromHex(TextSecondaryHex),
                            [Grid.RowProperty] = 1
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children = { cancelButton, confirmButton },
                            [Grid.RowProperty] = 2
                        }
                    }
                }
            };

            await dialog.ShowDialog(this);
            return confirmed;
        }

        private static string GetProjectTypeDisplayName(InteractorProjectType projectType)
        {
            return projectType switch
            {
                InteractorProjectType.FlipSwitch => "Flip switch",
                InteractorProjectType.ThumbSlider => "Thumb slider",
                InteractorProjectType.PushButton => "Push button",
                InteractorProjectType.IndicatorLight => "Indicator light",
                _ => "Rotary knob"
            };
        }

        private static string GetProjectTypeDescription(InteractorProjectType projectType)
        {
            return projectType switch
            {
                InteractorProjectType.FlipSwitch => "Toggle switch workflow with base and lever meshes.",
                InteractorProjectType.ThumbSlider => "Linear slider with backplate and thumb meshes.",
                InteractorProjectType.PushButton => "Momentary button with push animation scaffold.",
                InteractorProjectType.IndicatorLight => "LED indicator with bezel, dome, and emitter rig.",
                _ => "Encoder and knob-focused workflow with rotation animation."
            };
        }

        private static Viewbox CreateKnobIcon()
        {
            var canvas = CreateIconCanvas();

            var outline = new ShapeEllipse
            {
                Width = 13,
                Height = 13,
                Stroke = BrushFromHex(TextSecondaryHex),
                StrokeThickness = 1.4
            };
            Canvas.SetLeft(outline, 1.5);
            Canvas.SetTop(outline, 1.5);
            canvas.Children.Add(outline);

            canvas.Children.Add(new ShapePath
            {
                Data = Geometry.Parse("M8,2 L8,5.5"),
                Stroke = BrushFromHex(AccentHex),
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round
            });

            return WrapIcon(canvas);
        }

        private static Viewbox CreateSwitchIcon()
        {
            var canvas = CreateIconCanvas();

            var basePlate = new ShapeRectangle
            {
                Width = 8,
                Height = 10,
                RadiusX = 2,
                RadiusY = 2,
                Stroke = BrushFromHex(TextSecondaryHex),
                StrokeThickness = 1.2
            };
            Canvas.SetLeft(basePlate, 4);
            Canvas.SetTop(basePlate, 4.5);
            canvas.Children.Add(basePlate);

            canvas.Children.Add(new ShapePath
            {
                Data = Geometry.Parse("M8,8 L11.5,3"),
                Stroke = BrushFromHex(AccentHex),
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round
            });

            return WrapIcon(canvas);
        }

        private static Viewbox CreateSliderIcon()
        {
            var canvas = CreateIconCanvas();
            canvas.Children.Add(new ShapePath
            {
                Data = Geometry.Parse("M2.5,8 L13.5,8"),
                Stroke = BrushFromHex(TextSecondaryHex),
                StrokeThickness = 1.6,
                StrokeLineCap = PenLineCap.Round
            });

            var thumb = new ShapeRectangle
            {
                Width = 4,
                Height = 8,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = BrushFromHex(AccentSubtleHex),
                Stroke = BrushFromHex(AccentHex),
                StrokeThickness = 1
            };
            Canvas.SetLeft(thumb, 6);
            Canvas.SetTop(thumb, 4);
            canvas.Children.Add(thumb);

            return WrapIcon(canvas);
        }

        private static Viewbox CreateButtonIcon()
        {
            var canvas = CreateIconCanvas();

            var outer = new ShapeRectangle
            {
                Width = 11,
                Height = 11,
                RadiusX = 3,
                RadiusY = 3,
                Stroke = BrushFromHex(TextSecondaryHex),
                StrokeThickness = 1.2
            };
            Canvas.SetLeft(outer, 2.5);
            Canvas.SetTop(outer, 2.5);
            canvas.Children.Add(outer);

            var inner = new ShapeRectangle
            {
                Width = 7,
                Height = 7,
                RadiusX = 2,
                RadiusY = 2,
                Fill = BrushFromHex(AccentSubtleHex),
                Stroke = BrushFromHex(AccentHex),
                StrokeThickness = 1
            };
            Canvas.SetLeft(inner, 4.5);
            Canvas.SetTop(inner, 4.5);
            canvas.Children.Add(inner);

            return WrapIcon(canvas);
        }

        private static Viewbox CreateIndicatorIcon()
        {
            var canvas = CreateIconCanvas();

            var ring = new ShapeEllipse
            {
                Width = 12,
                Height = 12,
                Stroke = BrushFromHex(TextSecondaryHex),
                StrokeThickness = 1.2
            };
            Canvas.SetLeft(ring, 2);
            Canvas.SetTop(ring, 2);
            canvas.Children.Add(ring);

            var glow = new ShapeEllipse
            {
                Width = 6,
                Height = 6,
                Fill = BrushFromHex(AccentSubtleHex),
                Stroke = BrushFromHex(AccentHex),
                StrokeThickness = 1
            };
            Canvas.SetLeft(glow, 5);
            Canvas.SetTop(glow, 5);
            canvas.Children.Add(glow);

            return WrapIcon(canvas);
        }

        private static Canvas CreateIconCanvas()
        {
            return new Canvas
            {
                Width = 16,
                Height = 16
            };
        }

        private static Viewbox WrapIcon(Canvas canvas)
        {
            return new Viewbox
            {
                Width = 24,
                Height = 24,
                Child = canvas
            };
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
    }
}
