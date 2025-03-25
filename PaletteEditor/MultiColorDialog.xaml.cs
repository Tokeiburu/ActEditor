using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GRF.FileFormats.PalFormat;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF.Styles;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for AdjustColorDialog.xaml
	/// </summary>
	public partial class MultiColorDialog : TkWindow {
		private Pal _pal;

		public MultiColorDialog() : base("Palette edit", "pal.png", SizeToContent.Manual, ResizeMode.NoResize) {
			InitializeComponent();

			PreviewKeyDown += new KeyEventHandler(_multiColorDialog_PreviewKeyDown);
			ShowInTaskbar = false;
			Loaded += delegate {
				if (Owner != null) {
					Window parent = Owner;

					if (parent.Left + parent.ActualWidth + ActualWidth > SystemParameters.FullPrimaryScreenWidth) {
						Left = SystemParameters.FullPrimaryScreenWidth - ActualWidth;
					}
					else {
						Left = parent.Left + parent.ActualWidth;
					}

					Top = parent.Top;
				}
			};

			_mainTabControl.SelectionChanged += new SelectionChangedEventHandler(_mainTabControl_SelectionChanged);
		}

		public TabControl MainTabControl {
			get { return _mainTabControl; }
		}

		public int Selected {
			get { return _mainTabControl.SelectedIndex; }
		}

		public SingleColorEditControl SingleColorEditControl {
			get { return _sce; }
		}

		public GradientColorEditControl GradientColorEditControl {
			get { return _gceControl; }
		}

		public AdjustColorControl AdjustColorControl {
			get { return _accControl; }
		}

		private void _mainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_mainTabControl.SelectedIndex == 0) {
				Keyboard.Focus(SingleColorEditControl.PaletteSelector._gridFocus);
			}
			else if (_mainTabControl.SelectedIndex == 1) {
				Keyboard.Focus(GradientColorEditControl.PaletteSelector._gridFocus);
			}
			else if (_mainTabControl.SelectedIndex == 2) {
				Keyboard.Focus(AdjustColorControl.PaletteSelector._gridFocus);
			}
		}

		private void _multiColorDialog_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (_pal == null) return;

			if (ApplicationShortcut.Is(ApplicationShortcut.Undo)) {
				_pal.Commands.Undo();
				e.Handled = true;
			}

			if (ApplicationShortcut.Is(ApplicationShortcut.Redo)) {
				_pal.Commands.Redo();
				e.Handled = true;
			}
		}

		public void SelectTab(int index) {
			_mainTabControl.SelectedIndex = index;
		}

		public void SetPalette(Pal pal) {
			_pal = pal;

			SingleColorEditControl.SetPalette(_pal);
			GradientColorEditControl.SetPalette(_pal);
			AdjustColorControl.SetPalette(_pal);
		}
	}
}