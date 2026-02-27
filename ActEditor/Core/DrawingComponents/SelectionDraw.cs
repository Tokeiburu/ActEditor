using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.FrameEditor;

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
			BufferedBrushes.Register(SelectionBorder, ActEditorConfiguration.ActEditorSelectionBorder);
			BufferedBrushes.Register(SelectionOverlay, ActEditorConfiguration.ActEditorSelectionBorderOverlay);
		}

		public SelectionDraw() {
			IsHitTestVisible = false;

			ActEditorConfiguration.ActEditorSelectionBorder.PropertyChanged += _onPropertyChanged;
			ActEditorConfiguration.ActEditorSelectionBorderOverlay.PropertyChanged += _onPropertyChanged;
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

		public override void Render(FrameRenderer renderer) {
		}

		public void Render(FrameRenderer renderer, Rect rect) {
			if (_line == null) {
				_line = new Rectangle();
				renderer.Canvas.Children.Add(_line);
				_line.StrokeThickness = 1;
				_line.SnapsToDevicePixels = true;
				_line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
				_onPropertyChanged();
			}

			_line.Height = (int) rect.Height;
			_line.Width = (int) rect.Width;
			_line.Margin = new Thickness((int) rect.X, (int) rect.Y, 0, 0);
			
			Canvas.SetZIndex(_line, 99999);
		}

		public override void QuickRender(FrameRenderer renderer) {
		}

		public override void Remove(FrameRenderer renderer) {
			if (_line != null)
				renderer.Canvas.Children.Remove(_line);
		}

		private void _onPropertyChanged() {
			_line.Fill = BufferedBrushes.GetBrush(SelectionOverlay);
			_line.Stroke = BufferedBrushes.GetBrush(SelectionBorder);
		}

		public override void Unload(FrameRenderer renderer) {
			base.Unload(renderer);

			ActEditorConfiguration.ActEditorSelectionBorder.PropertyChanged -= _onPropertyChanged;
			ActEditorConfiguration.ActEditorSelectionBorderOverlay.PropertyChanged -= _onPropertyChanged;
		}
	}
}