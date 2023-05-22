using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for an Act object.
	/// </summary>
	public class ActDraw : DrawingComponent {
		private readonly Act _act;
		private readonly IFrameRendererEditor _editor;
		private readonly List<DrawingComponent> _components = new List<DrawingComponent>();
		private bool _componentsInitiated;
		private readonly IFrameRenderer _renderer;

		/// <summary>
		/// Initializes a new instance of the <see cref="ActDraw"/> class.
		/// </summary>
		/// <param name="act">The act.</param>
		public ActDraw(Act act) {
			_act = act;
		}

		public ActDraw(Act act, IFrameRendererEditor editor) {
			_act = act;
			_editor = editor;
		}

		public ActDraw(Act act, IFrameRenderer renderer) {
			_act = act;
			_renderer = renderer;
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="ActDraw" /> is the act currently being edited.
		/// </summary>
		public bool Primary {
			get { return _act.Name == null; }
		}

		/// <summary>
		/// Gets a list of all the components being drawn.
		/// </summary>
		public ReadOnlyCollection<DrawingComponent> Components {
			get { return _components.AsReadOnly(); }
		}

		public override void Render(IFrameRenderer renderer) {
			if (renderer == null) throw new ArgumentNullException("renderer");

			if (!_componentsInitiated) {
				int actionIndex = renderer.SelectedAction;
				int frameIndex = renderer.SelectedFrame;

				if (actionIndex >= _act.NumberOfActions) return;
				if (frameIndex >= _act[actionIndex].NumberOfFrames) {
					if (Primary)
						return;

					frameIndex = frameIndex % _act[actionIndex].NumberOfFrames;
				}

				Frame frame = _act[actionIndex, frameIndex];

				for (int i = 0; i < frame.NumberOfLayers; i++) {
					var layer = _editor != null ? new LayerDraw(_editor, _act, i) : new LayerDraw(_renderer, _act, i);

					if (Primary) {
						layer.Selected += (s, e, a) => OnSelected(e, a);
					}

					_components.Add(layer);
				}

				_componentsInitiated = true;
			}

			foreach (var dc in Components) {
				if (Primary) {
					dc.IsSelectable = true;
				}

				dc.Render(renderer);
			}
		}

		public override void QuickRender(IFrameRenderer renderer) {
			foreach (var dc in Components) {
				dc.QuickRender(renderer);
			}
		}

		public override void Remove(IFrameRenderer renderer) {
			foreach (var dc in Components) {
				dc.Remove(renderer);
			}
		}

		public void Render(IFrameRenderer renderer, int layerIndex) {
			Components[layerIndex].Render(renderer);
		}

		public override void Select() {
			foreach (var comp in Components) {
				comp.Select();
			}
		}

		public void Select(int layer) {
			if (layer > -1 && layer < Components.Count) {
				Components[layer].Select();
			}
		}

		public void Deselect(int layer) {
			if (layer > -1 && layer < Components.Count) {
				Components[layer].IsSelected = false;
			}
		}

		public void Deselect() {
			foreach (LayerDraw sd in Components) {
				sd.IsSelected = false;
			}
		}

		public LayerDraw Get(int layerIndex) {
			if (layerIndex < 0 || layerIndex >= Components.Count) return null;

			return Components[layerIndex] as LayerDraw;
		}
	}
}