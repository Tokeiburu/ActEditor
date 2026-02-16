using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.FrameEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ActEditor.Core.DrawingComponents {
	public class LineDraw : DrawingComponent {
		public const string GridLineHorizontalBrush = "Horizontal";
		public const string GridLineVerticalBrush = "Vertical";
		public Pen LinePenHorizontal;
		public Pen LinePenVertical;

		static LineDraw() {
			BufferedBrushes.Register(GridLineHorizontalBrush, ActEditorConfiguration.ActEditorGridLineHorizontal);
			BufferedBrushes.Register(GridLineVerticalBrush, ActEditorConfiguration.ActEditorGridLineVertical);
		}

		private LineRenderer _renderer = new LineRenderer();
		private IFrameRendererEditor _editor;

		public LineDraw(IFrameRendererEditor editor) {
			ActEditorConfiguration.ActEditorGridLineHorizontal.PropertyChanged += _onPropertyChanged;
			ActEditorConfiguration.ActEditorGridLineVertical.PropertyChanged += _onPropertyChanged;
			_onPropertyChanged();

			_editor = editor;
			_renderer.SetAct(this, editor, editor.FrameRenderer);
		}

		public override void QuickRender(FrameRenderer renderer) {
			_renderer.InvalidateVisual();
		}

		public override void Render(FrameRenderer renderer) {
			if (!renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Add(_renderer);

			_renderer.InvalidateVisual();
		}

		public override void Remove(FrameRenderer renderer) {

		}

		public override void Unload(FrameRenderer renderer) {
			if (renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Remove(_renderer);

			ActEditorConfiguration.ActEditorGridLineHorizontal.PropertyChanged -= _onPropertyChanged;
			ActEditorConfiguration.ActEditorGridLineVertical.PropertyChanged -= _onPropertyChanged;
		}

		private void _onPropertyChanged() {
			LinePenHorizontal = new Pen(BufferedBrushes.GetBrush(GridLineHorizontalBrush), 1);
			LinePenHorizontal.Freeze();
			LinePenVertical = new Pen(BufferedBrushes.GetBrush(GridLineHorizontalBrush), 1);
			LinePenVertical.Freeze();
			_renderer?.InvalidateVisual();
		}
	}

	public class LineRenderer : FrameworkElement {
		private LineDraw _lineDraw;
		private IFrameRendererEditor _editor;
		private FrameRenderer _frameRenderer;
		private VisualCollection _visuals;

		protected override int VisualChildrenCount => _visuals.Count;
		protected override Visual GetVisualChild(int index) => _visuals[index];

		private DrawingVisual _gridVisual = new DrawingVisual();

		public void SetAct(LineDraw lineDraw, IFrameRendererEditor editor, FrameRenderer frameRenderer) {
			_lineDraw = lineDraw;
			_editor = editor;
			_frameRenderer = frameRenderer;
			_visuals = new VisualCollection(frameRenderer.Canvas);
		}

		protected override void OnRender(DrawingContext drawingContext) {
			if (_visuals.Count == 0) {
				_visuals.Add(_gridVisual);
			}

			drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, _frameRenderer.Canvas.ActualWidth, _frameRenderer.Canvas.ActualHeight));

			using (var dc = _gridVisual.RenderOpen()) {
				double x = Math.Round(_frameRenderer.RelativeCenter.X * _frameRenderer.Canvas.ActualWidth) + 0.5d;
				double y = Math.Round(_frameRenderer.RelativeCenter.Y * _frameRenderer.Canvas.ActualHeight) - 0.5d;
				dc.DrawLine(_lineDraw.LinePenVertical, new Point(x, 0), new Point(x, _frameRenderer.Canvas.ActualHeight));
				dc.DrawLine(_lineDraw.LinePenHorizontal, new Point(0, y), new Point(_frameRenderer.Canvas.ActualWidth, y));
			}
		}
	}
}
