using ActEditor.ApplicationConfiguration;
using ActEditor.Core.ListEditCommands;
using ActEditor.Tools.PaletteEditorTool;
using ErrorManager;
using GrfToWpfBridge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TokeiLibrary;

namespace ActEditor.Core.WPF.Dialogs {
	public class SelectableListBoxExtension {
		private ListBox _listBox;
		private ScrollViewer _gridEvents;
		private double _dataDisplayWidth;
		private double _dataDisplayHeight;
		private ScrollViewer _svListBox;
		private MouseDownData _mouseDownData = new MouseDownData();
		private DispatcherTimer _autoScrollTimer;
		private double _scrollDirection;
		private Rectangle _rectSelection;
		private Rectangle _rectInsertion;
		private Window _window;
		public Cursor DragAndDropCursor;

		public delegate void DraggedDoneEventHandler(int insertIndex);
		public event DraggedDoneEventHandler ListItemsDropped;
		public event DraggedDoneEventHandler ClipboardItemsDropped;

		public ListBoxMode Mode { get; set; } = ListBoxMode.None;
		public ListBoxDragMode DragMode { get; set; } = ListBoxDragMode.None;
		public ListBoxSelectionExtension SelectionManager { get; set; }

		private Point _mouseAreaStart;
		private int _insertTarget;
		private IEditData[] _clipboardData;

		public enum ListBoxDragMode {
			None,
			ListItems,
			ClipboardItems,
		}

		public enum ListBoxMode {
			None,
			Dragging,
			AreaSelect,
		}

		public class MouseDownData {
			public bool IsSelected { get; set; }
			public bool HasItem { get; set; }
			public IEditData Item { get; set; }
			public ListBoxItem ListBoxItem { get; set; }
			public Point MousePositionListBox { get; set; }
			public Point MousePositionListSv { get; set; }

			public void Clear() {
				IsSelected = false;
				HasItem = false;
				ListBoxItem = null;
				Item = null;
			}

			public void Set(ListBoxItem item, ListBox listBox, ScrollViewer sv) {
				if (item == null) {
					Clear();
					return;
				}

				IsSelected = item.IsSelected;
				HasItem = true;
				ListBoxItem = item;
				Item = ListBoxItem.Content as IEditData;
				MousePositionListBox = Mouse.GetPosition(listBox);
				MousePositionListSv = Mouse.GetPosition(sv);
			}
		}

		public SelectableListBoxExtension(Window window, ListBox listBox, ScrollViewer gridEvents, Rectangle rectSelection, Rectangle rectInsertion, double dataDisplayWidth, double dataDisplayHeight) {
			_listBox = listBox;
			_gridEvents = gridEvents;
			_dataDisplayWidth = dataDisplayWidth;
			_dataDisplayHeight = dataDisplayHeight;
			_rectSelection = rectSelection;
			_rectInsertion = rectInsertion;
			_window = window;
			SelectionManager = new ListBoxSelectionExtension(listBox);

			var brushFill = new SolidColorBrush(ActEditorConfiguration.ActEditorSelectionBorderOverlay.Get().ToColor());
			var brushStroke = new SolidColorBrush(ActEditorConfiguration.ActEditorSelectionBorder.Get().ToColor());

			brushFill.Freeze();
			brushStroke.Freeze();

			_rectSelection.Fill = brushFill;
			_rectSelection.Stroke = brushStroke;

			_listBox.PreviewMouseRightButtonUp += _listBox_PreviewMouseRightButtonUp;
			_listBox.PreviewMouseRightButtonDown += _listBox_PreviewMouseRightButtonDown;
			_listBox.PreviewMouseLeftButtonDown += _listBox_PreviewMouseLeftButtonDown;
			_listBox.PreviewMouseLeftButtonUp += _listBox_PreviewMouseLeftButtonUp;
			_listBox.PreviewMouseMove += (s, e) => OnMouseMove(e);
			_gridEvents.PreviewMouseMove += (s, e) => OnMouseMove(e);
			_gridEvents.PreviewMouseLeftButtonUp += _listBox_PreviewMouseLeftButtonUp;
			_gridEvents.PreviewMouseWheel += _gridEvents_MouseWheel;

			_listBox.Loaded += delegate {
				_svListBox = WpfUtilities.FindChild<ScrollViewer>(_listBox);
			};

			_autoScrollTimer = new DispatcherTimer(DispatcherPriority.Render);
			_autoScrollTimer.Interval = TimeSpan.FromMilliseconds(50);
			_autoScrollTimer.Tick += _autoScrollTimer_Tick;
		}

		private void _autoScrollTimer_Tick(object sender, EventArgs e) {
			if (_scrollDirection < 0)
				_svListBox.LineUp();
			else if (_scrollDirection > 0)
				_svListBox.LineDown();

			OnMouseMove();
		}

		private void _listBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			switch (Mode) {
				case ListBoxMode.Dragging:
					EndDrag();

					switch (DragMode) {
						case ListBoxDragMode.ListItems:
							ListItemsDropped?.Invoke(_insertTarget);
							break;
						case ListBoxDragMode.ClipboardItems:
							ClipboardItemsDropped?.Invoke(_insertTarget);
							break;
					}
					break;
				case ListBoxMode.AreaSelect:
					EndAreaSelect();
					break;
				default:
					if (!IsMouseWithinListBoxData()) {
						SelectionManager.ClearSelection();
					}
					else if (_mouseDownData.HasItem && _mouseDownData.IsSelected) {
						SelectionManager.SetSelected(_mouseDownData.Item);
					}
					break;
			}
		}

		private void _listBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			if (!IsMouseWithinListBoxData()) {
				e.Handled = true;
				return;
			}
		}

		private void _listBox_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
			if (!IsMouseWithinListBoxData()) {
				e.Handled = true;
				return;
			}
		}

		private void _listBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			// Let ButtonUp process it
			if (Mode != ListBoxMode.None) {
				e.Handled = true;
				return;
			}

			var data = GetListBoxItemUnderMouse();

			_mouseDownData.Set(data, _listBox, _svListBox);

			if (_mouseDownData.HasItem) {
				if (_mouseDownData.IsSelected) {
					if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None)
						SelectionManager.SetSelected(_mouseDownData.Item);

					e.Handled = true;
					return;
				}
			}
			else {
				if (!IsMouseWithinListBoxData()) {
					e.Handled = true;
				}

				return;
			}
		}

		public bool MouseReachedMinimumDrag() {
			var mousePosition = Mouse.GetPosition(_listBox);
			double distance = (mousePosition - _mouseDownData.MousePositionListBox).Length;

			return distance > SystemParameters.MinimumHorizontalDragDistance;
		}

		public void OnMouseMove(MouseEventArgs e = null) {
			switch (Mode) {
				case ListBoxMode.Dragging:
					SetInsertionLine();
					break;
				case ListBoxMode.AreaSelect:
					DoAreaSelect(e);
					break;
				case ListBoxMode.None:
					if (Mouse.LeftButton == MouseButtonState.Pressed && !_mouseDownData.HasItem && MouseReachedMinimumDrag()) {
						BeginAreaSelect();
					}
					else if (Mouse.LeftButton == MouseButtonState.Pressed && _mouseDownData.HasItem && _mouseDownData.IsSelected && MouseReachedMinimumDrag()) {
						BeginDrag(ListBoxDragMode.ListItems);
					}
					break;
			}
		}

		public double Clamp(double value, double min, double max) {
			if (value < min)
				return min;
			if (value > max)
				return max;
			return value;
		}

		public void DoAreaSelect(MouseEventArgs e = null) {
			var mousePositionSv = Mouse.GetPosition(_svListBox);
			var currentAreaPoint = new Point(mousePositionSv.X, mousePositionSv.Y + _svListBox.VerticalOffset * _dataDisplayHeight);
			var startAreaPoint = _mouseAreaStart;
			int selectFrom = (int)(currentAreaPoint.Y / _dataDisplayHeight);
			int selectTo = (int)(startAreaPoint.Y / _dataDisplayHeight);

			if (selectFrom > selectTo) {
				var t = selectFrom;
				selectFrom = selectTo;
				selectTo = t;
			}

			selectFrom = Math.Max(0, selectFrom);

			// Clip area points
			currentAreaPoint.Y = Clamp(currentAreaPoint.Y, _svListBox.VerticalOffset * _dataDisplayHeight, _svListBox.VerticalOffset * _dataDisplayHeight + _svListBox.ActualHeight);
			currentAreaPoint.X = Clamp(currentAreaPoint.X, 0, _svListBox.ViewportWidth);
			startAreaPoint.Y = Clamp(startAreaPoint.Y, _svListBox.VerticalOffset * _dataDisplayHeight, _svListBox.ActualHeight + _svListBox.VerticalOffset * _dataDisplayHeight);
			
			var topLeftPoint = new Point(Math.Min(startAreaPoint.X, currentAreaPoint.X), Math.Min(startAreaPoint.Y, currentAreaPoint.Y));
			var bottomRightPoint = new Point(Math.Max(startAreaPoint.X, currentAreaPoint.X), Math.Max(startAreaPoint.Y, currentAreaPoint.Y));

			if (topLeftPoint.X < 0)
				topLeftPoint.X = 0;

			var width = Math.Abs(topLeftPoint.X - bottomRightPoint.X);
			var height = Math.Abs(topLeftPoint.Y - bottomRightPoint.Y);

			_rectSelection.Width = width;
			_rectSelection.Height = height;

			var screenPoint = new Point(topLeftPoint.X + 1, topLeftPoint.Y + 1 - _svListBox.VerticalOffset * _dataDisplayHeight);
			_rectSelection.Margin = new Thickness(screenPoint.X, screenPoint.Y, 0, 0);
			_rectSelection.Visibility = Visibility.Visible;

			if (topLeftPoint.X <= _dataDisplayWidth) {
				var layers = new List<IEditData>();

				for (int i = selectFrom; i <= selectTo && i < _listBox.Items.Count; i++) {
					layers.Add((IEditData)_listBox.Items[i]);
				}

				SelectionManager.SetSelected(layers, useSavedState: true);
			}
			else {
				SelectionManager.SetSelected(new List<IEditData>(), useSavedState: true);
			}

			if (mousePositionSv.Y < 0)
				AutoScrollUp();
			else if (mousePositionSv.Y > _svListBox.ActualHeight)
				AutoScrollDown();
			else
				StopAutoScroll();

			if (e != null)
				e.Handled = true;
		}

		public void BeginAreaSelect() {
			Mode = ListBoxMode.AreaSelect;
			var mousePositionSv = Mouse.GetPosition(_svListBox);
			_mouseAreaStart = new Point(mousePositionSv.X, mousePositionSv.Y + _svListBox.VerticalOffset * _dataDisplayHeight);
			SelectionManager.ClearSelection();
			SelectionManager.SaveState();
			_gridEvents.IsHitTestVisible = true;
			_gridEvents.CaptureMouse();
		}

		public void EndAreaSelect() {
			_gridEvents.ReleaseMouseCapture();
			_rectSelection.Visibility = Visibility.Hidden;
			Mode = ListBoxMode.None;
			_gridEvents.IsHitTestVisible = false;
			StopAutoScroll();
		}

		public void BeginDrag(ListBoxDragMode mode) {
			DragMode = mode;
			Mode = ListBoxMode.Dragging;

			if (DragAndDropCursor == null)
				DragAndDropCursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_dad.png"), Width = 21, Height = 31 }, new Point() { X = 0, Y = 0 });

			_window.Cursor = DragAndDropCursor;

			SetInsertionLine();
			_gridEvents.CaptureMouse();
		}

		public void EndDrag() {
			_window.Cursor = null;
			Mode = ListBoxMode.None;
			_gridEvents.ReleaseMouseCapture();
			_gridEvents.IsHitTestVisible = false;
			_rectInsertion.Visibility = Visibility.Hidden;
			StopAutoScroll();
		}

		public void SetInsertionLine() {
			_rectInsertion.Visibility = Visibility.Visible;

			var mousePosition = Mouse.GetPosition(_listBox);
			Point target = mousePosition;
			target.X = 0;

			var virtualInsert = (int)(mousePosition.Y / _dataDisplayHeight);

			if (virtualInsert < 0) {
				virtualInsert = 0;
				AutoScrollUp();
			}
			else if (virtualInsert + _svListBox.VerticalOffset >= _listBox.Items.Count) {
				virtualInsert = (int)(_listBox.Items.Count - _svListBox.VerticalOffset);
			}
			else if (target.Y > _listBox.ActualHeight) {
				AutoScrollDown();
			}
			else {
				StopAutoScroll();
			}

			target.Y = virtualInsert * _dataDisplayHeight;
			_rectInsertion.Margin = new Thickness(0, target.Y - 2, _svListBox.ComputedVerticalScrollBarVisibility == Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0, 0);
			_insertTarget = (int)(virtualInsert + _svListBox.VerticalOffset);
		}

		private void _gridEvents_MouseWheel(object sender, MouseWheelEventArgs e) {
			if (Mode == ListBoxMode.AreaSelect || Mode == ListBoxMode.Dragging) {
				if (e.Delta > 0)
					_svListBox.LineUp();
				else if (e.Delta < 0)
					_svListBox.LineDown();

				OnMouseMove();
			}

			e.Handled = true;
		}

		public bool IsMouseWithinSvViewport() {
			var mousePosition = Mouse.GetPosition(_svListBox);

			if (mousePosition.X >= 0 && mousePosition.X < _svListBox.ViewportWidth &&
				mousePosition.Y >= 0 && mousePosition.Y < _svListBox.ActualHeight)
				return true;

			return false;
		}

		public bool IsMouseWithinListBoxData() {
			return GetListBoxItemUnderMouse() == null ? false : true;
		}

		public ListBoxItem GetListBoxItemUnderMouse() {
			var mousePosition = Mouse.GetPosition(_listBox);

			if (mousePosition.X < 0 || mousePosition.X > _dataDisplayWidth)
				return null;

			return _listBox.GetObjectAtPoint<ListBoxItem>(mousePosition);
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

		public void UpdateSelectionFromCommand(IListEditCommand<IEditData> command) {
			if (command == null)
				return;

			// Fix selection
			List<IListEditCommand<IEditData>> commands = new List<IListEditCommand<IEditData>>();

			if (command is ListEditGroupCommand<IEditData> groupCmd) {
				commands = groupCmd.Commands;
			}
			else {
				commands.Add(command);
			}

			List<IEditData> newSelection = new List<IEditData>();

			foreach (var cmd in commands) {
				if (cmd is ListEditCommand editCmd) {
					newSelection.AddRange(editCmd.Selection);
				}
			}

			SelectionManager.ClearSelection(ignoreKeyModifiers: true);
			SelectionManager.SetSelected(newSelection.OfType<IEditData>().ToList(), ignoreKeyModifiers: true);
		}

		public int GetInsertIndexFromSelectedItem() {
			if (_listBox.SelectedItem == null)
				return -1;

			var lastIdx = _listBox.Items.IndexOf(_listBox.SelectedItem);

			if (lastIdx < 0)
				return 0;

			foreach (var selection in _listBox.SelectedItems.OfType<IEditData>().OrderBy(p => p.Index)) {
				if (selection.Index < lastIdx)
					continue;

				if (selection.Index == lastIdx)
					lastIdx++;
				else
					break;
			}

			return lastIdx;
		}

		public bool IsClipboardDataEmpty() {
			return _clipboardData == null || _clipboardData.Length == 0;
		}

		public void MoveAt() {
			try {
				if (_listBox.SelectedItems.Count == 0)
					return;

				BeginDrag(ListBoxDragMode.ListItems);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void InsertAt() {
			try {
				if (IsClipboardDataEmpty())
					return;

				BeginDrag(ListBoxDragMode.ClipboardItems);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void Copy() {
			try {
				_clipboardData = _listBox.SelectedItems.OfType<IEditData>().ToArray();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public List<IEditData> GetClipboardData() {
			if (IsClipboardDataEmpty())
				return null;

			return _clipboardData.ToList();
		}
	}

	public class ListBoxSelectionExtension {
		private ListBox _listBox;

		enum SelectionState {
			NotSelected,
			Selected,
			AddedSelected,
			RemovedSelected,
		}

		private SelectionState[] _savedSelectionState;

		public ListBoxSelectionExtension(ListBox listBox) {
			_listBox = listBox;
		}

		public void SaveState() {
			SelectionState[] selectionArray = new SelectionState[_listBox.Items.Count];

			foreach (var select in _listBox.SelectedItems.OfType<IEditData>()) {
				selectionArray[select.Index] = SelectionState.Selected;
			}

			_savedSelectionState = selectionArray;
		}

		public void ClearSelection(bool ignoreKeyModifiers = false) {
			SetSelected(new List<IEditData>(), ignoreKeyModifiers: ignoreKeyModifiers);
		}

		public void SetSelected(IEditData selection, bool ignoreKeyModifiers = false) {
			SetSelected(new List<IEditData> { selection }, true, ignoreKeyModifiers: ignoreKeyModifiers);
		}

		public void SaveSelectionState() {
			SelectionState[] selectionArray = new SelectionState[_listBox.Items.Count];

			foreach (var select in _listBox.SelectedItems.OfType<IEditData>()) {
				selectionArray[select.Index] = SelectionState.Selected;
			}

			_savedSelectionState = selectionArray;
		}

		public void SetSelected(List<IEditData> selection, bool isSingleItem = false, bool useSavedState = false, bool ignoreKeyModifiers = false) {
			SelectionState[] selectionArray;

			if (useSavedState) {
				selectionArray = new SelectionState[_savedSelectionState.Length];
				Array.Copy(_savedSelectionState, selectionArray, _savedSelectionState.Length);

				SelectionState[] currentSelectionArray = new SelectionState[_listBox.Items.Count];

				foreach (var select in _listBox.SelectedItems.OfType<IEditData>()) {
					currentSelectionArray[select.Index] = SelectionState.Selected;
				}

				// Compare both selection arrays
				for (int i = 0; i < currentSelectionArray.Length; i++) {
					if (currentSelectionArray[i] == selectionArray[i])
						continue;

					if (selectionArray[i] == SelectionState.NotSelected)
						selectionArray[i] = SelectionState.RemovedSelected;

					if (selectionArray[i] == SelectionState.Selected)
						selectionArray[i] = SelectionState.AddedSelected;
				}
			}
			else {
				selectionArray = new SelectionState[_listBox.Items.Count];

				foreach (var select in _listBox.SelectedItems.OfType<IEditData>()) {
					selectionArray[select.Index] = SelectionState.Selected;
				}
			}

			if (!ignoreKeyModifiers && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
				foreach (var item in selection) {
					if (selectionArray[item.Index] == SelectionState.Selected)
						selectionArray[item.Index] = SelectionState.RemovedSelected;
					else if (selectionArray[item.Index] == SelectionState.AddedSelected)
						selectionArray[item.Index] = SelectionState.NotSelected;
					else if (selectionArray[item.Index] == SelectionState.RemovedSelected)
						selectionArray[item.Index] = SelectionState.Selected;
					else if (selectionArray[item.Index] == SelectionState.NotSelected)
						selectionArray[item.Index] = SelectionState.AddedSelected;
				}
			}
			else if (!ignoreKeyModifiers && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
				if (isSingleItem) {
					int startIndex = _listBox.SelectedItem != null ? ((IEditData)_listBox.SelectedItem).Index : 0;
					int targetIndex = selection[0].Index;

					for (int i = Math.Min(startIndex, targetIndex); i <= Math.Max(startIndex, targetIndex); i++) {
						if (selectionArray[targetIndex] == SelectionState.NotSelected)
							selectionArray[targetIndex] = SelectionState.AddedSelected;
					}
				}
				else {
					foreach (var item in selection) {
						if (selectionArray[item.Index] == SelectionState.NotSelected)
							selectionArray[item.Index] = SelectionState.AddedSelected;
						else if (selectionArray[item.Index] == SelectionState.RemovedSelected)
							selectionArray[item.Index] = SelectionState.Selected;
					}
				}
			}
			else {
				for (int i = 0; i < selectionArray.Length; i++) {
					if (selectionArray[i] == SelectionState.Selected)
						selectionArray[i] = SelectionState.RemovedSelected;
				}

				for (int i = 0; i < selection.Count; i++) {
					var select = selection[i];

					if (selectionArray[select.Index] == SelectionState.RemovedSelected)
						selectionArray[select.Index] = SelectionState.Selected;
					else if (selectionArray[select.Index] == SelectionState.NotSelected)
						selectionArray[select.Index] = SelectionState.AddedSelected;
				}
			}

			for (int i = 0; i < selectionArray.Length; i++) {
				var select = selectionArray[i];

				if (select == SelectionState.AddedSelected)
					_listBox.SelectedItems.Add(_listBox.Items[i]);
				else if (select == SelectionState.RemovedSelected)
					_listBox.SelectedItems.Remove(_listBox.Items[i]);
			}
		}
	}
}
