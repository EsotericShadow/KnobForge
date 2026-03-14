using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using System;
using System.Globalization;

namespace KnobForge.App.Controls
{
    public sealed class ValueInput : UserControl
    {
        private static readonly Color FieldBackgroundColor = Color.Parse("#1A1E24");
        private static readonly Color FieldBorderColor = Color.Parse("#3A4550");
        private static readonly Color TextColorValue = Color.Parse("#E6EEF5");
        private static readonly Color ArrowColorValue = Color.Parse("#8899AA");
        private static readonly Color ArrowHoverColorValue = Color.Parse("#BBCCDD");
        private static readonly Color ButtonHoverBackgroundColor = Color.Parse("#2A3440");
        private static readonly Color ButtonPressedBackgroundColor = Color.Parse("#354555");
        private static readonly Color DividerColorValue = Color.Parse("#2E3740");

        private readonly Border _rootBorder;
        private readonly TextBox _textBox;
        private readonly RepeatButton _upButton;
        private readonly RepeatButton _downButton;

        private bool _suppressTextCommit;
        private bool _isDragging;
        private bool _pointerPressed;
        private double _dragStartY;
        private double _dragStartValue;
        private KeyModifiers _lastKnownModifiers;

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<ValueInput, double>(nameof(Value), defaultValue: 0.0);

        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<ValueInput, double>(nameof(Minimum), defaultValue: 0.0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<ValueInput, double>(nameof(Maximum), defaultValue: 1.0);

        public static readonly StyledProperty<double> StepProperty =
            AvaloniaProperty.Register<ValueInput, double>(nameof(Step), defaultValue: 0.01);

        public static readonly StyledProperty<double> SkewFactorProperty =
            AvaloniaProperty.Register<ValueInput, double>(nameof(SkewFactor), defaultValue: 1.0);

        public static readonly StyledProperty<int> DecimalPlacesProperty =
            AvaloniaProperty.Register<ValueInput, int>(nameof(DecimalPlaces), defaultValue: 3);

        public static readonly StyledProperty<string> SuffixProperty =
            AvaloniaProperty.Register<ValueInput, string>(nameof(Suffix), defaultValue: string.Empty);

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double Step
        {
            get => GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public double SkewFactor
        {
            get => GetValue(SkewFactorProperty);
            set => SetValue(SkewFactorProperty, value);
        }

        public int DecimalPlaces
        {
            get => GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public string Suffix
        {
            get => GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public ValueInput()
        {
            MinWidth = 80;
            Height = 26;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            _textBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextColorValue),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                FontSize = 12,
                Padding = new Thickness(8, 2, 6, 2)
            };

            _upButton = CreateArrowButton("M3,7 L8,3 L13,7", isIncrement: true);
            _downButton = CreateArrowButton("M3,3 L8,7 L13,3", isIncrement: false);

            var arrowGrid = new Grid
            {
                Width = 16,
                RowDefinitions = new RowDefinitions("*,*")
            };
            arrowGrid.Children.Add(_upButton);
            Grid.SetRow(_upButton, 0);
            arrowGrid.Children.Add(_downButton);
            Grid.SetRow(_downButton, 1);

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };
            contentGrid.Children.Add(_textBox);
            Grid.SetColumn(_textBox, 0);
            contentGrid.Children.Add(arrowGrid);
            Grid.SetColumn(arrowGrid, 1);

            _rootBorder = new Border
            {
                Background = new SolidColorBrush(FieldBackgroundColor),
                BorderBrush = new SolidColorBrush(FieldBorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2.5),
                Child = contentGrid
            };

            Content = _rootBorder;

            _textBox.KeyDown += OnTextBoxKeyDown;
            _textBox.GotFocus += OnTextBoxGotFocus;
            _textBox.LostFocus += OnTextBoxLostFocus;
            _textBox.PointerPressed += OnTextBoxPointerPressed;
            _textBox.PointerMoved += OnTextBoxPointerMoved;
            _textBox.PointerReleased += OnTextBoxPointerReleased;
            _textBox.PointerCaptureLost += OnTextBoxPointerCaptureLost;

            AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Bubble);

            UpdateDisplayedText(force: true);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
            {
                double sanitized = SanitizeValue(Value);
                if (!AreClose(sanitized, Value))
                {
                    SetCurrentValue(ValueProperty, sanitized);
                    return;
                }

                UpdateDisplayedText(force: false);
                return;
            }

            if (change.Property == MinimumProperty ||
                change.Property == MaximumProperty ||
                change.Property == StepProperty)
            {
                double sanitized = SanitizeValue(Value);
                if (!AreClose(sanitized, Value))
                {
                    SetCurrentValue(ValueProperty, sanitized);
                    return;
                }
            }

            if (change.Property == DecimalPlacesProperty || change.Property == SuffixProperty)
            {
                UpdateDisplayedText(force: false);
            }
        }

        private RepeatButton CreateArrowButton(string geometryText, bool isIncrement)
        {
            var path = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(geometryText),
                Stroke = new SolidColorBrush(ArrowColorValue),
                StrokeThickness = 1.25,
                Width = 16,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Fill,
                Margin = new Thickness(0, 1, 0, 1)
            };

            var button = new RepeatButton
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width = 16,
                Delay = 420,
                Interval = 110,
                Content = path
            };

            button.Click += (_, _) => NudgeValue(isIncrement ? 1.0 : -1.0, GetCurrentModifiers());
            button.PointerEntered += (_, _) =>
            {
                button.Background = new SolidColorBrush(ButtonHoverBackgroundColor);
                path.Stroke = new SolidColorBrush(ArrowHoverColorValue);
            };
            button.PointerExited += (_, _) =>
            {
                button.Background = Brushes.Transparent;
                path.Stroke = new SolidColorBrush(ArrowColorValue);
            };
            button.PointerPressed += (_, _) =>
            {
                button.Background = new SolidColorBrush(ButtonPressedBackgroundColor);
                path.Stroke = new SolidColorBrush(ArrowHoverColorValue);
            };
            button.PointerReleased += (_, _) =>
            {
                button.Background = button.IsPointerOver
                    ? new SolidColorBrush(ButtonHoverBackgroundColor)
                    : Brushes.Transparent;
                path.Stroke = new SolidColorBrush(button.IsPointerOver ? ArrowHoverColorValue : ArrowColorValue);
            };
            button.PointerPressed += (_, e) => _lastKnownModifiers = e.KeyModifiers;
            button.PointerMoved += (_, e) => _lastKnownModifiers = e.KeyModifiers;

            return button;
        }

        private void OnTextBoxGotFocus(object? sender, GotFocusEventArgs e)
        {
            _textBox.Text = Value.ToString($"F{Math.Max(0, DecimalPlaces)}", CultureInfo.InvariantCulture);
            _textBox.SelectAll();
        }

        private void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            CommitText();
        }

        private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            _lastKnownModifiers = e.KeyModifiers;

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                CommitText();
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                RevertDisplayedText();
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
                e.Handled = true;
            }
        }

        private void OnTextBoxPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _lastKnownModifiers = e.KeyModifiers;
            _pointerPressed = true;
            _isDragging = false;
            _dragStartY = GetPointerScreenY(e);
            _dragStartValue = Value;
        }

        private void OnTextBoxPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_pointerPressed)
            {
                return;
            }

            _lastKnownModifiers = e.KeyModifiers;
            double currentY = GetPointerScreenY(e);
            double deltaY = _dragStartY - currentY;
            if (!_isDragging)
            {
                if (Math.Abs(deltaY) <= 2.0)
                {
                    return;
                }

                _isDragging = true;
                _textBox.Focusable = false;
                e.Pointer.Capture(_textBox);
            }

            int steps = (int)(deltaY / 6.0);
            double accel = 1.0;
            KeyModifiers modifiers = e.KeyModifiers;
            if (!modifiers.HasFlag(KeyModifiers.Shift))
            {
                accel = Math.Clamp(1.0 + Math.Abs(deltaY) / 220.0, 1.0, 6.0);
            }

            double delta = steps * Step * GetModifierMultiplier(modifiers) * accel;
            Value = SanitizeValue(_dragStartValue + delta);
            e.Handled = true;
        }

        private void OnTextBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                EndDrag(e.Pointer);
                e.Handled = true;
                return;
            }

            _pointerPressed = false;
        }

        private void OnTextBoxPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            EndDrag(e.Pointer);
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            _lastKnownModifiers = e.KeyModifiers;
            double deltaY = e.Delta.Y;
            if (Math.Abs(deltaY) < double.Epsilon)
            {
                return;
            }

            bool discrete = Math.Abs(Math.Abs(deltaY) - 1.0) < 0.001;
            double steps = discrete ? deltaY * 4.0 : deltaY * 16.0;
            Value = SanitizeValue(Value + steps * Step * GetModifierMultiplier(e.KeyModifiers));
            e.Handled = true;
        }

        private void EndDrag(IPointer? pointer)
        {
            _pointerPressed = false;
            _isDragging = false;
            _textBox.Focusable = true;
            pointer?.Capture(null);
            RevertDisplayedText();
        }

        private void CommitText()
        {
            if (_suppressTextCommit)
            {
                return;
            }

            if (TryParseDisplayText(_textBox.Text, out double parsed))
            {
                Value = SanitizeValue(parsed);
            }

            RevertDisplayedText();
        }

        private void RevertDisplayedText()
        {
            UpdateDisplayedText(force: true);
        }

        private void UpdateDisplayedText(bool force)
        {
            if (!force && _textBox.IsFocused)
            {
                return;
            }

            _suppressTextCommit = true;
            _textBox.Text = FormatDisplayText(Value);
            _suppressTextCommit = false;
        }

        private string FormatDisplayText(double value)
        {
            string text = value.ToString($"F{Math.Max(0, DecimalPlaces)}", CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(Suffix) ? text : text + Suffix;
        }

        private bool TryParseDisplayText(string? text, out double value)
        {
            string candidate = (text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(Suffix) && candidate.EndsWith(Suffix, StringComparison.Ordinal))
            {
                candidate = candidate[..^Suffix.Length].TrimEnd();
            }

            return double.TryParse(
                candidate,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value);
        }

        private void NudgeValue(double direction, KeyModifiers modifiers)
        {
            Value = SanitizeValue(Value + direction * Step * GetModifierMultiplier(modifiers));
        }

        private double SanitizeValue(double raw)
        {
            double minimum = Math.Min(Minimum, Maximum);
            double maximum = Math.Max(Minimum, Maximum);
            double clamped = Math.Clamp(raw, minimum, maximum);
            double step = Step;
            if (step > 0)
            {
                clamped = Math.Round((clamped - minimum) / step) * step + minimum;
                clamped = Math.Clamp(clamped, minimum, maximum);
            }

            return clamped;
        }

        private static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) <= 1e-9;
        }

        private static double GetModifierMultiplier(KeyModifiers mods)
        {
            double mult = 1.0;
            if (mods.HasFlag(KeyModifiers.Shift))
            {
                mult *= 0.2;
            }

            if (mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Meta))
            {
                mult *= 0.25;
            }

            if (mods.HasFlag(KeyModifiers.Alt))
            {
                mult *= 5.0;
            }

            return mult;
        }

        private KeyModifiers GetCurrentModifiers()
        {
            return _lastKnownModifiers;
        }

        private double GetPointerScreenY(PointerEventArgs e)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            return topLevel != null ? e.GetPosition(topLevel).Y : e.GetPosition(this).Y;
        }
    }
}
