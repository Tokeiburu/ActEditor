using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for an anchor.
	/// </summary>
	public class AnchorDraw : DrawingComponent {
		public const string AnchorBrushName = "AnchorDrawBrush";
		private Rectangle _line0;
		private Rectangle _line1;
		private Point _point;
		private bool _visible;

		static AnchorDraw() {
			BufferedBrushes.Register(AnchorBrushName, () => ActEditorConfiguration.ActEditorAnchorColor);
		}

		public AnchorDraw(Anchor anchor) {
			_point = new Point(anchor.OffsetX, anchor.OffsetY);
		}

		public AnchorDraw(Point point) {
			_point = point;
		}

		public bool Visible {
			get { return _visible; }
			set {
				if (value != _visible) {
					if (_line0 != null)
						_line0.Visibility = value ? Visibility.Visible : Visibility.Collapsed;

					if (_line1 != null)
						_line1.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
				}
				_visible = value;
			}
		}

		/// <summary>
		/// Renders the anchor at the specified offsets.
		/// </summary>
		/// <param name="renderer">The renderer.</param>
		/// <param name="point">The point.</param>
		public void RenderOffsets(IFrameRenderer renderer, Point point) {
			_point = point;
			_initLines(renderer);
			QuickRender(renderer);
		}

		private void _initLines(IFrameRenderer renderer) {
			if (_line0 == null) {
				_line0 = new Rectangle();

				renderer.Canva.Children.Add(_line0);
				_line0.StrokeThickness = 1;
				_line0.SnapsToDevicePixels = true;
				_line0.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
				_line0.Height = 2;
				_line0.Width = 20;
				_line0.Visibility = Visible ? Visibility.Visible : Visibility.Collapsed;

				TranslateTransform translate = new TranslateTransform();
				translate.X = -_line0.Width / 2;
				translate.Y = -_line0.Height / 2;

				_line0.RenderTransform = translate;
			}

			if (_line1 == null) {
				_line1 = new Rectangle();

				renderer.Canva.Children.Add(_line1);
				_line1.StrokeThickness = 1;
				_line1.SnapsToDevicePixels = true;
				_line1.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
				_line1.Height = 20;
				_line1.Width = 2;
				_line1.RenderTransformOrigin = new Point(0, 0);
				_line1.Visibility = Visible ? Visibility.Visible : Visibility.Collapsed;

				TranslateTransform translate = new TranslateTransform();
				translate.X = -_line1.Width / 2;
				translate.Y = -_line1.Height / 2;

				_line1.RenderTransform = translate;
			}
		}

		public override void Render(IFrameRenderer renderer) {
			_initLines(renderer);

			if (!renderer.Canva.Children.Contains(_line0)) {
				renderer.Canva.Children.Add(_line0);
				renderer.Canva.Children.Add(_line1);
			}

			QuickRender(renderer);
		}

		public override void QuickRender(IFrameRenderer renderer) {
			Thickness margin = new Thickness(_point.X * renderer.ZoomEngine.Scale + renderer.CenterX, _point.Y * renderer.ZoomEngine.Scale + renderer.CenterY, 0, 0);
			_line0.Margin = margin;
			_line1.Margin = margin;

			_line0.Stroke = BufferedBrushes.GetBrush(AnchorBrushName);
			_line1.Stroke = BufferedBrushes.GetBrush(AnchorBrushName);
		}

		public override void Remove(IFrameRenderer renderer) {
			if (_line0 != null)
				renderer.Canva.Children.Remove(_line0);

			if (_line1 != null)
				renderer.Canva.Children.Remove(_line1);
		}
	}
}