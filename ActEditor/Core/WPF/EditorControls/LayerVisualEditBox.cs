using ActEditor.Core.WPF.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using static ActEditor.Core.WPF.EditorControls.LayerVisualEditor;

namespace ActEditor.Core.WPF.EditorControls {
	internal class LayerVisualEditBox {
		private LayerVisualEditor _visualEditor;
		private TabAct _actEditor;
		private LayerEditorComponents _components;
		private TextBox _editBox;
		private bool _editBoxFocusEventDisabled = false;
		private bool _editBoxEventDisabled = false;
		private EditableDataValues _editValue = EditableDataValues.SpriteNumber;
		private int _editLayerIndex = -1;
		private TextBlock _editTextBlock;
		private VisualLayer _editVisualLayer;

		public TextBox EditBox => _editBox;
		public int EditLayerIndex => _editLayerIndex;
		public EditableDataValues EditValue => _editValue;
		public VisualLayer EditVisualLayer => _editVisualLayer;

		public LayerVisualEditBox(LayerVisualEditor visualEditor, TabAct actEditor, LayerEditorComponents components) {
			_visualEditor = visualEditor;
			_actEditor = actEditor;
			_components = components;

			_components.LayerEditor.PreviewMouseLeftButtonDown += _layerEditor_PreviewMouseLeftButtonDown;

			InitializeEditBox();
		}

		public void InitializeEditBox() {
			_editBox = new TextBox();
			_editBox.Text = "";
			_editBox.Width = 100;
			_editBox.Height = 17;
			_editBox.Background = Brushes.Transparent;
			_editBox.Visibility = Visibility.Collapsed;
			_editBox.HorizontalAlignment = HorizontalAlignment.Left;
			_editBox.VerticalAlignment = VerticalAlignment.Top;
			_editBox.TextAlignment = TextAlignment.Right;
			_editBox.Padding = new Thickness(0);
			_editBox.BorderThickness = new Thickness(0);
			_editBox.IsUndoEnabled = false;

			_components.Part_ContentOverlay.Children.Add(_editBox);

			_editBox.TextChanged += delegate {
				if (_editBoxEventDisabled) return;

				var visualLayer = _visualEditor.GetVisualLayer(_editLayerIndex);

				// Can happen if the focused box was selected, then put out of the scrollviewer's viewport.
				// Decide whether or not the layer data is valid
				if (visualLayer == null && _editVisualLayer != null) {
					if (_editLayerIndex < _actEditor.Act[_visualEditor.LastAid, _visualEditor.LastFid].NumberOfLayers) {
						visualLayer = _editVisualLayer;
						visualLayer.Set(_actEditor.Act, _actEditor.SelectedAction, _actEditor.SelectedFrame, visualLayer.LayerIndex);
						visualLayer.InternalUpdate();
					}
				}

				if (visualLayer != null) {
					visualLayer.SetLayerValue(_editBox.Text, _editValue);
					_editTextBlock.Text = _editBox.Text;
				}
			};

			_editBox.PreviewLostKeyboardFocus += _editBox_PreviewLostKeyboardFocus;
			_editBox.PreviewKeyDown += _editBox_PreviewKeyDown;
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

			var visualLayer = _visualEditor.GetVisualLayer(row);

			if (visualLayer == null || visualLayer.Visibility != Visibility.Visible || col < 0)
				return false;

			var block = visualLayer.GetBlockFromCol(col);

			if (block != null) {
				var blockPosition = block.TransformToVisual(_components.Part_ContentContainer).Transform(new Point(0, 0));
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

		private void _editBox_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (ApplicationShortcut.Is(ApplicationShortcut.Undo) || ApplicationShortcut.Is(ApplicationShortcut.Redo)) {
				UIElement element = _editBox;

				int maxlevel = 0;

				do {
					element = VisualTreeHelper.GetParent(element) as UIElement;
					maxlevel++;
				} while (element == null && maxlevel < 1000);

				if (element != null) {
					KeyEventArgs newarg = new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, e.Key);
					newarg.RoutedEvent = UIElement.KeyDownEvent;
					newarg.Source = sender;
					element.RaiseEvent(newarg);
				}
			}
		}

		private void _editBox_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			if (_editBoxFocusEventDisabled) return;

			_editLayerIndex = -1;
			_editBox.Visibility = Visibility.Collapsed;

			if (_editTextBlock != null && _editTextBlock.Visibility != Visibility.Visible)
				_editTextBlock.Visibility = Visibility.Visible;

			//Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (_editBox_PreviewLostKeyboardFocus)");
		}

		private void _layerEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			var point = e.GetPosition(_components.Part_ContentContainer);

			var layerIndex = (int)(point.Y / _visualEditor.PreviewElementHeight);
			var visualLayer = _visualEditor.GetVisualLayer(layerIndex);

			if (point.Y >= 0 && visualLayer != null && visualLayer.Visibility == Visibility.Visible) {
				if (SetEditBox(layerIndex, visualLayer.GetColumn(_components.Part_ContentContainer.GetObjectAtPoint<TextBlock>(point)))) {
					//e.Handled = true;
					return;
				}
			}

			_editBox.Visibility = Visibility.Collapsed;

			if (_editTextBlock != null && _editTextBlock.Visibility != Visibility.Visible)
				_editTextBlock.Visibility = Visibility.Visible;

			//Console.WriteLine("_editBox.Visibility = " + _editBox.Visibility + ", _editTextBlock.Visibility = " + _editTextBlock.Visibility + " (_layerEditor_PreviewMouseLeftButtonDown)");
		}
		
		public void UpdateEditBox() {
			if (_editTextBlock.Text != _editBox.Text) {
				UpdateEditBoxValue(_editTextBlock.Text);
			}
		}

		public void HideEditBox() {
			_editBox.Visibility = Visibility.Collapsed;
			_editTextBlock.Visibility = Visibility.Visible;
		}
	}
}
