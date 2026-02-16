using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for an Act object.
	/// </summary>
	public class ActDraw2 : DrawingComponent {
		private readonly Act _act;
		private readonly IFrameRendererEditor _editor;
		private readonly List<DrawingComponent> _components = new List<DrawingComponent>();
		private Renderer _renderer = new Renderer();

		public ActDraw2(Act act, IFrameRendererEditor editor) {
			_act = act;
			_editor = editor;
			_renderer.SetAct(editor, editor.FrameRenderer);

			if (_act.IsSelectable && editor.SelectionEngine != null) {
				editor.SelectionEngine.SelectionChanged += _selectionEngine_SelectionChanged;
			}
		}

		private void _selectionEngine_SelectionChanged(SelectionEngine selector, SelectionEngine.SelectionChangedEventArgs e) {
			_renderer.InvalidateVisual();
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="ActDraw" /> is the act currently being edited.
		/// </summary>
		public bool Primary => _act.Name == null;

		public int VisualCount => _renderer.Visuals.Count / 2;

		public override void Render(FrameRenderer renderer) {
			if (!renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Add(_renderer);

			_renderer.DirtyVisual();
			_renderer.InvalidateVisual();
		}

		public override void QuickRender(FrameRenderer renderer) {
			_renderer.DirtyVisual();
			_renderer.InvalidateVisual();
		}

		public override void Remove(FrameRenderer renderer) {
			
		}

		public void Render(FrameRenderer renderer, int layerIndex) {
			_renderer.DirtyVisual(layerIndex);
			_renderer.InvalidateVisual();
		}

		public void Remove(FrameRenderer renderer, int layerIndex) {
			
		}

		public override void Unload(FrameRenderer renderer) {
			base.Unload(renderer);

			_renderer.Unload();

			if (renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Remove(_renderer);

			if (_editor?.SelectionEngine != null)
				_editor.SelectionEngine.SelectionChanged -= _selectionEngine_SelectionChanged;
		}

		public int GetLayerIndexUnderMouse(Point position) {
			//if (Transform.Scale.ScaleX == 0 || Transform.Scale.ScaleY == 0) return false;

			DrawingVisual hitResult = null;

			VisualTreeHelper.HitTest(_editor.FrameRenderer.Canvas, null, result => {
				if (result.VisualHit is DrawingVisual visual) {
					hitResult = visual;
					return HitTestResultBehavior.Stop; // TOPMOST match
				}

				return HitTestResultBehavior.Continue;
			}, new PointHitTestParameters(position));

			if (hitResult == null)
				return -1;

			var idx = _renderer.Visuals.IndexOf(hitResult);
			
			if (idx > -1) {
				return idx / 2;
			}

			return -1;
		}
	}
}