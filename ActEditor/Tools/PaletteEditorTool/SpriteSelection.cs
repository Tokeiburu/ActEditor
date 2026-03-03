using GRF.Image;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteSelection {
		public Rect Area;
		public Rectangle SelectionRectangle;
		public Rectangle SelectionRectangleSub;
		private ImageViewer _imageViewer;

		public bool IsSelected { get; set; }

		public SpriteSelection(ImageViewer imageViewer, Rectangle selectionRectangle, Rectangle selectionRectangleSub) {
			SelectionRectangle = selectionRectangle;
			SelectionRectangleSub = selectionRectangleSub;
			_imageViewer = imageViewer;
		}

		public void Set(Rect selection) {
			SelectionRectangle.Visibility = Visibility.Visible;
			SelectionRectangleSub.Visibility = Visibility.Visible;
			Area = selection;
			IsSelected = true;
			_imageViewer.Update();
		}

		public void Set(Point p0, Point p1) {
			Set(new Rect(p0, p1));
		}

		public void Clear() {
			IsSelected = false;
			SelectionRectangle.Visibility = Visibility.Collapsed;
			SelectionRectangleSub.Visibility = Visibility.Collapsed;
		}

		public void Update(double left, double top, double scale) {
			if (SelectionRectangle.Visibility == Visibility.Visible) {
				SelectionRectangle.Margin = new Thickness(left + Area.X * scale, top + Area.Y * scale, 0, 0);
				SelectionRectangle.Width = Area.Width * scale;
				SelectionRectangle.Height = Area.Height * scale;

				SelectionRectangleSub.Margin = SelectionRectangle.Margin;
				SelectionRectangleSub.Width = SelectionRectangle.Width;
				SelectionRectangleSub.Height = SelectionRectangle.Height;
			}
		}

		public void CopyClipboard(GrfImage image) {
			try {
				if (_imageViewer._imageOperation.Visibility == Visibility.Visible)
					Clipboard.SetImage((BitmapSource)_imageViewer._imageOperation.Source);
				else
					Clipboard.SetImage(GetClippedImage(image).Cast<BitmapSource>());
			}
			catch {
			}
		}

		public GrfImage GetClippedImage(GrfImage image) {
			var clippedSelection = GetClippedArea();
			return image.Extract((int)clippedSelection.X, (int)clippedSelection.Y, (int)clippedSelection.Width, (int)clippedSelection.Height);
		}

		public Rect GetClippedArea() {
			Rect area = Area;
			area.Intersect(new Rect(0, 0, _imageViewer.ImageWidth, _imageViewer.ImageHeight));
			return area;
		}

		public bool IsMouseWithin(int x, int y) {
			const double epsilon = 0.01d;
			return IsSelected && Area.Contains(new Point(x + epsilon, y + epsilon));
		}
	}
}
