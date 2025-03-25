using System.Windows;
using TokeiLibrary;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for SingleColorEditDialog.xaml
	/// </summary>
	public partial class SingleColorEditDialog : Window {
		public SingleColorEditDialog() {
			InitializeComponent();

			Icon = ApplicationManager.PreloadResourceImage("pal.png");
		}

		public SingleColorEditControl SingleColorEditControl {
			get { return _sce; }
		}
	}
}