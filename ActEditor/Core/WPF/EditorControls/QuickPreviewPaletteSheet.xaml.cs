using GRF.Image;
using System.Windows;
using System.Windows.Media.Imaging;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for QuickPreviewPaletteSheet.xaml
	/// </summary>
	public partial class QuickPreviewPaletteSheet : TkWindow {
		public QuickPreviewPaletteSheet() : base("Quick sprite sheet preview", "busy.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
		}

		public void SetImage(GrfImage image) {
			if (image == null)
				_previewImage.Source = null;
			else
				_previewImage.Source = image.Cast<BitmapSource>();
		}
	}
}
