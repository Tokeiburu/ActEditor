using System;
using System.Collections.Generic;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for an Act object.
	/// </summary>
	public class ActDraw : DrawingComponent {
		private readonly Act _act;
		private readonly IFrameRendererEditor _editor;
		private readonly List<DrawingComponent> _components = new List<DrawingComponent>();

		public ActDraw(Act act, IFrameRendererEditor editor) {
			_act = act;
			_editor = editor;

			if (_act.IsSelectable && editor.SelectionEngine != null) {
				editor.SelectionEngine.SelectionChanged += _selectionEngine_SelectionChanged;
			}
		}

		private void _selectionEngine_SelectionChanged(SelectionEngine selector, SelectionEngine.SelectionChangedEventArgs e) {
			foreach (var index in e.Added) {
				if (index < _components.Count)
					((LayerDraw)_components[index]).DrawSelection();
			}

			foreach (var index in e.Removed) {
				if (index < _components.Count)
					((LayerDraw)_components[index]).DrawSelection();
			}
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="ActDraw" /> is the act currently being edited.
		/// </summary>
		public bool Primary => _act.Name == null;

		/// <summary>
		/// Gets a list of all the components being drawn.
		/// </summary>
		public List<DrawingComponent> Components => _components;

		private Frame _ensureComponents(FrameRenderer renderer) {
			if (renderer == null) throw new ArgumentNullException("renderer");

			int actionIndex = renderer.SelectedAction;
			int frameIndex = renderer.SelectedFrame;

			if (actionIndex >= _act.NumberOfActions) return null;
			if (frameIndex >= _act[actionIndex].NumberOfFrames) {
				if (Primary)
					return null;

				frameIndex = frameIndex % _act[actionIndex].NumberOfFrames;
			}

			Frame frame = _act[actionIndex, frameIndex];

			for (int i = _components.Count; i < frame.NumberOfLayers; i++) {
				_components.Add(new LayerDraw(_editor, _act, i));
			}

			return frame;
		}

		public override void Render(FrameRenderer renderer) {
			var frame = _ensureComponents(renderer);

			if (frame == null)
				return;

			for (int i = 0; i < frame.NumberOfLayers; i++) {
				_components[i].Render(renderer);
			}
		}

		public override void QuickRender(FrameRenderer renderer) {
			foreach (var dc in Components) {
				dc.QuickRender(renderer);
			}
		}

		public override void Remove(FrameRenderer renderer) {
			foreach (var dc in Components) {
				dc.Remove(renderer);
			}
		}

		public void Render(FrameRenderer renderer, int layerIndex) {
			_ensureComponents(renderer);

			Components[layerIndex].Render(renderer);
		}

		public void Remove(FrameRenderer renderer, int layerIndex) {
			Components[layerIndex].Remove(renderer);
		}

		public LayerDraw Get(int layerIndex) {
			if (layerIndex < 0 || layerIndex >= Components.Count) return null;

			return Components[layerIndex] as LayerDraw;
		}

		public override void Unload(FrameRenderer renderer) {
			base.Unload(renderer);

			if (_editor?.SelectionEngine != null)
				_editor.SelectionEngine.SelectionChanged -= _selectionEngine_SelectionChanged;
		}
	}
}