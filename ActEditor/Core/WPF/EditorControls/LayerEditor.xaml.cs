using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.Scripting.Scripts;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Control = System.Windows.Forms.Control;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for LayerEditor.xaml
	/// </summary>
	public partial class LayerEditor : UserControl {
		private readonly DispatcherTimer _autoScrollTimer;

		private readonly Stopwatch _watch = new Stopwatch();
		private TabAct _actEditor;
		private bool _hasMoved;
		private int _layerMouseDown = -1;
		private Point _oldPosition;
		private int _previousMouseDown = -1;
		private readonly LayerControlLoadThread _layerControlThread;

		public LayerEditor() {
			InitializeComponent();

			_displayGrid.ColumnDefinitions[1] = new ColumnDefinition {Width = new GridLength(SystemParameters.VerticalScrollBarWidth)};
			_autoScrollTimer = new DispatcherTimer(DispatcherPriority.Render);
			_autoScrollTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
			_autoScrollTimer.Tick += new EventHandler(_autoScrollTimer_Tick);

			_layerControlThread = new LayerControlLoadThread(this);
			_layerControlThread.Start();

			Dispatcher.ShutdownStarted += delegate {
				_layerControlThread.Stop();
			};

			PreviewKeyDown += new KeyEventHandler(_layerEditor_PreviewKeyDown);
		}

		public bool DoNotRemove { get; set; }

		public int SelectedAction {
			get { return _actEditor._frameSelector.SelectedAction; }
		}

		public int SelectedFrame {
			get { return _actEditor._frameSelector.SelectedFrame; }
		}

		public void SetReadonlyMode(bool value, bool fromAnimationPlaying = false) {
			if (Readonly == value)
				return;

			ClickSelectTextBox.EventsEnabled = !value;

			Readonly = value;

			if (fromAnimationPlaying && !ActEditorConfiguration.ActEditorRefreshLayerEditor) {
				this.Dispatch(p => p.IsEnabled = !Readonly);
			}
		}

		public bool Readonly { get; private set; }

		private void _layerEditor_PreviewKeyDown(object sender, KeyEventArgs e) {
			if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
			}
		}

		private void _autoScrollTimer_Tick(object sender, EventArgs e) {
			if (Mouse.LeftButton != MouseButtonState.Pressed) {
				StopAutoScroll();
				return;
			}

			if (_scrollDirection < 0 && _isAboveViewport())
				_sv.ScrollToVerticalOffset(_sv.VerticalOffset - _sfch.ActualHeight);
			else if (_scrollDirection > 0 && _isUnderViewport())
				_sv.ScrollToVerticalOffset(_sv.VerticalOffset + _sfch.ActualHeight);
			else
				StopAutoScroll();
		}

		private bool _isUnderViewport() {
			var position2 = Control.MousePosition;
			var position = PointFromScreen(new Point(position2.X, position2.Y));
			return position.Y > ActualHeight - 10;
		}

		private bool _isAboveViewport() {
			var position2 = Control.MousePosition;
			var position = PointFromScreen(new Point(position2.X, position2.Y));
			return this.GetObjectAtPoint<LayerControlHeader>(position) != null;
		}

		private LayerVisualEditor _visualEditor = new LayerVisualEditor();
		private int _lastInsertLineIndex;
		private int _scrollDirection;

		public void Init(TabAct actEditor) {
			LayerEditorComponents components = new LayerEditorComponents();

			components.Part_ScrollViewer = _sv;
			components.Part_Content = _sp;
			components.Part_ContentContainer = _gridBackground;
			components.Part_ContentOverlay = _gridOverlay;
			components.LayerEditor = this;

			_visualEditor.Init(actEditor, components);
			_actEditor = actEditor;

			_actEditor.ActLoaded += delegate {
				if (actEditor.Act == null) return;
				actEditor.Act.Commands.CommandRedo += (s, e) => _visualEditor.InvalidateVisual();
				actEditor.Act.Commands.CommandUndo += (s, e) => _visualEditor.InvalidateVisual();
			};

			_actEditor._frameSelector.AnimationPlaying += new ActIndexSelector.FrameIndexChangedDelegate(_frameSelector_AnimationPlaying);

			PreviewMouseDown += new MouseButtonEventHandler(_layerEditor_MouseDown);
			PreviewMouseUp += new MouseButtonEventHandler(_layerEditor_MouseUp);
			MouseMove += new MouseEventHandler(_layerEditor_MouseMove);
			MouseLeave += new MouseEventHandler(_layerEditor_MouseLeave);
			DragOver += new DragEventHandler(_layerEditor_DragOver);
			DragEnter += new DragEventHandler(_layerEditor_DragEnter);
			DragLeave += new DragEventHandler(_layerEditor_DragLeave);
			Drop += new DragEventHandler(_layerEditor_Drop);

			_actEditor.SelectionEngine.SelectionChanged += _selectionEngine_SelectionChanged;
		}

		private void _selectionEngine_SelectionChanged(SelectionEngine selector, SelectionEngine.SelectionChangedEventArgs e) {
			foreach (var index in e.Added) {
				_visualEditor.DrawSelection(index);
			}

			foreach (var index in e.Removed) {
				_visualEditor.DrawSelection(index);
			}
		}

		public void AsyncUpdateLayerControl(int layerIndex) {
			_layerControlThread.Update(layerIndex);
		}

		public void AsyncUpdateLayerControl(int[] layerIndexes) {
			_layerControlThread.Update(layerIndexes);
		}

		private void _frameSelector_AnimationPlaying(object sender, int mode) {
			if (mode == 0) {
				_watch.Stop();

				SetReadonlyMode(false, fromAnimationPlaying: true);
				DoNotRemove = false;

				int action = _actEditor._frameSelector.SelectedAction;
				int frame = _actEditor._frameSelector.SelectedFrame;
				this.Dispatch(p => p.Update());
			}
			else {
				SetReadonlyMode(true, fromAnimationPlaying: true);
				DoNotRemove = true;
			}

			this.Dispatch(delegate {
				IsHitTestVisible = mode == 0;
			});
		}

		private void _layerEditor_DragLeave(object sender, DragEventArgs e) {

			if (NativeMethods.GetCursorPos(out NativeMethods.POINT lpPoint)) {
				Point screenPos = new Point(lpPoint.X, lpPoint.Y);
				Point relativePos = this.PointFromScreen(screenPos);

				bool isStillOver = new Rect(0, 0, ActualWidth, ActualHeight).Contains(relativePos);

				if (!isStillOver) {
					_mouseLeave(true);
				}
			}
		}

		private void _layerEditor_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null)
				_enter(e.GetPosition(this));
			else
				e.Effects = DragDropEffects.None;
		}

		private void _layerEditor_DragOver(object sender, DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null)
				_move(e.GetPosition(this), true, e.Data);
			else
				e.Effects = DragDropEffects.None;
		}

		private void _layerEditor_Drop(object sender, DragEventArgs e) {
			try {
				object imageIndexObj = e.Data.GetData("ImageIndex");
				if (imageIndexObj == null) return;

				int imageIndex = (int) imageIndexObj;
				int dropIndex = _getIndexDrop(e.GetPosition(this), true);

				if (dropIndex > -1) {
					_clearPreviewDrag(_actEditor.Act[_actEditor.SelectedAction, _actEditor.SelectedFrame]);

					var selection = GetSelection();

					if (_actEditor.Act == null) return;

					_actEditor.Act.Commands.LayerAdd(_actEditor.SelectedAction, _actEditor.SelectedFrame, dropIndex, imageIndex);

					Update();
					_actEditor._rendererPrimary.Update();
					_actEditor.SelectionEngine.SetSelection(GenerateSelection(selection));
					e.Handled = true;
				}
			}
			finally {
				DisableInsertLine();
				_hasMoved = false;
				ReleaseMouseCapture();
			}
		}

		private void _layerEditor_MouseDown(object sender, MouseButtonEventArgs e) {
			_enter(e.GetPosition(this));
		}

		private void _layerEditor_MouseLeave(object sender, MouseEventArgs e) {
			_mouseLeave();
		}

		private void _layerEditor_MouseMove(object sender, MouseEventArgs e) {
			_move(e.GetPosition(this));
		}

		private void _layerEditor_MouseUp(object sender, MouseButtonEventArgs e) {
			try {
				int dropIndex = _getIndexDrop(e.GetPosition(this));

				if (dropIndex > -1) {
					var selection = GetSelection();

					if (_actEditor.Act == null) return;

					int start = _layerMouseDown;
					int range = 1;
					var frame = _actEditor.Act[SelectedAction, SelectedFrame];

					if (start >= frame.NumberOfLayers) return;

					if (_actEditor.SelectionEngine.IsSelected(start)) {
						for (int i = start - 1; i >= 0; i--) {
							if (_actEditor.SelectionEngine.IsSelected(i))
								start--;
							else
								break;
						}

						for (int i = start + 1; i < frame.NumberOfLayers; i++) {
							if (_actEditor.SelectionEngine.IsSelected(i))
								range++;
							else
								break;
						}
					}

					if (_actEditor.Act.Commands.LayerSwitchRange(_actEditor._frameSelector.SelectedAction, _actEditor._frameSelector.SelectedFrame, start, range, dropIndex)) {
						Update();
						_actEditor._rendererPrimary.Update();
						_actEditor.SelectionEngine.SetSelection(GenerateSelection(selection));
						e.Handled = true;
					}
				}

				if (e.ChangedButton == MouseButton.Right) {
					var layerControl = this.GetObjectAtPoint<VisualLayer>(e.GetPosition(this));
					bool layersSelected = false;

					if (layerControl != null && layerControl.Visibility == Visibility.Visible) {
						_actEditor.SelectionEngine.AddSelection(layerControl.LayerIndex);
						layersSelected = true;
						_actEditor.SelectionEngine.LatestSelected = layerControl.LayerIndex;
					}

					_miDelete.Visibility = layersSelected ? Visibility.Visible : Visibility.Collapsed;

					if (!layersSelected) {
						e.Handled = true;
					}
					else {
						ContextMenu.IsOpen = true;
						e.Handled = true;
					}
				}
			}
			finally {
				DisableInsertLine();
				_hasMoved = false;
				ReleaseMouseCapture();
			}
		}

		private void _enter(Point position) {
			_layerMouseDown = -1;
			var layerControl = this.GetObjectAtPoint<VisualLayer>(position);

			if (layerControl != null) {
				var textBox = this.GetObjectAtPoint<TextBlock>(position);

				if (layerControl.GetBlockFromCol(0) == textBox)
					_layerMouseDown = layerControl.LayerIndex;
				else
					_layerMouseDown = -1;
			}

			_oldPosition = position;
			_previousMouseDown = -1;
		}

		private void _move(Point current, bool overrideMouse = false, IDataObject dragData = null) {
			if (!overrideMouse && (current == _oldPosition || TkVector2.CalculateDistance(current.ToTkVector2(), _oldPosition.ToTkVector2()) <= 5)) return;

			if (Mouse.LeftButton == MouseButtonState.Pressed || overrideMouse) {
				_hasMoved = true;

				if (_layerMouseDown > -1 || overrideMouse) {
					int dropIndex = _getIndexDrop(current, overrideMouse);

					if (dropIndex > -1) {
						if (!IsMouseCaptured)
							CaptureMouse();

						SetInsertLine(dropIndex);

						// Update preview
						if (dragData != null) {
							object imageIndexObj = dragData.GetData("ImageIndex");

							if (imageIndexObj != null) {
								int imageIndex = (int)imageIndexObj;

								var frame = _actEditor.Act[_actEditor.SelectedAction, _actEditor.SelectedFrame];
								var layer = frame.Layers.Where(p => p.Preview).FirstOrDefault();

								if (layer != null) {
									int index = frame.Layers.IndexOf(layer);

									if (index != dropIndex) {
										_clearPreviewDrag(frame);
										layer = null;
									}
								}

								if (layer == null) {
									// Check if last index
									GrfImage grfImage = _actEditor.Act.Sprite.Images[imageIndex];
									layer = new Layer((grfImage.GrfImageType == GrfImageType.Indexed8) ? imageIndex : (imageIndex - _actEditor.Act.Sprite.NumberOfIndexed8Images), grfImage);
									layer.Preview = true;
									frame.Layers.Insert(dropIndex, layer);
									_actEditor._rendererPrimary.Update();
								}
							}
						}
					}
				}
			}
			else {
				var layer = _getControlUnderMouse();
				_visualEditor.PreviewSelect(layer);
			}
		}

		private void _clearPreviewDrag(Frame frame) {
			for (int i = 0; i < frame.Layers.Count; i++) {
				if (frame[i].Preview) {
					frame.Layers.RemoveAt(i);
					i--;
				}
			}
		}

		private void _mouseLeave(bool hideBar = false) {
			foreach (var visualLayer in _visualEditor.VisualLayers) {
				visualLayer.IsPreviewSelected = false;
			}

			if (hideBar) {
				DisableInsertLine();
				_hasMoved = false;
			}
		}

		public void DisableInsertLine() {
			_lineMoveLayer.Visibility = Visibility.Hidden;
		}

		public void SetInsertLine(int layerIndex) {
			if (_lineMoveLayer.Visibility == Visibility.Visible && layerIndex == _lastInsertLineIndex)
				return;

			var visualLayer = _visualEditor.GetVisualLayer(layerIndex);
			Point position;

			if (visualLayer != null && visualLayer.Visibility == Visibility.Visible) {
				// Current VisualLayer under the mouse
				position = new Point(visualLayer.Margin.Left, visualLayer.Margin.Top);
			}
			else {
				// Past the list of VisualLayer
				visualLayer = _visualEditor.GetVisualLayer(_actEditor.Act[SelectedAction, SelectedFrame].Layers.Count(p => !p.Preview) - 1);

				// There are no Layers in this Frame
				if (visualLayer == null) {
					position = new Point(0, 0);
				}
				else {
					position = new Point(visualLayer.Margin.Left, visualLayer.Margin.Top + visualLayer.ActualHeight);
				}
			}

			position.Y -= _sv.VerticalOffset;

			// Check if the position is within the LayerEditor's viewport
			if (position.Y > this.ActualHeight ||
				position.Y < 0) {
				DisableInsertLine();
				return;
			}

			// Adjust position with the header
			position.Y += _sfch.ActualHeight;

			// Adjust the line to center it
			position.Y -= 2;

			_lineMoveLayer.Stroke = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteSelectionBorder.Get().ToColor());
			_lineMoveLayer.Width = _gridBackground.ActualWidth;
			_lineMoveLayer.Margin = new Thickness(0, position.Y, 0, 0);

			if (_lineMoveLayer.Visibility != Visibility.Visible)
				_lineMoveLayer.Visibility = Visibility.Visible;

			_lastInsertLineIndex = layerIndex;
		}

		private VisualLayer _getControlUnderMouse() {
			Point point = Mouse.GetPosition(_gridBackground);
			
			if (point.X >= _gridBackground.ActualWidth)
				return null;

			return _visualEditor.GetVisualLayer((int)(point.Y / _visualEditor.PreviewElementHeight));
		}

		public void AutoScrollUp() {
			_scrollDirection = -1;
			if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
		}

		public void AutoScrollDown() {
			_scrollDirection = 1;
			if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
		}

		public void StopAutoScroll() {
			_scrollDirection = 0;
			_autoScrollTimer.Stop();
		}

		private int _getIndexDrop(Point point, bool overrideMouse = false) {
			if (_actEditor.Act == null)
				return -1;

			var screenPosition = Control.MousePosition;
			var position = _sv.PointFromScreen(new Point(screenPosition.X, screenPosition.Y));

			var layerControl = _sv.GetObjectAtPoint<VisualLayer>(position);
			var layerHeader = this.GetObjectAtPoint<LayerControlHeader>(point);
			var line = this.GetObjectAtPoint<Line>(point);

			int layerCount = _actEditor.Act[_actEditor.SelectedAction, _actEditor.SelectedFrame].Layers.Count(p => !p.Preview);

			if (_hasMoved) {
				if (layerHeader != null) {
					AutoScrollUp();
				}
				else if (_isUnderViewport()) {
					AutoScrollDown();
				}
				else {
					StopAutoScroll();
				}
			}

			// Handle actual viewport
			if (layerControl != null) {
				_previousMouseDown = layerControl.LayerIndex;

				if (_previousMouseDown > layerCount)
					_previousMouseDown = layerCount;

				return _previousMouseDown;
			}

			// Handle above viewport case
			if (layerHeader != null) {
				// Return first visual child
				_previousMouseDown = _visualEditor.VisualLayers.OrderBy(p => p.LayerIndex).First().LayerIndex;
				return _previousMouseDown;
			}

			if (_isUnderViewport()) {
				// Cannot happen?
				return -1;
			}

			return -1;
		}

		public HashSet<int> GenerateSelection(Layer[] selection) {
			HashSet<int> newSelection = new HashSet<int>();

			if (_actEditor.Act == null) return newSelection;

			Frame frame = _actEditor.Act[_actEditor._frameSelector.SelectedAction, _actEditor._frameSelector.SelectedFrame];

			for (int i = 0; i < selection.Length; i++) {
				int index = frame.Layers.IndexOf(selection[i]);

				if (index > -1) {
					newSelection.Add(index);
				}
			}

			return newSelection;
		}

		public Layer[] GetSelection() {
			return _actEditor.SelectionEngine.SelectedLayers;
		}

		public VisualLayer GetVisual(int layerIndex) {
			return _visualEditor.GetVisualLayer(layerIndex);
		}

		public void Update() {
			if (DoNotRemove && !ActEditorConfiguration.ActEditorRefreshLayerEditor)
				return;

			_visualEditor.InvalidateVisual();
		}

		public void Delete() {
			if (_actEditor.Act == null) return;

			try {
				_actEditor.Act.Commands.Begin();

				var items = _actEditor.SelectionEngine.SelectedItems.OrderByDescending(p => p).ToList();

				for (int i = 0; i < items.Count; i++) {
					_actEditor.Act.Commands.LayerDelete(_actEditor.SelectedAction, _actEditor.SelectedFrame, items[i]);
				}
			}
			catch (Exception err) {
				_actEditor.Act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_actEditor.Act.Commands.End();
			}

			_actEditor.SelectionEngine.DeselectAll();
			_actEditor.Act.InvalidateVisual();
		}

		private void _miDelete_Click(object sender, RoutedEventArgs e) {
			Delete();
		}

		public void MirrorFromOffset(FlipDirection direction) {
			if (_actEditor.Act == null) return;

			try {
				bool selected = _actEditor.SelectionEngine.SelectedItems.Count > 0;

				_actEditor.Act.Commands.Begin();
				
				foreach (var i in _actEditor.SelectionEngine.CurrentlySelected) {
					_actEditor.Act.Commands.MirrorFromOffset(_actEditor.SelectedAction, _actEditor.SelectedFrame, i, 0, direction);
				}

				if (!selected)
					_actEditor.Act.Commands.MirrorFromOffset(_actEditor.SelectedAction, _actEditor.SelectedFrame, 0, direction);
			}
			catch (Exception err) {
				_actEditor.Act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_actEditor.Act.Commands.End();
			}

			_actEditor.Act.InvalidateVisual();
		}

		private void _miFront_Click(object sender, RoutedEventArgs e) {
			BringToFront();
		}

		private void _miActionFront_Click(object sender, RoutedEventArgs e) {
			var alm = new ActionLayerMove(ActionLayerMove.MoveDirection.Down, _actEditor);
			if (alm.CanExecute(_actEditor.Act, SelectedAction, SelectedFrame, _actEditor.SelectionEngine.SelectedItems.ToArray())) {
				alm.Execute(_actEditor.Act, SelectedAction, SelectedFrame, _actEditor.SelectionEngine.SelectedItems.ToArray());
			}
		}

		private void _miActionBack_Click(object sender, RoutedEventArgs e) {
			var alm = new ActionLayerMove(ActionLayerMove.MoveDirection.Up, _actEditor);
			if (alm.CanExecute(_actEditor.Act, SelectedAction, SelectedFrame, _actEditor.SelectionEngine.SelectedItems.ToArray())) {
				alm.Execute(_actEditor.Act, SelectedAction, SelectedFrame, _actEditor.SelectionEngine.SelectedItems.ToArray());
			}
		}

		public void BringToFront() {
			Act act = _actEditor.Act;
			if (act == null) return;

			_bringTo(-1);
		}

		private void _bringTo(int index) {
			Layer[] layers = _actEditor.SelectionEngine.SelectedLayers;
			Act act = _actEditor.Act;

			if (layers.Length == 0) return;
			if (act == null) return;
			if (act[SelectedAction, SelectedFrame].NumberOfLayers <= 1) return;

			int count = act[SelectedAction, SelectedFrame].NumberOfLayers - layers.Length;

			try {
				act.Commands.BeginNoDelay();
				List<int> selected = new List<int>(_actEditor.SelectionEngine.CurrentlySelected).OrderByDescending(p => p).ToList();

				foreach (int select in selected) {
					act.Commands.LayerDelete(SelectedAction, SelectedFrame, select);
				}

				if (index < 0) {
					act.Commands.LayerAdd(SelectedAction, SelectedFrame, layers);
				}
				else {
					act.Commands.LayerAdd(SelectedAction, SelectedFrame, layers, index);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();

				if (index < 0) {
					_actEditor.SelectionEngine.SetSelection(count, layers.Length);
				}
				else {
					_actEditor.SelectionEngine.SetSelection(0, layers.Length);
				}
			}
		}

		public void BringToBack() {
			Act act = _actEditor.Act;
			if (act == null) return;

			_bringTo(0);
		}

		private void _miBack_Click(object sender, RoutedEventArgs e) {
			BringToBack();
		}

		private void _miCut_Click(object sender, RoutedEventArgs e) {
			_actEditor.Cut();
		}

		private void _miCopy_Click(object sender, RoutedEventArgs e) {
			_actEditor.Copy();
		}

		private void _miInvert_Click(object sender, RoutedEventArgs e) {
			_actEditor.SelectionEngine.SelectReverse();
		}

		private void _miSelect_Click(object sender, RoutedEventArgs e) {
			var main = _actEditor.SelectionEngine.Main;

			if (main != null) {
				int latestSelected = _actEditor.SelectionEngine.LatestSelected;

				if (latestSelected > -1 && latestSelected < main.Components.Count) {
					Layer layer = ((LayerDraw) main.Components[latestSelected]).Layer;

					if (_actEditor.Act.Sprite.GetImage(layer) != null) {
						_actEditor._spriteSelector.Select(layer.GetAbsoluteSpriteId(_actEditor.Act.Sprite));
					}
				}
			}
		}
	}
}