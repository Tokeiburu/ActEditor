using System;
using System.Collections.Generic;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using Utilities;

namespace Scripts {
    public class Script : IActScript {
        public object DisplayName {
            get { return "Remove unused palette colors"; }
        }

        public string Group {
            get { return "Scripts"; }
        }

        public string InputGesture {
			get { return "{Scripts.RemoveUnusedPaletteColors}"; }
        }

        public string Image {
            get { return "delete.png"; }
        }

        public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
            if (act == null || act.Sprite == null || act.Sprite.Palette == null)
                return;

            HashSet<byte> unusedIndexes = act.Sprite.GetUnusedPaletteIndexes();
			byte[] palette = Methods.Copy(act.Sprite.Palette.BytePalette);
			
            for (int i = 1; i < 256; i++) {
                if (unusedIndexes.Contains((byte)i)) {
                    palette[4 * i + 0] = 255;
                    palette[4 * i + 1] = 0;
                    palette[4 * i + 2] = 255;
                    palette[4 * i + 3] = 255;
                }
            }
			
			act.Commands.SpriteSetPalette(palette);
        }

        public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
            return act != null && act.Sprite != null && act.Sprite.Palette != null;
        }
    }
}