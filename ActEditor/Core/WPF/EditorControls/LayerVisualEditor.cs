using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.GenericControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TokeiLibrary;

namespace ActEditor.Core.WPF.EditorControls {
	public class LayerEditorComponents {
		public ScrollViewer Part_ScrollViewer;
		public StackPanel Part_Content;
		public Grid Part_ContentContainer;
		public Grid Part_ContentOverlay;
		public LayerEditor LayerEditor;
	}

	public class LayerVisualEditor {
		private LayerEditorComponents _components;
		private TabAct _actEditor;
		private LayerVisualEditBox _editBoxControl;
		
		public LayerVisualEditor() {
		}

		public void Init(TabAct actEditor, LayerEditorComponents components) {
			_components = components;
			_actEditor = actEditor;

			_actEditor.IndexSelector.FrameChanged += (s, e) => {
				if (_components.LayerEditor.DoNotRemove && !ActEditorConfiguration.ActEditorRefreshLayerEditor)
					return;

				InvalidateVisual();
			};
			_actEditor.IndexSelector.ActionChanged += (s, e) => InvalidateVisual();

			_actEditor.ActLoaded += delegate {
				_actEditor.Act.RenderInvalidated += (s) => InvalidateVisual();
			};

			_components.Part_ScrollViewer.ScrollChanged += _sv_ScrollChanged;
			_components.Part_ScrollViewer.SizeChanged += _sv_SizeChanged;

			_components.LayerEditor.PreviewKeyDown += _layerEditor_PreviewKeyDown;

			_editBoxControl = new LayerVisualEditBox(this, actEditor, _components);
		}

		private int _focusRow = -1;
		private int _focusCol = -1;

		private void _layerEditor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if (e.Key == Key.Tab) {
				// Find where the focus is currently at!
				IdentifyFocus();

				if (_focusRow == -1 || _focusCol == -1) {
					SetFocus(0, 0);
				}
				else {
					if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
						_focusCol--;
					}
					else {
						_focusCol++;
					}

					if (_focusCol >= 9) {
						_focusCol = 1;
						_focusRow++;
					}

					if (_focusCol < 1) {
						_focusCol = 8;
						_focusRow--;
					}

					if (_focusRow >= _actEditor.Act[_lastAid, _lastFid].NumberOfLayers)
						_focusRow = 0;

					if (_focusRow < 0)
						_focusRow = _actEditor.Act[_lastAid, _lastFid].NumberOfLayers - 1;

					SetFocus(_focusRow, _focusCol);
				}

				e.Handled = true;
			}
		}

		private void IdentifyFocus() {
			_focusRow = -1;
			_focusCol = -1;

			if (Keyboard.FocusedElement == _editBoxControl.EditBox) {
				_focusRow = _editBoxControl.EditLayerIndex;
				_focusCol = (int)_editBoxControl.EditValue;
				return;
			}

			var visualLayer = WpfUtilities.FindParentControl<VisualLayer>(Keyboard.FocusedElement as DependencyObject);

			if (visualLayer == null)
				return;

			var layerEditor = WpfUtilities.FindParentControl<LayerEditor>(visualLayer);

			if (layerEditor == null)
				return;

			_focusRow = visualLayer.LayerIndex;

			if (Keyboard.FocusedElement is CheckBox) {
				_focusCol = 4;
			}
			else if (Keyboard.FocusedElement is LayerColorSelector) {
				_focusCol = 5;
			}
		}

		public enum EditableDataValues {
			LayerIndex,
			SpriteNumber,
			OffsetX,
			OffsetY,
			Mirror,
			Color,
			ScaleX,
			ScaleY,
			Rotation
		}

		private void SetFocus(int row, int col) {
			_focusRow = row;
			_focusCol = col;

			var visualLayer = GetVisualLayer(row);

			if (visualLayer == null) {
				// Need to force the visual layer!
				var targetOffset = _focusRow * PreviewElementHeight;

				if (_components.Part_ScrollViewer.VerticalOffset > targetOffset || targetOffset == 0) {
					_components.Part_ScrollViewer.ScrollToVerticalOffset(targetOffset);
				}
				else {
					_components.Part_ScrollViewer.ScrollToVerticalOffset(targetOffset + _components.Part_ScrollViewer.ViewportHeight - PreviewElementHeight);
				}

				_components.Part_ScrollViewer.Dispatcher.BeginInvoke(new Action(() =>
				{
					visualLayer = GetVisualLayer(row);

					if (visualLayer == null)
						return;

					visualLayer.SetFocus((EditableDataValues)col, this);
				}), DispatcherPriority.Loaded);

				return;
			}

			if (visualLayer == null)
				return;

			visualLayer.SetFocus((EditableDataValues)col, this);
		}

		private void _sv_SizeChanged(object sender, SizeChangedEventArgs e) {
			_updateVisual();
		}

		private void _sv_ScrollChanged(object sender, ScrollChangedEventArgs e) {
			// Call instantly
			//InvalidateVisual();

			if (e.VerticalChange == 0)
				return;

			_updateVisual(false);
		}

		private bool _updatePending = false;

		public void InvalidateVisual() {
			if (_updatePending)
				return;

			_updatePending = true;

			_actEditor.Dispatcher.BeginInvoke(new System.Action(delegate {
				_updatePending = false;
				_updateVisual();
			}), DispatcherPriority.Background);
		}

		public List<VisualLayer> VisualLayers => _visualLayers;
		private VisualLayer _focussedLayer;
		private List<VisualLayer> _visualLayers = new List<VisualLayer>();
		private Dictionary<int, VisualLayer> _layerIdx2VisualControl = new Dictionary<int, VisualLayer>();

		public double PreviewElementHeight = 17;
		public bool PreviewElementHeightSet = false;

		private int _lastAid = -1;
		private int _lastFid = -1;

		public int LastAid => _lastAid;
		public int LastFid => _lastFid;

		private void _updateVisual(bool canCancel = true) {
			Stopwatch watch = Stopwatch.StartNew();

			var aid = _actEditor.SelectedAction;
			var fid = _actEditor.SelectedFrame;

			var minimumHeight = _components.Part_ScrollViewer.ActualHeight;
			int lineCount = 0;
			int numberOfLayers = 0;

			if (_actEditor != null) {
				var frame = _actEditor.Act.TryGetFrame(aid, fid);

				if (frame != null)
					lineCount = frame.Layers.Count;
			}

			numberOfLayers = lineCount;

			var targetHeight = lineCount * PreviewElementHeight;
			targetHeight = Math.Max(minimumHeight, targetHeight);

			_components.Part_ContentContainer.Height = targetHeight;
			_components.Part_ContentOverlay.Height = targetHeight;
			
			int elementCount = (int)(Math.Ceiling(minimumHeight / PreviewElementHeight) + 1);

			_components.Part_Content.Height = elementCount * PreviewElementHeight;

			// Store correct layers
			Dictionary<int, VisualLayer> validLayers = new Dictionary<int, VisualLayer>();
			List<int> dirtyLayers = new List<int>();
			_layerIdx2VisualControl.Clear();

			if (Keyboard.FocusedElement == _editBoxControl.EditBox) {
				var oldFocussedLayer = _focussedLayer;
				_focussedLayer = _editBoxControl.EditVisualLayer;

				if (oldFocussedLayer != _focussedLayer && oldFocussedLayer != null && !_visualLayers.Contains(oldFocussedLayer)) {
					_components.Part_ContentContainer.Children.Remove(oldFocussedLayer);
				}
			}

			var start = (int)(_components.Part_ScrollViewer.ContentVerticalOffset / PreviewElementHeight);

			for (int i = 0; i < _visualLayers.Count; i++) {
				var layerControl = _visualLayers[i];

				if (layerControl.LayerIndex >= start && layerControl.LayerIndex < start + elementCount) {
					validLayers[i] = layerControl;
					_layerIdx2VisualControl[layerControl.LayerIndex] = layerControl;
					layerControl.IsVisualDirty = false;
				}
				else {
					dirtyLayers.Add(i);
					layerControl.IsVisualDirty = true;
				}
			}

			// Fetch missing indexes
			Queue<int> missingLayerIndexes = FetchMissingVisualLayerIndexes(elementCount, start, _layerIdx2VisualControl.Keys);

			if (_visualLayers.Count != elementCount) {
				UpdateVisualLayerAmount(elementCount, aid, fid, dirtyLayers, missingLayerIndexes);
				canCancel = false;
			}

			// Update visual controls
			for (int i = 0; i < elementCount; i++) {
				var visualLayer = _visualLayers[i];
				int layerIdx;

				if (visualLayer.IsVisualDirty) {
					if (_focussedLayer != null && _focussedLayer.LayerIndex == visualLayer.LayerIndex) {
						visualLayer = CreateNewVisualLayer();
						_visualLayers[i] = visualLayer;
					}

					layerIdx = missingLayerIndexes.Dequeue();

					if (_focussedLayer != null && _focussedLayer.LayerIndex == layerIdx) {
						_components.Part_ContentContainer.Children.Remove(visualLayer);
						visualLayer = _focussedLayer;
						_visualLayers[i] = _focussedLayer;
					}

					_layerIdx2VisualControl[layerIdx] = visualLayer;
					visualLayer.Margin = new Thickness(0, layerIdx * PreviewElementHeight, 0, 0);

					if (visualLayer.LayerIndex == -1)
						_components.Part_ContentContainer.Children.Add(visualLayer);
				}
				else {
					layerIdx = visualLayer.LayerIndex;
				}

				visualLayer.Set(_actEditor.Act, aid, fid, layerIdx);
				visualLayer.InternalUpdate();

				if (visualLayer == _editBoxControl.EditVisualLayer && visualLayer == _focussedLayer) {
					_editBoxControl.UpdateEditBox();
				}

				if (canCancel && watch.ElapsedMilliseconds > 10) {
					Console.WriteLine("took too long! " + watch.ElapsedMilliseconds);
					InvalidateVisual();
					return;
				}
			}

			for (int i = 0; i < elementCount; i++) {
				var visualLayer = _visualLayers[i];

				if (visualLayer.LayerIndex < numberOfLayers) {
					if (visualLayer.Visibility != Visibility.Visible)
						visualLayer.Visibility = Visibility.Visible;
					//if (visualLayer == _editVisualLayer && visualLayer == _focussedLayer && _editBox.Visibility != Visibility.Visible) {
					//	_editBox.Visibility = Visibility.Visible;
					//	_editTextBlock.Visibility = Visibility.Hidden;
					//	Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (_updateVisual1)");
					//}
				}
				else {
					if (visualLayer.Visibility != Visibility.Hidden)
						visualLayer.Visibility = Visibility.Hidden;
					if (visualLayer == _editBoxControl.EditVisualLayer && visualLayer == _focussedLayer && _editBoxControl.EditBox.Visibility != Visibility.Collapsed) {
						_editBoxControl.HideEditBox();
						//Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (_updateVisual2)");
					}
				}
			}

			_lastAid = aid;
			_lastFid = fid;

			if (!PreviewElementHeightSet && _visualLayers.Count > 0 && _visualLayers[0].ActualHeight > 0) {
				PreviewElementHeightSet = true;
				PreviewElementHeight = _visualLayers[0].ActualHeight;
				InvalidateVisual();
			}
		}

		private Queue<int> FetchMissingVisualLayerIndexes(int elementCount, int start, Dictionary<int, VisualLayer>.KeyCollection keys) {
			bool[] exists = new bool[elementCount];
			Queue<int> missingLayerIndexes = new Queue<int>();

			foreach (var index in keys) {
				exists[index - start] = true;
			}

			for (int i = 0; i < elementCount; i++) {
				if (exists[i])
					continue;

				missingLayerIndexes.Enqueue(start + i);
			}

			return missingLayerIndexes;
		}

		private void UpdateVisualLayerAmount(int elementCount, int aid, int fid, List<int> dirtyLayers, Queue<int> missingLayerIndexes) {
			if (_visualLayers.Count > elementCount) {
				// Delete dirty layers, the virtualization viewport size was reduced
				foreach (var idx in dirtyLayers.OrderByDescending(p => p)) {
					// Keep the focussed layer in the grid
					if (_focussedLayer != _visualLayers[idx]) {
						_components.Part_ContentContainer.Children.Remove(_visualLayers[idx]);
					}

					_visualLayers.RemoveAt(idx);
				}
			}

			// Add new layers, the virtualization viewport was increased
			if (_visualLayers.Count < elementCount) {
				for (int i = _visualLayers.Count; i < elementCount; i++) {
					int layerIdx = missingLayerIndexes.Dequeue();
					VisualLayer visualLayer;

					if (_focussedLayer != null && layerIdx == _focussedLayer.LayerIndex) {
						visualLayer = _focussedLayer;
					}
					else {
						visualLayer = CreateNewVisualLayer();
						visualLayer.Set(_actEditor.Act, aid, fid, layerIdx);
						visualLayer.Margin = new Thickness(0, layerIdx * PreviewElementHeight, 0, 0);
						visualLayer.IsVisualDirty = false;
						_components.Part_ContentContainer.Children.Add(visualLayer);
					}
					
					_layerIdx2VisualControl[layerIdx] = visualLayer;
					_visualLayers.Add(visualLayer);
				}
			}
		}

		public VisualLayer CreateNewVisualLayer() {
			VisualLayer visualLayer = new VisualLayer(_actEditor.Act, _actEditor, -1);
			visualLayer.IsVisualLayer = true;
			//visualLayer.Height = PreviewElementHeight;
			visualLayer.VerticalAlignment = VerticalAlignment.Top;
			visualLayer.IsVisualDirty = true;
			return visualLayer;
		}

		public void PreviewSelect(VisualLayer layer) {
			foreach (var visualLayer in _visualLayers) {
				if (visualLayer == layer)
					visualLayer.IsPreviewSelected = true;
				else
					visualLayer.IsPreviewSelected = false;
			}
		}

		public void DrawSelection(int layerIndex) {
			if (_layerIdx2VisualControl.TryGetValue(layerIndex, out VisualLayer visualLayer)) {
				visualLayer.DrawSelection();
			}
		}

		public VisualLayer GetVisualLayer(int layerIndex) {
			if (_layerIdx2VisualControl.TryGetValue(layerIndex, out VisualLayer visualLayer)) {
				return visualLayer;
			}

			return null;
		}

		public void SetEditBox(int layerIndex, int col) {
			_editBoxControl.SetEditBox(layerIndex, col);
		}
	}
}
