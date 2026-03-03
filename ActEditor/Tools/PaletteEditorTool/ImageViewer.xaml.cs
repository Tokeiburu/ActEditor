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
		public Point ImageOperationPosition;
		public SpriteSelection Selection;

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

		public int ImageWidth => (int)_imageSprite.Source.Width;
		public int ImageHeight => (int)_imageSprite.Source.Height;

		public ImageViewer() {
			InitializeComponent();
			_cbZoom.SelectedIndex = 3;

			KeyDown += new KeyEventHandler(_spriteViewer_KeyDown);
			_primary.MouseWheel += new MouseWheelEventHandler(_scrollViewer_MouseWheel);
			_primary.MouseDown += new MouseButtonEventHandler(_scrollViewer_PreviewMouseDown);
			_primary.MouseMove += new MouseEventHandler(_scrollViewer_PreviewMouseMove);
			_primary.SizeChanged += delegate {
				_updatePreview();
			};

			MouseDown += new MouseButtonEventHandler(_imageViewer_PreviewMouseDown);
			MouseMove += _imageViewer_PreviewMouseMove;
			MouseUp += new MouseButtonEventHandler(_imageViewer_PreviewMouseUp);
			MouseUp += delegate { Cursor = Cursors.Arrow; };
			ZoomEngine.ZoomFunction = ZoomEngine.DefaultLimitZoom;

			if (ActEditorConfiguration.ThemeIndex == 1) {
				if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
					return;
				
				var image = new GrfImage(ApplicationManager.GetResource("background.png"));
				image.Multiply(0.5f);
				_imageBackground.Source = image.Cast<BitmapSource>();
			}

			Selection = new SpriteSelection(this, _rectOverlay, _rectOverlaySub);

			//_rectSizeBR.MouseDown += _rectSize_PreviewMouseDown;
			//_rectSizeBR.MouseMove += _rectSize_MouseMove;
			//_rectSizeBR.MouseUp += _rectSize_MouseUp;
		}

		//private void _rectSize_MouseUp(object sender, MouseButtonEventArgs e) {
		//	_rectSizeBR.ReleaseMouseCapture();
		//}
		//
		//private void _rectSize_MouseMove(object sender, MouseEventArgs e) {
		//	if (!_rectSizeBR.IsMouseCaptured)
		//		return;
		//
		//	if (_rectResize.Visibility != Visibility.Visible)
		//		_rectResize.Visibility = Visibility.Visible;
		//
		//	SetRectResize(new Rect(new Point(0, 0), _mouse2World()));
		//}
		//
		//private void SetRectResize(Rect rect) {
		//
		//	double offsetX = _imageSprite.Width / 2;
		//	double offsetY = _imageSprite.Height / 2;
		//
		//	var left = CenterX - offsetX;
		//	var top = CenterY - offsetY;
		//
		//	_rectResize.Margin = new Thickness(left + rect.Left * _zoomEngine.Scale, top + rect.Top * _zoomEngine.Scale, 0, 0);
		//	_rectResize.Width = rect.Width * _zoomEngine.Scale;
		//	_rectResize.Height = rect.Height * _zoomEngine.Scale;
		//}
		//
		//private Point _mouse2World() {
		//	Point imagePoint = Mouse.GetPosition(_imageSprite);
		//	imagePoint.X = (imagePoint.X) / (_imageSprite.Width);
		//	imagePoint.Y = (imagePoint.Y) / (_imageSprite.Height);
		//
		//	return new Point((int)Math.Round(_currentBitmap.PixelWidth * imagePoint.X), (int)Math.Round(_currentBitmap.PixelHeight * imagePoint.Y));
		//}
		//
		//private void _rectSize_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
		//	_rectSizeBR.CaptureMouse();
		//
		//
		//}

		private void _imageViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
			ReleaseMouseCapture();
			//Cursor = Cursors.Arrow;
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
					OnPixelClicked((int)Math.Floor(_currentBitmap.PixelWidth * imagePoint.X), (int)Math.Floor(_currentBitmap.PixelHeight * imagePoint.Y), isWithin);
					return;
				}
			}

			if (_currentBitmap != null) {
				OnPixelClicked((int)Math.Floor(_currentBitmap.PixelWidth * imagePoint.X), (int)Math.Floor(_currentBitmap.PixelHeight * imagePoint.Y), isWithin);
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

		public void Update() {
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

			Selection?.Update(left, top, _zoomEngine.Scale);

			if (_imageOperation.Visibility == Visibility.Visible) {
				_imageOperation.Margin = new Thickness(left + ImageOperationPosition.X * _zoomEngine.Scale, top + ImageOperationPosition.Y * _zoomEngine.Scale, 0, 0);
				_imageOperation.Width = _imageOperation.Source.Width * _zoomEngine.Scale;
				_imageOperation.Height = _imageOperation.Source.Height * _zoomEngine.Scale;
			}

			//_updateResizePositionRectangles(left, top, _zoomEngine.Scale);
		}

		//private void _updateResizePositionRectangles(double left, double top, double scale) {
		//	_rectSizeBR.Margin = new Thickness(left + _imageSprite.Width, top + _imageSprite.Height, 0, 0);
		//}

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

		public void SetOverlayOperation(Point position, BitmapSource image) {
			_imageOperation.Visibility = Visibility.Visible;
			if (image != null)
				_imageOperation.Source = image;
			ImageOperationPosition = position;
			_updatePreview();
		}

		public void ClearOverlayOperation() {
			_imageOperation.Visibility = Visibility.Collapsed;
		}
	}
}