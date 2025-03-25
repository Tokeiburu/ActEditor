using System.Windows;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for AdjustColorDialog.xaml
	/// </summary>
	public partial class AdjustColorDialog : Window {
		public AdjustColorDialog() {
			InitializeComponent();
		}

		public AdjustColorControl AdjustColorControl {
			get { return _accControl; }
		}
	}
}