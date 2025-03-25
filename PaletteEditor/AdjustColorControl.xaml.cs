using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities.Controls;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for AdjustColorControl.xaml
	/// </summary>
	public partial class AdjustColorControl : UserControl {
		private readonly double[] _hueDistances = new double[256];
		private readonly double[] _hueMultipliers = new double[256];
		private readonly byte[] _orignalPalette = new byte[1024];
		private byte[] _oldBytes = new byte[32];
		private Pal _pal;

		
		public bool PalAtTop {
			get { return (bool)GetValue(PalAtTopProperty); }
			set { SetValue(PalAtTopProperty, value); }
		}
		public static DependencyProperty PalAtTopProperty = DependencyProperty.Register("PalAtTop", typeof(bool), typeof(AdjustColorControl), new PropertyMetadata(new PropertyChangedCallback(OnPalAtTopChanged)));

		private static void OnPalAtTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var obj = d as AdjustColorControl;

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

		public AdjustColorControl() {
			InitializeComponent();

			Loaded += delegate {
				_sliderHue.SetPosition(0.5, true);
				_sliderSaturation.SetPosition(0.5, true);
				_sliderLightness.SetPosition(0.5, true);
				_sliderFuzziness.SetPosition(0, false);

				_tbHue.Text = "0";
				_tbSaturation.Text = "0";
				_tbLightness.Text = "0";
				_tbFuzziness.Text = "0";
			};

			_paletteSelector.SelectionChanged += new ObservableList.ObservableListEventHandler(_paletteSelector_SelectionChanged);

			_sliderFuzziness.PreviewMouseDown += (e, a) => _colorPicker_PreviewColorChanged();
			_sliderHue.PreviewMouseDown += (e, a) => _colorPicker_PreviewColorChanged();
			_sliderSaturation.PreviewMouseDown += (e, a) => _colorPicker_PreviewColorChanged();
			_sliderLightness.PreviewMouseDown += (e, a) => _colorPicker_PreviewColorChanged();
		}

		public PaletteSelector PaletteSelector {
			get { return _paletteSelector; }
		}

		private void _colorPicker_PreviewColorChanged() {
			if (_paletteSelector.SelectedItems.Count <= 0) return;

			int index = _paletteSelector.SelectedItems.Last();

			_oldBytes = new byte[32];
			byte[] palette = _pal.BytePalette;
			Buffer.BlockCopy(palette, index * 4, _oldBytes, 0, _oldBytes.Length);
		}

		private void _paletteSelector_SelectionChanged(object sender, ObservabableListEventArgs args) {
			MultiColorDialog window = WpfUtilities.FindDirectParentControl<MultiColorDialog>(this);

			if (_pal.Commands.IsLocked) return;

			if (window != null) {
				if (window.Selected == 2) {
					_sliderFuzziness_ValueChanged(this, _sliderFuzziness.Position);
				}
			}
			else {
				_sliderFuzziness_ValueChanged(this, _sliderFuzziness.Position);
			}
		}

		public void SetPalette(Pal pal) {
			if (_pal != null)
				_pal.PaletteChanged -= _pal_PaletteChanged;

			_setOriginalPalette(pal);
			pal.PaletteChanged += _pal_PaletteChanged;
		}

		private void _setOriginalPalette(Pal pal) {
			_pal = pal;
			Buffer.BlockCopy(_pal.BytePalette, 0, _orignalPalette, 0, 1024);
			_paletteSelector.SetPalette(pal);
		}

		private void _pal_PaletteChanged(object sender) {
			_setOriginalPalette(_pal);

			if (_paletteSelector.SelectedItem != null) {
				_paletteSelector_SelectionChanged(this, null);
			}
		}

		private byte[] _copyOriginal() {
			byte[] copy = new byte[1024];
			Buffer.BlockCopy(_orignalPalette, 0, copy, 0, 1024);
			return copy;
		}

		private void _sliderFuzziness_ValueChanged(object sender, double value) {
			_tbFuzziness.Text = String.Format("{0:0}", value * 127d);

			if (_paletteSelector.SelectedItem == null)
				return;

			GrfColor color = new Pal(_orignalPalette, false).GetColor(_paletteSelector.SelectedItem.Value);

			const double XMarker = 0.8d;
			const double YFactor = 1 / 2d;
			const double ComputedValueA = (XMarker * YFactor - 1d) / (XMarker - 1d);
			const double ComputedValueB = 1d - ComputedValueA;

			if (value < XMarker)
				value = value * YFactor;
			else
				value = ComputedValueA * value + ComputedValueB;

			double fullVal = value;

			value = value / 2d;

			double hue = color.Hue;
			double hueMin = hue - value;
			double hueMax = hue + value;
			double rotatedHue;

			if (fullVal <= 0) {
				for (int i = 0; i < 256; i++) {
					_hueMultipliers[i] = 0;
				}

				if (_paletteSelector.SelectedItem != null) {
					_hueMultipliers[_paletteSelector.SelectedItem.Value] = 1f;
				}
			}
			else {
				byte[] original = _copyOriginal();
				Pal originalPal = new Pal(original);

				for (int i = 0; i < 256; i++) {
					GrfColor palColor = originalPal.GetColor(i);

					if (_hueBetween(palColor.Hue, hueMin, hueMax, out rotatedHue)) {
						_hueMultipliers[i] = (1d - Math.Abs((((rotatedHue - hueMin) / fullVal) - 0.5d) * 2d));
						_hueDistances[i] = Math.Abs((((rotatedHue - hueMin) / fullVal) - 0.5d) * 2d);
					}
					else {
						_hueMultipliers[i] = 0;
						_hueDistances[i] = 1;
					}
				}
			}

			_sliderHue.GradientBackground = _generateHueGradient(color);
			_update();
		}

		private Brush _generateHueGradient(GrfColor color) {
			LinearGradientBrush brush = new LinearGradientBrush();

			brush.StartPoint = new Point(0, 0);
			brush.EndPoint = new Point(1, 0);

			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue - 3d / 6d, color.Saturation, color.Brightness).ToColor(), 0 / 6d));
			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue - 2d / 6d, color.Saturation, color.Brightness).ToColor(), 1 / 6d));
			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue - 1d / 6d, color.Saturation, color.Brightness).ToColor(), 2 / 6d));
			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue - 0d / 6d, color.Saturation, color.Brightness).ToColor(), 3 / 6d));
			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue + 1d / 6d, color.Saturation, color.Brightness).ToColor(), 4 / 6d));
			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue + 2d / 6d, color.Saturation, color.Brightness).ToColor(), 5 / 6d));
			brush.GradientStops.Add(new GradientStop(GrfColor.FromHsv(color.Hue + 3d / 6d, color.Saturation, color.Brightness).ToColor(), 6 / 6d));

			return brush;
		}

		private void _print(double[] palette) {
			Console.WriteLine();
			Console.WriteLine("####################################");
			for (int i = 0; i < 16; i++) {
				Console.Write("{ ");
				for (int j = 0; j < 16; j++) {
					Console.Write(String.Format("{0:0.00}; ", palette[16 * i + j]));
				}
				Console.WriteLine(" }");
			}
		}

		private bool _hueBetween(double hue, double hueMin, double hueMax, out double rotatedHue) {
			if (hueMin <= hue && hue <= hueMax) {
				rotatedHue = hue;
				return true;
			}

			if (hue < hueMin && hue < hueMax) {
				while (hue < hueMin && hue < hueMax) {
					hue += 1f;
				}

				if (hueMin <= hue && hue <= hueMax) {
					rotatedHue = hue;
					return true;
				}

				rotatedHue = double.NaN;
				return false;
			}

			if (hue > hueMin && hue > hueMax) {
				while (hue > hueMin && hue > hueMax) {
					hue -= 1f;
				}

				if (hueMin <= hue && hue <= hueMax) {
					rotatedHue = hue;
					return true;
				}

				rotatedHue = double.NaN;
				return false;
			}

			rotatedHue = double.NaN;
			return false;
		}

		private void _sliderHue_ValueChanged(object sender, double value) {
			double val = value * 360f - 180;
			_tbHue.Text = String.Format("{0:0}", val);

			_update();
		}

		private void _update() {
			_pal.EnableRaiseEvents = false;

			try {
				_pal.PaletteChanged -= _pal_PaletteChanged;
				byte[] original = _copyOriginal();
				Pal originalPal = new Pal(original);

				double hueDiff = _sliderHue.Position - 0.5d;
				double satDiff = _sliderSaturation.Position * 2d - 1d;
				bool negSat = satDiff < 0;
				satDiff = Math.Abs(satDiff);
				double ligDiff = _sliderLightness.Position - 0.5d;

				for (int i = 0; i < 256; i++) {
					if (_hueMultipliers[i] == 0) continue;

					GrfColor palColor = originalPal.GetColor(i);

					double satDiff2 = - palColor.Hsl.S * satDiff / (satDiff - 1.0048);
					satDiff2 = negSat ? -1 * satDiff2 : satDiff2;

					GrfColor palColor2 = GrfColor.FromHsl(
						palColor.Hue + hueDiff * _hueMultipliers[i],
						GrfColor.ClampDouble(palColor.Hsl.S + satDiff2 * _hueMultipliers[i]),
						GrfColor.ClampDouble(palColor.Lightness + ligDiff * _hueMultipliers[i]),
						palColor.A);

					Buffer.BlockCopy(palColor2.ToRgbaBytes(), 0, original, 4 * i, 4);
				}

				original[3] = 0;
				_pal.Commands.SetRawBytesInPalette(0, _copyOriginal(), original);
			}
			finally {
				_pal.EnableRaiseEvents = true;
				_pal.OnPaletteChanged();
				_pal.PaletteChanged += _pal_PaletteChanged;
			}
		}

		private void _sliderSaturation_ValueChanged(object sender, double value) {
			double val = value * 200f - 100;
			_tbSaturation.Text = String.Format("{0:0}", val);

			_update();
		}

		private void _sliderLightness_ValueChanged(object sender, double value) {
			double val = value * 200f - 100;
			_tbLightness.Text = String.Format("{0:0}", val);

			_update();
		}

		private void _buttonFix_Click(object sender, RoutedEventArgs e) {
			if (_pal != null)
				_setOriginalPalette(_pal);
		}
	}
}