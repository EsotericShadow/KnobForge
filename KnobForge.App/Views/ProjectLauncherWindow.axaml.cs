using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using KnobForge.App.ProjectFiles;
using KnobForge.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShapeEllipse = Avalonia.Controls.Shapes.Ellipse;
using ShapePath = Avalonia.Controls.Shapes.Path;
using ShapeRectangle = Avalonia.Controls.Shapes.Rectangle;

namespace KnobForge.App.Views
{
    public partial class ProjectLauncherWindow : Window
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
        private const string TextTertiaryHex = "#707C88";
        private const string AccentHex = "#4A90B8";
        private const string AccentSubtleHex = "#2A4A60";

        private readonly Button _newProjectButton;
        private readonly Button _openSelectedProjectButton;
        private readonly Button _browseProjectButton;
        private readonly ListBox _projectListBox;
        private readonly TextBlock _statusTextBlock;
        private readonly TextBlock _launcherVersionTextBlock;
        private readonly StackPanel _emptyStatePanel;
        private readonly Button _emptyStateNewProjectButton;
        private readonly ObservableCollection<ProjectCard> _projectCards = new();

        public ProjectLauncherWindow()
        {
            InitializeComponent();

            _newProjectButton = this.FindControl<Button>("NewProjectButton")
                ?? throw new InvalidOperationException("NewProjectButton not found.");
            _openSelectedProjectButton = this.FindControl<Button>("OpenSelectedProjectButton")
                ?? throw new InvalidOperationException("OpenSelectedProjectButton not found.");
            _browseProjectButton = this.FindControl<Button>("BrowseProjectButton")
                ?? throw new InvalidOperationException("BrowseProjectButton not found.");
            _projectListBox = this.FindControl<ListBox>("ProjectListBox")
                ?? throw new InvalidOperationException("ProjectListBox not found.");
            _statusTextBlock = this.FindControl<TextBlock>("LauncherStatusTextBlock")
                ?? throw new InvalidOperationException("LauncherStatusTextBlock not found.");
            _launcherVersionTextBlock = this.FindControl<TextBlock>("LauncherVersionTextBlock")
                ?? throw new InvalidOperationException("LauncherVersionTextBlock not found.");
            _emptyStatePanel = this.FindControl<StackPanel>("EmptyStatePanel")
                ?? throw new InvalidOperationException("EmptyStatePanel not found.");
            _emptyStateNewProjectButton = this.FindControl<Button>("EmptyStateNewProjectButton")
                ?? throw new InvalidOperationException("EmptyStateNewProjectButton not found.");

            _projectListBox.ItemsSource = _projectCards;
            _projectListBox.SelectionChanged += OnProjectSelectionChanged;
            _projectListBox.DoubleTapped += OnProjectListDoubleTapped;
            _newProjectButton.Click += OnNewProjectButtonClicked;
            _openSelectedProjectButton.Click += OnOpenSelectedProjectButtonClicked;
            _browseProjectButton.Click += OnBrowseProjectButtonClicked;
            _emptyStateNewProjectButton.Click += OnNewProjectButtonClicked;
            Opened += OnLauncherOpened;

            _launcherVersionTextBlock.Text = GetLauncherVersionLabel();
            UpdateSelectionActions();
        }

        public event Action<ProjectLauncherResult>? LaunchRequested;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLauncherOpened(object? sender, EventArgs e)
        {
            ReloadProjects();
        }

        private void ReloadProjects()
        {
            DisposeThumbnails();
            _projectCards.Clear();

            IReadOnlyList<KnobProjectLauncherEntry> entries = KnobProjectFileStore.GetLauncherEntries();
            foreach (KnobProjectLauncherEntry entry in entries)
            {
                _projectCards.Add(new ProjectCard(entry));
            }

            bool hasProjects = _projectCards.Count > 0;
            _projectListBox.IsVisible = hasProjects;
            _emptyStatePanel.IsVisible = !hasProjects;

            _statusTextBlock.Text = hasProjects
                ? $"{_projectCards.Count} project{(_projectCards.Count == 1 ? string.Empty : "s")}"
                : string.Empty;

            if (hasProjects)
            {
                _projectListBox.SelectedIndex = 0;
            }

            UpdateSelectionActions();
        }

        private void OnProjectSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionActions();
        }

        private void OnProjectListDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (_projectListBox.SelectedItem is ProjectCard card)
            {
                LaunchRequested?.Invoke(new ProjectLauncherResult(card.FilePath));
            }
        }

        private async void OnNewProjectButtonClicked(object? sender, RoutedEventArgs e)
        {
            InteractorProjectType? projectType = await ShowProjectTypePickerAsync();
            if (projectType == null)
            {
                return;
            }

            LaunchRequested?.Invoke(ProjectLauncherResult.ForNewProject(projectType.Value));
        }

        private void OnOpenSelectedProjectButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (_projectListBox.SelectedItem is not ProjectCard card)
            {
                return;
            }

            LaunchRequested?.Invoke(new ProjectLauncherResult(card.FilePath));
        }

        private async void OnBrowseProjectButtonClicked(object? sender, RoutedEventArgs e)
        {
            FilePickerOpenOptions options = new()
            {
                AllowMultiple = false,
                Title = "Open Monozukuri Project",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Monozukuri Project")
                    {
                        Patterns = new[] { $"*{KnobProjectFileStore.FileExtension}" }
                    }
                }
            };

            string suggestedFolder = KnobProjectFileStore.EnsureDefaultProjectsDirectory();
            if (Directory.Exists(suggestedFolder))
            {
                var folder = await StorageProvider.TryGetFolderFromPathAsync(suggestedFolder);
                if (folder != null)
                {
                    options.SuggestedStartLocation = folder;
                }
            }

            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                return;
            }

            string? path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                path = files[0].Path.LocalPath;
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                LaunchRequested?.Invoke(new ProjectLauncherResult(path));
            }
        }

        private void UpdateSelectionActions()
        {
            _openSelectedProjectButton.IsEnabled = _projectListBox.SelectedItem is ProjectCard;
        }

        private static string GetLauncherVersionLabel()
        {
            Assembly assembly = typeof(ProjectLauncherWindow).Assembly;
            string? informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return $"v{TrimBuildMetadata(informationalVersion)}";
            }

            Version? version = assembly.GetName().Version;
            if (version != null)
            {
                return version.Build > 0
                    ? $"v{version.Major}.{version.Minor}.{version.Build}"
                    : $"v{version.Major}.{version.Minor}";
            }

            return "dev";
        }

        private static string TrimBuildMetadata(string value)
        {
            int plusIndex = value.IndexOf('+');
            return plusIndex >= 0 ? value[..plusIndex] : value;
        }

        private async Task<InteractorProjectType?> ShowProjectTypePickerAsync()
        {
            InteractorProjectType? selectedType = null;
            var dialog = new Window
            {
                Title = "New Project",
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

            // Title
            var titleBlock = new TextBlock
            {
                Text = "Choose Project Type",
                FontSize = 22,
                FontWeight = FontWeight.SemiBold,
                Foreground = BrushFromHex(TextPrimaryHex)
            };
            Grid.SetRow(titleBlock, 0);
            root.Children.Add(titleBlock);

            // Subtitle
            var subtitleBlock = new TextBlock
            {
                Text = "Select the type of audio plugin control you want to create.",
                FontSize = 12,
                Foreground = BrushFromHex(TextSecondaryHex),
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(subtitleBlock, 1);
            root.Children.Add(subtitleBlock);

            // Separator
            var separator = new Border
            {
                Height = 1,
                Background = BrushFromHex(BorderSubtleHex),
                Margin = new Thickness(0, 16, 0, 16)
            };
            Grid.SetRow(separator, 2);
            root.Children.Add(separator);

            // Card list
            var cardList = new StackPanel { Spacing = 10 };

            cardList.Children.Add(CreateTypeCard(
                dialog,
                "Rotary knob",
                "Encoder and knob-focused workflow with rotation animation.",
                CreateKnobIcon(),
                InteractorProjectType.RotaryKnob,
                value => selectedType = value));
            cardList.Children.Add(CreateTypeCard(
                dialog,
                "Flip switch",
                "Toggle switch workflow with base and lever meshes.",
                CreateSwitchIcon(),
                InteractorProjectType.FlipSwitch,
                value => selectedType = value));
            cardList.Children.Add(CreateTypeCard(
                dialog,
                "Thumb slider",
                "Linear slider with backplate and thumb meshes.",
                CreateSliderIcon(),
                InteractorProjectType.ThumbSlider,
                value => selectedType = value));
            cardList.Children.Add(CreateTypeCard(
                dialog,
                "Push button",
                "Momentary button with push animation scaffold.",
                CreateButtonIcon(),
                InteractorProjectType.PushButton,
                value => selectedType = value));
            cardList.Children.Add(CreateTypeCard(
                dialog,
                "Indicator light",
                "LED indicator with bezel, dome, and emitter rig.",
                CreateIndicatorIcon(),
                InteractorProjectType.IndicatorLight,
                value => selectedType = value));

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = cardList
            };
            Grid.SetRow(scrollViewer, 3);
            root.Children.Add(scrollViewer);

            // Cancel button (proper ghost button, not a raw TextBlock)
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

        private static Border CreateTypeCard(
            Window dialog,
            string title,
            string description,
            Control icon,
            InteractorProjectType projectType,
            Action<InteractorProjectType> onSelected)
        {
            var card = new Border
            {
                Background = BrushFromHex(Surface1Hex),
                BorderBrush = BrushFromHex(BorderDefaultHex),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14),
                Cursor = new Cursor(StandardCursorType.Hand)
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
                Text = title,
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

                onSelected(projectType);
                dialog.Close();
                e.Handled = true;
            };

            return card;
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

        protected override void OnClosed(EventArgs e)
        {
            DisposeThumbnails();
            base.OnClosed(e);
        }

        internal async Task ShowProjectLoadErrorDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 480,
                Height = 240,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushFromHex(Surface0Hex)
            };

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 100,
                Background = BrushFromHex(AccentSubtleHex),
                BorderBrush = BrushFromHex(AccentHex),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromHex(TextPrimaryHex),
                Padding = new Thickness(14, 8),
                CornerRadius = new CornerRadius(6)
            };
            okButton.Click += (_, _) => dialog.Close();

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
                            Spacing = 8,
                            Children = { okButton },
                            [Grid.RowProperty] = 2
                        }
                    }
                }
            };

            await dialog.ShowDialog(this);
        }

        private void DisposeThumbnails()
        {
            foreach (ProjectCard card in _projectCards)
            {
                card.Dispose();
            }
        }

        public sealed class ProjectLauncherResult
        {
            public ProjectLauncherResult(string? projectPath, InteractorProjectType? projectType = null)
            {
                ProjectPath = projectPath;
                ProjectType = projectType;
            }

            public string? ProjectPath { get; }
            public InteractorProjectType? ProjectType { get; }
            public bool IsNewProject => string.IsNullOrWhiteSpace(ProjectPath);

            public static ProjectLauncherResult ForNewProject(InteractorProjectType projectType)
            {
                return new ProjectLauncherResult(null, projectType);
            }
        }

        private sealed class ProjectCard : IDisposable
        {
            public ProjectCard(KnobProjectLauncherEntry entry)
            {
                FilePath = entry.FilePath;
                DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? Path.GetFileNameWithoutExtension(entry.FilePath)
                    : entry.DisplayName.Trim();
                DateTime saved = entry.SavedUtc.ToUniversalTime();
                SavedDisplay = saved == DateTime.MinValue
                    ? "Saved: unknown"
                    : $"Saved: {saved.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
                Thumbnail = KnobProjectFileStore.TryDecodeThumbnail(entry.ThumbnailPngBase64);
            }

            public string FilePath { get; }
            public string DisplayName { get; }
            public string SavedDisplay { get; }
            public Bitmap? Thumbnail { get; }
            public bool HasThumbnail => Thumbnail != null;
            public bool ShowPlaceholder => Thumbnail == null;

            public void Dispose()
            {
                Thumbnail?.Dispose();
            }
        }
    }
}
