using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using System;
using System.Linq;
using Utilities;

namespace ActEditor.Core.Scripts.Effects {
	public class SpriteOutlineEffect : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public GrfColor Color;
			public int Thickness;
		}

		private EffectOptions _options = new EffectOptions();
		private int _paletteInsertIndex = -1;

		public SpriteOutlineEffect() : base("Sprite outline") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Color", new GrfColor(255, 255, 255, 255), null, null);
			effect.AddProperty("Thickness", 1, 1, 5);
			_animationComponent.SetEditType(AnimationEditTypes.TargetOnly);
			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Color = effect.GetProperty<GrfColor>("Color");
			_options.Thickness = effect.GetProperty<int>("Thickness");

			_generateBgra32Images = false;
			_paletteInsertIndex = -1;

			if (_actInput.Sprite.Palette != null) {
				var colors = _actInput.Sprite.Palette.Colors.ToList();

				for (int i = 1; i < colors.Count; i++) {
					if (_options.Color.Equals(colors[i])) {
						_paletteInsertIndex = i;
						break;
					}
				}

				if (_paletteInsertIndex == -1) {
					var unused = _actInput.Sprite.GetUnusedPaletteIndexes();

					if (unused.Count == 0) {
						_generateBgra32Images = true;
					}
					else {
						_paletteInsertIndex = unused.First();
						_actInput.Sprite.Palette.SetColor(_paletteInsertIndex, _options.Color);
					}
				}
			}
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			var sprIndex = layer.SprSpriteIndex;

			if (sprIndex.Valid) {
				SpriteIndex newSpriteIndex;

				if (!_transformedSprites.TryGetValue((sprIndex, 0), out newSpriteIndex)) {
					var image = act.Sprite.GetImage(sprIndex).Copy();

					if (_generateBgra32Images)
						image.Convert(GrfImageType.Bgra32);

					newSpriteIndex = act.Sprite.InsertAny(image);
					ProcessImage(image, 0, animLength);
					_transformedSprites[(sprIndex, 0)] = newSpriteIndex;
				}

				layer.SprSpriteIndex = newSpriteIndex;

				PostProcessLayer(act, layer, 0, animLength);
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			if (_options.Thickness < 0)
				return;

			for (int i = 0; i < _options.Thickness; i++) {
				img.Crop(-1);
				var imgCopy = img.Copy();

				for (int x = 0; x < img.Width; x++) {
					for (int y = 0; y < img.Height; y++) {
						if (imgCopy.IsPixelTransparent(x, y)) {
							if (
								(x == 0 || imgCopy.IsPixelTransparent(x - 1, y)) &&
								(x == img.Width - 1 || imgCopy.IsPixelTransparent(x + 1, y)) &&
								(y == 0 || imgCopy.IsPixelTransparent(x, y - 1)) &&
								(y == img.Height - 1 || imgCopy.IsPixelTransparent(x, y + 1))) {
								continue;
							}

							if (img.GrfImageType == GrfImageType.Indexed8)
								img.Pixels[y * img.Width + x] = (byte)_paletteInsertIndex;
							else
								img.SetColor(x, y, _options.Color);
						}
					}
				}
			}
		}

		public override string Group => "Effects/Global";
		public override string InputGesture => "{Dialog.SpriteOutline}";
		public override string Image => "empty.png";

		#endregion
	}
}
