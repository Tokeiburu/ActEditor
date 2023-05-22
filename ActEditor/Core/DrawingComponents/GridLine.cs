using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for a grid line.
	/// </summary>
	public class GridLine : DrawingComponent {
		public const string GridLineHorizontalBrush = "Horizontal";
		public const string GridLineVerticalBrush = "Vertical";
		private readonly Orientation _orientation;
		private Rectangle _line;
		private bool _visible = true;

		static GridLine() {
			BufferedBrushes.Register(GridLineHorizontalBrush, () => ActEditorConfiguration.ActEditorGridLineHorizontal);
			BufferedBrushes.Register(GridLineVerticalBrush, () => ActEditorConfiguration.ActEditorGridLineVertical);
		}

		public GridLine(Orientation orientation) {
			_orientation = orientation;
			IsHitTestVisible = false;
		}

		public bool Visible {
			get { return _visible; }
			set {
				_visible = value;

				if (_line != null) {
					_line.Visibility = value ? Visibility.Visible : Visibility.Hidden;
				}
			}
		}

		public override void Render(IFrameRenderer renderer) {
			if (_line != null) {
				if (_orientation == Orientation.Horizontal)
					_line.Visibility = ActEditorConfiguration.ActEditorGridLineHVisible ? Visibility.Visible : Visibility.Hidden;
				else
					_line.Visibility = ActEditorConfiguration.ActEditorGridLineVVisible ? Visibility.Visible : Visibility.Hidden;
			}

			if (_line == null) {
				_line = new Rectangle();
				renderer.Canva.Children.Add(_line);
				_line.StrokeThickness = 1;
				_line.SnapsToDevicePixels = true;
				_line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
				_line.Height = 1;
				_line.Width = 1;
			}

			if (_orientation == Orientation.Horizontal) {
				_line.Margin = new Thickness(0, renderer.CenterY, 0, 0);
				_line.Width = renderer.Canva.ActualWidth + 50;
				_line.Stroke = _getColor();
			}
			else if (_orientation == Orientation.Vertical) {
				_line.Margin = new Thickness(renderer.CenterX, 0, 0, 0);
				_line.Height = renderer.Canva.ActualHeight + 50;
				_line.Stroke = _getColor();
			}
		}

		public override void QuickRender(IFrameRenderer renderer) {
			if (_line == null) {
				Render(renderer);
			}
			else {
				if (_orientation == Orientation.Horizontal) {
					_line.Margin = new Thickness(0, renderer.CenterY, 0, 0);
					_line.Width = renderer.Canva.ActualWidth + 50;
				}
				else if (_orientation == Orientation.Vertical) {
					_line.Margin = new Thickness(renderer.CenterX, 0, 0, 0);
					_line.Height = renderer.Canva.ActualHeight + 50;
				}

				_line.Stroke = _getColor();
			}
		}

		private Brush _getColor() {
			return BufferedBrushes.GetBrush(_orientation == Orientation.Horizontal ? GridLineHorizontalBrush : GridLineVerticalBrush);
		}

		public override void Remove(IFrameRenderer renderer) {
			if (_line != null)
				renderer.Canva.Children.Remove(_line);
		}
	}
}