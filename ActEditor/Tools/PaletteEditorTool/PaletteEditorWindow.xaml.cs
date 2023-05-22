using System.Windows;
using ActEditor.ApplicationConfiguration;

namespace ActEditor.Tools.PaletteEditorTool {
	/// <summary>
	/// Interaction logic for PaletteEditorWindo.xaml
	/// </summary>
	public partial class PaletteEditorWindow : Window {
		public PaletteEditorWindow() {
			InitializeComponent();

			ShowInTaskbar = true;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			SizeToContent = SizeToContent.WidthAndHeight;

			Loaded += delegate {
				SizeToContent = SizeToContent.Manual;
				MinHeight = 440;

				MinWidth = 300 + (ActEditorConfiguration.PaletteEditorOpenWindowsEdits ? 0 : 840);
			};
		}

		public SpriteEditorControl PaletteEditor {
			get { return _palEditor; }
		}
	}
}