﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColorPicker;
using ColorPicker.Sliders;
using GRF.Graphics;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;

namespace ActEditor.Core.WPF.GenericControls {
	/// <summary>
	/// Interaction logic for QuickColorSelector.xaml
	/// </summary>
	public partial class QuickColorSelector : UserControl {
		private static bool _isShown;
		private static readonly Brush _sharedGridBackground;
		private static readonly HashSet<char> _allowed = new HashSet<char> {'a', 'b', 'c', 'd', 'e', 'f'};
		private Point _oldPosition;
		private readonly List<Color> _timerColors = new List<Color>();

		public int PreviewUpdateInterval {
			get { return _previewUpdateInterval; }
			set {
				_previewUpdateInterval = value;

				if (_previewUpdateInterval == 0) {
					_timer_Tick(null, null);
				}
				else {
					if (_timer == null)
						_timer = new DispatcherTimer();

					_timer.Interval = new TimeSpan(0, 0, 0, 0, value);
					_timer.Tick += new EventHandler(_timer_Tick);
				}	
			}
		}

		private void _timer_Tick(object sender, EventArgs e) {
			if (_timerColors.Count > 0) {
				var lastColor = _timerColors.Last();
				_timerColors.Clear();

				if (_defaultColor != null) {
					_reset.Visibility = _defaultColor().Replace("0x", "#") == lastColor.ToGrfColor().ToHexString().Replace("0x", "#") ? Visibility.Collapsed : Visibility.Visible;
				}

				_previewPanelBg.Fill = new SolidColorBrush(lastColor);
				SliderGradient.GradientPickerColorEventHandler handler = PreviewColorChanged;
				if (handler != null) handler(this, lastColor);
			}

			if (_timer != null)
				_timer.Stop();
		}

		private DispatcherTimer _timer;
		private int _previewUpdateInterval;

		static QuickColorSelector() {
			VisualBrush brush = new VisualBrush {Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute, TileMode = TileMode.Tile};
			//Grid grid = new Grid();
			Image image = new Image {Source = ApplicationManager.PreloadResourceImage("background.png")};
			image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
			image.Width = 256;
			image.Height = 256;
			brush.Visual = image;

			_sharedGridBackground = brush;
		}

		public QuickColorSelector() {
			InitializeComponent();

			_border.MouseEnter += new MouseEventHandler(_quickColorSelector_MouseEnter);
			_border.MouseLeave += new MouseEventHandler(_quickColorSelector_MouseLeave);
			_previewPanelBg.Fill = new SolidColorBrush(Colors.White);
			_border.MouseDown += new MouseButtonEventHandler(_quickColorSelector_MouseDown);
			_border.MouseMove += new MouseEventHandler(_quickColorSelector_MouseMove);
			_border.DragEnter += new DragEventHandler(_quickColorSelector_DragEnter);
			_border.DragOver += _quickColorSelector_DragEnter;
			_border.DragLeave += new DragEventHandler(_quickColorSelector_DragLeave);
			_border.Drop += new DragEventHandler(_quickColorSelector_Drop);

			_grid.Background = _sharedGridBackground;
		}

		public new bool IsEnabled {
			get { return base.IsEnabled; }
			set {
				if (value) {
					_borderEnabled.BorderBrush = Brushes.Transparent;
					_borderEnabled.Background = Brushes.Transparent;
				}
				else {
					var systemBrush = SystemColors.ControlBrush;
					var brush1 = new SolidColorBrush(Color.FromArgb(150, systemBrush.Color.R, systemBrush.Color.G, systemBrush.Color.B));
					var brush2 = new SolidColorBrush(Color.FromArgb(230, systemBrush.Color.R, systemBrush.Color.G, systemBrush.Color.B));

					_borderEnabled.BorderBrush = brush1;
					_borderEnabled.Background = brush2;
				}

				base.IsEnabled = value;
			}
		}

		public GrfColor InitialColor { get; set; }

		public Color Color {
			get { return ((SolidColorBrush) _previewPanelBg.Fill).Color; }
			set {
				if (value == ((SolidColorBrush)_previewPanelBg.Fill).Color) {
					if (_defaultColor != null) {
						_reset.Visibility = _defaultColor().Replace("0x", "#") == value.ToGrfColor().ToHexString().Replace("0x", "#") ? Visibility.Collapsed : Visibility.Visible;
					}

					return;
				}

				_previewPanelBg.Fill = new SolidColorBrush(value);
				OnColorChanged(value);
			}
		}

		public event SliderGradient.GradientPickerColorEventHandler ColorChanged;
		public event SliderGradient.GradientPickerColorEventHandler PreviewColorChanged;

		public void OnPreviewColorChanged(Color value) {
			if (PreviewUpdateInterval > 0) {
				_timerColors.Add(value);
				
				if (!_timer.IsEnabled) {
					_timer_Tick(null, null);
					_timer.Start();
				}
			}
			else {
				if (_defaultColor != null) {
					_reset.Visibility = _defaultColor().Replace("0x", "#") == value.ToGrfColor().ToHexString().Replace("0x", "#") ? Visibility.Collapsed : Visibility.Visible;
				}

				_previewPanelBg.Fill = new SolidColorBrush(value);
				SliderGradient.GradientPickerColorEventHandler handler = PreviewColorChanged;
				if (handler != null) handler(this, value);
			}
		}

		public void OnColorChanged(Color value) {
			if (_defaultColor != null) {
				_reset.Visibility = _defaultColor().Replace("0x", "#") == value.ToGrfColor().ToHexString().Replace("0x", "#") ? Visibility.Collapsed : Visibility.Visible;
			}

			SliderGradient.GradientPickerColorEventHandler handler = ColorChanged;
			if (handler != null) handler(this, value);
		}

		private void _quickColorSelector_DragLeave(object sender, DragEventArgs e) {
			_border.BorderBrush = Brushes.Black;
		}

		private void _quickColorSelector_Drop(object sender, DragEventArgs e) {
			if (e.Data.GetData("GrfColor") != null) {
				GrfColor color = e.Data.GetData("GrfColor") as GrfColor;

				if (color != null) {
					InitialColor = Color.ToGrfColor();
					OnPreviewColorChanged(color.ToColor());
					OnColorChanged(color.ToColor());
				}
			}
			else {
				var txt = e.Data.GetData("System.String") as string;

				if (_isColorFormat(txt)) {
					GrfColor color = new GrfColor(txt);

					InitialColor = Color.ToGrfColor();
					OnPreviewColorChanged(color.ToColor());
					OnColorChanged(color.ToColor());
				}
			}
		}

		private void _quickColorSelector_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetData("GrfColor") != null) {
				e.Effects = DragDropEffects.All;
				_border.BorderBrush = Brushes.Green;
			}
			else {
				var txt = e.Data.GetData("System.String") as string;

				if (_isColorFormat(txt)) {
					e.Effects = DragDropEffects.All;
					_border.BorderBrush = Brushes.Green;
				}
				else {
					e.Effects = DragDropEffects.None;
				}
			}
		}

		private bool _isValidCharacter(char c) {
			return char.IsDigit(c) || _allowed.Contains(char.ToLower(c));
		}

		private bool _isColorFormat(string txt) {
			if (txt == null) return false;

			if (txt.StartsWith("#")) {
				txt = txt.Substring(1);
			}
			else if (txt.StartsWith("0x") || txt.StartsWith("0X")) {
				txt = txt.Substring(2);
			}

			if (txt.Length == 6 || txt.Length == 8) {
				return txt.All(_isValidCharacter);
			}

			return false;
		}

		private void _quickColorSelector_MouseMove(object sender, MouseEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed) {
				TkVector2 dist = e.GetPosition(this).ToTkVector2() - _oldPosition.ToTkVector2();

				if (dist.Length > 4) {
					DataObject data = new DataObject();
					data.SetData("GrfColor", Color.ToGrfColor());
					data.SetText(Color.ToGrfColor().ToHexString());
					DragDrop.DoDragDrop(this, data, DragDropEffects.All);
				}
			}
		}

		private void _quickColorSelector_MouseDown(object sender, MouseButtonEventArgs e) {
			_oldPosition = e.GetPosition(this);
		}

		public void SetColor(Color color) {
			_previewPanelBg.Fill = new SolidColorBrush(color);
		}

		private void _quickColorSelector_MouseLeave(object sender, MouseEventArgs e) {
			_border.BorderBrush = Brushes.Black;
			Mouse.OverrideCursor = null;
		}

		private void _quickColorSelector_MouseEnter(object sender, MouseEventArgs e) {
			_border.BorderBrush = Brushes.Blue;
			Mouse.OverrideCursor = Cursors.Hand;
		}

		private void _previewPanelBg_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			_previewBackground();
		}

		private void _previewBackground() {
			if (_isShown)
				return;

			_isShown = true;

			PickerDialog dialog = new PickerDialog(Color);
			dialog.Owner = WpfUtilities.TopWindow;

			InitialColor = Color.ToGrfColor();

			dialog.PickerControl.ColorChanged += delegate(object s, Color newColor) {
				OnPreviewColorChanged(newColor);
			};

			dialog.Closed += delegate {
				_isShown = false;

				if (dialog.DialogResult == false) {
					PreviewUpdateInterval = 0;
					OnPreviewColorChanged(dialog.PickerControl.InitialColor);
				}
				else if (dialog.DialogResult) {
					OnColorChanged(dialog.PickerControl.SelectedColor);
				}
			};

			dialog.Show();
		}

		public void Init(ConfigAskerSetting setting) {
			_defaultColor = () => setting.Default;

			if (_defaultColor != null) {
				_reset.Visibility = _defaultColor().Replace("0x", "#") == Color.ToGrfColor().ToHexString().Replace("0x", "#") ? Visibility.Collapsed : Visibility.Visible;
			}
		}

		public void Init(Func<string> setting) {
			_defaultColor = setting;

			if (_defaultColor != null) {
				_reset.Visibility = _defaultColor().Replace("0x", "#") == Color.ToGrfColor().ToHexString().Replace("0x", "#") ? Visibility.Collapsed : Visibility.Visible;
			}
		}

		private Func<string> _defaultColor;

		private void _reset_Click(object sender, RoutedEventArgs e) {
			if (_defaultColor != null) {
				Color = new GrfColor(_defaultColor()).ToColor();
			}
		}
	}
}