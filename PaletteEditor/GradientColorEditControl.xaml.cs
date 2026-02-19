using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPicker;
using ColorPicker.Core;
using ColorPicker.Core.Commands;
using ColorPicker.Sliders;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Utilities.Commands;
using Utilities.Controls;
using ICommand = ColorPicker.Core.Commands.ICommand;

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
			_colorPicker.ColorChanged += new SliderGradient.GradientPickerColorEventHandler(_colorPicker_ColorChanged);
			_colorPicker.Commands.PreviewCommandExecuted += new AbstractCommand<ICommand>.AbstractCommandsEventHandler(_commands_PreviewCommandExecuted);
			_gpGradient.PreviewMouseDown += (e, a) => _colorPicker_PreviewColorChanged(this, Colors.White);
			_colorPicker.PreviewColorChanged += new SliderGradient.GradientPickerColorEventHandler(_colorPicker_PreviewColorChanged);
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

		private void _colorPicker_PreviewColorChanged(object sender, Color value) {
			if (_paletteSelector.SelectedItems.Count <= 0) return;

			Border component = _getSelectedComponent();

			if (component != null) {
				int baseIndex = _paletteSelector.SelectedItems.Last();
				baseIndex = baseIndex / 16 * 16 + (baseIndex % 16) / 8 * 8;

				_paletteOldSelected = new byte[32];
				byte[] palette = _pal.BytePalette;
				Buffer.BlockCopy(palette, baseIndex * 4, _paletteOldSelected, 0, _paletteOldSelected.Length);
			}
		}

		private void _commands_PreviewCommandExecuted(object sender, ICommand command) {
			if (_paletteSelector.SelectedItems.Count <= 0) return;

			Border component = _getSelectedComponent();
			ChangeColor changeColor = (ChangeColor) command;

			byte[] gradient = _makeGradientCommon(component, changeColor.NewColor);

			if (gradient != null) {
				int baseIndex = _paletteSelector.SelectedItems[0];
				_pal.EnableRaiseEvents = false;
				_pal.Commands.SetRawBytesInPalette(baseIndex * 4, _paletteOldSelected, gradient);
				_pal.EnableRaiseEvents = true;
			}
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

		private void _setColor(Color colorFirst, Color colorMiddle, int arrayIndex, float factor, byte[] colorsData) {
			byte[] color = new byte[4];
			color[0] = _clamp((int) (colorFirst.R + (colorMiddle.R - colorFirst.R) * factor));
			color[1] = _clamp((int) (colorFirst.G + (colorMiddle.G - colorFirst.G) * factor));
			color[2] = _clamp((int) (colorFirst.B + (colorMiddle.B - colorFirst.B) * factor));
			color[3] = 255;

			Buffer.BlockCopy(color, 0, colorsData, arrayIndex, 4);
		}

		private void _setGradient() {
			Color first = _getColor(_colorFirst).Value;
			Color last = _getColor(_colorLast).Value;
			Color? middle = _getColor(_colorMiddle);

			double max =
				(((first.R - middle.Value.R) / (255f - (middle.Value.R >= 255 ? 1 : middle.Value.R)) +
				  (first.G - middle.Value.G) / (255f - (middle.Value.G >= 255 ? 1 : middle.Value.G)) +
				  (first.B - middle.Value.B) / (255f - (middle.Value.B >= 255 ? 1 : middle.Value.B))) +
				 ((last.R - middle.Value.R) / (0f - (middle.Value.R <= 0 ? 1 : middle.Value.R)) +
				  (last.G - middle.Value.G) / (0f - (middle.Value.G <= 0 ? 1 : middle.Value.G)) +
				  (last.B - middle.Value.B) / (0f - (middle.Value.B <= 0 ? 1 : middle.Value.B)))) / 6f;

			var firstGrf = first.ToGrfColor().Hsv;
			var middleGrf = middle.Value.ToGrfColor().Hsv;
			var lastGrf = last.ToGrfColor().Hsv;

			max = middleGrf.Saturation - firstGrf.Saturation + firstGrf.Brightness - middleGrf.Brightness;
			_gpGradient.SetPosition(max, true);
		}

		private void _setColorPicker(Color? value) {
			if (value != null)
				_colorPicker.SelectColorNoEvents(value.Value);
		}

		private byte _clamp(int color) {
			return (byte) (color < 0 ? 0 : color > 255 ? 255 : color);
		}

		private byte[] _makeGradient(Color firstColor, Color lastColor) {
			byte[] colorsData = new byte[8 * 4];

			for (int i = 0; i < 8; i++) {
				Color color;

				color = Color.FromArgb(
					255,
					_clamp((int) ((lastColor.R - firstColor.R) / 7f * i + firstColor.R)),
					_clamp((int) ((lastColor.G - firstColor.G) / 7f * i + firstColor.G)),
					_clamp((int) ((lastColor.B - firstColor.B) / 7f * i + firstColor.B))
					);

				Buffer.BlockCopy(color.ToBytesRgba(), 0, colorsData, 4 * i, 4);
			}

			return colorsData;
		}

		private byte[] _makeGradient2(Color firstColor, Color lastColor, bool left) {
			byte[] colorsData = new byte[8 * 4];
			int baseIndex = _paletteSelector.SelectedItems[0];

			Buffer.BlockCopy(_pal.BytePalette, baseIndex * 4, colorsData, 0, 8 * 4);

			if (left) {
				//lastColor = new GrfColor(colorsData, 4 * 4).ToColor();

				for (int i = 0; i < 4; i++) {
					Color color;

					color = Color.FromArgb(
						255,
						_clamp((int)((lastColor.R - firstColor.R) / 7f * 2 * i + firstColor.R)),
						_clamp((int)((lastColor.G - firstColor.G) / 7f * 2 * i + firstColor.G)),
						_clamp((int)((lastColor.B - firstColor.B) / 7f * 2 * i + firstColor.B))
						);

					Buffer.BlockCopy(color.ToBytesRgba(), 0, colorsData, 4 * i, 4);
				}
			}
			else {
				//firstColor = new GrfColor(colorsData, 3 * 4).ToColor();

				for (int i = 0; i < 4; i++) {
					Color color;

					color = Color.FromArgb(
						255,
						_clamp((int)((lastColor.R - firstColor.R) / 7f * (2 * i + 1) + firstColor.R)),
						_clamp((int)((lastColor.G - firstColor.G) / 7f * (2 * i + 1) + firstColor.G)),
						_clamp((int)((lastColor.B - firstColor.B) / 7f * (2 * i + 1) + firstColor.B))
						);

					Buffer.BlockCopy(color.ToBytesRgba(), 0, colorsData, 4 * i + 16, 4);
				}
			}
			
			return colorsData;
		}

		/// <summary>
		/// Makes a gradient from 3 colors.
		/// </summary>
		/// <param name="firstColor">The first color.</param>
		/// <param name="middleColor">The middle color.</param>
		/// <param name="lastColor">The last color.</param>
		/// <returns></returns>
		private byte[] _makeGradient3(Color firstColor, Color middleColor, Color lastColor) {
			byte[] colorsData = new byte[8 * 4];

			for (int i = 0; i < 4; i++) {
				Color color;

				color = Color.FromArgb(
					255,
					_clamp((int)((middleColor.R - firstColor.R) / 7f * (2 * i) + firstColor.R)),
					_clamp((int)((middleColor.G - firstColor.G) / 7f * (2 * i) + firstColor.G)),
					_clamp((int)((middleColor.B - firstColor.B) / 7f * (2 * i) + firstColor.B))
					);

				Buffer.BlockCopy(color.ToBytesRgba(), 0, colorsData, 4 * i, 4);
			}

			for (int i = 0; i < 4; i++) {
				Color color;

				color = Color.FromArgb(
					255,
					_clamp((int)((lastColor.R - middleColor.R) / 7f * (2 * i + 1) + middleColor.R)),
					_clamp((int)((lastColor.G - middleColor.G) / 7f * (2 * i + 1) + middleColor.G)),
					_clamp((int)((lastColor.B - middleColor.B) / 7f * (2 * i + 1) + middleColor.B))
					);

				Buffer.BlockCopy(color.ToBytesRgba(), 0, colorsData, 4 * i + 16, 4);
			}

			return colorsData;
		}

		private byte[] _makeGradient(Color colorMiddle, double factor) {
			byte[] colorsData = new byte[8 * 4];

			int baseIndex = _paletteSelector.SelectedItems.Count - 8;

			GrfColor colorMiddleGrf = colorMiddle.ToGrfColor();
			HsvColor hsvColorMiddle = colorMiddleGrf.Hsv;
			int hue = (((int)(hsvColorMiddle.Hue * 100)) / 5);

			double[] hues = new double[8];

			switch (hue) {
				case 19: hues = new double[] { -0.921550671550672, -0.974519632414369, -0.979717813051146, -0.986507936507937, -0.011671335200747, -0.0173160173160173, -0.0357793390580275, -0.0587301587301587 }; break;
				case 18: hues = new double[] { 0.0156202950918397, 0.0109924026590693, 0.00886045221251197, 0.00226942628903393, -0.0028314546430489, -0.00895061728395063, -0.0209034792368127, -0.0236229819563155 }; break;
				case 17: hues = new double[] { 0.0562448304383788, 0.032051282051282, 0.020940170940171, 0.00824175824175832, -0.0112820512820513, -0.0295429208472686, -0.0512820512820512, -0.0762820512820512 }; break;
				case 16: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 15: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 14: hues = new double[] { -0.0595238095238096, -0.0428571428571429, -0.00824175824175832, 0, 0.00369458128078815, -0.0195238095238096, -0.0292207792207793, -0.0428571428571429 }; break;
				case 13: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 12: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 11: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 10: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 9: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 8: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 7: hues = new double[] { -0.143725389488101, -0.112596553773024, -0.0737608644585389, -0.0171717171717172, 0.019387422613229, 0.0923520923520924, 0.163780663780664, 0.201346801346801 }; break;
				case 6: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 5: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				case 4: hues = new double[] { -0.08, -0.0574011299435028, -0.0227083333333333, -0.00121212121212116, -0.000289855072463735, 0.02, 0.0533333333333333, 0.0496296296296297 }; break;
				case 3: hues = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }; break;
				//case 2: hues = new double[] { 0.037037037037037, 0.0316606929510155, 0.0197956577266922, 0.00617283950617284, -0.00783475783475787, -0.0243664717348928, -0.0601851851851852, 0.822751322751323 }; break;
				case 2: hues = new double[] { 0.037037037037037, 0.0316606929510155, 0.0197956577266922, 0.00617283950617284, -0.00783475783475787, -0.0243664717348928, -0.0601851851851852, -0.082751322751323 }; break;
				case 1: hues = new double[] { 0.09641456582633, 0.0373763963416692, 0.0123371087565461, 0.00254856040776966, -0.00360492022896115, -0.0100657085264012, -0.0213585434173669, -0.0334553176109153 }; break;
				case 0: hues = new double[] { 0.0818293683347006, 0.04857367475292, 0.0261739253794865, 0.0101944341677971, -0.00861938319314996, -0.0154353795730357, -0.0237302468157074, -0.0377358490566038 }; break;
				default:
					hues = new double[] { 0.059496996996997, 0.0337418300653595, 0.0138888888888889, 0.000893839383938388, -0.00231481481481483, -0.00743464052287582, -0.0138888888888889, -0.0175376647834275 };
					break;
			}

			if (hues[0] == 0) {
				//hues = new double[] { 0.0156202950918397, 0.0109924026590693, 0.00886045221251197, 0.00226942628903393, -0.0028314546430489, -0.00895061728395063, -0.0209034792368127, -0.0236229819563155 };
			}

			for (int i = 0; i < 8; i++) {
				HsvColor colorD = new GrfColor(colorMiddleGrf).Hsv;
				colorD.Hue += hues[i];
				_applyFactor(colorD, factor - i * (2 * factor) / 7);
				Buffer.BlockCopy(colorD.ToColor().ToRgbaBytes(), 0, colorsData, 4 * i, 4);
			}

			return colorsData;
		}

		private void _applyFactor(HsvColor color, double factor) {
			if (factor > 0) {
				double remains = (color.Brightness + factor - 1);
				color.Brightness += factor;

				if (remains > 0) {
					color.Saturation -= remains;
				}
			}
			else {
				factor *= -1;
				double remains = (color.Brightness - factor);
				color.Brightness -= factor;

				if (remains < 0) {
					color.Saturation -= remains;
				}
			}
		}

		private void _gpGradient_ValueChanged(object sender, double value) {
			if (_getSelectedComponent() != _colorMiddle) {
				_select(1);
				_setColorPicker(_getColor(_colorMiddle));
			}

			if (_paletteSelector.SelectedItems.Count <= 0) return;

			int baseIndex = _paletteSelector.SelectedItems[0];
			_paletteSelector.SelectionChanged -= _paletteSelector_SelectionChanged;
			byte[] gradient = _makeGradient(_getColor(_colorMiddle).Value, value);
			_paletteSelector.Palette[baseIndex / 16, (baseIndex % 16) / 8 * 8] = gradient;

			_pal.EnableRaiseEvents = false;
			_pal.Commands.SetRawBytesInPalette(baseIndex * 4, _paletteOldSelected, gradient);
			_pal.EnableRaiseEvents = true;

			_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;

			_setColors(true);
		}

		private byte[] _makeGradientCommon(Border component, Color value) {
			byte[] gradient = null;

			if (component == _colorFirst) {
				if (_locked[0]) {
					_lock(0);
				}

				if (_locked[1]) {
					gradient = _makeGradient2(value, _getColor(_colorMiddle).Value, true);
				}
				else {
					gradient = _makeGradient(value, _getColor(_colorLast).Value);
				}
			}
			else if (component == _colorLast) {
				if (_locked[2]) {
					_lock(2);
				}

				if (_locked[1]) {
					gradient = _makeGradient2(_getColor(_colorMiddle).Value, value, false);
				}
				else {
					gradient = _makeGradient(_getColor(_colorFirst).Value, value);
				}
			}
			else if (component == _colorMiddle) {
				if (_locked[1]) {
					_lock(1);
				}

				if (_locked[0] && _locked[2]) {
					gradient = _makeGradient3(_getColor(_colorFirst).Value, value, _getColor(_colorLast).Value);
				}
				else if (_locked[0]) {
					gradient = _makeGradient(_getColor(_colorMiddle).Value, _gpGradient.Position);
					gradient = _makeGradient3(_getColor(_colorFirst).Value, value, GrfColor.FromByteArray(gradient, 28, GrfImageType.Indexed8).ToColor());
				}
				else if (_locked[2]) {
					gradient = _makeGradient(_getColor(_colorMiddle).Value, _gpGradient.Position);
					gradient = _makeGradient3(GrfColor.FromByteArray(gradient, 0, GrfImageType.Indexed8).ToColor(), value, _getColor(_colorLast).Value);
				}
				else {
					gradient = _makeGradient(_getColor(_colorMiddle).Value, _gpGradient.Position);
				}
			}

			return gradient;
		}

		private void _colorPicker_ColorChanged(object sender, Color value) {
			if (_paletteSelector.SelectedItems.Count <= 0) return;

			Border component = _getSelectedComponent();

			byte[] gradient = _makeGradientCommon(component, value);

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

				_setColors(color0.ToColor(), value, color7.ToColor());
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

		private void _colorFirst_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			_lock(0);
		}

		private void _colorMiddle_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			_lock(1);
		}

		private void _colorLast_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			_lock(2);
		}

		public void FocusGrid() {
			PaletteSelector.GridFocus.Focus();
			Keyboard.Focus(PaletteSelector.GridFocus);
		}
	}
}