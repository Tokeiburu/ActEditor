using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteModifiedCommand : IPaletteCommand {
		private readonly Spr _currentSprite;
		private readonly Spr _oldSprite;
		private readonly Spr _newSprite;

		public SpriteModifiedCommand(Spr currentSprite, Spr oldSprite, Spr newSprite) {
			_currentSprite = currentSprite;
			_oldSprite = oldSprite;
			_newSprite = newSprite;
		}

		public void Execute(Pal palette) {
			for (int i = 0; i < _currentSprite.Images.Count; i++) {
				_currentSprite.Images[i] = _newSprite.Images[i];
			}

			_currentSprite.Palette.OnPaletteChanged();
		}

		public void Undo(Pal palette) {
			for (int i = 0; i < _currentSprite.Images.Count; i++) {
				_currentSprite.Images[i] = _oldSprite.Images[i];
			}

			_currentSprite.Palette.OnPaletteChanged();
		}

		public string CommandDescription {
			get { return "Source sprite changed"; }
		}
	}
}
