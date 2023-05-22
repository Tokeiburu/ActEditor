using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TokeiLibrary.Shortcuts;

namespace ActEditor.Core.WPF.GenericControls {
	/// <summary>
	/// Interaction logic for ClickSelectTextBox2.xaml
	/// </summary>
	public partial class ClickSelectTextBox2 : UserControl {
		public TextAlignment TextAlignment {
			set {
				_tbox.TextAlignment = value;
				_tblock.TextAlignment = value;
			}
		}

		public bool IsReadOnly {
			get { return _tbox.IsReadOnly; }
			set { _tbox.IsReadOnly = value; }
		}

		public event TextChangedEventHandler TextChanged;

		protected virtual void OnTextChanged(TextChangedEventArgs e) {
			TextChangedEventHandler handler = TextChanged;
			if (handler != null) handler(this, e);
		}

		public ClickSelectTextBox2() {
			InitializeComponent();

			_tbox.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(_selectivelyIgnoreMouseButton), true);
			_tbox.AddHandler(GotKeyboardFocusEvent, new RoutedEventHandler(_selectAllText), true);
			_tbox.AddHandler(MouseDoubleClickEvent, new RoutedEventHandler(_selectAllText), true);

			_tbox.IsUndoEnabled = false;
			_tbox.TextChanged += new TextChangedEventHandler(_tbox_TextChanged);
			_tbox.PreviewKeyDown += new KeyEventHandler(_clickSelectTextBox_KeyDown);

			_tbox.DragOver += new DragEventHandler(_tbox_DragOver);
			_tbox.DragEnter += new DragEventHandler(_tbox_DragEnter);
		}

		public string Text {
			get { return _tbox.Text; }
			set {
				if (ClickSelectTextBox.EventsEnabled) {
					_tblock.Visibility = Visibility.Collapsed;
					_tbox.Text = value;
				}
				else {
					if (IsLoaded) {
						_setText(value);
					}
				}
			}
		}

		private void _setText(string value) {
			if (_tblock.Visibility != Visibility.Visible || _tblock.Width <= 0) {
				_tblock.Visibility = Visibility.Visible;
				_tblock.Width = ActualWidth;
				_tblock.Background = ((Grid)(Parent)).Background;
			}

			_tblock.Text = value;
		}

		private void _clickSelectTextBox_KeyDown(object sender, KeyEventArgs e) {
			if (ApplicationShortcut.Is(ApplicationShortcut.Undo) || ApplicationShortcut.Is(ApplicationShortcut.Redo)) {
				UIElement element = this;

				int maxlevel = 0;

				do {
					element = VisualTreeHelper.GetParent(element) as UIElement;
					maxlevel++;
				} while (element == null && maxlevel < 1000);

				if (element != null) {
					KeyEventArgs newarg = new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, e.Key);
					newarg.RoutedEvent = KeyDownEvent;
					newarg.Source = sender;
					element.RaiseEvent(newarg);
				}
			}
		}

		private void _tbox_TextChanged(object sender, TextChangedEventArgs e) {
			if (!ClickSelectTextBox.EventsEnabled) {
				e.Handled = true;
			}
			else {
				OnTextChanged(e);
			}
		}

		private void _tbox_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null) {
				e.Effects = DragDropEffects.All;
				e.Handled = true;
			}
		}

		private void _tbox_DragOver(object sender, DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null) {
				e.Effects = DragDropEffects.All;
				e.Handled = true;
			}
		}

		private static void _selectivelyIgnoreMouseButton(object sender, MouseButtonEventArgs e) {
			// Find the TextBox
			DependencyObject parent = e.OriginalSource as UIElement;
			while (parent != null && !(parent is TextBox))
				parent = VisualTreeHelper.GetParent(parent);

			if (parent != null) {
				var textBox = (TextBox)parent;
				if (!textBox.IsKeyboardFocusWithin) {
					// If the text box is not yet focussed, give it the focus and
					// stop further processing of this click event.
					textBox.Focus();
					e.Handled = true;
				}
			}
		}

		private static void _selectAllText(object sender, RoutedEventArgs e) {
			var textBox = e.OriginalSource as TextBox;
			if (textBox != null)
				textBox.SelectAll();
		}

		public void UpdateBackground() {
			if (_tblock != null) {
				_tblock.Background = ((Grid)(Parent)).Background;
			}
		}
	}
}
