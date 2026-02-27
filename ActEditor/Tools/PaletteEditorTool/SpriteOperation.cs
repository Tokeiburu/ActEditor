using ErrorManager;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using System;

namespace ActEditor.Tools.PaletteEditorTool {
	public enum GradientOperation {
		SwapPaletteColors,
		SwapSpriteIndexes,
		SwapSpriteIndexesAndPaletteColors,
		Redirect,
	}

	public class SpriteOperation {
		public static void ApplyGradientChange(Spr spr, SpriteEditorState state, GradientOperation mode) {
			try {
				spr.Palette.Commands.BeginNoDelay();

				Spr oldSprite = new Spr(spr);
				Spr newSprite = new Spr(spr);
				int length = 1;
				var paletteSelector = state.IsGradientEditorSelected ? state.GradientEditor.PaletteSelector : state.SingleEditor.PaletteSelector;

				if (state.IsGradientEditorSelected)
					length = 8;

				if (state.IsGradientEditorSelected) {
					if (paletteSelector.SelectedItems.Count != 16)
						throw new Exception("You must select two gradients to switch them.");
				}
				else {
					if (paletteSelector.SelectedItems.Count != 2)
						throw new Exception("You must select two colors to switch them.");
				}

				byte p1 = (byte)paletteSelector.SelectedItems[0];
				byte p2 = (byte)paletteSelector.SelectedItems[length];

				var d1 = new byte[4 * length];
				var d2 = new byte[4 * length];

				Buffer.BlockCopy(spr.Palette.BytePalette, p1 * 4, d1, 0, 4 * length);
				Buffer.BlockCopy(spr.Palette.BytePalette, p2 * 4, d2, 0, 4 * length);

				switch (mode) {
					case GradientOperation.SwapPaletteColors:
						spr.Palette.Commands.SetRawBytesInPalette(p1 * 4, d2);
						spr.Palette.Commands.SetRawBytesInPalette(p2 * 4, d1);
						break;
					case GradientOperation.SwapSpriteIndexes:
					case GradientOperation.SwapSpriteIndexesAndPaletteColors:
						for (int i = 0; i < newSprite.NumberOfIndexed8Images; i++) {
							var image = newSprite.Images[i];

							for (int k = 0; k < image.Pixels.Length; k++) {
								if (image.Pixels[k] >= p1 && image.Pixels[k] < p1 + length) {
									image.Pixels[k] = (byte)(p2 + image.Pixels[k] - p1);
								}
								else if (image.Pixels[k] >= p2 && image.Pixels[k] < p2 + length) {
									image.Pixels[k] = (byte)(p1 + image.Pixels[k] - p2);
								}
							}
						}

						if (mode == GradientOperation.SwapSpriteIndexesAndPaletteColors) {
							spr.Palette.Commands.SetRawBytesInPalette(p1 * 4, d2);
							spr.Palette.Commands.SetRawBytesInPalette(p2 * 4, d1);
						}

						spr.Palette.Commands.StoreAndExecute(new SpriteModifiedCommand(spr, oldSprite, newSprite));
						break;
					case GradientOperation.Redirect:
						for (int i = 0; i < newSprite.NumberOfIndexed8Images; i++) {
							var image = newSprite.Images[i];

							for (int k = 0; k < image.Pixels.Length; k++) {
								if (image.Pixels[k] >= p1 && image.Pixels[k] < p1 + length) {
									image.Pixels[k] = (byte)(p2 + image.Pixels[k] - p1);
								}
							}
						}

						spr.Palette.Commands.StoreAndExecute(new SpriteModifiedCommand(spr, oldSprite, newSprite));
						break;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				spr.Palette.Commands.End();
			}
		}

		public static void ApplyGrayscale(Spr spr, SpriteEditorState state, GrayscaleMode mode) {
			try {
				spr.Palette.Commands.BeginNoDelay();

				var image = state.SelectedImage.Copy();
				image.Grayscale(mode);
				spr.Palette.Commands.SetRawBytesInPalette(0, image.Palette);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				spr.Palette.Commands.End();
			}
		}
	}
}
