using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.Scripts;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using GRF.Threading;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Utilities.Extension;
using Control = System.Windows.Forms.Control;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for LayerEditor.xaml
	/// </summary>
	public partial class LayerEditor : UserControl {
		private readonly DispatcherTimer _timer;
		private readonly UpdateThread _updateThread = new UpdateThread();

		private readonly Stopwatch _watch = new Stopwatch();
		private TabAct _actEditor;
		private bool _hasMoved;
		private int _layerMouseDown = -1;
		private Point _oldPosition;
		private int _previousMouseDown = -1;
		private LayerControlProvider _provider;
		private bool _isLoaded;
		private readonly LayerControlLoadThread _layerControlThread;

		public LayerEditor() {
			InitializeComponent();

			_displayGrid.ColumnDefinitions[1] = new ColumnDefinition {Width = new GridLength(SystemParameters.VerticalScrollBarWidth)};
			_timer = new DispatcherTimer();
			_timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
			_timer.Tick += new EventHandler(_timer_Tick);

			_updateThread.Start(this);
			_layerControlThread = new LayerControlLoadThread(this);
			_layerControlThread.Start();

			Dispatcher.ShutdownStarted += delegate {
				_layerControlThread.Stop();
			};

			PreviewKeyDown += new KeyEventHandler(_layerEditor_PreviewKeyDown);

			double previousOffsetY = 0;

			_sv.ScrollChanged += delegate {
				if (previousOffsetY == _sv.VerticalOffset)
					return;

				previousOffsetY = _sv.VerticalOffset;

				foreach (var layerControl in _sp.Children.OfType<LayerControl>()) {
					if (layerControl.Dirty) {
						layerControl.Update();
					}
				}
			};
		}

		public LayerControlProvider Provider {
			get { return _provider; }
		}

		public bool DoNotRemove { get; set; }
		public bool IgnoreUpdate { get; set; }
		private string _hasBeenDrawn { get; set; }

		public int SelectedAction {
			get { return _actEditor._frameSelector.SelectedAction; }
		}

		public int SelectedFrame {
			get { return _actEditor._frameSelector.SelectedFrame; }
		}

		public Func<bool> ImageExists {
			get { return _imageExists; }
		}

		private void _layerEditor_PreviewKeyDown(object sender, KeyEventArgs e) {
			if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
			}
		}

		private void _timer_Tick(object sender, EventArgs e) {
			if (_isUnderViewport()) {
				_sv.ScrollToVerticalOffset(_sv.VerticalOffset + _sfch.ActualHeight);
			}
			else if (_isAboveViewport()) {
				_sv.ScrollToVerticalOffset(_sv.VerticalOffset - _sfch.ActualHeight);
			}
			else {
				_timer.Stop();
			}
		}

		private bool _isUnderViewport() {
			var position2 = Control.MousePosition;
			var position = PointFromScreen(new Point(position2.X, position2.Y));
			return position.Y > ActualHeight && position.Y < ActualHeight + 50;
		}

		private bool _isAboveViewport() {
			var position2 = Control.MousePosition;
			var position = PointFromScreen(new Point(position2.X, position2.Y));
			return this.GetObjectAtPoint<LayerControlHeader>(position) != null;
		}

		public void Init(TabAct actEditor) {
			_actEditor = actEditor;

			_actEditor._frameSelector.FrameChanged += (s, e) => Update();
			_actEditor._frameSelector.ActionChanged += (s, e) => Update();

			_actEditor.ActLoaded += delegate {
				if (actEditor.Act == null) return;

				_hasBeenDrawn = null;
				actEditor.Act.Commands.CommandRedo += (s, e) => _fieldsUpdate();
				actEditor.Act.Commands.CommandUndo += (s, e) => _fieldsUpdate();
			};

			_actEditor._rendererPrimary.Selected += new DrawingComponent.DrawingComponentDelegate(_rendererPrimary_Selected);
			_actEditor._frameSelector.AnimationPlaying += new ActIndexSelector.FrameIndexChangedDelegate(_frameSelector_AnimationPlaying);

			PreviewMouseDown += new MouseButtonEventHandler(_layerEditor_MouseDown);
			PreviewMouseUp += new MouseButtonEventHandler(_layerEditor_MouseUp);
			MouseMove += new MouseEventHandler(_layerEditor_MouseMove);
			MouseLeave += new MouseEventHandler(_layerEditor_MouseLeave);
			DragOver += new DragEventHandler(_layerEditor_DragOver);
			DragEnter += new DragEventHandler(_layerEditor_DragEnter);
			DragLeave += new DragEventHandler(_layerEditor_DragLeave);
			Drop += new DragEventHandler(_layerEditor_Drop);
		}

		public void AsyncUpdateLayerControl(int layerIndex) {
			_layerControlThread.Update(layerIndex);
		}

		public void AsyncUpdateLayerControl(int[] layerIndexes) {
			_layerControlThread.Update(layerIndexes);
		}

		private void _frameSelector_AnimationPlaying(object sender, int actionindex) {
			if (actionindex == 0) {
				_watch.Stop();
				DoNotRemove = false;
				IgnoreUpdate = false;

				int action = _actEditor._frameSelector.SelectedAction;
				int frame = _actEditor._frameSelector.SelectedFrame;

				if (_hasBeenDrawn == null || _hasBeenDrawn != action + "," + frame)
					this.Dispatch(p => p.Update());

				this.Dispatch(p => p.IsEnabled = true);
			}
			else {
				if (!ActEditorConfiguration.ActEditorRefreshLayerEditor)
					this.Dispatch(p => p.IsEnabled = false);

				DoNotRemove = true;
			}
		}

		private void _layerEditor_DragLeave(object sender, DragEventArgs e) {
			_mouseLeave(true);
		}

		private void _layerEditor_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null)
				_enter(e.GetPosition(this));
			else
				e.Effects = DragDropEffects.None;
		}

		private void _layerEditor_DragOver(object sender, DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null)
				_move(e.GetPosition(this), true);
			else
				e.Effects = DragDropEffects.None;
		}

		private void _layerEditor_Drop(object sender, DragEventArgs e) {
			try {
				object imageIndexObj = e.Data.GetData("ImageIndex");

				if (imageIndexObj == null) return;

				int imageIndex = (int) imageIndexObj;
				int dropIndex = _getIndexDrop(e.GetPosition(this), true);

				if (dropIndex <= -1)
					dropIndex = _getIndexDrop(e.GetPosition(this), true);

				if (dropIndex > -1) {
					List<Utilities.Extension.Tuple<Layer, bool>> selection = GetSelection();

					if (_actEditor.Act == null) return;

					_actEditor.Act.Commands.LayerAdd(_actEditor.SelectedAction, _actEditor.SelectedFrame, dropIndex, imageIndex);

					Update();
					_actEditor._rendererPrimary.Update();
					_actEditor.SelectionEngine.SetSelection(GenerateSelection(selection));
					UpdateSelection();
					e.Handled = true;
				}
			}
			finally {
				_lineMoveLayer.Visibility = Visibility.Hidden;
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
					List<Utilities.Extension.Tuple<Layer, bool>> selection = GetSelection();

					if (_actEditor.Act == null) return;

					int start = _layerMouseDown;
					int range = 1;

					var startFrame = _provider.Get(start);

					if (start >= _sp.Children.Count) return;

					if (startFrame.IsSelected) {
						for (int i = start - 1; i >= 0; i--) {
							if (_provider.Get(i).IsSelected)
								start--;
							else
								break;
						}

						for (int i = start + 1; i < _sp.Children.Count; i++) {
							if (_provider.Get(i).IsSelected)
								range++;
							else
								break;
						}
					}

					if (_actEditor.Act.Commands.LayerSwitchRange(_actEditor._frameSelector.SelectedAction, _actEditor._frameSelector.SelectedFrame, start, range, dropIndex)) {
						Update();
						_actEditor._rendererPrimary.Update();
						_actEditor.SelectionEngine.SetSelection(GenerateSelection(selection));
						UpdateSelection();
						e.Handled = true;
					}
				}

				if (e.ChangedButton == MouseButton.Right) {
					var layerControl = this.GetObjectAtPoint<LayerControl>(e.GetPosition(this));
					bool layersSelected = false;

					if (layerControl != null) {
						int index = _sp.Children.IndexOf(layerControl);

						if (index > -1) {
							_actEditor.SelectionEngine.Select(index);
							layersSelected = true;
							_actEditor.SelectionEngine.LatestSelected = index;
						}
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
				_lineMoveLayer.Visibility = Visibility.Hidden;
				_hasMoved = false;

				ReleaseMouseCapture();
			}
		}

		private void _enter(Point position) {
			_layerMouseDown = -1;
			var layerControl = this.GetObjectAtPoint<LayerControl>(position);

			if (layerControl != null) {
				var textBox = this.GetObjectAtPoint<TextBox>(position);

				if (textBox == null)
					_layerMouseDown = _sp.Children.IndexOf(layerControl);
				else
					_layerMouseDown = -1;
			}

			_oldPosition = position;
			_previousMouseDown = -1;
		}

		private void _move(Point current, bool overrideMouse = false) {
			if (!overrideMouse && (current == _oldPosition || TkVector2.CalculateDistance(current.ToTkVector2(), _oldPosition.ToTkVector2()) <= 5)) return;

			if (Mouse.LeftButton == MouseButtonState.Pressed || overrideMouse) {
				_hasMoved = true;

				if (_layerMouseDown > -1 || overrideMouse) {
					if (!overrideMouse && _provider.Get(_layerMouseDown) != null) _provider.Get(_layerMouseDown).IsPreviewSelected = true;

					int dropIndex = _getIndexDrop(current, overrideMouse);

					if (dropIndex > -1) {
						if (!IsMouseCaptured)
							CaptureMouse();

						_lineMoveLayer.Stroke = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteSelectionBorder.ToColor());
						_lineMoveLayer.Visibility = Visibility.Visible;

						double offsetY = _sfch.ActualHeight;

						for (int i = 0; i < dropIndex; i++) {
							offsetY += LayerControl.ActualHeightBuffered;
						}

						offsetY -= _sv.VerticalOffset;

						if (offsetY >= ActualHeight - 1) {
							offsetY = ActualHeight - 1;
						}

						if (_isAboveViewport()) {
							if (_sv.VerticalOffset > 0)
								_lineMoveLayer.Visibility = Visibility.Hidden;
							else
								offsetY = _sfch.ActualHeight;
						}

						if (_isUnderViewport()) {
							if (_sv.VerticalOffset < _sv.ScrollableHeight)
								_lineMoveLayer.Visibility = Visibility.Hidden;
							else {
								if (_sv.ScrollableHeight > 0)
									offsetY = ActualHeight - 1;
							}
						}

						if (offsetY < _sfch.ActualHeight) {
							_lineMoveLayer.Visibility = Visibility.Hidden;
						}

						_lineMoveLayer.Margin = new Thickness(0, offsetY - 2, SystemParameters.VerticalScrollBarWidth, 0);
					}
				}
			}
			else {
				var layer = _getControlUnderMouse();

				for (int i = 0; i < _sp.Children.Count; i++) {
					var tmp = _provider.Get(i);

					if (tmp != layer) {
						tmp.IsPreviewSelected = false;
					}
				}

				if (layer != null)
					layer.IsPreviewSelected = true;
			}
		}

		private void _mouseLeave(bool hideBar = false) {
			for (int i = 0; i < _sp.Children.Count; i++) {
				var layer = _provider.Get(i);
				layer.IsPreviewSelected = false;
			}

			if (hideBar) {
				_lineMoveLayer.Visibility = Visibility.Hidden;
				_hasMoved = false;
			}
		}

		private LayerControl _getControlUnderMouse() {
			Point point = Mouse.GetPosition(this);
			return this.GetObjectAtPoint<LayerControl>(point);
		}

		private int _getIndexDrop(Point point, bool overrideMouse = false) {
			var layerControl = this.GetObjectAtPoint<LayerControl>(point);
			var layerHeader = this.GetObjectAtPoint<LayerControlHeader>(point);
			var line = this.GetObjectAtPoint<Line>(point);

			if (_isAboveViewport() || layerHeader != null) {
				if (_sv.VerticalOffset != 0) {
					if (!_timer.IsEnabled) {
						_timer_Tick(null, null);
						_timer.Start();
						return _previousMouseDown;
					}
				}
			}

			if (_isUnderViewport()) {
				if (_sv.VerticalOffset != _sv.ScrollableHeight) {
					if (!_timer.IsEnabled) {
						_timer_Tick(null, null);
						_timer.Start();
						return _previousMouseDown;
					}
				}
			}

			if (_hasMoved && (_layerMouseDown > -1 || overrideMouse)) {
				Point relativeToViewport = new Point(point.X, point.Y - _sfch.ActualHeight);

				bool isOutside = relativeToViewport.X < 0 || relativeToViewport.Y < 0 || relativeToViewport.X > _sv.ViewportWidth || relativeToViewport.Y > _sv.ViewportHeight;

				int currentIndex = -1;

				if (_isAboveViewport() && _sv.VerticalOffset == 0) {
					currentIndex = 0;
					_previousMouseDown = currentIndex;
					return currentIndex;
				}

				if (layerHeader != null) {
					return _previousMouseDown;
				}

				if (layerControl != null) {
					currentIndex = _sp.Children.IndexOf(layerControl);
				}

				if (_isUnderViewport() && _sv.VerticalOffset == _sv.ScrollableHeight) {
					currentIndex = _sp.Children.Count;
					_previousMouseDown = currentIndex;
					return currentIndex;
				}

				if (isOutside || (currentIndex < 0 && line != null)) {
					if (_previousMouseDown < 0) {
						if (line != null) {
							// Invalid!
							// Moves the mouse 6 pixels down and retrieve the frame
							_previousMouseDown = _getIndexDrop(new Point(point.X, point.Y + 6), overrideMouse);
						}
					}

					return _previousMouseDown;
				}

				if (currentIndex < 0)
					currentIndex = _sp.Children.Count;

				_previousMouseDown = currentIndex;
				return currentIndex;
			}

			return -1;
		}

		public HashSet<int> GenerateSelection(List<Utilities.Extension.Tuple<Layer, bool>> selection) {
			HashSet<int> newSelection = new HashSet<int>();

			if (_actEditor.Act == null) return newSelection;

			Frame frame = _actEditor.Act[_actEditor._frameSelector.SelectedAction, _actEditor._frameSelector.SelectedFrame];

			for (int i = 0; i < selection.Count; i++) {
				if (selection[i].Item2) {
					int index = frame.Layers.IndexOf(selection[i].Item1);

					if (index > -1) {
						newSelection.Add(index);
					}
				}
			}

			return newSelection;
		}

		public List<Utilities.Extension.Tuple<Layer, bool>> GetSelection() {
			List<Utilities.Extension.Tuple<Layer, bool>> selection = new List<Utilities.Extension.Tuple<Layer, bool>>();

			if (_actEditor.Act == null) return selection;

			for (int i = 0; i < _sp.Children.Count; i++) {
				var layer = _actEditor.Act[_actEditor._frameSelector.SelectedAction, _actEditor._frameSelector.SelectedFrame, i];
				selection.Add(new Utilities.Extension.Tuple<Layer, bool>(layer, ((LayerControl)_sp.Children[i]).IsSelected));
			}

			return selection;
		}

		public LayerControl Get(int layerIndex) {
			return _sp.Children[layerIndex] as LayerControl;
		}

		public void Update() {
			if (DoNotRemove) {
				if (!ActEditorConfiguration.ActEditorRefreshLayerEditor) {
					return;
				}

				int[] layerIndexes = new int[_actEditor.Act[SelectedAction, SelectedFrame].Layers.Count];

				for (int i = 0; i < layerIndexes.Length; i++)
					layerIndexes[i] = i;
				
				//AsyncUpdateLayerControl(layerIndexes);
				_updateThread.Add(new UpdateInfo(SelectedAction, SelectedFrame));
			}
			else {
				if (_provider == null) {
					_actEditor.LayerEditor._sv.Loaded += delegate {
						if (_isLoaded)
							return;

						_provider = new LayerControlProvider(_actEditor);
						_isLoaded = true;
						ThreadUpdate(SelectedFrame);
					};
				}
				else {
					ThreadUpdate(SelectedFrame);
				}
			}
		}

		internal void ThreadUpdate(int selectedFrame) {
			Act act = _actEditor.Act;

			if (act == null) return;

			if (DoNotRemove) {
				_hasBeenDrawn = null;
				_specialUpdate(selectedFrame);
				return;
			}

			int numberOfFrames = _actEditor.Frame.NumberOfLayers;
			int action = _actEditor._frameSelector.SelectedAction;
			int frame = _actEditor._frameSelector.SelectedFrame;

			_hasBeenDrawn = action + "," + frame;

			if (numberOfFrames < _sp.Children.Count) {
				_sp.Children.RemoveRange(numberOfFrames, _sp.Children.Count - numberOfFrames);

				if (selectedFrame != SelectedFrame) return;
			}

			for (int i = 0; i < _sp.Children.Count; i++) {
				_provider.Get(i).Set(act, action, frame, i, false);

				if (selectedFrame != SelectedFrame) return;
			}

			for (int i = _sp.Children.Count; i < numberOfFrames; i++) {
				var layer = _provider.Get(i);
				layer.Set(act, action, frame, i, false);
				layer.IsSelected = false;
				_sp.Children.Add(layer);

				if (selectedFrame != SelectedFrame) return;
			}
		}

		private void _specialUpdate(int selectedFrame) {
			if (!DoNotRemove) return;

			Act act = _actEditor.Act;

			if (act == null) return;
			if (selectedFrame != this.Dispatch(() => SelectedFrame)) return;

			int numberOfFrames = _actEditor.Frame.NumberOfLayers;
			int action = _actEditor._frameSelector.SelectedAction;
			int frame = _actEditor._frameSelector.SelectedFrame;
			int max = numberOfFrames;

			double currentHeight = 0;
			double offsetYBegin = _sv.VerticalOffset;
			double offsetYEnd = offsetYBegin + _sv.ViewportHeight;

			for (int i = 0, count = max; i < count; i++) {
				//if (currentHeight > offsetYEnd) {
				//	return;
				//}

				LayerControl ctr = _provider.Get(i);

				_sp.Dispatch(delegate {
					if (i >= _sp.Children.Count) {
						ctr.IsSelected = false;
						_sp.Children.Add(ctr);
					}
				});

				//if (currentHeight < offsetYBegin) {
				//	currentHeight += LayerControl.ActualHeightBuffered;
				//
				//	if (currentHeight < offsetYBegin)
				//		continue;
				//}
				//else {
				//	currentHeight += LayerControl.ActualHeightBuffered;
				//}

				if (!DoNotRemove) return;

				ctr.Dispatch(() => ctr.Set(act, action, frame, i, true));

				if (selectedFrame != this.Dispatch(() => SelectedFrame)) return;
			}

			int childrenCount = this.Dispatch(() => _sp.Children.Count);

			if (childrenCount > 0) {
				currentHeight = _provider.Get(0).ActualHeight * max;

				for (int i = max; i < childrenCount; i++) {
					if (currentHeight > offsetYEnd)
						return;

					LayerControl ctr = _provider.Get(i);

					ctr.Dispatch(ctr.SetNull);
					currentHeight += LayerControl.ActualHeightBuffered;

					if (selectedFrame != this.Dispatch(() => SelectedFrame)) return;
					if (!DoNotRemove) return;
				}
			}
		}

		public void UpdateSelection() {
			for (int i = 0; i < _sp.Children.Count; i++) {
				_provider.Get(i).IsSelected = _actEditor.SelectionEngine.SelectedItems.Contains(i);
			}
		}

		private void _rendererPrimary_Selected(object sender, int index, bool selected) {
			if (index < 0 || index >= _sp.Children.Count) return;

			((LayerControl) _sp.Children[index]).IsSelected = selected;
		}

		private void _fieldsUpdate() {
			Act act = _actEditor.Act;

			if (act == null) return;

			int action = _actEditor._frameSelector.SelectedAction;
			int frame = _actEditor._frameSelector.SelectedFrame;

			if (action >= act.NumberOfActions || frame >= act[action].NumberOfFrames) return;

			if (act[action, frame].NumberOfLayers != _sp.Children.Count) {
				Update();
			}

			for (int i = 0; i < _sp.Children.Count; i++) {
				((LayerControl) _sp.Children[i]).Set(act, action, frame, i, false);
			}

			//UpdateSelection();
		}

		public void Delete() {
			if (_actEditor.Act == null) return;

			try {
				_actEditor.Act.Commands.Begin();

				for (int i = _sp.Children.Count - 1; i >= 0; i--) {
					if (_provider.Get(i).IsSelected) {
						_actEditor.Act.Commands.LayerDelete(_actEditor.SelectedAction, _actEditor.SelectedFrame, i);
					}
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
				bool selected = false;

				_actEditor.Act.Commands.Begin();
				
				for (int i = _sp.Children.Count - 1; i >= 0; i--) {
					if (_provider.Get(i).IsSelected) {
						selected = true;
						_actEditor.Act.Commands.MirrorFromOffset(_actEditor.SelectedAction, _actEditor.SelectedFrame, i, 0, direction);
					}
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

		public void Reset() {
			_sp.Children.Clear();
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

		private void _miBack_Click(object sender, RoutedEventArgs e) {
			Act act = _actEditor.Act;
			if (act == null) return;

			_bringTo(0);
		}

		public void BringToBack() {
			Act act = _actEditor.Act;
			if (act == null) return;

			_bringTo(0);
		}

		private void _miCut_Click(object sender, RoutedEventArgs e) {
			_actEditor._rendererPrimary.Cut();
		}

		private void _miCopy_Click(object sender, RoutedEventArgs e) {
			_actEditor._rendererPrimary.Copy();
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

		private bool _imageExists() {
			var main = _actEditor.SelectionEngine.Main;

			if (main != null) {
				int latestSelected = _actEditor.SelectionEngine.LatestSelected;

				if (latestSelected > -1 && latestSelected < main.Components.Count) {
					Layer layer = ((LayerDraw) main.Components[latestSelected]).Layer;

					return _actEditor.Act.Sprite.GetImage(layer) != null;
				}
			}

			return false;
		}

		#region Nested type: UpdateInfo

		public class UpdateInfo {
			public int ActionIndex;
			public int FrameIndex;

			public UpdateInfo(int actionIndex, int frameIndex) {
				ActionIndex = actionIndex;
				FrameIndex = frameIndex;
			}
		}

		#endregion

		#region Nested type: UpdateThread

		public class UpdateThread : PausableThread {
			private readonly object _updateLock = new object();
			private readonly Queue<UpdateInfo> _updateQueue = new Queue<UpdateInfo>();
			private LayerEditor _layerEditor;

			public void Start(LayerEditor layerEditor) {
				_layerEditor = layerEditor;
				IsPaused = true;
				GrfThread.StartSTA(_start);
			}

			private void _start() {
				while (true) {
					if (IsPaused) {
						Pause();
					}

					bool anyLeft = true;

					lock (_updateLock) {
						if (_updateQueue.Count == 0)
							anyLeft = false;
					}

					if (!anyLeft) {
						IsPaused = true;
						continue;
					}

					UpdateInfo updateInfo = null;

					lock (_updateLock) {
						if (_updateQueue.Count == 1) {
							updateInfo = _updateQueue.Dequeue();
						}
						else if (_updateQueue.Count > 1) {
							_updateQueue.Dequeue();
						}
					}

					if (updateInfo == null)
						continue;

					if (_layerEditor.DoNotRemove) {
						//_layerEditor.BeginDispatch(() => _layerEditor.ThreadUpdate());
						_layerEditor.ThreadUpdate(updateInfo.FrameIndex);
						//_layerEditor.Dispatch(() => );
					}

					Thread.Sleep(20);
				}
			}

			public void Add(UpdateInfo updateInfo) {
				lock (_updateLock) {
					_updateQueue.Enqueue(updateInfo);
				}

				if (IsPaused)
					IsPaused = false;
			}
		}

		#endregion
	}
}