using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPicker;
using ColorPicker.Core;
using GRF.FileFormats.PalFormat;
using GRF.Graphics;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Utilities.Controls;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for GradientColorEdit.xaml
	/// </summary>
	public partial class GradientColorEditControl : UserControl {
		private Pal _pal;
		private byte[] _paletteOldSelected;
		private bool[] _locked = new bool[3];

		public bool PalAtTop {
			get { return (bool)GetValue(PalAtTopProperty); }
			set { SetValue(PalAtTopProperty, value); }
		}
		public static DependencyProperty PalAtTopProperty = DependencyProperty.Register("PalAtTop", typeof(bool), typeof(GradientColorEditControl), new PropertyMetadata(new PropertyChangedCallback(OnPalAtTopChanged)));
		private bool _assigned;

		public Grid PrimaryGrid {
			get { return _primaryGrid; }
		}

		private static void OnPalAtTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var obj = d as GradientColorEditControl;
			
			if (obj != null) {
				if (Boolean.Parse(e.NewValue.ToString())) {
					obj._paletteSelector.SetValue(Grid.RowProperty, 0);
					obj._paletteSelector.SetValue(Grid.ColumnProperty, 1);
					//obj._paletteSelector.HorizontalAlignment = HorizontalAlignment.Right;
				}
				else {
					obj._paletteSelector.SetValue(Grid.RowProperty, 1);
					obj._paletteSelector.SetValue(Grid.ColumnProperty, 0);
					//obj._paletteSelector.HorizontalAlignment = HorizontalAlignment.Left;
				}
			}
		}

		public GradientColorEditControl() {
			InitializeComponent();

			_paletteSelector.UseLargeSelector = true;
			_paletteSelector.IsMultipleColorsSelectable = true;
			_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;
			_colorPicker.ColorChanged += (s, e) => _colorPicker_ColorChanged(e);

			Loaded += delegate {
				Window parent = WpfUtilities.FindParentControl<Window>(this);

				if (parent != null)
					parent.PreviewKeyDown += new KeyEventHandler(_gce_PreviewKeyDown);
			};

			_setColorPicker(_getColor(_colorMiddle));
			_select(1);
		}

		public Grid GradientGrid {
			get { return _gridGradient; }
		}

		public WrapPanel Panel {
			get { return _wrapPanel; }
		}

		public PickerControl PickerControl {
			get { return _colorPicker; }
		}

		public PaletteSelector PaletteSelector {
			get { return _paletteSelector; }
		}

		public void SetPalette(Pal pal) {
			_pal = pal;
			_paletteSelector.SetPalette(pal);
			pal.Commands.CommandRedo += _palUpdate;
			pal.Commands.CommandUndo += _palUpdate;

			if (!_assigned) {
				_paletteSelector.PaletteSelectorPaletteChanged += sender => _palUpdate(sender, null);
				_assigned = true;
			}
		}

		private void _palUpdate(object sender, IPaletteCommand command) {
			if (_paletteSelector.SelectedItems.Count > 0)
				_paletteSelector_SelectionChanged(this, new ObservabableListEventArgs(new List<object>(), ObservableListEventType.Added));
		}

		private void _gce_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if (_pal.Commands.CanUndo) {
					_pal.Commands.Undo();
					_setColors();
					_setGradient();
					_setColorPicker(_getColor(_getSelectedComponent()));
				}
				e.Handled = true;
			}

			if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if (_pal.Commands.CanRedo) {
					_pal.Commands.Redo();
					_setColors();
					_setGradient();
					_setColorPicker(_getColor(_getSelectedComponent()));
				}
				e.Handled = true;
			}
		}

		private void _setColors(bool ignoreMiddle = false) {
			int baseIndex = _paletteSelector.SelectedItems.Count - 8;
			GrfColor color0 = _paletteSelector.Palette.GetColor(_paletteSelector.SelectedItems[baseIndex]);
			GrfColor color3 = _paletteSelector.Palette.GetColor(_paletteSelector.SelectedItems[baseIndex + 3]);
			GrfColor color4 = _paletteSelector.Palette.GetColor(_paletteSelector.SelectedItems[baseIndex + 4]);
			GrfColor color7 = _paletteSelector.Palette.GetColor(_paletteSelector.SelectedItems[baseIndex + 7]);

			if (ignoreMiddle) {
				_setColors(
					color0.ToColor(),
					null,
					color7.ToColor());
			}
			else {
				_setColors(
					color0.ToColor(),
					Color.FromArgb(255,
					               _clamp((color3.R + color4.R) / 2),
					               _clamp((color3.G + color4.G) / 2),
					               _clamp((color3.B + color4.B) / 2)),
					color7.ToColor());
			}
		}

		private void _setColors(Color first, Color? middle, Color last) {
			_colorFirst.Background = new SolidColorBrush {Color = first};

			if (middle != null) {
				_colorMiddle.Background = new SolidColorBrush {Color = middle.Value};
				BitmapSource imageTop = PickerGradientHelper.CreateFourColorImage(
					middle.Value,
					Colors.White,
					middle.Value,
					middle.Value, 136, 10,
					out byte[] pixelsTop);
				BitmapSource imageBottom = PickerGradientHelper.CreateFourColorImage(
					middle.Value,
					middle.Value,
					Colors.Black,
					middle.Value, 136, 10,
					out byte[] pixelsBottom);

				byte[] pixels = new byte[pixelsTop.Length + pixelsBottom.Length];

				Buffer.BlockCopy(pixelsTop, 0, pixels, 0, pixelsTop.Length);
				Buffer.BlockCopy(pixelsBottom, 0, pixels, pixelsTop.Length, pixelsBottom.Length);

				ImageBrush brush = new ImageBrush();
				brush.ImageSource = BitmapSource.Create(imageTop.PixelWidth,
				                                        imageBottom.PixelHeight + imageTop.PixelHeight, 96, 96,
				                                        PixelFormats.Rgb24, null, pixels, imageTop.PixelWidth * 3);
				_gpGradient.GradientBackground = brush;
			}

			_colorLast.Background = new SolidColorBrush {Color = last};
		}

		private void _setGradient() {
			Color first = _getColor(_colorFirst).Value;
			Color last = _getColor(_colorLast).Value;
			Color? middle = _getColor(_colorMiddle);

			var firstGrf = first.ToGrfColor().Hsl;
			var middleGrf = middle.Value.ToGrfColor().Hsl;
			var lastGrf = last.ToGrfColor().Hsl;

			double max = Math.Min(firstGrf.Lightness - middleGrf.Lightness, middleGrf.Lightness - lastGrf.Lightness);
			_gpGradient.SetPosition(max, true);
		}

		private void _setColorPicker(Color? value) {
			if (value != null)
				_colorPicker.SelectColorNoEvents(value.Value);
		}

		private byte _clamp(int color) {
			return (byte) (color < 0 ? 0 : color > 255 ? 255 : color);
		}

		public class GradientTargetData {
			public Color? FirstColor;
			public Color? MiddleColor;
			public Color? LastColor;
			public GradientSelectionType Selection = GradientSelectionType.Middle;
		}

		public enum GradientSelectionType {
			First,
			Middle,
			Last,
		}

		public enum GradientBlendMode {
			Linear,
		}

		private byte[] _makeGradient(GradientTargetData target) {
			byte[] colorsData = Methods.Copy(_paletteOldSelected);
			int baseIndex = _paletteSelector.SelectedItems.Count - 8;
			
			Color selectedColor = new Color();

			switch (target.Selection) {
				case GradientSelectionType.First:
					selectedColor = target.FirstColor.Value;
					break;
				case GradientSelectionType.Middle:
					selectedColor = target.MiddleColor.Value;
					break;
				case GradientSelectionType.Last:
					selectedColor = target.LastColor.Value;
					break;
			}
			
			switch (target.Selection) {
				case GradientSelectionType.First:
					if (target.MiddleColor == null)
						_applyGradientSlope(0, 8, selectedColor, target.LastColor.Value, colorsData);
					else
						_applyGradientSlope(0, 4, selectedColor, target.MiddleColor.Value, colorsData);
					break;
				case GradientSelectionType.Middle:
					if (target.FirstColor != null && target.LastColor != null) {
						_applyGradientSlope(0, 4, target.FirstColor.Value, selectedColor, colorsData);
						_applyGradientSlope(4, 4, selectedColor, target.LastColor.Value, colorsData);
					}
					else if (target.FirstColor != null) {
						_applyGradientSlope(0, 4, target.FirstColor.Value, selectedColor, colorsData);
						_applyGradientSlope(4, 4, selectedColor, colorsData);
					}
					else if (target.LastColor != null) {
						_applyGradientSlope(0, 4, selectedColor, colorsData);
						_applyGradientSlope(4, 4, selectedColor, target.LastColor.Value, colorsData);
					}
					else {
						_applyGradientSlope(0, 8, selectedColor, colorsData);
					}
					break;
				case GradientSelectionType.Last:
					if (target.MiddleColor == null)
						_applyGradientSlope(0, 8, target.FirstColor.Value, selectedColor, colorsData);
					else
						_applyGradientSlope(4, 4, target.MiddleColor.Value, selectedColor, colorsData);
					break;
			}

			return colorsData;
		}

		private void _applyGradientSlope(int from, int length, in Color selectedColor, byte[] gradient) {
			var grayScale = selectedColor.R == selectedColor.G && selectedColor.G == selectedColor.B;

			if (!grayScale && (
				_colorPicker.ColorMode == ColorMode.Hue ||
				_colorPicker.ColorMode == ColorMode.Sat ||
				_colorPicker.ColorMode == ColorMode.Bright)) {
				var grfColorVector3 = GrfColor.FromByteArray(_paletteOldSelected, 4 * 3, GrfImageType.Indexed8).ToTkVector4();
				var grfColorVector4 = GrfColor.FromByteArray(_paletteOldSelected, 4 * 4, GrfImageType.Indexed8).ToTkVector4();

				var hsvOriginalColor = ((grfColorVector3 + grfColorVector4) * 0.5f).ToGrfColor().Hsv;
				var hsvSelectedColor = selectedColor.ToGrfColor().Hsv;

				var diffHue = hsvSelectedColor.Hue - hsvOriginalColor.Hue;
				var diffSat = hsvSelectedColor.Saturation - hsvOriginalColor.Saturation;
				var diffBright = hsvSelectedColor.Brightness - hsvOriginalColor.Brightness;

				for (int i = 0; i < length; i++) {
					var hsvColor = GrfColor.FromByteArray(_paletteOldSelected, 4 * (i + from), GrfImageType.Indexed8).Hsv;
					hsvColor.Hue = _fixHsvValue(hsvColor.Hue + diffHue);
					hsvColor.Saturation = hsvColor.Saturation + diffSat;
					hsvColor.Brightness = hsvColor.Brightness + diffBright;

					if (hsvColor.Brightness > 1) {
						hsvColor.Saturation += 1 - hsvColor.Brightness;
					}
					else if (hsvColor.Brightness < 0) {
						hsvColor.Saturation += hsvColor.Brightness;
					}

					Buffer.BlockCopy(hsvColor.ToColor().ToRgbaBytes(), 0, gradient, 4 * (i + from), 4);
				}
			}
			else {
				double factor = _gpGradient.Position;
				var hslMiddleColor = selectedColor.ToGrfColor().Hsl;
				
				for (int i = 0; i < length; i++) {
					HslColor colorD = hslMiddleColor;
					colorD.Lightness += factor - i * (2 * factor) / 7;
					Buffer.BlockCopy(colorD.ToColor().ToRgbaBytes(), 0, gradient, 4 * i, 4);
				}
			}
		}

		private void _applyGradientSlope(int from, int length, in Color startColor, in Color targetColor, byte[] gradient) {
			double[] colorsDiffs = new double[4];
			double[] slopes = new double[length];

			colorsDiffs[0] = targetColor.A - startColor.A;
			colorsDiffs[1] = targetColor.R - startColor.R;
			colorsDiffs[2] = targetColor.G - startColor.G;
			colorsDiffs[3] = targetColor.B - startColor.B;

			for (int i = 0; i < length; i++) {
				if (length == 8) {
					slopes[i] = i * 1d / 7d;
				}
				else if (length == 4 && from == 0) {
					slopes[i] = i * 1d / 3.5d;
				}
				else if (length == 4 && from == 4) {
					slopes[i] = i * 1d / 3.5d + 1d / 7d;
				}
			}

			for (int i = 0; i < length; i++) {
				Color color;

				color = Color.FromArgb(
					255,
					Methods.ClampToColorByte((int)(colorsDiffs[1] * slopes[i] + startColor.R)),
					Methods.ClampToColorByte((int)(colorsDiffs[2] * slopes[i] + startColor.G)),
					Methods.ClampToColorByte((int)(colorsDiffs[3] * slopes[i] + startColor.B))
					);

				Buffer.BlockCopy(color.ToBytesRgba(), 0, gradient, 4 * (from + i), 4);
			}
		}

		private double _fixHsvValue(double value) {
			if (value < 0)
				return value + 1;
			if (value > 1)
				return value - 1;
			return value;
		}

		private byte[] _makeGradient(Color colorMiddle, double factor) {
			var grfColorMiddle = colorMiddle.ToGrfColor();

			HslColor hslColor = grfColorMiddle.Hsl;

			byte[] colorsData = new byte[8 * 4];

			int baseIndex = _paletteSelector.SelectedItems.Count - 8;

			for (int i = 0; i < 8; i++) {
				HslColor colorD = hslColor;
				colorD.Lightness += factor - i * (2 * factor) / 7;
				Buffer.BlockCopy(colorD.ToColor().ToRgbaBytes(), 0, colorsData, 4 * i, 4);
			}

			return colorsData;
		}

		private void _gpGradient_ValueChanged(object sender, ValueEventArgs args) {
			if (args.Preview)
				PreviewModifyColor();

			if (_getSelectedComponent() != _colorMiddle) {
				_select(1);
				_setColorPicker(_getColor(_colorMiddle));
			}

			if (_paletteSelector.SelectedItems.Count <= 0) return;

			int baseIndex = _paletteSelector.SelectedItems[0];
			_paletteSelector.SelectionChanged -= _paletteSelector_SelectionChanged;
			byte[] gradient = _makeGradient(_getColor(_colorMiddle).Value, args.Value);
			_paletteSelector.Palette[baseIndex / 16, (baseIndex % 16) / 8 * 8] = gradient;

			if (!args.Preview) {
				_pal.Commands.SetRawBytesInPalette(baseIndex * 4, _paletteOldSelected, gradient);
				_paletteOldSelected = null;
			}

			_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;

			_setColors(true);
		}

		public void PreviewModifyColor() {
			if (_paletteSelector.SelectedItems.Count <= 0) return;

			Border component = _getSelectedComponent();
			_colorPicker.Commands.ClearCommands();

			if (component != null) {
				if (_paletteOldSelected != null)
					return;

				int baseIndex = _paletteSelector.SelectedItems.Last();
				baseIndex = baseIndex / 16 * 16 + (baseIndex % 16) / 8 * 8;

				_paletteOldSelected = new byte[32];
				//Console.WriteLine("_paletteOldSelected set");
				byte[] palette = _pal.BytePalette;
				Buffer.BlockCopy(palette, baseIndex * 4, _paletteOldSelected, 0, _paletteOldSelected.Length);
				return;
			}
		}

		private byte[] _makeGradientCommon(Border component, Color value) {
			byte[] gradient = null;
			GradientTargetData targetData = new GradientTargetData();

			if (component == _colorFirst) {
				if (_locked[0])
					_lock(0);

				targetData.Selection = GradientSelectionType.First;
				targetData.FirstColor = value;
				targetData.MiddleColor = _locked[1] ? (Color?)_getColor(_colorMiddle).Value : null;
				targetData.LastColor = _getColor(_colorLast).Value;

				gradient = _makeGradient(targetData);
			}
			else if (component == _colorMiddle) {
				if (_locked[1])
					_lock(1);

				targetData.Selection = GradientSelectionType.Middle;
				targetData.FirstColor = _locked[0] ? (Color?)_getColor(_colorFirst).Value : null;
				targetData.MiddleColor = value;
				targetData.LastColor = _locked[2] ? (Color?)_getColor(_colorLast).Value : null;

				gradient = _makeGradient(targetData);
			}
			else if (component == _colorLast) {
				if (_locked[2])
					_lock(2);

				targetData.Selection = GradientSelectionType.Last;
				targetData.FirstColor = _getColor(_colorFirst).Value;
				targetData.MiddleColor = _locked[1] ? (Color?)_getColor(_colorMiddle).Value : null;
				targetData.LastColor = value;

				gradient = _makeGradient(targetData);
			}

			return gradient;
		}

		private void _colorPicker_ColorChanged(ColorEventArgs args) {
			if (_paletteSelector.SelectedItems.Count <= 0) return;

			if (args.Preview)
				PreviewModifyColor();

			Border component = _getSelectedComponent();

			byte[] gradient = _makeGradientCommon(component, args.Value);

			if (component == _colorFirst) {
				_paletteSelector.SelectionChanged -= _paletteSelector_SelectionChanged;
				//byte[] gradient = _makeGradient(value, _getColor(_colorLast).Value);
				int baseIndex = _paletteSelector.SelectedItems.Last();
				_paletteSelector.Palette[baseIndex / 16, (baseIndex % 16) / 8 * 8] = gradient;
				_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;
				_setGradient();
				_setColors();
			}
			else if (component == _colorLast) {
				_paletteSelector.SelectionChanged -= _paletteSelector_SelectionChanged;
				//byte[] gradient = _makeGradient(_getColor(_colorFirst).Value, value);
				int baseIndex = _paletteSelector.SelectedItems.Last();
				_paletteSelector.Palette[baseIndex / 16, (baseIndex % 16) / 8 * 8] = gradient;
				_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;
				_setGradient();
				_setColors();
			}
			else if (component == _colorMiddle) {
				_paletteSelector.SelectionChanged -= _paletteSelector_SelectionChanged;
				//byte[] gradient = _makeGradient(value, _gpGradient.Position);
				int baseIndex = _paletteSelector.SelectedItems.Last();
				_paletteSelector.Palette[baseIndex / 16, (baseIndex % 16) / 8 * 8] = gradient;
				_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;

				baseIndex = _paletteSelector.SelectedItems.Count - 8;
				GrfColor color0 = _paletteSelector.Palette.GetColor(_paletteSelector.SelectedItems[baseIndex]);
				GrfColor color7 = _paletteSelector.Palette.GetColor(_paletteSelector.SelectedItems[baseIndex + 7]);

				_setColors(color0.ToColor(), args.Value, color7.ToColor());
			}

			if (!args.Preview) {
				int baseIndex = _paletteSelector.SelectedItems[0];
				_pal.EnableRaiseEvents = false;
				_pal.Commands.SetRawBytesInPalette(baseIndex * 4, _paletteOldSelected, gradient);
				_colorPicker.Commands.ClearCommands();
				_paletteOldSelected = null;
				//Console.WriteLine("_paletteOldSelected unset");
				_pal.EnableRaiseEvents = true;
			}
		}

		private void _paletteSelector_SelectionChanged(object sender, ObservabableListEventArgs args) {
			if (args.Action == ObservableListEventType.Added) {
				_setColors();
				_setColorPicker(_getColor(_getSelectedComponent()));
				_setGradient();
			}
		}

		private void _select(int column) {
			_colorOverlay.Visibility = Visibility.Visible;
			_colorOverlay.SetValue(Grid.ColumnProperty, column);
		}

		private void _lock(int column) {
			List<Border> borders = new List<Border>();
			borders.Add(_colorOverlayLock0);
			borders.Add(_colorOverlayLock1);
			borders.Add(_colorOverlayLock2);

			_locked[column] = !_locked[column];

			borders[column].Visibility = _locked[column] ? Visibility.Visible : Visibility.Hidden;
		}

		private Border _getSelectedComponent() {
			if (_colorOverlay.Visibility == Visibility.Hidden)
				return null;

			int index = (int) _colorOverlay.GetValue(Grid.ColumnProperty);

			switch (index) {
				case 0:
					return _colorFirst;
				case 1:
					return _colorMiddle;
				case 2:
					return _colorLast;
			}

			return null;
		}

		private Color? _getColor(Border colorFirst) {
			if (colorFirst == null)
				return null;

			if (colorFirst.Background == null)
				return null;

			return ((SolidColorBrush) colorFirst.Background).Color;
		}

		private void _colorFirst_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			_setColorPicker(_getColor(_colorFirst));
			_select(0);
		}

		private void _colorMiddle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			_setColorPicker(_getColor(_colorMiddle));
			_select(1);
		}

		private void _colorLast_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			_setColorPicker(_getColor(_colorLast));
			_select(2);
		}

		private void _colorFirst_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => _lock(0);
		private void _colorMiddle_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => _lock(1);
		private void _colorLast_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => _lock(2);

		public void FocusGrid() {
			PaletteSelector.GridFocus.Focus();
			Keyboard.Focus(PaletteSelector.GridFocus);
		}
	}
}