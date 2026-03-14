using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KnobForge.App.Controls;
using KnobForge.Core.MaterialGraph;
using KnobForge.Core.MaterialGraph.Nodes;
using KnobForge.Core.Scene;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private bool _updatingGraphUi;
        private Guid? _selectedGraphNodeId;
        private Bitmap? _graphPreviewBitmap;

        private sealed class GraphEndpointChoice
        {
            public required Guid NodeId { get; init; }
            public required string PortName { get; init; }
            public required string Display { get; init; }
            public override string ToString() => Display;
        }

        private void InitializeMaterialGraphEditor()
        {
            if (_addNodeTypeCombo != null)
            {
                _addNodeTypeCombo.ItemsSource = GraphNodeTypeRegistry.GetAllTypes().Keys.OrderBy(name => name).ToArray();
                if (_addNodeTypeCombo.SelectedIndex < 0)
                {
                    _addNodeTypeCombo.SelectedIndex = 0;
                }
            }

            if (_graphEnabledCheckBox != null)
            {
                _graphEnabledCheckBox.PropertyChanged += OnGraphEnabledChanged;
            }

            if (_graphNodeListBox != null)
            {
                _graphNodeListBox.SelectionChanged += OnGraphNodeSelectionChanged;
            }

            if (_addNodeButton != null)
            {
                _addNodeButton.Click += OnAddGraphNodeClicked;
            }

            if (_removeNodeButton != null)
            {
                _removeNodeButton.Click += OnRemoveGraphNodeClicked;
            }

            if (_addConnectionButton != null)
            {
                _addConnectionButton.Click += OnAddGraphConnectionClicked;
            }

            if (_removeConnectionButton != null)
            {
                _removeConnectionButton.Click += OnRemoveGraphConnectionClicked;
            }

            if (_graphPreviewBakeButton != null)
            {
                _graphPreviewBakeButton.Click += OnGraphPreviewBakeClicked;
            }

            RefreshMaterialGraphEditorUi(GetSelectedMaterialNodeOrNull());
        }

        private void RefreshMaterialGraphEditorUi(MaterialNode? material)
        {
            if (_graphEnabledCheckBox == null ||
                _graphValidationText == null ||
                _graphNodeListBox == null ||
                _graphConnectionListBox == null ||
                _nodePropertiesPanel == null ||
                _addNodeButton == null ||
                _removeNodeButton == null ||
                _addConnectionButton == null ||
                _removeConnectionButton == null ||
                _graphPreviewBakeButton == null)
            {
                return;
            }

            _updatingGraphUi = true;
            try
            {
                bool hasMaterial = material != null;
                MaterialGraph? graph = material?.Graph;
                bool enabled = graph != null;

                _graphEnabledCheckBox.IsEnabled = hasMaterial;
                _graphEnabledCheckBox.IsChecked = enabled;

                if (_addNodeTypeCombo != null)
                {
                    _addNodeTypeCombo.IsEnabled = enabled;
                }

                _addNodeButton.IsEnabled = enabled;
                _addConnectionButton.IsEnabled = enabled;
                _graphPreviewBakeButton.IsEnabled = enabled;

                if (!enabled || graph == null)
                {
                    _graphNodeListBox.ItemsSource = Array.Empty<string>();
                    _graphConnectionListBox.ItemsSource = Array.Empty<string>();
                    _nodePropertiesPanel.Children.Clear();
                    _removeNodeButton.IsEnabled = false;
                    _removeConnectionButton.IsEnabled = false;
                    SetGraphValidationMessage("Graph disabled. Legacy material path active.", new SolidColorBrush(Color.Parse("#A9B4BF")));
                    return;
                }

                if (_selectedGraphNodeId == null || graph.GetNodeById(_selectedGraphNodeId.Value) == null)
                {
                    _selectedGraphNodeId = graph.FindOutputNode()?.Id ?? graph.Nodes.FirstOrDefault()?.Id;
                }

                string[] nodeItems = graph.Nodes
                    .Select((node, index) => $"{index + 1}. {node.TypeId} [{node.Id.ToString("N")[..8]}]")
                    .ToArray();
                _graphNodeListBox.ItemsSource = nodeItems;
                int nodeIndex = graph.Nodes.FindIndex(node => node.Id == _selectedGraphNodeId);
                _graphNodeListBox.SelectedIndex = nodeIndex;
                _removeNodeButton.IsEnabled = nodeIndex >= 0;

                string[] connectionItems = graph.Connections
                    .Select(conn => FormatConnection(graph, conn))
                    .ToArray();
                _graphConnectionListBox.ItemsSource = connectionItems;
                _removeConnectionButton.IsEnabled = _graphConnectionListBox.SelectedIndex >= 0 && graph.Connections.Count > 0;

                UpdateGraphValidationState(graph);
                RebuildGraphNodeProperties(material, graph.GetNodeById(_selectedGraphNodeId ?? Guid.Empty));
            }
            finally
            {
                _updatingGraphUi = false;
            }
        }

        private void OnGraphEnabledChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _updatingGraphUi || e.Property != ToggleButton.IsCheckedProperty)
            {
                return;
            }

            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            if (material == null || _graphEnabledCheckBox == null)
            {
                return;
            }

            bool enabled = _graphEnabledCheckBox.IsChecked == true;
            if (enabled)
            {
                if (material.Graph == null)
                {
                    material.Graph = CreateDefaultGraph(material);
                    _selectedGraphNodeId = material.Graph.FindOutputNode()?.Id;
                }
            }
            else
            {
                material.Graph = null;
                _selectedGraphNodeId = null;
                SetGraphPreviewBitmap(null);
            }

            CaptureUndoSnapshotIfChanged();
            RefreshMaterialGraphEditorUi(material);
        }

        private void OnGraphNodeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingUi || _updatingGraphUi || _graphNodeListBox == null)
            {
                return;
            }

            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            MaterialGraph? graph = material?.Graph;
            if (graph == null)
            {
                return;
            }

            int index = _graphNodeListBox.SelectedIndex;
            if (index < 0 || index >= graph.Nodes.Count)
            {
                _selectedGraphNodeId = null;
                RebuildGraphNodeProperties(material, null);
                return;
            }

            _selectedGraphNodeId = graph.Nodes[index].Id;
            RebuildGraphNodeProperties(material, graph.Nodes[index]);
        }

        private void OnAddGraphNodeClicked(object? sender, RoutedEventArgs e)
        {
            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            MaterialGraph? graph = material?.Graph;
            if (material == null || graph == null || _addNodeTypeCombo?.SelectedItem is not string typeId)
            {
                return;
            }

            GraphNode? node = GraphNodeTypeRegistry.CreateByTypeId(typeId);
            if (node == null)
            {
                return;
            }

            node.EditorPosition = new System.Numerics.Vector2(32f * graph.Nodes.Count, 48f * graph.Nodes.Count);
            graph.AddNode(node);
            _selectedGraphNodeId = node.Id;
            CaptureUndoSnapshotIfChanged();
            RefreshMaterialGraphEditorUi(material);
        }

        private void OnRemoveGraphNodeClicked(object? sender, RoutedEventArgs e)
        {
            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            MaterialGraph? graph = material?.Graph;
            if (material == null || graph == null || _selectedGraphNodeId == null)
            {
                return;
            }

            graph.RemoveNode(_selectedGraphNodeId.Value);
            _selectedGraphNodeId = graph.FindOutputNode()?.Id ?? graph.Nodes.FirstOrDefault()?.Id;
            CaptureUndoSnapshotIfChanged();
            RefreshMaterialGraphEditorUi(material);
        }

        private async void OnAddGraphConnectionClicked(object? sender, RoutedEventArgs e)
        {
            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            MaterialGraph? graph = material?.Graph;
            if (material == null || graph == null)
            {
                return;
            }

            (GraphEndpointChoice? source, GraphEndpointChoice? target)? selection = await ShowGraphConnectionDialogAsync(graph);
            if (selection == null || selection.Value.source == null || selection.Value.target == null)
            {
                return;
            }

            graph.Connect(
                selection.Value.source.NodeId,
                selection.Value.source.PortName,
                selection.Value.target.NodeId,
                selection.Value.target.PortName);
            CaptureUndoSnapshotIfChanged();
            RefreshMaterialGraphEditorUi(material);
        }

        private void OnRemoveGraphConnectionClicked(object? sender, RoutedEventArgs e)
        {
            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            MaterialGraph? graph = material?.Graph;
            if (material == null || graph == null || _graphConnectionListBox == null)
            {
                return;
            }

            int index = _graphConnectionListBox.SelectedIndex;
            if (index < 0 || index >= graph.Connections.Count)
            {
                return;
            }

            GraphConnection connection = graph.Connections[index];
            graph.Disconnect(connection.TargetNodeId, connection.TargetPortName);
            CaptureUndoSnapshotIfChanged();
            RefreshMaterialGraphEditorUi(material);
        }

        private async void OnGraphPreviewBakeClicked(object? sender, RoutedEventArgs e)
        {
            MaterialNode? material = GetSelectedMaterialNodeOrNull();
            MaterialGraph? graph = material?.Graph;
            if (material == null || graph == null)
            {
                return;
            }

            List<string> errors = graph.Validate();
            if (errors.Count > 0)
            {
                UpdateGraphValidationState(graph);
                return;
            }

            if (_graphPreviewBakeButton != null)
            {
                _graphPreviewBakeButton.IsEnabled = false;
            }

            try
            {
                Dictionary<string, TextureData> textures = await Task.Run(() => LoadGraphTextures(graph));
                GraphBakeResult bake = await Task.Run(() => GraphEvaluator.BakeGraph(graph, 256, 256, textures));
                Bitmap? bitmap = CreateAvaloniaBitmapFromRgba(bake.Albedo, bake.Width, bake.Height);
                await Dispatcher.UIThread.InvokeAsync(() => SetGraphPreviewBitmap(bitmap));
            }
            catch (Exception ex)
            {
                SetGraphValidationMessage($"Preview bake failed: {ex.Message}", new SolidColorBrush(Color.Parse("#FF6B6B")));
            }
            finally
            {
                if (_graphPreviewBakeButton != null)
                {
                    _graphPreviewBakeButton.IsEnabled = true;
                }
            }
        }

        private void RebuildGraphNodeProperties(MaterialNode? material, GraphNode? node)
        {
            if (_nodePropertiesPanel == null)
            {
                return;
            }

            _nodePropertiesPanel.Children.Clear();
            if (node == null)
            {
                _nodePropertiesPanel.Children.Add(new TextBlock
                {
                    Text = "Select a node to edit its parameters.",
                    Foreground = new SolidColorBrush(Color.Parse("#A9B4BF"))
                });
                return;
            }

            _nodePropertiesPanel.Children.Add(new TextBlock
            {
                Text = node.TypeId,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#D8E2EA"))
            });

            foreach (PropertyInfo property in GetEditableNodeProperties(node))
            {
                _nodePropertiesPanel.Children.Add(new TextBlock
                {
                    Text = ToDisplayName(property.Name),
                    Foreground = new SolidColorBrush(Color.Parse("#D8E2EA"))
                });

                Control editor = BuildNodePropertyEditor(material, node, property);
                _nodePropertiesPanel.Children.Add(editor);
            }
        }

        private Control BuildNodePropertyEditor(MaterialNode? material, GraphNode node, PropertyInfo property)
        {
            Type propertyType = property.PropertyType;
            object? value = property.GetValue(node);

            if (propertyType == typeof(float))
            {
                (double minimum, double maximum, double step, int decimals, string suffix) = GetFloatEditorConfig(property.Name);
                var input = new ValueInput
                {
                    Minimum = minimum,
                    Maximum = maximum,
                    Step = step,
                    DecimalPlaces = decimals,
                    Suffix = suffix,
                    Value = Convert.ToDouble(value ?? 0f),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                input.PropertyChanged += (_, args) =>
                {
                    if (_updatingUi || _updatingGraphUi || args.Property != ValueInput.ValueProperty)
                    {
                        return;
                    }

                    property.SetValue(node, (float)input.Value);
                    UpdateGraphValidationState(material?.Graph);
                    CaptureUndoSnapshotIfChanged();
                };
                return input;
            }

            if (propertyType == typeof(int))
            {
                (double minimum, double maximum) = GetIntEditorConfig(property.Name);
                var input = new ValueInput
                {
                    Minimum = minimum,
                    Maximum = maximum,
                    Step = 1,
                    DecimalPlaces = 0,
                    Value = Convert.ToDouble(value ?? 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                input.PropertyChanged += (_, args) =>
                {
                    if (_updatingUi || _updatingGraphUi || args.Property != ValueInput.ValueProperty)
                    {
                        return;
                    }

                    property.SetValue(node, (int)Math.Round(input.Value));
                    UpdateGraphValidationState(material?.Graph);
                    CaptureUndoSnapshotIfChanged();
                };
                return input;
            }

            if (propertyType == typeof(string))
            {
                var panel = new StackPanel { Spacing = 4 };
                var textBox = new TextBox { Text = value as string ?? string.Empty };
                textBox.LostFocus += (_, _) =>
                {
                    if (_updatingUi || _updatingGraphUi)
                    {
                        return;
                    }

                    property.SetValue(node, textBox.Text ?? string.Empty);
                    UpdateGraphValidationState(material?.Graph);
                    CaptureUndoSnapshotIfChanged();
                };
                panel.Children.Add(textBox);

                if (property.Name.Contains("FilePath", StringComparison.OrdinalIgnoreCase))
                {
                    var browseButton = new Button { Content = "Browse...", Width = 90 };
                    browseButton.Click += async (_, _) =>
                    {
                        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = "Select Graph Texture",
                            AllowMultiple = false,
                            FileTypeFilter = new[]
                            {
                                new FilePickerFileType("Image Files")
                                {
                                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
                                }
                            }
                        });
                        IStorageFile? file = files.FirstOrDefault();
                        if (file == null)
                        {
                            return;
                        }

                        string path = file.Path.LocalPath;
                        textBox.Text = path;
                        property.SetValue(node, path);
                        UpdateGraphValidationState(material?.Graph);
                        CaptureUndoSnapshotIfChanged();
                    };
                    panel.Children.Add(browseButton);
                }

                return panel;
            }

            if (propertyType.IsEnum)
            {
                var comboBox = new ComboBox
                {
                    ItemsSource = Enum.GetValues(propertyType),
                    SelectedItem = value
                };
                comboBox.SelectionChanged += (_, _) =>
                {
                    if (_updatingUi || _updatingGraphUi || comboBox.SelectedItem == null)
                    {
                        return;
                    }

                    property.SetValue(node, comboBox.SelectedItem);
                    UpdateGraphValidationState(material?.Graph);
                    CaptureUndoSnapshotIfChanged();
                };
                return comboBox;
            }

            if (propertyType == typeof(List<KnobForge.Core.MaterialGraph.Nodes.GradientStop>))
            {
                var textBox = new TextBox
                {
                    AcceptsReturn = true,
                    Text = SerializeGradientStops((List<KnobForge.Core.MaterialGraph.Nodes.GradientStop>?)value),
                    MinHeight = 96
                };
                textBox.LostFocus += (_, _) =>
                {
                    if (_updatingUi || _updatingGraphUi)
                    {
                        return;
                    }

                    if (TryParseGradientStops(textBox.Text, out List<KnobForge.Core.MaterialGraph.Nodes.GradientStop> stops))
                    {
                        property.SetValue(node, stops);
                        UpdateGraphValidationState(material?.Graph);
                        CaptureUndoSnapshotIfChanged();
                    }
                };
                return textBox;
            }

            return new TextBlock
            {
                Text = value?.ToString() ?? string.Empty,
                Foreground = new SolidColorBrush(Color.Parse("#A9B4BF"))
            };
        }

        private async Task<(GraphEndpointChoice? source, GraphEndpointChoice? target)?> ShowGraphConnectionDialogAsync(MaterialGraph graph)
        {
            List<GraphEndpointChoice> sources = graph.Nodes
                .SelectMany(node => node.GetPorts()
                    .Where(port => port.Direction == PortDirection.Output)
                    .Select(port => new GraphEndpointChoice
                    {
                        NodeId = node.Id,
                        PortName = port.Name,
                        Display = $"{node.TypeId}.{port.Name}"
                    }))
                .ToList();

            List<GraphEndpointChoice> targets = graph.Nodes
                .SelectMany(node => node.GetPorts()
                    .Where(port => port.Direction == PortDirection.Input)
                    .Select(port => new GraphEndpointChoice
                    {
                        NodeId = node.Id,
                        PortName = port.Name,
                        Display = $"{node.TypeId}.{port.Name}"
                    }))
                .ToList();

            if (sources.Count == 0 || targets.Count == 0)
            {
                return null;
            }

            var sourceCombo = new ComboBox { ItemsSource = sources, SelectedIndex = 0, Width = 420 };
            var targetCombo = new ComboBox { ItemsSource = targets, SelectedIndex = 0, Width = 420 };
            var okButton = new Button { Content = "OK", Width = 80, IsDefault = true };
            var cancelButton = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            var errorText = new TextBlock { Foreground = new SolidColorBrush(Color.Parse("#FF6B6B")), TextWrapping = TextWrapping.Wrap };

            var dialog = new Window
            {
                Title = "Connect Nodes",
                Width = 520,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Border
                {
                    Padding = new Thickness(16),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Source Output" },
                            sourceCombo,
                            new TextBlock { Text = "Target Input" },
                            targetCombo,
                            errorText,
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 8,
                                Children = { cancelButton, okButton }
                            }
                        }
                    }
                }
            };

            (GraphEndpointChoice? source, GraphEndpointChoice? target)? result = null;
            okButton.Click += (_, _) =>
            {
                GraphEndpointChoice? source = sourceCombo.SelectedItem as GraphEndpointChoice;
                GraphEndpointChoice? target = targetCombo.SelectedItem as GraphEndpointChoice;
                if (source == null || target == null)
                {
                    errorText.Text = "Select both a source output and target input.";
                    return;
                }

                if (source.NodeId == target.NodeId)
                {
                    errorText.Text = "Self-connections are not allowed.";
                    return;
                }

                result = (source, target);
                dialog.Close();
            };
            cancelButton.Click += (_, _) => dialog.Close();

            await dialog.ShowDialog(this);
            return result;
        }

        private void UpdateGraphValidationState(MaterialGraph? graph)
        {
            if (graph == null)
            {
                SetGraphValidationMessage("Graph disabled. Legacy material path active.", new SolidColorBrush(Color.Parse("#A9B4BF")));
                return;
            }

            List<string> errors = graph.Validate();
            if (errors.Count == 0)
            {
                SetGraphValidationMessage("Graph valid. Texture bake will use the graph path.", new SolidColorBrush(Color.Parse("#8FD19E")));
            }
            else
            {
                SetGraphValidationMessage(string.Join(Environment.NewLine, errors), new SolidColorBrush(Color.Parse("#FF6B6B")));
            }
        }

        private void SetGraphValidationMessage(string message, IBrush foreground)
        {
            if (_graphValidationText == null)
            {
                return;
            }

            _graphValidationText.Text = message;
            _graphValidationText.Foreground = foreground;
        }

        private static IEnumerable<PropertyInfo> GetEditableNodeProperties(GraphNode node)
        {
            return node.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite)
                .Where(property => property.Name is not nameof(GraphNode.Id) and not nameof(GraphNode.EditorPosition) and not nameof(GraphNode.TypeId))
                .OrderBy(property => property.Name);
        }

        private static string ToDisplayName(string propertyName)
        {
            return string.Concat(propertyName.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
        }

        private static (double minimum, double maximum, double step, int decimals, string suffix) GetFloatEditorConfig(string propertyName)
        {
            if (propertyName.Contains("Rotation", StringComparison.OrdinalIgnoreCase)) return (-360, 360, 0.1, 1, " deg");
            if (propertyName.Contains("Tiling", StringComparison.OrdinalIgnoreCase)) return (-16, 16, 0.01, 2, string.Empty);
            if (propertyName.Contains("Offset", StringComparison.OrdinalIgnoreCase)) return (-16, 16, 0.01, 2, string.Empty);
            if (propertyName.Contains("Scale", StringComparison.OrdinalIgnoreCase)) return (0, 100, 0.01, 2, string.Empty);
            if (propertyName.Contains("Persistence", StringComparison.OrdinalIgnoreCase)) return (0, 1, 0.01, 2, string.Empty);
            if (propertyName.Contains("Lacunarity", StringComparison.OrdinalIgnoreCase)) return (0.1, 8, 0.01, 2, string.Empty);
            if (propertyName.Contains("Jitter", StringComparison.OrdinalIgnoreCase)) return (0, 1, 0.01, 2, string.Empty);
            if (propertyName.Contains("Brightness", StringComparison.OrdinalIgnoreCase)) return (-1, 1, 0.01, 2, string.Empty);
            if (propertyName.Contains("Contrast", StringComparison.OrdinalIgnoreCase)) return (0, 4, 0.01, 2, string.Empty);
            if (propertyName is "R" or "G" or "B" or "X" or "Y" or "Z") return (0, 1, 0.01, 2, string.Empty);
            if (propertyName.Contains("Width", StringComparison.OrdinalIgnoreCase)) return (0.001, 1, 0.001, 3, string.Empty);
            if (propertyName.Contains("Height", StringComparison.OrdinalIgnoreCase)) return (0.001, 1, 0.001, 3, string.Empty);
            if (propertyName.Contains("Mortar", StringComparison.OrdinalIgnoreCase)) return (0, 0.49, 0.001, 3, string.Empty);
            if (propertyName.Contains("RowOffset", StringComparison.OrdinalIgnoreCase)) return (0, 1, 0.01, 2, string.Empty);
            if (propertyName.Contains("Position", StringComparison.OrdinalIgnoreCase)) return (0, 1, 0.01, 2, string.Empty);
            return (-100, 100, 0.01, 2, string.Empty);
        }

        private static (double minimum, double maximum) GetIntEditorConfig(string propertyName)
        {
            if (propertyName.Contains("Octaves", StringComparison.OrdinalIgnoreCase)) return (1, 8);
            if (propertyName.Contains("Seed", StringComparison.OrdinalIgnoreCase)) return (-100000, 100000);
            return (0, 256);
        }

        private static string FormatConnection(MaterialGraph graph, GraphConnection connection)
        {
            GraphNode? sourceNode = graph.GetNodeById(connection.SourceNodeId);
            GraphNode? targetNode = graph.GetNodeById(connection.TargetNodeId);
            string sourceName = sourceNode?.TypeId ?? "MissingSource";
            string targetName = targetNode?.TypeId ?? "MissingTarget";
            return $"{sourceName}.{connection.SourcePortName} -> {targetName}.{connection.TargetPortName}";
        }

        private MaterialNode? GetSelectedMaterialNodeOrNull()
        {
            MaterialNode[] materials = GetAvailableMaterialNodes();
            int index = ClampSelectedMaterialIndex(materials);
            return index >= 0 && index < materials.Length ? materials[index] : null;
        }

        private static MaterialGraph CreateDefaultGraph(MaterialNode material)
        {
            var graph = new MaterialGraph();
            var albedo = new ConstantFloat3Node
            {
                X = material.BaseColor.X,
                Y = material.BaseColor.Y,
                Z = material.BaseColor.Z,
                EditorPosition = new System.Numerics.Vector2(24f, 24f)
            };
            var roughness = new ConstantNode
            {
                Value = material.Roughness,
                EditorPosition = new System.Numerics.Vector2(24f, 96f)
            };
            var metallic = new ConstantNode
            {
                Value = material.Metallic,
                EditorPosition = new System.Numerics.Vector2(24f, 168f)
            };
            var output = new PBROutputNode
            {
                EditorPosition = new System.Numerics.Vector2(260f, 96f)
            };

            graph.AddNode(albedo);
            graph.AddNode(roughness);
            graph.AddNode(metallic);
            graph.AddNode(output);
            graph.Connect(albedo.Id, "Value", output.Id, "Albedo");
            graph.Connect(roughness.Id, "Value", output.Id, "Roughness");
            graph.Connect(metallic.Id, "Value", output.Id, "Metallic");
            return graph;
        }

        private static Dictionary<string, TextureData> LoadGraphTextures(MaterialGraph graph)
        {
            var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
            foreach (TextureMapNode node in graph.Nodes.OfType<TextureMapNode>())
            {
                if (string.IsNullOrWhiteSpace(node.FilePath) || textures.ContainsKey(node.FilePath))
                {
                    continue;
                }

                TextureData? texture = LoadTextureData(node.FilePath);
                if (texture != null)
                {
                    textures[node.FilePath] = texture;
                }
            }
            return textures;
        }

        private static TextureData? LoadTextureData(string filePath)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch
            {
                return null;
            }

            if (!File.Exists(fullPath))
            {
                return null;
            }

            using SKBitmap? bitmap = SKBitmap.Decode(fullPath);
            if (bitmap == null)
            {
                return null;
            }

            using SKBitmap? converted = bitmap.Copy(SKColorType.Rgba8888);
            if (converted == null)
            {
                return null;
            }

            return new TextureData
            {
                Width = converted.Width,
                Height = converted.Height,
                Rgba8 = converted.GetPixelSpan().ToArray()
            };
        }

        private static Bitmap? CreateAvaloniaBitmapFromRgba(byte[] rgba8, int width, int height)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            rgba8.CopyTo(bitmap.GetPixelSpan());
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        private void SetGraphPreviewBitmap(Bitmap? bitmap)
        {
            if (_graphPreviewBitmap != null)
            {
                _graphPreviewBitmap.Dispose();
                _graphPreviewBitmap = null;
            }

            _graphPreviewBitmap = bitmap;
            if (_graphPreviewImage != null)
            {
                _graphPreviewImage.Source = _graphPreviewBitmap;
            }
        }

        private static string SerializeGradientStops(List<KnobForge.Core.MaterialGraph.Nodes.GradientStop>? stops)
        {
            if (stops == null || stops.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, stops.Select(stop => $"{stop.Position:0.###}:{stop.R:0.###},{stop.G:0.###},{stop.B:0.###}"));
        }

        private static bool TryParseGradientStops(string? text, out List<KnobForge.Core.MaterialGraph.Nodes.GradientStop> stops)
        {
            stops = new List<KnobForge.Core.MaterialGraph.Nodes.GradientStop>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !float.TryParse(parts[0], out float position))
                {
                    return false;
                }

                string[] colorParts = parts[1].Split(',', StringSplitOptions.TrimEntries);
                if (colorParts.Length != 3 ||
                    !float.TryParse(colorParts[0], out float r) ||
                    !float.TryParse(colorParts[1], out float g) ||
                    !float.TryParse(colorParts[2], out float b))
                {
                    return false;
                }

                stops.Add(new KnobForge.Core.MaterialGraph.Nodes.GradientStop
                {
                    Position = position,
                    R = r,
                    G = g,
                    B = b
                });
            }

            return stops.Count > 0;
        }
    }
}
