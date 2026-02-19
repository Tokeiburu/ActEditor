using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using TokeiLibrary;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts.Effects {
	public class SmokeFadeEffect : ImageProcessingEffect {
		public override string Image => "effect_smoke_fade.png";
		public override string Group => @"Effects/Dead";

		public class EffectOptions {
			public GrfColor SmokeColor;
			public GrfColor SpriteOverlayColor;
			public float OverlayBrightness;
			public float SpreadMult;
			public int SmokeCount;
			public Random Rng = new Random(1234);
		}

		private EffectOptions _options = new EffectOptions();
		private GrfImage _smoke;

		public SmokeFadeEffect() : base("Smoke fade") {
			_inputGesture = "{Dialog.AnimationSmokeFade}";
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("SpriteOverlayColor", new GrfColor(255, 255, 255, 255), default, default);
			effect.AddProperty("SmokeColor", new GrfColor(255, 82, 239, 211), default, default);
			effect.AddProperty("SmokeCount", 15, 1, 50);
			effect.AddProperty("OverlayBrightness", 0f, 0f, 100f);
			effect.AddProperty("SpreadMult", 1f, 0f, 2f);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.SetAnimation(4);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);

			_options.SpriteOverlayColor = effect.GetProperty<GrfColor>("SpriteOverlayColor");
			_options.SmokeColor = effect.GetProperty<GrfColor>("SmokeColor");
			_options.SmokeCount = effect.GetProperty<int>("SmokeCount");
			_options.OverlayBrightness = effect.GetProperty<float>("OverlayBrightness");
			_options.SpreadMult = effect.GetProperty<float>("SpreadMult");
		}

		public override void OnBackupCommand(EffectConfiguration effect) {
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));
			_smokeData.Clear();
		}

		private Dictionary<int, List<(int x, int y, float scale, TkVector2 dir)>> _smokeData = new Dictionary<int, List<(int x, int y, float scale, TkVector2 dir)>>();
		private SpriteIndex _smokeSprIndex;

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			base.OnPreviewProcessAction(act, action, aid);

			if (_smokeData.Count != 0)
				return;

			TkVector2 min = new TkVector2(float.MaxValue, float.MaxValue);
			TkVector2 max = new TkVector2(float.MinValue, float.MinValue);

			int bAid = aid / 8 * 8;
			Action actionB = act[bAid];

			actionB.AllFrames(frame => {
				var bb = ActImaging.Imaging.GenerateFrameBoundingBox(act, frame);

				min.X = Math.Min(min.X, bb.Min.X);
				min.Y = Math.Min(min.Y, bb.Min.Y);

				max.X = Math.Max(max.X, bb.Max.X);
				max.Y = Math.Max(max.Y, bb.Max.Y);
			});

			if (_smoke == null) {
				_smoke = new GrfImage(ApplicationManager.GetResource("smoke.png"));
			}

			min.X += _smoke.Width / 2 - 10;
			max.X -= _smoke.Width / 2;
			min.Y += _smoke.Height / 2 - 20;
			max.Y -= _smoke.Height / 2;
			min = min * _options.SpreadMult;
			max = max * _options.SpreadMult;

			float rangeX = max.X - min.X;
			float rangeY = max.Y - min.Y;

			_smokeData[0] = new List<(int x, int y, float, TkVector2 dir)>();
			_smokeData[1] = new List<(int x, int y, float, TkVector2 dir)>();
			_smokeData[2] = new List<(int x, int y, float, TkVector2 dir)>();
			_smokeData[3] = new List<(int x, int y, float, TkVector2 dir)>();
			TkVector2 center = new TkVector2(-5, 0);
			int length = 30;

			TkVector2 a0;
			TkVector2 a1;
			TkVector2 a2;
			TkVector2 a3;

			a0 = new TkVector2(act[bAid, 0, 0].OffsetX, act[bAid, 0, 0].OffsetY);

			try {
				a1 = new TkVector2(act[bAid + 2, 0, 0].OffsetX, act[bAid + 2, 0, 0].OffsetY);
				a2 = new TkVector2(act[bAid + 4, 0, 0].OffsetX, act[bAid + 4, 0, 0].OffsetY);
				a3 = new TkVector2(act[bAid + 6, 0, 0].OffsetX, act[bAid + 6, 0, 0].OffsetY);
			}
			catch {
				a1 = a2 = a3 = a0;
			}

			for (int i = 0; i < _options.SmokeCount; i++) {
				float scale = (float)Math.Round((float)(_options.Rng.NextDouble() * 0.55f) + 0.65f, 2);
				int x = (int)(_options.Rng.NextDouble() * rangeX + min.X);
				int y = (int)(_options.Rng.NextDouble() * rangeY + min.Y);
				TkVector2 start = new TkVector2(x, y);
				TkVector2 dir = start - center;
				dir.Normalize();
				dir = dir * length;
				_smokeData[0].Add((x, y, scale, dir));

				var diff = a1 - a0;
				_smokeData[1].Add((x + (int)diff.X, y + (int)diff.Y, scale, dir));
				_smokeData[2].Add((-x - (int)diff.X, y + (int)diff.Y, scale, new TkVector2(-dir.X, dir.Y)));
				_smokeData[3].Add((-x, y, scale, new TkVector2(-dir.X, dir.Y)));
			}

			_smokeSprIndex = act.Sprite.InsertAny(_smoke);
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			int aid = act.Actions.IndexOf(action);
			animLength = animLength <= 0 ? action.Frames.Count : animLength;

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor.Value);

					ProcessLayer(act, layer, step, animLength);
				}

				foreach (var data in _smokeData[(aid % 8) / 2]) {
					Layer layer = new Layer(_smokeSprIndex);
					layer.ScaleX = data.scale;
					layer.ScaleY = data.scale;
					layer.Color = new GrfColor(_options.SmokeColor);
					if (mult < 0.5)
						layer.Color.A = (byte)(layer.Color.A * 2 * mult);
					else
						layer.Color.A = (byte)(layer.Color.A * (1f - 2 * (mult - 0.5f)));
					layer.OffsetX = data.x + (int)(mult * data.dir.X);
					layer.OffsetY = data.y + (int)(mult * data.dir.Y);
					frame.Layers.Add(layer);
				}
			}
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			var sprIndex = layer.SprSpriteIndex;

			if (sprIndex.Valid) {
				SpriteIndex newSpriteIndex;

				if (!_transformedSprites.TryGetValue((sprIndex, step), out newSpriteIndex)) {
					var copyImage = act.Sprite.GetImage(sprIndex).Copy();
					copyImage.Convert(GrfImageType.Bgra32);
					newSpriteIndex = act.Sprite.InsertAny(copyImage);
					GrfImage image = act.Sprite.GetImage(newSpriteIndex);
					ProcessImage(image, step, animLength);
					_transformedSprites[(sprIndex, step)] = newSpriteIndex;
				}

				layer.SprSpriteIndex = newSpriteIndex;
				layer.Color = new GrfColor((byte)(_options.SpriteOverlayColor.A * (1f - (float)step / (animLength - 1))), _options.SpriteOverlayColor.R, _options.SpriteOverlayColor.G, _options.SpriteOverlayColor.B);

				PostProcessLayer(act, layer, step, animLength);
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			byte min = 255;
			byte max = 0;

			for (int i = 0; i < img.Pixels.Length; i += 4) {
				for (int j = 0; j < 3; j++) {
					if (img.Pixels[i + j] < min)
						min = img.Pixels[i + j];
					if (img.Pixels[i + j] > min)
						max = img.Pixels[i + j];
				}
			}

			float range = max - min;
			float l = _options.OverlayBrightness;

			for (int i = 0; i < img.Pixels.Length; i += 4) {
				if (img.Pixels[i + 3] == 0)
					continue;

				var b = Math.Max(Math.Max(img.Pixels[i + 0], img.Pixels[i + 1]), img.Pixels[i + 2]);

				b = (byte)Math.Min(255, b / range * (255 - 70) + 70);

				if (l > 0)
					b = (byte)Math.Min(255, b + (255 - b) / 255f * l);

				img.Pixels[i + 0] = b;
				img.Pixels[i + 1] = b;
				img.Pixels[i + 2] = b;
			}
		}

		public override void EnsureFrameCount(Action action, int animStart, int animLength, bool loopMissingFrames) {
			base.EnsureFrameCount(action, animStart, animLength, false);
		}
	}
}
