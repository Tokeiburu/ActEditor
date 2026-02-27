using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities.Tools;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SpriteConverterFormatDialog.xaml
	/// </summary>
	public partial class SpriteConverterFormatDialog : TkWindow {
		#region Bgra32Mode enum

		public enum Bgra32Mode {
			Normal,
			PixelIndexZero,
			PixelIndexPink,
			FirstPixel,
			LastPixel
		}

		#endregion

		private readonly GrfImage _image;
		private readonly Spr _spr;
		private readonly List<GrfImage> _images = new List<GrfImage>();
		private readonly List<CheckBox> _rbs = new List<CheckBox>();
		private readonly List<Border> _borders = new List<Border>();
		private readonly List<ScrollViewer> _svs = new List<ScrollViewer>();
		private readonly HashSet<byte> _unusedIndexes = new HashSet<byte>();
		private byte[] _originalPalette;
		private GrfImage _result;

		private bool _svEventsEnabled;
		private ZoomEngine _zoom;
		private bool _isLoading;

		public SpriteConverterFormatDialog(byte[] originalPalette, GrfImage image, Spr spr, int option = -1) : base("Format conflict", "app.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			if (originalPalette == null) throw new ArgumentNullException("originalPalette");

			InitializeComponent();

			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			_images.Add(null);
			_images.Add(null);
			_images.Add(null);
			_images.Add(null);
			_images.Add(null);
			_images.Add(null);
			_originalPalette = originalPalette;
			RepeatOption = option;

			if (RepeatOption <= -1)
				Visibility = Visibility.Visible;
			else {
				Visibility = Visibility.Hidden;
				Width = 0;
				Height = 0;
				WindowStyle = WindowStyle.None;
				ShowInTaskbar = false;
				ShowActivated = false;
			}

			_image = image;

			_spr = spr;
			_unusedIndexes = spr.GetUnusedPaletteIndexes();
			_unusedIndexes.Remove(0);

			_description.Text = "The image is invalid for this operation. Select one of options below.";

			_cbTransparency.SelectionChanged += _cbTransparency_SelectionChanged;
			_cbDithering.Checked += _cbDithering_Checked;
			_cbDithering.Unchecked += _cbDithering_Unchecked;

			_setScrollViewers();

			WpfUtilities.AddMouseInOutUnderline(_cbDithering);
			WpfUtilities.AddMouseInOutUnderline(_cbRepeat);
			WpfUtilities.AddMouseInOutUnderline(_rbBgra32, _rbMatch, _rbOriginalPalette, _rbMerge, _rbMergeOctLab, _rbMergeOctRgb);

			Loaded += delegate {
				if (RepeatOption <= -1) {
					this.MinWidth = this.Width;
					this.MinHeight = this.Height;
					SizeToContent = SizeToContent.Manual;
				}

				_load();

				if (RepeatOption <= -1 && Owner != null) {
					this.Left = Owner.Left + (Owner.Width - this.ActualWidth) / 2;
					this.Top = Owner.Top + (Owner.Height - this.ActualHeight) / 2;
				}
			};

			_sv1.MouseLeftButtonUp += (s, e) => _rb_Checked(_rbOriginalPalette, null);
			_sv2.MouseLeftButtonUp += (s, e) => _rb_Checked(_rbBgra32, null);
			_sv3.MouseLeftButtonUp += (s, e) => _rb_Checked(_rbMatch, null);
			_sv4.MouseLeftButtonUp += (s, e) => _rb_Checked(_rbMerge, null);
			_sv5.MouseLeftButtonUp += (s, e) => _rb_Checked(_rbMergeOctRgb, null);
			_sv6.MouseLeftButtonUp += (s, e) => _rb_Checked(_rbMergeOctLab, null);
		}

		private bool _repeatBehavior { get; set; }
		public int RepeatOption { get; private set; }

		public GrfImage Result {
			get { return _result; }
			set {
				if (value != null && value.GrfImageType == GrfImageType.Indexed8) {
					_imagePalette.Source = ImageProvider.GetImage(value.Palette, ".pal").Cast<BitmapSource>();
				}
				_result = value;
			}
		}

		private void _setScrollViewers() {
			_svs.Add(_sv0);
			_svs.Add(_sv1);
			_svs.Add(_sv2);
			_svs.Add(_sv3);
			_svs.Add(_sv4);
			_svs.Add(_sv5);
			_svs.Add(_sv6);

			_svs.ForEach(p => p.ScrollChanged += (e, a) => { if (_svEventsEnabled) _setAllScrollViewers(p); });

			_zoom = new ZoomEngine();
			_zoom.MaxScale = 10;
			_zoom.MinScale = 1;
			Vector topLeft = new Vector(0, 0);
			Vector bottomRight = new Vector(0, 0);

			foreach (var sv in _svs) {
				var img = (Image)sv.Content;

				sv.PreviewMouseWheel += (sender, e) => {
					e.Handled = true;

					var mousePosition = e.GetPosition(sv);

					// Top left position
					topLeft.X = sv.HorizontalOffset / sv.ExtentWidth;
					topLeft.Y = sv.VerticalOffset / sv.ExtentHeight;

					// Bottom right position
					bottomRight.X = (sv.HorizontalOffset + sv.ActualWidth) / sv.ExtentWidth;
					bottomRight.Y = (sv.VerticalOffset + sv.ActualHeight) / sv.ExtentHeight;

					_zoom.Zoom(e.Delta);

					Vector imagePosition = new Vector(
						topLeft.X + (mousePosition.X / sv.ActualWidth) * (bottomRight.X - topLeft.X),
						topLeft.Y + (mousePosition.Y / sv.ActualHeight) * (bottomRight.Y - topLeft.Y));

					Vector diff = imagePosition - topLeft;
					Vector start = imagePosition - diff / _zoom.Scale * _zoom.OldScale;

					foreach (var sv2 in _svs) {
						var img2 = (Image)sv2.Content;
						img2.Width = _image.Width * _zoom.Scale;
						img2.Height = _image.Height * _zoom.Scale;

						img2.UpdateLayout();
						sv2.UpdateLayout();
					}

					sv.ScrollToHorizontalOffset(start.X * sv.ExtentWidth);
					sv.ScrollToVerticalOffset(start.Y * sv.ExtentHeight);
				};
			}

			_svEventsEnabled = true;
		}

		private void _setAllScrollViewers(ScrollViewer sv) {
			_svEventsEnabled = false;
			_svs.ForEach(p => {
				if (sv.ScrollableWidth > 0)
					p.ScrollToHorizontalOffset(sv.HorizontalOffset / sv.ScrollableWidth * p.ScrollableWidth);
				if (sv.ScrollableHeight > 0)
					p.ScrollToVerticalOffset(sv.VerticalOffset / sv.ScrollableHeight * p.ScrollableHeight);
			});
			_svEventsEnabled = true;
		}

		private void _load() {
			try {
				_isLoading = true;
				_rbs.Add(_rbOriginalPalette);
				_rbs.Add(_rbMatch);
				_rbs.Add(_rbMerge);
				_rbs.Add(_rbBgra32);
				_rbs.Add(_rbMergeOctRgb);
				_rbs.Add(_rbMergeOctLab);

				for (int i = 0; i < _rbs.Count; i++) {
					var parent = WpfUtilities.FindDirectParentControl<Border>(_rbs[i]);
					_borders.Add(parent);
				}

				_imageReal.Source = _image.Cast<BitmapSource>();
				_setImageDimensions(_imageReal);

				if (_image.GrfImageType == GrfImageType.Indexed8) {
					_images[0] = GrfImage.SprConvert(_spr, _image, false, GrfImage.SprTransparencyMode.Normal, GrfImage.SprConvertMode.Original);
					_imageOriginal.Source = _images[0].Cast<BitmapSource>();
				}
				else {
					_rbOriginalPalette.IsEnabled = false;
				}

				_setImageDimensions(_imageOriginal);
				_setImageDimensions(_imageClosestMatch);
				_setImageDimensions(_imageMergePalette);
				_setImageDimensions(_imageToBgra32);
				_setImageDimensions(_imageMergePaletteOctRgb);
				_setImageDimensions(_imageMergePaletteOctLab);

				if (RepeatOption > -1) {
					_repeatBehavior = true;
				}

				switch (ActEditorConfiguration.FormatConflictOption) {
					case 0:
						_rbOriginalPalette.IsChecked = true;
						break;
					case 1:
						_rbMatch.IsChecked = true;
						break;
					case 2:
						_rbMerge.IsChecked = true;
						break;
					case 3:
						_rbBgra32.IsChecked = true;
						break;
					case 4:
						_rbMergeOctRgb.IsChecked = true;
						break;
					case 5:
						_rbMergeOctLab.IsChecked = true;
						break;
				}

				_cbTransparency.SelectedIndex = ActEditorConfiguration.TransparencyMode;
				_cbDithering.IsChecked = ActEditorConfiguration.UseDithering;
				_imagePalette.Source = ImageProvider.GetImage(_originalPalette, ".pal").Cast<BitmapSource>();
				_isLoading = false;
				_update();
				_updateSelection();

				Loaded += new RoutedEventHandler(_spriteConverterFormatDialog_Loaded);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_isLoading = false;
			}
		}

		private void _spriteConverterFormatDialog_Loaded(object sender, RoutedEventArgs e) {
			if (RepeatOption > -1) {
				_repeatBehavior = true;
				_buttonOk_Click(null, null);
			}
			else {
				Visibility = Visibility.Visible;
			}
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
		}

		private void _setImageDimensions(FrameworkElement image) {
			image.Width = _image.Width;
			image.Height = _image.Height;

			Point lastPosition = new Point();
			ScrollViewer sv = WpfUtilities.FindDirectParentControl<ScrollViewer>(image);
			sv.PreviewMouseLeftButtonDown += (sender, args) => {
				lastPosition = args.GetPosition(sv);
				if (lastPosition.X < sv.ViewportWidth &&
					lastPosition.Y < sv.ViewportHeight)
					sv.CaptureMouse();
			};

			sv.PreviewMouseRightButtonDown += (sender, args) => {
				lastPosition = args.GetPosition(sv);
				if (lastPosition.X < sv.ViewportWidth &&
					lastPosition.Y < sv.ViewportHeight)
					sv.CaptureMouse();
			};

			sv.MouseLeftButtonUp += delegate {
				sv.ReleaseMouseCapture();
			};

			sv.MouseRightButtonUp += delegate {
				sv.ReleaseMouseCapture();
			};

			sv.MouseMove += (sender, args) => {
				if (!sv.IsMouseCaptured) return;
				var newPosition = args.GetPosition(sv);
				var delta = newPosition - lastPosition;
				lastPosition = newPosition;
				try {
					_svEventsEnabled = false;
					_sv0.ScrollToVerticalOffset(_sv0.VerticalOffset - delta.Y);
					_sv0.ScrollToHorizontalOffset(_sv0.HorizontalOffset - delta.X);
				}
				finally {
					_svEventsEnabled = true;
				}
			};
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			if (ActEditorConfiguration.FormatConflictOption == -1) {
				WindowProvider.ShowDialog("Please select an option or cancel.");
				return;
			}

			RepeatOption = _repeatBehavior ? ActEditorConfiguration.FormatConflictOption : -1;
			DialogResult = true;
			Close();
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;

			if (_cbRepeat.IsChecked == true)
				RepeatOption = -2;

			Close();
		}

		private void _cbTransparency_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			ActEditorConfiguration.TransparencyMode = _cbTransparency.SelectedIndex;
			_update();
			_updateSelection();
		}

		private void _cbDithering_Checked(object sender, RoutedEventArgs e) {
			ActEditorConfiguration.UseDithering = true;
			_update();
			_updateSelection();
		}

		private void _cbDithering_Unchecked(object sender, RoutedEventArgs e) {
			ActEditorConfiguration.UseDithering = false;
			_update();
			_updateSelection();
		}

		private void _update() {
			try {
				if (_isLoading)
					return;

				bool dithering = _cbDithering.IsChecked == true;

				_images[1] = GrfImage.SprConvert(_spr, _image, dithering, (GrfImage.SprTransparencyMode)_cbTransparency.SelectedIndex, GrfImage.SprConvertMode.BestMatch);
				_images[2] = GrfImage.SprConvert(_spr, _image, dithering, (GrfImage.SprTransparencyMode)_cbTransparency.SelectedIndex, GrfImage.SprConvertMode.MergeOld);
				_images[3] = GrfImage.SprConvert(_spr, _image, dithering, (GrfImage.SprTransparencyMode)_cbTransparency.SelectedIndex, GrfImage.SprConvertMode.Bgra32);
				_images[4] = GrfImage.SprConvert(_spr, _image, dithering, (GrfImage.SprTransparencyMode)_cbTransparency.SelectedIndex, GrfImage.SprConvertMode.MergeRgb);
				_images[5] = GrfImage.SprConvert(_spr, _image, dithering, (GrfImage.SprTransparencyMode)_cbTransparency.SelectedIndex, GrfImage.SprConvertMode.MergeLab);

				_imageClosestMatch.Source = _images[1].Cast<BitmapSource>();
				_imageMergePalette.Source = _images[2].Cast<BitmapSource>();
				_imageToBgra32.Source = _images[3].Cast<BitmapSource>();
				_imageMergePaletteOctRgb.Source = _images[4].Cast<BitmapSource>();
				_imageMergePaletteOctLab.Source = _images[5].Cast<BitmapSource>();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		#region Checkboxes

		private void _uncheckAll(object sender = null) {
			_rbs.ForEach(p => p.Unchecked -= _rb_Unchecked);
			_rbs.ForEach(p => p.IsChecked = false);
			_borders.ForEach(p => p.BorderBrush = Brushes.Transparent);

			if (sender != null) {
				var cb = (CheckBox)sender;

				if (cb.IsEnabled) {
					_rbs.ForEach(p => p.Checked -= _rb_Checked);
					cb.IsChecked = true;
					var border = WpfUtilities.FindDirectParentControl<Border>((CheckBox)sender);
					border.BorderBrush = (SolidColorBrush)Application.Current.Resources["SpriteConverterSelectionBorderBrush"];
					_updateSelection();
					_rbs.ForEach(p => p.Checked += _rb_Checked);
				}
			}

			_rbs.ForEach(p => p.Unchecked += _rb_Unchecked);
		}

		private void _updateSelection() {
			bool somethingIsChecked = true;

			if (_rbOriginalPalette.IsChecked == true) {
				ActEditorConfiguration.FormatConflictOption = 0;
			}
			else if (_rbMatch.IsChecked == true) {
				ActEditorConfiguration.FormatConflictOption = 1;
			}
			else if (_rbMerge.IsChecked == true) {
				ActEditorConfiguration.FormatConflictOption = 2;
			}
			else if (_rbBgra32.IsChecked == true) {
				ActEditorConfiguration.FormatConflictOption = 3;
			}
			else if (_rbMergeOctRgb.IsChecked == true) {
				ActEditorConfiguration.FormatConflictOption = 4;
			}
			else if (_rbMergeOctLab.IsChecked == true) {
				ActEditorConfiguration.FormatConflictOption = 5;
			}
			else {
				ActEditorConfiguration.FormatConflictOption = -1;
				somethingIsChecked = false;
			}

			if (somethingIsChecked) {
				Result = _images[ActEditorConfiguration.FormatConflictOption];
			}

			RepeatOption = _repeatBehavior ? ActEditorConfiguration.FormatConflictOption : -1;
		}

		private void _rb_Checked(object sender, RoutedEventArgs e) {
			_uncheckAll(sender);
		}

		private void _rb_Unchecked(object sender, RoutedEventArgs e) {
			((CheckBox)sender).IsChecked = true;
		}

		private void _cbRepeat_Checked(object sender, RoutedEventArgs e) {
			_repeatBehavior = true;
		}

		private void _cbRepeat_Unchecked(object sender, RoutedEventArgs e) {
			_repeatBehavior = false;
		}

		#endregion
	}
}