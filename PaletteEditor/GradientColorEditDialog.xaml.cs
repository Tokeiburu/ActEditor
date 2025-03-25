using System.Windows;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for GradientColorEditWindow.xaml
	/// </summary>
	public partial class GradientColorEditDialog : Window {
		public GradientColorEditDialog() {
			InitializeComponent();
		}

		public GradientColorEditControl GradientColorEditControl {
			get { return _gceControl; }
		}
	}
}