using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for the selection rectangle.
	/// </summary>
	public class SelectionDraw : DrawingComponent {
		public const string SelectionBorder = "Selection_Border";
		public const string SelectionOverlay = "Selection_Overlay";
		private Rectangle _line;
		private bool _visible = true;

		static SelectionDraw() {
			BufferedBrushes.Register(SelectionBorder, () => ActEditorConfiguration.ActEditorSelectionBorder);
			BufferedBrushes.Register(SelectionOverlay, () => ActEditorConfiguration.ActEditorSelectionBorderOverlay);
		}

		public SelectionDraw() {
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
		}

		public void Render(IFrameRenderer renderer, Rect rect) {
			if (_line == null) {
				_line = new Rectangle();
				renderer.Canva.Children.Add(_line);
				_line.StrokeThickness = 1;
				_line.SnapsToDevicePixels = true;
				_line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
			}

			_line.Fill = BufferedBrushes.GetBrush(SelectionOverlay);
			_line.Height = (int) rect.Height;
			_line.Width = (int) rect.Width;

			_line.Margin = new Thickness((int) rect.X, (int) rect.Y, 0, 0);
			_line.Stroke = BufferedBrushes.GetBrush(SelectionBorder);
		}

		public override void QuickRender(IFrameRenderer renderer) {
		}

		public override void Remove(IFrameRenderer renderer) {
			if (_line != null)
				renderer.Canva.Children.Remove(_line);
		}
	}
}