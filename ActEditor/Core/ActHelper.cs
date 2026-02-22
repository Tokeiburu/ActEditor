using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;

namespace ActEditor.Core {
	public static class ActHelper {
		public static TokeiLibrary.WPF.RangeObservableCollection<string> GetAnimations(Act act) {
			return new TokeiLibrary.WPF.RangeObservableCollection<string>(act.GetAnimations());
		}

		public static void TrimImages(Act act, byte tolerance = 0x10, bool keepPerfectAlignment = false) {
			List<ActIndex> indexes = new List<ActIndex>();

			act.AllLayersAdv((index, layer) => {
				indexes.Add(index);
			});

			TrimImages(act, indexes, tolerance, keepPerfectAlignment);
		}

		public static void TrimImages(Act act, List<ActIndex> layers, byte tolerance = 0x10, bool keepPerfectAlignment = false) {
			var planes = new Dictionary<ActIndex, Plane>();
			
			Dictionary<SpriteIndex, GrfImage> images = new Dictionary<SpriteIndex, GrfImage>();

			foreach (var index in layers) {
				var layer = act[index];

				planes[index] = layer.ToPlane(act);

				if (!images.ContainsKey(layer.SprSpriteIndex)) {
					images[layer.SprSpriteIndex] = act.Sprite.GetImage(layer.SprSpriteIndex);
				}
			}

			foreach (var entry in images) {
				var image = entry.Value;
				SpriteIndex spriteIndex = entry.Key;
				if (image == null) continue;
				var oriWidth = image.Width;
				var oriHeight = image.Height;

				if (image.Width == 103)
					oriWidth = image.Width;

				var trimLengths = image.GetTrimLengths(tolerance);

				int[] trims = new int[] {
					Math.Max(0, trimLengths.Left - 1),
					Math.Max(0, trimLengths.Top - 1),
					Math.Max(0, trimLengths.Right - 1),
					Math.Max(0, trimLengths.Bottom - 1)
				};

				if (keepPerfectAlignment) {
					if ((trims[0] + trims[2]) % 2 == 1) {
						if (trims[0] > trims[2])
							trims[0]--;
						else
							trims[2]--;
					}

					if ((trims[1] + trims[3]) % 2 == 1) {
						if (trims[1] > trims[3])
							trims[1]--;
						else
							trims[3]--;
					}
				}

				image.Crop(trims[0], trims[1], trims[2], trims[3]);

				if (image.Width == 0 || image.Height == 0) {
					image.Crop(-1, -1, 0, 0);
					image.SetPixelTransparent(0, 0);
				}

				// Adjust layers that use this image
				act.AllLayersAdv((index, layer) => {
					if (layer.SpriteIndex < 0) return;
					if (layer.SprSpriteIndex != spriteIndex)
						return;

					//if (index.ActionIndex != 2) return;

					var previousCenter = planes[index].Center;
					var newPlane = Layer2PlaneAdjust(act, layer, oriWidth, oriHeight, trims);
					var diff = newPlane.Center - previousCenter;

					layer.OffsetX += (int)Math.Round(diff.X, MidpointRounding.AwayFromZero);
					layer.OffsetY += (int)Math.Round(diff.Y, MidpointRounding.AwayFromZero);
				});
			}
		}

		public static Plane Layer2PlaneAdjust(Act act, Layer layer, int oriWidth, int oriHeight, int[] trims) {
			if (layer.SpriteIndex < 0) return null;
			var image = layer.GetImage(act.Sprite);

			Plane plane = new Plane(oriWidth, oriHeight);
			plane.Translate(layer.Mirror ? -(image.Width + 1) % 2 : 0, 0);
			plane.Crop(trims[0], trims[1], trims[2], trims[3]);

			if ((trims[0] + trims[2]) % 2 == 1) {
				plane.Translate(layer.Mirror ? -1.5f : 0.5f, 0);
			}

			if ((trims[1] + trims[3]) % 2 == 1) {
				plane.Translate(0, -0.5f);
			}

			plane.ScaleX(layer.ScaleX * (layer.Mirror ? -1f : 1f));
			plane.ScaleY(layer.ScaleY);
			plane.RotateZ(-layer.Rotation);
			plane.Translate(layer.OffsetX, layer.OffsetY);
			return plane;
		}
	}
}
