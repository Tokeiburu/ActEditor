using System.Windows.Media.Imaging;
using GRF.FileFormats.SprFormat;
using GRF.Image;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteViewer : ImageViewer {
		private Spr _spr;

		public void SetSpr(Spr spr) {
			_spr = spr;
		}

		public void Load(int index) {
			Load(_spr.Images[index].Cast<BitmapSource>());
		}

		public void LoadIndexed8(int index) {
			Load(_spr.Images[index].Cast<BitmapSource>());
		}

		public void LoadImage(GrfImage image) {
			Load(image.Cast<BitmapSource>());
		}

		public void LoadBgra32(int index) {
			Load(_spr.Images[index + _spr.NumberOfIndexed8Images].Cast<BitmapSource>());
		}
	}
}