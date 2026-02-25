using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.Image;
using TokeiLibrary;
using Utilities.Tools;

namespace ActEditor.Tools.PaletteEditorTool {
	/// <summary>
	/// Interaction logic for SpriteViewer.xaml
	/// </summary>
	public partial class ImageViewer : UserControl {
		#region Delegates

		public delegate void ImageViewerEventHandler(object sender, int x, int y, bool isWithin);

		#endregion

		private readonly ZoomEngine _zoomEngine = new ZoomEngine { ZoomInMultiplier = () => ActEditorConfiguration.ActEditorZoomInMultiplier };
		private BitmapSource _currentBitmap;
		private Point? _oldPosition;
		private Point _relativeCenter = new Point(0.5, 0.5);

		public BitmapSource Bitmap {
			get { return _currentBitmap; }
		}

		public ZoomEngine ZoomEngine {
			get { return _zoomEngine; }
		}

		public Point RelativeCenter {
			get { return _relativeCenter; }
			set { _relativeCenter = value; }
		}

		public int CenterX {
			get { return (int)(_primary.ActualWidth * _relativeCenter.X); }
		}

		public int CenterY {
			get { return (int)(_primary.ActualHeight * _relativeCenter.Y); }
		}

		public Image PreviewImage {
			get { return _imageSprite; }
		}

		public ImageViewer() {
			InitializeComponent();
			_cbZoom.SelectedIndex = 3;

			KeyDown += new KeyEventHandler(_spriteViewer_KeyDown);
			_primary.PreviewMouseWheel += new MouseWheelEventHandler(_scrollViewer_MouseWheel);
			_primary.PreviewMouseDown += new MouseButtonEventHandler(_scrollViewer_PreviewMouseDown);
			_primary.PreviewMouseMove += new MouseEventHandler(_scrollViewer_PreviewMouseMove);
			_primary.SizeChanged += delegate {
				_updatePreview();
			};

			PreviewMouseDown += new MouseButtonEventHandler(_imageViewer_PreviewMouseDown);
			PreviewMouseMove += _imageViewer_PreviewMouseMove;
			PreviewMouseUp += new MouseButtonEventHandler(_imageViewer_PreviewMouseUp);
			MouseUp += delegate { Cursor = Cursors.Arrow; };
			ZoomEngine.ZoomFunction = ZoomEngine.DefaultLimitZoom;

			if (ActEditorConfiguration.ThemeIndex == 1) {
				if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
					return;
				
				var image = new GrfImage(ApplicationManager.GetResource("background.png"));
				image.Multiply(0.5f);
				_imageBackground.Source = image.Cast<BitmapSource>();
			}
		}

		private void _imageViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
			ReleaseMouseCapture();
			_oldPosition = null;
		}

		private void _imageViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
			_oldPosition = e.GetPosition(this);
		}

		public event ImageViewerEventHandler PixelClicked;
		public event ImageViewerEventHandler PixelMoved;

		protected virtual void OnPixelMoved(int x, int y, bool isWithin) {
			ImageViewerEventHandler handler = PixelMoved;
			if (handler != null) handler(this, x, y, isWithin);
		}

		public void OnPixelClicked(int x, int y, bool isWithin) {
			ImageViewerEventHandler handler = PixelClicked;
			if (handler != null) handler(this, x, y, isWithin);
		}

		private void _imageViewer_PreviewMouseMove(object sender, MouseEventArgs e) {
			if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed) {
				if (!IsMouseCaptured)
					CaptureMouse();

				if (_oldPosition == null)
					return;

				Point oldPosition = _oldPosition.Value;
				Point current = e.GetPosition(this);

				double deltaX = (current.X - oldPosition.X);
				double deltaY = (current.Y - oldPosition.Y);

				_relativeCenter.X = _relativeCenter.X + deltaX / _primary.ActualWidth;
				_relativeCenter.Y = _relativeCenter.Y + deltaY / _primary.ActualHeight;

				_oldPosition = current;
				_updatePreview();
			}
		}

		private void _scrollViewer_PreviewMouseMove(object sender, MouseEventArgs e) {
			Point imagePoint = Mouse.GetPosition(_imageSprite);
			imagePoint.X = (imagePoint.X) / (_imageSprite.Width);
			imagePoint.Y = (imagePoint.Y) / (_imageSprite.Height);
			bool isWithin = imagePoint.X >= 0 && imagePoint.X < 1 &&
							imagePoint.Y >= 0 && imagePoint.Y < 1;

			if (e.LeftButton == MouseButtonState.Pressed) {
				if (_currentBitmap != null) {
					OnPixelClicked((int)(_currentBitmap.PixelWidth * imagePoint.X), (int)(_currentBitmap.PixelHeight * imagePoint.Y), isWithin);
					return;
				}
			}

			if (_currentBitmap != null) {
				OnPixelMoved((int)(_currentBitmap.PixelWidth * imagePoint.X), (int)(_currentBitmap.PixelHeight * imagePoint.Y), isWithin);
				return;
			}
		}

		private void _scrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
			_scrollViewer_PreviewMouseMove(sender, e);
		}

		private void _scrollViewer_MouseWheel(object sender, MouseWheelEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed) return;

			_zoomEngine.Zoom(e.Delta);

			Point mousePosition = e.GetPosition(_primary);

			// The relative center must be moved as well!
			double diffX = mousePosition.X / _primary.ActualWidth - _relativeCenter.X;
			double diffY = mousePosition.Y / _primary.ActualHeight - _relativeCenter.Y;

			_relativeCenter.X = mousePosition.X / _primary.ActualWidth - diffX / _zoomEngine.OldScale * _zoomEngine.Scale;
			_relativeCenter.Y = mousePosition.Y / _primary.ActualHeight - diffY / _zoomEngine.OldScale * _zoomEngine.Scale;

			_cbZoom.SelectedIndex = -1;
			_cbZoom.Text = _zoomEngine.ScaleText;

			_updatePreview();
		}

		private void _spriteViewer_KeyDown(object sender, KeyEventArgs e) {
		}

		public void Load(BitmapSource image) {
			((VisualBrush)_borderSprite.Background).Viewport = new Rect(0, 0, 16d / image.PixelWidth, 16d / image.PixelHeight);
			_currentBitmap = image;
			_updatePreview();
		}

		private void _updatePreview() {
			// Set the image with the current zoom scale
			_imageSprite.Source = _currentBitmap;
			_imageSprite.Width = (_currentBitmap == null ? 32 : _currentBitmap.PixelWidth) * _zoomEngine.Scale;
			_imageSprite.Height = (_currentBitmap == null ? 32 : _currentBitmap.PixelHeight) * _zoomEngine.Scale;

			double offsetX = _imageSprite.Width / 2;
			double offsetY = _imageSprite.Height / 2;

			var left = CenterX - offsetX;
			var top = CenterY - offsetY;

			_borderSprite.Width = _imageSprite.Width;
			_borderSprite.Height = _imageSprite.Height;

			_borderSpriteGlow.Width = _imageSprite.Width;
			_borderSpriteGlow.Height = _imageSprite.Height;

			_imageSprite.Margin = new Thickness(left, top, 0, 0);
			_borderSprite.Margin = new Thickness(left, top, 0, 0);
			_borderSpriteGlow.Margin = new Thickness(left, top, 0, 0);
		}

		public void ForceUpdatePreview(BitmapSource bitmap) {
			_currentBitmap = bitmap;
			_updatePreview();
		}

		private void _cbZoom_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbZoom.SelectedIndex < 0) return;

			_zoomEngine.SetZoom(double.Parse(((string)((ComboBoxItem)_cbZoom.SelectedItem).Content).Replace(" %", "")) / 100f);
			_cbZoom.Text = _zoomEngine.ScaleText;

			_updatePreview();
		}

		private void _cbZoom_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				try {
					string text = _cbZoom.Text;

					text = text.Replace(" ", "").Replace("%", "");

					double value = double.Parse(text);

					_zoomEngine.SetZoom(value / 100f);
					_cbZoom.Text = _zoomEngine.ScaleText;
					_updatePreview();
					e.Handled = true;
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
			}
		}

		public void Clear() {
			_currentBitmap = null;
			_imageSprite.Source = null;
		}
	}
}