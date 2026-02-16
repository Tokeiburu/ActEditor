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
using System.Windows.Media;
using System.Windows.Threading;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;

namespace ActEditor.Core.WPF.EditorControls {
	public class LayerVisualEditor {
		private TabAct _actEditor;
		private ScrollViewer _sv;
		private StackPanel _sp;
		private LayerEditor _layerEditor;
		private Grid _gridBackground;
		private Grid _gridOverlay;
		private TextBox _editBox;

		public LayerVisualEditor() {
		}

		public void Init(TabAct actEditor) {
			_actEditor = actEditor;
			_sv = _actEditor.LayerEditor._sv;
			_sp = _actEditor.LayerEditor._sp;
			_layerEditor = _actEditor.LayerEditor;
			_gridBackground = _actEditor.LayerEditor._gridBackground;
			_gridOverlay = _actEditor.LayerEditor._gridOverlay;

			_actEditor.IndexSelector.FrameChanged += (s, e) => {
				if (_layerEditor.DoNotRemove && !ActEditorConfiguration.ActEditorRefreshLayerEditor)
					return;

				InvalidateVisual();
			};
			_actEditor.IndexSelector.ActionChanged += (s, e) => InvalidateVisual();

			_actEditor.ActLoaded += delegate {
				_actEditor.Act.RenderInvalidated += (s) => InvalidateVisual();
			};

			_sv.ScrollChanged += _sv_ScrollChanged;
			_sv.SizeChanged += _sv_SizeChanged;

			_layerEditor.PreviewKeyDown += _layerEditor_PreviewKeyDown;
			_layerEditor.PreviewMouseLeftButtonDown += _layerEditor_PreviewMouseLeftButtonDown;

			InitializeEditTextBox();
		}

		private void InitializeEditTextBox() {
			_editBox = new TextBox();
			_editBox.Text = "Test";
			_editBox.Width = 100;
			_editBox.Height = 17;
			_editBox.Background = Brushes.Transparent;
			_editBox.Visibility = Visibility.Collapsed;
			_editBox.HorizontalAlignment = HorizontalAlignment.Left;
			_editBox.VerticalAlignment = VerticalAlignment.Top;
			_editBox.TextAlignment = TextAlignment.Right;
			_editBox.Padding = new Thickness(0);
			_editBox.BorderThickness = new Thickness(0);

			_gridOverlay.Children.Add(_editBox);

			_editBox.TextChanged += delegate {
				if (_editBoxEventDisabled) return;

				var visualLayer = GetVisualLayer(_editLayerIndex);

				if (visualLayer != null) {
					visualLayer.SetLayerValue(_editBox.Text, _editValue);
					_editTextBlock.Text = _editBox.Text;
				}
			};

			_editBox.PreviewLostKeyboardFocus += _editBox_PreviewLostKeyboardFocus;
		}

		private void _editBox_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			if (_editBoxFocusEventDisabled) return;

			_editLayerIndex = -1;
			_editBox.Visibility = Visibility.Collapsed;

			if (_editTextBlock != null && _editTextBlock.Visibility != Visibility.Visible)
				_editTextBlock.Visibility = Visibility.Visible;

			//Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (_editBox_PreviewLostKeyboardFocus)");
		}

		private bool _editBoxFocusEventDisabled = false;
		private bool _editBoxEventDisabled = false;
		private EditableDataValues _editValue = EditableDataValues.SpriteNumber;
		private int _editLayerIndex = -1;
		private TextBlock _editTextBlock;
		private VisualLayer _editVisualLayer;

		private void _layerEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			var point = e.GetPosition(_gridBackground);

			var layerIndex = (int)(point.Y / PreviewElementHeight);
			var visualLayer = GetVisualLayer(layerIndex);

			if (point.Y >= 0 && visualLayer != null && visualLayer.Visibility == Visibility.Visible) {
				if (SetEditBox(layerIndex, visualLayer.GetColumn(_gridBackground.GetObjectAtPoint<TextBlock>(point)))) {
					//e.Handled = true;
					return;
				}
			}

			_editBox.Visibility = Visibility.Collapsed;

			if (_editTextBlock != null && _editTextBlock.Visibility != Visibility.Visible)
				_editTextBlock.Visibility = Visibility.Visible;

			//Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (_layerEditor_PreviewMouseLeftButtonDown)");
		}

		public void FocusEditBox() {
			_editBoxFocusEventDisabled = true;

			_editBox.Dispatcher.BeginInvoke(new System.Action(delegate {
				_editBox.Focus();
				_editBox.SelectAll();
				_editBoxFocusEventDisabled = false;
			}), DispatcherPriority.Render);
		}

		public void UpdateEditBoxValue(string text) {
			try {
				_editBoxEventDisabled = true;
				_editBox.Text = text;
			}
			finally {
				_editBoxEventDisabled = false;
			}
		}

		public bool SetEditBox(int row, int col) {
			if (_editBox.Visibility == Visibility.Visible && row == _editLayerIndex && col == (int)_editValue)
				return true;

			var visualLayer = GetVisualLayer(row);

			if (visualLayer == null || visualLayer.Visibility != Visibility.Visible || col < 0)
				return false;

			var block = visualLayer.GetBlockFromCol(col);

			if (block != null) {
				var blockPosition = block.TransformToVisual(_gridBackground).Transform(new Point(0, 0));
				var editValue = (EditableDataValues)col;

				if (editValue != EditableDataValues.LayerIndex) {
					if (_editTextBlock != null && _editTextBlock.Visibility != Visibility.Visible && _editTextBlock != block) {
						_editTextBlock.Visibility = Visibility.Visible;
					}
					_editBox.Visibility = Visibility.Visible;
					_editTextBlock = block;
					_editTextBlock.Visibility = Visibility.Hidden;
					//Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (SetEditBox)");
					_editValue = editValue;
					_editBox.Width = ((Border)block.Parent).ActualWidth - 1;
					_editBox.Height = block.ActualHeight;
					//_editBox.Height = ((Border)block.Parent).ActualHeight;
					_editVisualLayer = visualLayer;

					_editBox.Margin = new Thickness(blockPosition.X, blockPosition.Y, 0, 0);
					
					UpdateEditBoxValue(block.Text);

					_editLayerIndex = row;

					FocusEditBox();
					return true;
				}
			}

			return false;
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

			if (Keyboard.FocusedElement == _editBox) {
				_focusRow = _editLayerIndex;
				_focusCol = (int)_editValue;
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

				if (_sv.VerticalOffset > targetOffset || targetOffset == 0) {
					_sv.ScrollToVerticalOffset(targetOffset);
				}
				else {
					_sv.ScrollToVerticalOffset(targetOffset + _sv.ViewportHeight - PreviewElementHeight);
				}

				_sv.Dispatcher.BeginInvoke(new Action(() =>
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

		private void _updateVisual(bool canCancel = true) {
			Stopwatch watch = Stopwatch.StartNew();

			var aid = _actEditor.SelectedAction;
			var fid = _actEditor.SelectedFrame;

			var minimumHeight = _sv.ActualHeight;
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

			_gridBackground.Height = targetHeight;
			_gridOverlay.Height = targetHeight;
			
			int elementCount = (int)(Math.Ceiling(minimumHeight / PreviewElementHeight) + 1);
			
			_sp.Height = elementCount * PreviewElementHeight;

			// Store correct layers
			Dictionary<int, VisualLayer> validLayers = new Dictionary<int, VisualLayer>();
			List<int> dirtyLayers = new List<int>();
			_layerIdx2VisualControl.Clear();

			if (Keyboard.FocusedElement == _editBox) {
				var oldFocussedLayer = _focussedLayer;
				_focussedLayer = _editVisualLayer;

				if (oldFocussedLayer != _focussedLayer && oldFocussedLayer != null && !_visualLayers.Contains(oldFocussedLayer)) {
					_gridBackground.Children.Remove(oldFocussedLayer);
				}
			}

			var start = (int)(_sv.ContentVerticalOffset / PreviewElementHeight);

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
						_gridBackground.Children.Remove(visualLayer);
						visualLayer = _focussedLayer;
						_visualLayers[i] = _focussedLayer;
					}

					_layerIdx2VisualControl[layerIdx] = visualLayer;
					visualLayer.Margin = new Thickness(0, layerIdx * PreviewElementHeight, 0, 0);

					if (visualLayer.LayerIndex == -1)
						_gridBackground.Children.Add(visualLayer);
				}
				else {
					layerIdx = visualLayer.LayerIndex;
				}

				visualLayer.Set(_actEditor.Act, aid, fid, layerIdx);
				visualLayer.InternalUpdate();

				if (visualLayer == _editVisualLayer && visualLayer == _focussedLayer) {
					if (_editTextBlock.Text != _editBox.Text) {
						UpdateEditBoxValue(_editTextBlock.Text);
					}
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
					if (visualLayer == _editVisualLayer && visualLayer == _focussedLayer && _editBox.Visibility != Visibility.Collapsed) {
						_editBox.Visibility = Visibility.Collapsed;
						_editTextBlock.Visibility = Visibility.Visible;
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
						_gridBackground.Children.Remove(_visualLayers[idx]);
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
						_gridBackground.Children.Add(visualLayer);
					}
					
					_layerIdx2VisualControl[layerIdx] = visualLayer;
					_visualLayers.Add(visualLayer);
				}
			}
		}

		private VisualLayer CreateNewVisualLayer() {
			VisualLayer visualLayer = new VisualLayer(_actEditor.Act, _actEditor, -1);
			visualLayer.IsVisualLayer = true;
			//visualLayer.Height = PreviewElementHeight;
			visualLayer.VerticalAlignment = VerticalAlignment.Top;
			visualLayer.IsVisualDirty = true;
			return visualLayer;
		}

		internal void PreviewSelect(VisualLayer layer) {
			foreach (var visualLayer in _visualLayers) {
				if (visualLayer == layer)
					visualLayer.IsPreviewSelected = true;
				else
					visualLayer.IsPreviewSelected = false;
			}
		}

		internal void DrawSelection(int layerIndex) {
			if (_layerIdx2VisualControl.TryGetValue(layerIndex, out VisualLayer visualLayer)) {
				visualLayer.DrawSelection();
			}
		}

		internal VisualLayer GetVisualLayer(int layerIndex) {
			if (_layerIdx2VisualControl.TryGetValue(layerIndex, out VisualLayer visualLayer)) {
				return visualLayer;
			}

			return null;
		}
	}
}
