using System.Windows;
using ActEditor.ApplicationConfiguration;
using TokeiLibrary.WPF.Styles;
using Utilities;

namespace ActEditor.Tools.PaletteEditorTool {
	/// <summary>
	/// Interaction logic for PaletteEditorWindo.xaml
	/// </summary>
	public partial class ResizeDialog : TkWindow {
		public ResizeDialog() {
			InitializeComponent();
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		public int MarginLeft => FormatConverters.IntConverterNoThrow(_tbLeft.Text);
		public int MarginRight => FormatConverters.IntConverterNoThrow(_tbRight.Text);
		public int MarginTop => FormatConverters.IntConverterNoThrow(_tbTop.Text);
		public int MarginBottom => FormatConverters.IntConverterNoThrow(_tbBottom.Text);
	}
}