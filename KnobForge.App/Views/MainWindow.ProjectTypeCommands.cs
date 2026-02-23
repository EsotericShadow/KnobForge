using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System;
using System.Threading.Tasks;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
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
                Width = 480,
                Height = 390,
                MinWidth = 440,
                MinHeight = 340,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10
            };

            content.Children.Add(new TextBlock
            {
                Text = "Switch the current project to a different interactor workflow:",
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = $"Current: {GetProjectTypeDisplayName(currentType)}",
                FontSize = 12,
                Foreground = Brushes.Gray
            });

            content.Children.Add(CreateProjectTypePickerButton(
                dialog,
                currentType,
                InteractorProjectType.RotaryKnob,
                value => selectedType = value));
            content.Children.Add(CreateProjectTypePickerButton(
                dialog,
                currentType,
                InteractorProjectType.FlipSwitch,
                value => selectedType = value));
            content.Children.Add(CreateProjectTypePickerButton(
                dialog,
                currentType,
                InteractorProjectType.ThumbSlider,
                value => selectedType = value));
            content.Children.Add(CreateProjectTypePickerButton(
                dialog,
                currentType,
                InteractorProjectType.PushButton,
                value => selectedType = value));

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 90
            };
            cancelButton.Click += (_, _) => dialog.Close();
            actions.Children.Add(cancelButton);
            content.Children.Add(actions);

            dialog.Content = content;
            await dialog.ShowDialog(this);
            return selectedType;
        }

        private static Button CreateProjectTypePickerButton(
            Window dialog,
            InteractorProjectType currentType,
            InteractorProjectType targetType,
            Action<InteractorProjectType> onSelected)
        {
            bool isCurrent = currentType == targetType;
            string title = GetProjectTypeDisplayName(targetType);
            if (isCurrent)
            {
                title = $"{title} (Current)";
            }

            var button = new Button
            {
                IsEnabled = !isCurrent,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 10),
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 14,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = GetProjectTypeDescription(targetType),
                            FontSize = 11,
                            Foreground = Brushes.Gray
                        }
                    }
                }
            };

            button.Click += (_, _) =>
            {
                onSelected(targetType);
                dialog.Close();
            };

            return button;
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
                Height = 220,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var confirmButton = new Button
            {
                Content = confirmText,
                MinWidth = 108
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
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
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

        private static string GetProjectTypeDisplayName(InteractorProjectType projectType)
        {
            return projectType switch
            {
                InteractorProjectType.FlipSwitch => "Flip Switch",
                InteractorProjectType.ThumbSlider => "Thumb Slider",
                InteractorProjectType.PushButton => "Push Button",
                _ => "Rotary Knob"
            };
        }

        private static string GetProjectTypeDescription(InteractorProjectType projectType)
        {
            return projectType switch
            {
                InteractorProjectType.FlipSwitch => "Toggle switch workflow with base + lever meshes.",
                InteractorProjectType.ThumbSlider => "Slider workflow with backplate + thumb meshes.",
                InteractorProjectType.PushButton => "Button workflow scaffold (geometry to be expanded).",
                _ => "Encoder and knob-focused workflow."
            };
        }
    }
}
