using System;
using System.Collections.Generic;
using GRF.FileFormats.ActFormat;
using GRF.Image;

namespace Scripts {
    public class Script : IActScript {
        public object DisplayName {
            get { return "Remove unused palette"; }
        }

        public string Group {
            get { return "Scripts"; }
        }

        public string InputGesture {
            get { return null; }
        }

        public string Image {
            get { return "delete.png"; }
        }

        public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
            if (act == null || act.Sprite == null || act.Sprite.Palette == null)
                return;

            HashSet<byte> usedIndexes = new HashSet<byte>();
            foreach (var image in act.Sprite.Images) {
                if (image.GrfImageType == GrfImageType.Indexed8) {
                    foreach (byte idx in image.Pixels)
                        usedIndexes.Add(idx);
                }
            }
            byte[] palette = act.Sprite.Palette.BytePalette;
            for (int i = 0; i < 256; i++) {
                if (!usedIndexes.Contains((byte)i)) {
                    palette[4 * i + 0] = 255;
                    palette[4 * i + 1] = 0;
                    palette[4 * i + 2] = 255;
                    palette[4 * i + 3] = 255;
                }
            }
            act.Sprite.Palette.SetPalette(palette);
        }

        public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
            return act != null && act.Sprite != null && act.Sprite.Palette != null;
        }
    }
}