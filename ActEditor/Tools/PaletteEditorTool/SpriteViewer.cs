using System;
using System.Windows.Media.Imaging;
using GRF.FileFormats.SprFormat;
using GRF.Image;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteViewer : ImageViewer {
		private Spr _spr;

		public GrfImage LoadedImage { get; internal set; }

		public void SetSpr(Spr spr) {
			_spr = spr;
		}

		public void LoadIndexed8(int index) {
			LoadImage(_spr.Images[index]);
		}

		public void LoadImage(GrfImage image) {
			image.Palette[3] = 0;
			LoadedImage = image;
			Load(image.Cast<BitmapSource>());
		}

		public GrfImage GetImage(int index) {
			if (index < 0)
				return null;

			return _spr.Images[index];
		}
	}
}