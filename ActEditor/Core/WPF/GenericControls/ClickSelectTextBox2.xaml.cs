using ActEditor.ApplicationConfiguration;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TokeiLibrary.Shortcuts;
using Utilities;

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

			_tbox.PreviewMouseLeftButtonDown += _selectivelyIgnoreMouseButton;
			_tbox.GotKeyboardFocus += _selectAllText;
			_tbox.MouseDoubleClick += _selectAllText;

			_tbox.IsUndoEnabled = false;
			_tbox.TextChanged += _tbox_TextChanged;
			_tbox.PreviewKeyDown += _clickSelectTextBox_KeyDown;

			_tbox.DragOver += _tbox_DragOver;
			_tbox.DragEnter += _tbox_DragEnter;
		}

		private int? _cachedValueInt = null;
		private float? _cachedValueFloat = null;
		private bool _assignedTextBox = false;

		public string Text {
			get {
				return _assignedTextBox ? _tbox.Text : _tblock.Text;
			}
			set {
				VisibilityCheck();

				if (ClickSelectTextBox.EventsEnabled) {
					// Assign to main text box
					if (!_assignedTextBox || _cachedValueInt != null || _cachedValueFloat != null) {
						_tbox.Text = value;
						_assignedTextBox = true;
						_cachedValueInt = null;
						_cachedValueFloat = null;
					}
				}
				else {
					// Assign to preview
					if (_tblock.Width <= 0 || double.IsNaN(_tblock.Width)) {
						_tblock.Width = ActualWidth;
					}

					// Not assigned to preview yet or cache value is different
					if (_assignedTextBox || _cachedValueInt != null || _cachedValueFloat != null) {
						_tblock.Text = value;
						_assignedTextBox = false;
						_cachedValueInt = null;
						_cachedValueFloat = null;
					}
				}
			}
		}

		public void SetValue(int value) {
			VisibilityCheck();

			if (ClickSelectTextBox.EventsEnabled) {
				// Assign to main text box
				if (!_assignedTextBox || _cachedValueInt != value) {
					_tbox.Text = value.ToString(CultureInfo.InvariantCulture);
					_assignedTextBox = true;
					_cachedValueInt = value;
				}
			}
			else {
				// Assign to preview
				if (_tblock.Width <= 0 || double.IsNaN(_tblock.Width)) {
					_tblock.Width = ActualWidth;
				}

				// Not assigned to preview yet or cache value is different
				if (_assignedTextBox || _cachedValueInt != value) {
					_tblock.Text = value.ToString(CultureInfo.InvariantCulture);
					_assignedTextBox = false;
					_cachedValueInt = value;
				}
			}
		}

		public void SetValue(float value) {
			VisibilityCheck();

			if (ClickSelectTextBox.EventsEnabled) {
				// Assign to main text box
				if (!_assignedTextBox || _cachedValueFloat != value) {
					_tbox.Text = value.ToString("0.######", CultureInfo.InvariantCulture);
					_assignedTextBox = true;
					_cachedValueFloat = value;
				}
			}
			else {
				// Assign to preview
				if (_tblock.Width <= 0 || double.IsNaN(_tblock.Width)) {
					_tblock.Width = ActualWidth;
				}

				// Not assigned to preview yet or cache value is different
				if (_assignedTextBox || _cachedValueFloat != value) {
					_tblock.Text = value.ToString("0.######", CultureInfo.InvariantCulture);
					_assignedTextBox = false;
					_cachedValueFloat = value;
				}
			}
		}

		public void VisibilityCheck() {
			if (ClickSelectTextBox.EventsEnabled) {
				if (_tbox.Visibility != Visibility.Visible) {
					_tbox.Visibility = Visibility.Visible;
					_tblock.Visibility = Visibility.Collapsed;
				}
			}
			else {
				if (IsLoaded) {
					if (_tbox.Visibility == Visibility.Visible) {
						_tbox.Visibility = Visibility.Collapsed;
						_tblock.Visibility = Visibility.Visible;
					}
				}
			}
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
			if (textBox != null) {
				if (ActEditorConfiguration.ThemeIndex == 0) {
					textBox.SelectionBrush = SystemColors.HighlightBrush;
					textBox.SelectionTextBrush = SystemColors.HighlightTextBrush;
					textBox.SelectionOpacity = 0.4f;
				}
				else {
					textBox.SelectionBrush = new SolidColorBrush(Color.FromArgb(255, 74, 84, 192));
					textBox.SelectionTextBrush = Brushes.White;
					textBox.SelectionOpacity = 1;
				}

				textBox.SelectAll();
			}
		}

		public void UpdateBackground() {
			if (_tblock != null) {
				_tblock.Background = ((Grid)(Parent)).Background;
			}
		}

		public void EndPreview() {
			if (_tblock.Visibility == Visibility.Visible) {
				_tbox.Text = _tblock.Text;
				_assignedTextBox = true;

				_tblock.Visibility = Visibility.Collapsed;
				_tbox.Visibility = Visibility.Visible;
			}
		}
	}
}
