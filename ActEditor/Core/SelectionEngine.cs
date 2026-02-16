using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.FrameEditor;
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
		private FrameRenderer _renderer;

		public delegate void SelectionChangedEventHandler(SelectionEngine selector, SelectionChangedEventArgs e);

		public event SelectionChangedEventHandler SelectionChanged;

		public class SelectionChangedEventArgs {
			public List<int> Added = new List<int>();
			public List<int> Removed = new List<int>();
		}

		public void OnSelectionChanged(SelectionChangedEventArgs args) {
			if (args.Added.Count == 0 && args.Removed.Count == 0)
				return;

			SelectionChangedEventHandler handler = SelectionChanged;
			if (handler != null) handler(this, args);
		}

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
					var layers = _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers;
					return SelectedItems.Where(p => p < main.Components.Count && p < layers.Count).Select(p => (LayerDraw) main.Components[p]).ToList();
				}

				return new List<LayerDraw>();
			}
		}

		/// <summary>
		/// Gets the selected layers.
		/// </summary>
		public Layer[] SelectedLayers {
			get {
				List<Layer> layers = new List<Layer>();
				List<Layer> frameLayers = _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers;

				foreach (int selectedIndex in SelectedItems.OrderBy(p => p)) {
					if (selectedIndex < frameLayers.Count) {
						layers.Add(frameLayers[selectedIndex]);
					}
				}

				return layers.ToArray();
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
				return _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers.ToArray();
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

			_editor.IndexSelector.ActionChanged += (s, e) => _internalFullClearSelection();

			_editor.ActLoaded += delegate {
				if (_editor.Act == null) return;

				_editor.Act.Commands.CommandUndo += _onCommandsOnCommandUndo;
				_editor.Act.Commands.CommandRedo += _onCommandsOnCommandUndo;
			};
		}

		public static SelectionEngine DummyEngine(FrameRenderer renderer) {
			var engine = new SelectionEngine();
			engine._init(renderer);
			return engine;
		}

		private void _init(FrameRenderer renderer) {
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
		}

		/// <summary>
		/// Set the selection from the specified rectangle.
		/// </summary>
		/// <param name="rect">The rectangle.</param>
		/// <param name="zoom">The zoom engine.</param>
		/// <param name="absoluteCenter">The absolute center.</param>
		public void Select(Rect rect, ZoomEngine zoom, Point absoluteCenter) {
			// The rectangle coordinates are absolute, we
			// calculate the components by their absolute offsets
			// as well!
			rect = new Rect(rect.X - absoluteCenter.X, rect.Y - absoluteCenter.Y, rect.Width, rect.Height);
			
			var selectionAreas = _getSelectionAreas(zoom);

			SelectionChangedEventArgs args = new SelectionChangedEventArgs();

			for (int i = 0; i < selectionAreas.Length; i++) {
				if (rect.IntersectsWith(selectionAreas[i])) {
					if (SelectedItems.Add(i))
						args.Added.Add(i);
				}
				else {
					if (SelectedItems.Remove(i))
						args.Removed.Add(i);
				}
			}

			OnSelectionChanged(args);
		}

		/// <summary>
		/// Selects all.
		/// </summary>
		public void SelectAll() {
			if (_editor != null && _editor.LayerEditor != null && _editor.LayerEditor.DoNotRemove) return;

			var main = Main;

			if (main != null) {
				SelectionChangedEventArgs args = new SelectionChangedEventArgs();
				List<Layer> frameLayers = _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers;

				for (int i = 0; i < frameLayers.Count; i++) {
					if (SelectedItems.Add(i))
						args.Added.Add(i);
				}

				for (int i = frameLayers.Count; i < main.Components.Count; i++) {
					if (SelectedItems.Remove(i))
						args.Removed.Add(i);
				}

				OnSelectionChanged(args);
			}
		}

		/// <summary>
		/// Deselects all.
		/// </summary>
		public void DeselectAll() {
			if (_editor != null && _editor.LayerEditor != null && _editor.LayerEditor.DoNotRemove) return;
			
			SelectionChangedEventArgs args = new SelectionChangedEventArgs();

			var items = SelectedItems.ToList();

			for (int i = 0; i < items.Count; i++) {
				if (SelectedItems.Remove(items[i]))
					args.Removed.Add(items[i]);
			}

			OnSelectionChanged(args);
		}

		/// <summary>
		/// Sets the selection by range.
		/// </summary>
		/// <param name="start">The start index.</param>
		/// <param name="length">The length.</param>
		public void SetSelection(int start, int length) {
			SelectionChangedEventArgs args = new SelectionChangedEventArgs();

			foreach (var i in SelectedItems) {
				args.Removed.Add(i);
			}

			SelectedItems.Clear();

			for (int i = start; i < start + length; i++) {
				if (args.Removed.Contains(i)) {
					args.Removed.Remove(i);
				}
				else {
					args.Added.Add(i);
				}

				SelectedItems.Add(i);
			}

			OnSelectionChanged(args);
		}

		/// <summary>
		/// Sets the selection by the layer index.
		/// </summary>
		/// <param name="index">The layer index.</param>
		public void SetSelection(int index) {
			SetSelection(new HashSet<int>() { index });
		}

		/// <summary>
		/// Selects or unselects the layer under the mouse.
		/// </summary>
		/// <param name="oldPosition">The old position.</param>
		/// <param name="e">The <see cref="MouseEventArgs" /> instance containing the event data.</param>
		public void SelectUnderMouse(Point oldPosition, MouseEventArgs e) {
			//var main = Components.OfType<ActDraw2>().FirstOrDefault(p => p.Primary);
			//
			//if (main != null) {
			//	var layerIndex = main.GetLayerIndexUnderMouse(oldPosition);
			//	InvertSelection(layerIndex);
			//}

			var main = Main;
			
			if (main != null) {
				var components = new List<DrawingComponent>(main.Components);
				components.Reverse();
			
				foreach (LayerDraw sd in components) {
					if (sd.IsMouseUnder(_renderer.PointToScreen(oldPosition)) && sd.IsMouseUnder(e)) {
						InvertSelection(sd.LayerIndex);
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
			//var main = Components.OfType<ActDraw2>().FirstOrDefault(p => p.Primary);
			//
			//if (main != null) {
			//	var layerIndex = main.GetLayerIndexUnderMouse(position);
			//	return layerIndex > -1;
			//}
			//
			//return null;

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
			SelectionChangedEventArgs args = new SelectionChangedEventArgs();

			if (SelectedItems.Add(index)) {
				args.Added.Add(index);
				LatestSelected = index;
				OnSelectionChanged(args);
			}
		}

		/// <summary>
		/// Removes a layer index from the selection.
		/// </summary>
		/// <param name="index">The layer index.</param>
		public void RemoveSelection(int index) {
			SelectionChangedEventArgs args = new SelectionChangedEventArgs();

			if (SelectedItems.Remove(index)) {
				args.Removed.Add(index);
				OnSelectionChanged(args);
			}
		}

		/// <summary>
		/// Inverts the selection state of a layer index.
		/// </summary>
		/// <param name="index">The layer index.</param>
		public void InvertSelection(int index) {
			if (index < 0)
				return;

			if (IsSelected(index))
				RemoveSelection(index);
			else
				AddSelection(index);
		}

		/// <summary>
		/// Sets the selection by a selection list of indexes.
		/// </summary>
		/// <param name="selection">The selection list.</param>
		public void SetSelection(HashSet<int> selection) {
			SelectionChangedEventArgs args = new SelectionChangedEventArgs();

			foreach (var i in SelectedItems) {
				args.Removed.Add(i);
			}

			foreach (var i in selection) {
				if (args.Removed.Contains(i)) {
					args.Removed.Remove(i);
				}
				else {
					args.Added.Add(i);
				}
			}

			SelectedItems = selection;
			OnSelectionChanged(args);
		}

		/// <summary>
		/// Selects the reverse of the selection provided by a list.
		/// </summary>
		public void SelectReverse() {
			var main = Main;
			
			if (main != null) {
				SelectionChangedEventArgs args = new SelectionChangedEventArgs();

				// Remove unused selection
				foreach (var i in SelectedItems.ToList()) {
					if (i >= main.Components.Count)
						SelectedItems.Remove(i);
				}

				List<Layer> frameLayers = _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers;

				for (int i = 0; i < main.Components.Count && i < frameLayers.Count; i++) {
					if (SelectedItems.Contains(i)) {
						SelectedItems.Remove(i);
						args.Removed.Add(i);
					}
					else {
						SelectedItems.Add(i);
						args.Added.Add(i);
					}
				}

				OnSelectionChanged(args);
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
					InvertSelection(layerIndex);
				}
				else {
					int from = lastSelected < layerIndex ? lastSelected : layerIndex;
					int to = lastSelected < layerIndex ? layerIndex : lastSelected;

					SelectionChangedEventArgs args = new SelectionChangedEventArgs();
					List<Layer> frameLayers = _act[_renderer.SelectedAction, _renderer.SelectedFrame].Layers;

					for (int i = from; i <= to && i < main.Components.Count && i < frameLayers.Count; i++) {
						if (SelectedItems.Add(i))
							args.Added.Add(i);
					}

					OnSelectionChanged(args);
				}
			}
		}

		private Rect[] _getSelectionAreas(ZoomEngine zoom) {
			List<Rect> rectangles = new List<Rect>();
			Spr sprite = _renderer.Act.Sprite;
			Act act = _renderer.Act;

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

					var diffOffsets = _getAnchorOffsetsFromBase(act);

					double offsetX = (layer.OffsetX + diffOffsets.X) * zoom.Scale;
					double offsetY = (layer.OffsetY + diffOffsets.Y) * zoom.Scale;

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

		private (int X, int Y) _getAnchorOffsetsFromBase(Act act) {
			int diffX = 0;
			int diffY = 0;

			var frame = act[_renderer.SelectedAction, _renderer.SelectedFrame];

			if (act.AnchoredTo != null && frame.Anchors.Count > 0) {
				Frame frameReference = act.AnchoredTo.TryGetFrame(_renderer.SelectedAction, _renderer.SelectedFrame);

				if (frameReference != null && frameReference.Anchors.Count > 0) {
					diffX = frameReference.Anchors[0].OffsetX - frame.Anchors[0].OffsetX;
					diffY = frameReference.Anchors[0].OffsetY - frame.Anchors[0].OffsetY;

					if (act.AnchoredTo.AnchoredTo != null) {
						frameReference = act.AnchoredTo.AnchoredTo.TryGetFrame(_renderer.SelectedAction, _renderer.SelectedFrame);

						if (frameReference != null && frameReference.Anchors.Count > 0) {
							diffX = frameReference.Anchors[0].OffsetX - frame.Anchors[0].OffsetX;
							diffY = frameReference.Anchors[0].OffsetY - frame.Anchors[0].OffsetY;
						}
					}
				}
			}

			return (diffX, diffY);
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

		private void _internalFullClearSelection() {
			if (ActEditorConfiguration.KeepPreviewSelectionFromActionChange) {
				return;
			}

			DeselectAll();
		}

		public void PopSelectedLayerState() {
			foreach (LayerDraw layer in SelectedLayerDraws) {
				layer.SaveInitialData();
			}
		}

		public void UpdateSelection(Rect rect, bool show) {
			if (_editor.IndexSelector == null) return;
			if (_editor.IndexSelector.IsPlaying) return;

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

		public bool IsSelected(int index) {
			return SelectedItems.Contains(index);
		}
	}
}