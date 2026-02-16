using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;
using TokeiLibrary;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for an anchor.
	/// </summary>
	public class AnchorDraw : DrawingComponent {
		public const string AnchorBrushName = "AnchorDrawBrush";
		private Rectangle[] _lines = new Rectangle[2];
		private Point _point;
		private bool _visible;

		static AnchorDraw() {
			BufferedBrushes.Register(AnchorBrushName, ActEditorConfiguration.ActEditorAnchorColor);
		}

		public AnchorDraw(Point point) {
			_point = point;

			ActEditorConfiguration.ActEditorAnchorColor.PropertyChanged += _actEditorAnchorColor_PropertyChanged;
		}

		private void _actEditorAnchorColor_PropertyChanged() {
			this.Dispatch(delegate {
				foreach (var line in _lines) {
					line.Stroke = BufferedBrushes.GetBrush(AnchorBrushName);
				}
			});
		}

		public AnchorDraw(Anchor anchor) : this(new Point(anchor.OffsetX, anchor.OffsetY)) {
		}

		public bool Visible {
			get => _visible;
			set {
				if (value != _visible) {
					foreach (var line in _lines) {
						if (line != null)
							line.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
					}
				}

				_visible = value;
			}
		}

		/// <summary>
		/// Renders the anchor at the specified offsets.
		/// </summary>
		/// <param name="renderer">The renderer.</param>
		/// <param name="point">The point.</param>
		public void RenderOffsets(FrameRenderer renderer, Point point) {
			_point = point;
			_initLines(renderer);
			QuickRender(renderer);
		}

		private void _initLines(FrameRenderer renderer) {
			for (int i = 0; i < _lines.Length; i++) {
				if (_lines[i] == null) {
					_lines[i] = new Rectangle();

					var line = _lines[i];

					renderer.Canvas.Children.Add(line);
					line.StrokeThickness = 1;
					line.SnapsToDevicePixels = true;
					line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

					if (i == 0) {
						line.Height = 2;
						line.Width = 20;
					}
					else {
						line.Height = 20;
						line.Width = 2;
					}

					line.Visibility = Visible ? Visibility.Visible : Visibility.Collapsed;

					TranslateTransform translate = new TranslateTransform();
					translate.X = -line.Width / 2;
					translate.Y = -line.Height / 2;

					line.RenderTransform = translate;

					Canvas.SetZIndex(line, 99997 + i);
					line.Stroke = BufferedBrushes.GetBrush(AnchorBrushName);
				}
			}
		}

		public override void Render(FrameRenderer renderer) {
			_initLines(renderer);

			foreach (var line in _lines) {
				if (!renderer.Canvas.Children.Contains(line)) {
					renderer.Canvas.Children.Add(line);
				}
			}

			QuickRender(renderer);
		}

		public override void QuickRender(FrameRenderer renderer) {
			Thickness margin = new Thickness(_point.X * renderer.ZoomEngine.Scale + renderer.CenterX, _point.Y * renderer.ZoomEngine.Scale + renderer.CenterY, 0, 0);

			foreach (var line in _lines) {
				line.Margin = margin;
			}
		}

		public override void Remove(FrameRenderer renderer) {
			foreach (var line in _lines) {
				if (line != null)
					renderer.Canvas.Children.Remove(line);
			}
		}

		public override void Unload(FrameRenderer renderer) {
			base.Unload(renderer);

			ActEditorConfiguration.ActEditorAnchorColor.PropertyChanged -= _actEditorAnchorColor_PropertyChanged;
		}
	}
}