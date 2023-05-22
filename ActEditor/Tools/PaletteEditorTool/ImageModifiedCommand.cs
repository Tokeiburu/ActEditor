using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;

namespace ActEditor.Tools.PaletteEditorTool {
	public class ImageModifiedCommand : IPaletteCommand {
		private readonly Spr _sprite;
		private readonly int _index;
		private GrfImage _oldSprite;
		private readonly GrfImage _newSprite;

		public ImageModifiedCommand(Spr sprite, int index, GrfImage newSprite) {
			_sprite = sprite;
			_index = index;
			_newSprite = newSprite;
		}

		public void Execute(Pal palette) {
			if (_oldSprite == null) {
				_oldSprite = _sprite.Images[_index].Copy();
			}

			_sprite.Images[_index] = _newSprite;
			_sprite.Palette.OnPaletteChanged();
		}

		public void Undo(Pal palette) {
			_sprite.Images[_index] = _oldSprite;
			_sprite.Palette.OnPaletteChanged();
		}

		public string CommandDescription {
			get { return "Sprite changed"; }
		}
	}
}
