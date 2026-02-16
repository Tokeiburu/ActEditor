using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.FrameEditor;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for a grid line.
	/// </summary>
	public class GridLine : DrawingComponent {
		public const string GridLineHorizontalBrush = "Horizontal";
		public const string GridLineVerticalBrush = "Vertical";
		private readonly Orientation _orientation;
		private Rectangle _line;

		static GridLine() {
			BufferedBrushes.Register(GridLineHorizontalBrush, ActEditorConfiguration.ActEditorGridLineHorizontal);
			BufferedBrushes.Register(GridLineVerticalBrush, ActEditorConfiguration.ActEditorGridLineVertical);
		}

		public GridLine(Orientation orientation) {
			_orientation = orientation;
			IsHitTestVisible = false;

			if (_orientation == Orientation.Horizontal)
				ActEditorConfiguration.ActEditorGridLineHorizontal.PropertyChanged += _onPropertyChanged;
			else
				ActEditorConfiguration.ActEditorGridLineVertical.PropertyChanged += _onPropertyChanged;
		}

		private void _onPropertyChanged() {
			_line.Stroke = _getColor();
		}

		public override void Render(FrameRenderer renderer) {
			if (_line != null) {
				if (_orientation == Orientation.Horizontal)
					_line.Visibility = ActEditorConfiguration.ActEditorGridLineHVisible ? Visibility.Visible : Visibility.Hidden;
				else
					_line.Visibility = ActEditorConfiguration.ActEditorGridLineVVisible ? Visibility.Visible : Visibility.Hidden;
			}

			if (_line == null) {
				var dpi = VisualTreeHelper.GetDpi(renderer.Canvas);

				_line = new Rectangle();
				renderer.Canvas.Children.Add(_line);
				_line.StrokeThickness = 1;
				_line.SnapsToDevicePixels = true;
				_line.UseLayoutRounding = true;
				_line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
				_line.Height = 1 * dpi.DpiScaleY;
				_line.Width = 1 * dpi.DpiScaleX;

				_line.Stroke = _getColor();
			}

			QuickRender(renderer);
		}

		public override void QuickRender(FrameRenderer renderer) {
			if (_line == null) {
				Render(renderer);
			}
			else {
				if (_orientation == Orientation.Horizontal) {
					_line.Margin = new Thickness(0, renderer.CenterY - 1, 0, 0);
					_line.Width = renderer.Canvas.ActualWidth + 50;
				}
				else if (_orientation == Orientation.Vertical) {
					_line.Margin = new Thickness(renderer.CenterX, 0, 0, 0);
					_line.Height = renderer.Canvas.ActualHeight + 50;
				}
			}
		}

		private Brush _getColor() {
			return BufferedBrushes.GetBrush(_orientation == Orientation.Horizontal ? GridLineHorizontalBrush : GridLineVerticalBrush);
		}

		public override void Remove(FrameRenderer renderer) {
			if (_line != null)
				renderer.Canvas.Children.Remove(_line);
		}

		public override void Unload(FrameRenderer renderer) {
			base.Unload(renderer);

			if (_orientation == Orientation.Horizontal)
				ActEditorConfiguration.ActEditorGridLineHorizontal.PropertyChanged -= _onPropertyChanged;
			else
				ActEditorConfiguration.ActEditorGridLineVertical.PropertyChanged -= _onPropertyChanged;
		}
	}
}