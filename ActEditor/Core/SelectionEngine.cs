using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using Utilities.Tools;

namespace ActEditor.Core {
	/// <summary>
	/// The SelectionEngine class manages all the selection
	/// events and interactions.
	/// </summary>
	public class SelectionEngine {
		public const double SelectionRange = 0.80d; // The selection range determines the sensitivity of the selection (1 = 100%).
		private IFrameRendererEditor _editor;
		private IFrameRenderer _renderer;

		public SelectionEngine() {
			SelectedItems = new HashSet<int>();
		}

		/// <summary>
		/// Gets the selected items indexes.
		/// Warning: This list may include items that do not exist in the current frame index.
		/// Warning: Use CurrentlySelected for the currently used indexes.
		/// </summary>
		public HashSet<int> SelectedItems { get; private set; }

		/// <summary>
		/// Gets the components of the FrameRenderer.
		/// </summary>
		public List<DrawingComponent> Components { get; private set; }

		/// <summary>
		/// Gets the primary Act drawing object.
		/// </summary>
		public ActDraw Main {
			get { return Components.OfType<ActDraw>().FirstOrDefault(p => p.Primary); }
		}

		/// <summary>
		/// Gets the last selected index.
		/// Todo: Cover all usages and make sure this property actually always returns the latest index.
		/// </summary>
		public int LatestSelected { get; internal set; }

		/// <summary>
		/// Gets the selected LayerDraw.
		/// </summary>
		public List<LayerDraw> SelectedLayerDraws {
			get {
				var main = Main;

				if (main != null) {
					return SelectedItems.Where(p => p < main.Components.Count).Select(p => (LayerDraw) main.Components[p]).ToList();
				}

				return new List<LayerDraw>();
			}
		}

		/// <summary>
		/// Gets the selected layers.
		/// </summary>
		public Layer[] SelectedLayers {
			get {
				var main = Main;

				if (main != null) {
					List<Layer> layers = new List<Layer>();
					List<Layer> frameLayers = _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers;

					foreach (int selectedIndex in SelectedItems.OrderBy(p => p)) {
						if (selectedIndex < frameLayers.Count) {
							layers.Add(frameLayers[selectedIndex]);
						}
					}

					return layers.ToArray();
				}

				return new Layer[] {};
			}
		}

		/// <summary>
		/// Gets the currently selected items.
		/// </summary>
		public HashSet<int> CurrentlySelected {
			get {
				var main = Main;

				if (main != null) {
					HashSet<int> selected = new HashSet<int>();
					int count = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].NumberOfLayers;

					foreach (int selectedIndex in SelectedItems) {
						if (selectedIndex < count) {
							selected.Add(selectedIndex);
						}
					}

					return selected;
				}

				return new HashSet<int>();
			}
		}

		private IEnumerable<Layer> _allLayers {
			get {
				var main = Main;

				if (main != null) {
					return _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers.ToArray();
				}

				return new Layer[] {};
			}
		}

		private Act _act {
			get {
				if (_editor == null)
					return _renderer.Act;

				return _editor.Act;
			}
		}

		/// <summary>
		/// Inits the selection engine.
		/// </summary>
		/// <param name="editor">The act editor.</param>
		public void Init(IFrameRendererEditor editor) {
			_editor = editor;

			_renderer = _editor.FrameRenderer;

			Components = _renderer.Components;

			_editor.FrameSelector.FrameChanged += (s, e) => _refreshSelection();
			_editor.FrameSelector.SpecialFrameChanged += (s, e) => _refreshSelection();
			_editor.FrameSelector.ActionChanged += (s, e) => _internalFullClearSelection();

			_editor.ActLoaded += delegate {
				if (_editor.Act == null) return;

				_editor.Act.Commands.CommandUndo += _onCommandsOnCommandUndo;
				_editor.Act.Commands.CommandRedo += _onCommandsOnCommandUndo;
			};
		}

		public static SelectionEngine DummyEngine(IFrameRenderer renderer) {
			var engine = new SelectionEngine();
			engine._init(renderer);
			return engine;
		}

		private void _init(IFrameRenderer renderer) {
			_renderer = renderer;

			Components = _renderer.Components;

			if (_renderer.Act != null) {
				_renderer.Act.Commands.CommandUndo += _onCommandsOnCommandUndo;
				_renderer.Act.Commands.CommandRedo += _onCommandsOnCommandUndo;
			}
		}

		private void _onCommandsOnCommandUndo(object sender, IActCommand command) {
			var cmdAction = _getCommand<ActionCommand>(command);

			if (cmdAction != null) {
				_internalFullClearSelection();
			}
			else {
				_internalCleanSelection();
			}
		}

		/// <summary>
		/// Clears the selection.
		/// </summary>
		public void ClearSelection() {
			SelectedItems.Clear();

			var main = Main;

			if (main != null) {
				main.Deselect();
			}
		}

		/// <summary>
		/// Set the selection from the specified rectangle.
		/// </summary>
		/// <param name="rect">The rectangle.</param>
		/// <param name="zoom">The zoom engine.</param>
		/// <param name="absoluteCenter">The absolute center.</param>
		public void Select(Rect rect, ZoomEngine zoom, Point absoluteCenter) {
			var main = Main;

			if (main != null) {
				// The rectangle coordinates are absolute, we
				// calculate the components by their absolute offsets
				// as well!
				rect = new Rect(rect.X - absoluteCenter.X, rect.Y - absoluteCenter.Y, rect.Width, rect.Height);

				var selectionAreas = _getSelectionAreas(zoom);

				for (int i = 0; i < selectionAreas.Length; i++) {
					if (rect.IntersectsWith(selectionAreas[i])) {
						Select(i);
					}
					else {
						RemoveSelection(i);
					}
				}
			}
		}

		/// <summary>
		/// Selects the specified layer.
		/// </summary>
		/// <param name="index">The index.</param>
		public void Select(int index) {
			var main = Main;

			if (main != null) {
				main.Select(index);
				SelectedItems.Add(index);
			}
		}

		/// <summary>
		/// Selects all.
		/// </summary>
		public void SelectAll() {
			if (_editor != null && _editor.LayerEditor != null && _editor.LayerEditor.DoNotRemove) return;

			var main = Main;

			if (main != null) {
				SelectedItems.Clear();
				main.Select();
				_internalSetSelection(0, main.Components.Count);
			}
		}

		/// <summary>
		/// Deselects all.
		/// </summary>
		public void DeselectAll() {
			if (_editor != null && _editor.LayerEditor != null && _editor.LayerEditor.DoNotRemove) return;

			var main = Main;

			if (main != null) {
				SelectedItems.Clear();
				main.Deselect();
			}
		}

		/// <summary>
		/// Sets the selection by range.
		/// </summary>
		/// <param name="start">The start index.</param>
		/// <param name="length">The length.</param>
		public void SetSelection(int start, int length) {
			var main = Main;

			if (main != null) {
				SelectedItems.Clear();

				for (int i = 0; i < main.Components.Count; i++) {
					if (start <= i && i < start + length) {
						main.Components[i].IsSelected = true;
						SelectedItems.Add(i);
					}
					else {
						main.Components[i].IsSelected = false;
					}
				}
			}
		}

		/// <summary>
		/// Sets the selection by the layer index.
		/// </summary>
		/// <param name="index">The layer index.</param>
		public void SetSelection(int index) {
			var main = Main;

			if (main != null) {
				SelectedItems.Clear();

				for (int i = 0; i < main.Components.Count; i++) {
					if (i == index) {
						main.Components[i].IsSelected = true;
						SelectedItems.Add(i);
					}
					else {
						main.Components[i].IsSelected = false;
					}
				}
			}
		}

		/// <summary>
		/// Selects or unselects the layer under the mouse.
		/// </summary>
		/// <param name="oldPosition">The old position.</param>
		/// <param name="e">The <see cref="MouseEventArgs" /> instance containing the event data.</param>
		public void SelectUnderMouse(Point oldPosition, MouseEventArgs e) {
			var main = Main;

			if (main != null) {
				var components = new List<DrawingComponent>(main.Components);
				components.Reverse();

				foreach (LayerDraw sd in components) {
					if (sd.IsMouseUnder(_renderer.PointToScreen(oldPosition)) && sd.IsMouseUnder(e)) {
						sd.IsSelected = !sd.IsSelected;
						break;
					}
				}
			}
		}

		/// <summary>
		/// Determines whether a layer is under the mouse or not.
		/// </summary>
		/// <param name="position">The position.</param>
		/// <returns>True if the layer is under the mouse; false if the layer is not under the mouse; null if the state is undefined.</returns>
		public bool? IsUnderMouse(Point position) {
			var main = Main;

			if (main != null) {
				var components = new List<DrawingComponent>(main.Components);

				foreach (LayerDraw sd in components) {
					if (sd.IsMouseUnder(_renderer.PointToScreen(position))) {
						return true;
					}
				}

				return false;
			}

			return null;
		}

		/// <summary>
		/// Adds a layer index to the selection.
		/// </summary>
		/// <param name="index">The layer index.</param>
		public void AddSelection(int index) {
			SelectedItems.Add(index);

			var main = Main;

			if (main != null) {
				LatestSelected = index;
				main.Select(index);
			}
		}

		/// <summary>
		/// Removes a layer index from the selection.
		/// </summary>
		/// <param name="index">The layer index.</param>
		public void RemoveSelection(int index) {
			SelectedItems.Remove(index);

			var main = Main;

			if (main != null) {
				main.Deselect(index);
			}
		}

		/// <summary>
		/// Refreshes the selection.
		/// </summary>
		public void RefreshSelection() {
			_refreshSelection();
		}

		/// <summary>
		/// Sets the selection by a selection list of indexes.
		/// </summary>
		/// <param name="selection">The selection list.</param>
		public void SetSelection(HashSet<int> selection) {
			SelectedItems = selection;
			_refreshSelection();
		}

		/// <summary>
		/// Selects the reverse of the selection provided by a list.
		/// </summary>
		public void SelectReverse() {
			var selected = new HashSet<int>(CurrentlySelected);
			SelectedItems.Clear();
			var main = Main;

			if (main != null) {
				for (int i = 0; i < main.Components.Count; i++) {
					if (selected.Contains(i)) {
						main.Components[i].IsSelected = false;
					}
					else {
						main.Components[i].IsSelected = true;
						SelectedItems.Add(i);
					}
				}
			}
		}

		/// <summary>
		/// Selects a range of layers based on the latest selected index.
		/// </summary>
		/// <param name="layerIndex">Index of the layer.</param>
		public void SelectUpToFromShift(int layerIndex) {
			var main = Main;
			int lastSelected = LatestSelected;

			if (main != null) {
				if (lastSelected == layerIndex) {
					main.Components[layerIndex].IsSelected = !main.Components[layerIndex].IsSelected;
				}
				else {
					int from = lastSelected < layerIndex ? lastSelected : layerIndex;
					int to = lastSelected < layerIndex ? layerIndex : lastSelected;

					for (int i = from; i <= to && i < main.Components.Count; i++) {
						main.Components[i].IsSelected = true;
					}
				}
			}
		}

		private void _internalSetSelection(int from, int count) {
			for (int i = from; i < from + count; i++) {
				SelectedItems.Add(i);
			}
		}

		private Rect[] _getSelectionAreas(ZoomEngine zoom) {
			List<Rect> rectangles = new List<Rect>();
			Spr sprite = _editor == null ? _renderer.Act.Sprite : _editor.Act.Sprite;

			foreach (Layer layer in _allLayers) {
				if (layer.SpriteIndex > -1) {
					GrfImage image = layer.GetImage(sprite);
					double width = 0;
					double height = 0;

					if (image != null) {
						width = image.Width;
						height = image.Height;
					}

					width = width * zoom.Scale * layer.ScaleX;
					height = height * zoom.Scale * layer.ScaleY;
					double offsetX = layer.OffsetX * zoom.Scale;
					double offsetY = layer.OffsetY * zoom.Scale;

					double left = offsetX - width * SelectionRange * 0.5d;
					double top = offsetY - height * SelectionRange * 0.5d;
					rectangles.Add(new Rect(new Point(left, top), new Point(left + width * SelectionRange, top + height * SelectionRange)));
				}
				else {
					rectangles.Add(new Rect(layer.OffsetX * zoom.Scale, layer.OffsetY * zoom.Scale, 0, 0));
				}
			}

			return rectangles.ToArray();
		}

		private T _getCommand<T>(IActCommand command) where T : class, IActCommand {
			var cmd = command as ActGroupCommand;

			if (cmd != null) {
				return cmd.Commands.FirstOrDefault(p => p.GetType() == typeof (T)) as T;
			}

			if (command is T) {
				return command as T;
			}

			return null;
		}

		private void _internalCleanSelection() {
			if (_editor != null &&  _editor.LayerEditor != null) {
				foreach (int selected in SelectedItems) {
					_editor.LayerEditor.Provider.Get(selected).IsSelected = false;
				}
			}

			_refreshSelection();
		}

		private void _internalFullClearSelection() {
			if (ActEditorConfiguration.KeepPreviewSelectionFromActionChange) {
				_refreshSelection();
				return;
			}

			if (_editor != null && _editor.LayerEditor != null) {
				foreach (int selected in SelectedItems) {
					_editor.LayerEditor.Provider.Get(selected).IsSelected = false;
				}
			}

			SelectedItems.Clear();
			_refreshSelection();
		}

		private void _refreshSelection() {
			var main = Main;

			if (main != null) {
				for (int i = 0; i < main.Components.Count; i++) {
					if (SelectedItems.Contains(i)) {
						main.Components[i].IsSelected = true;
					}
					else {
						main.Components[i].IsSelected = false;
					}
				}
			}
		}

		public void PopSelectedLayerState() {
			foreach (LayerDraw layer in SelectedLayerDraws) {
				layer.SaveInitialData();
			}
		}

		public void UpdateSelection(Rect rect, bool show) {
			if (_editor.FrameSelector == null) return;
			if (_editor.FrameSelector.IsPlaying) return;

			var selectionDraw = Components.OfType<SelectionDraw>().FirstOrDefault();

			if (show) {
				if (selectionDraw == null) {
					selectionDraw = new SelectionDraw();
					Components.Add(selectionDraw);
				}

				selectionDraw.Render(_editor.FrameRenderer, rect);
				selectionDraw.Visible = true;

				Select(rect, _editor.FrameRenderer.ZoomEngine, new Point(_editor.FrameRenderer.CenterX, _editor.FrameRenderer.CenterY));
			}
			else {
				if (selectionDraw != null) {
					selectionDraw.Visible = false;
				}
			}
		}
	}
}