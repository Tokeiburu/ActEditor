using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TokeiLibrary.Shortcuts;

namespace ActEditor.Core.WPF.GenericControls {
	public class ClickSelectTextBox : TextBox {
		private static readonly Thickness _sharedThickness = new Thickness(3, 0, 3, 1);
		private readonly QuickTextPreviewAdorner _adorner;
		private readonly TextBlock _tblock = new TextBlock();

		static ClickSelectTextBox() {
			EventsEnabled = true;
		}

		public ClickSelectTextBox() {
			AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(_selectivelyIgnoreMouseButton), true);
			AddHandler(GotKeyboardFocusEvent, new RoutedEventHandler(_selectAllText), true);
			AddHandler(MouseDoubleClickEvent, new RoutedEventHandler(_selectAllText), true);

			IsUndoEnabled = false;

			PreviewKeyDown += new KeyEventHandler(_clickSelectTextBox_KeyDown);

			_tblock.Padding = _sharedThickness;
			_tblock.IsHitTestVisible = false;
			_tblock.Visibility = Visibility.Collapsed;
			_tblock.Background = Brushes.White;
			_tblock.VerticalAlignment = VerticalAlignment.Center;
			_tblock.TextAlignment = TextAlignment.Right;
			_tblock.ClipToBounds = true;

			_adorner = new QuickTextPreviewAdorner(_tblock, this);

			bool isLoaded = false;

			Loaded += delegate {
				if (isLoaded) return;
				var layer = AdornerLayer.GetAdornerLayer(this);
				if (layer != null) {
					layer.Add(_adorner);
				}

				if (!EventsEnabled)
					Text = _tblock.Text;

				isLoaded = true;
			};
		}

		public static bool EventsEnabled { get; set; }

		public new string Text {
			get { return (string) GetValue(TextProperty); }
			set {
				if (EventsEnabled) {
					_tblock.Visibility = Visibility.Collapsed;
					SetValue(TextProperty, value);
				}
				else {
					if (IsLoaded) {
						_setText(value);
					}
				}
			}
		}

		private void _setText(string value) {
			if (_adorner.Parent == null) {
				_tblock.Text = value;
				return;
			}

			if (_tblock.Visibility != Visibility.Visible || _tblock.Width <= 0) {
				_tblock.Visibility = Visibility.Visible;
				_tblock.Width = ActualWidth;
				_tblock.Background = ((Grid) (Parent)).Background;
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

		protected override void OnTextChanged(TextChangedEventArgs e) {
			if (EventsEnabled)
				base.OnTextChanged(e);
		}

		protected override void OnDragOver(DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null) {
				e.Effects = DragDropEffects.All;
				return;
			}

			base.OnDragOver(e);
		}

		protected override void OnDragEnter(DragEventArgs e) {
			if (e.Data.GetData("ImageIndex") != null) {
				e.Effects = DragDropEffects.All;
				return;
			}

			base.OnDragEnter(e);
		}

		private static void _selectivelyIgnoreMouseButton(object sender, MouseButtonEventArgs e) {
			// Find the TextBox
			DependencyObject parent = e.OriginalSource as UIElement;
			while (parent != null && !(parent is TextBox))
				parent = VisualTreeHelper.GetParent(parent);

			if (parent != null) {
				var textBox = (TextBox) parent;
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
				_tblock.Background = ((Grid) (Parent)).Background;
			}
		}
	}
}