using GRF.FileFormats.ActFormat;
using GRF.Image;
using System;
using System.Linq;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class RadialErosionEffect : ImageProcessingEffect {
		public override string Image => "effect_radial_erosion.png";
		public override string Group => @"Effects/Dead";

		public class EffectOptions {
			public float TargetX;
			public float TargetY;
			public float NoiseStrength;
		}

		private EffectOptions _options = new EffectOptions();
		private float[,] _noise;

		public RadialErosionEffect() : base("Radial erosion") {
			_inputGesture = "{Dialog.AnimationRadialErosion}";
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("TargetX", 0.5f, 0.0f, 1.0f);
			effect.AddProperty("TargetY", 0.7f, 0.0f, 1.0f);
			effect.AddProperty("End color", new GrfColor(255, 0, 0, 0), default, default);
			effect.AddProperty("NoiseStrength", 25f, 0f, 100f);

			_animationComponent.DefaultSaveData.SetAnimation(4);
			_animationComponent.DefaultSaveData.SetLayers(0);
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.TargetX = effect.GetProperty<float>("TargetX");
			_options.TargetY = effect.GetProperty<float>("TargetY");
			_options.NoiseStrength = effect.GetProperty<float>("NoiseStrength");
			TargetColor = effect.GetProperty<GrfColor>("End color");
		}

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			base.OnPreviewProcessAction(act, action, aid);

			int maxWidth = act.Sprite.Images.Max(p => p.Width);
			int maxHeight = act.Sprite.Images.Max(p => p.Height);
			_noise = GenerateNoise(maxWidth, maxHeight, 88888);
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			int w = img.Width;
			int h = img.Height;

			int targetX = (int)(_options.TargetX * img.Width);
			int targetY = (int)(_options.TargetY * img.Height);

			float maxDist = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
			float radius = maxDist * (1f - (float)step / totalSteps);

			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					int dx = x - targetX;
					int dy = y - targetY;

					float dist = (float)Math.Sqrt(dx * dx + dy * dy);

					float fracturedDist = dist + (_noise[x, y] - 0.5f) * _options.NoiseStrength;

					if (fracturedDist > radius) {
						if (img.GrfImageType == GrfImageType.Indexed8)
							img.Pixels[w * y + x] = 0;
						else
							img.Pixels[(w * y + x) * 4 + 3] = 0;
					}
				}
			}
		}

		public float[,] GenerateNoise(int w, int h, int seed) {
			var rnd = new Random(seed);
			var noise = new float[w, h];

			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
					noise[x, y] = (float)rnd.NextDouble();

			return noise;
		}
	}
}
