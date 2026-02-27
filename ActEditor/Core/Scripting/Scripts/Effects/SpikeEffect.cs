using GRF.FileFormats.ActFormat;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class SpikeErosionEffect : ImageProcessingEffect {
		public override string Group => @"Effects/Dead";
		public override string Image => "effect_spike_erosion.png";

		public class EffectOptions {
			public float TargetX;
			public float TargetY;
			public int SpikesPerFrame;
			public float SpikeHalfAngle;
			public float SpikeRatioLength;
			public float SpikeStart;
			public Func<float, float> D0;
			public Func<float, float> D1;
			public int RandomSeed = 1234;
		}

		private EffectOptions _options = new EffectOptions();
		private ActIndex _index;
		private List<bool[,]> _cutMasks;

		public SpikeErosionEffect() : base("Spike erosion") {
			_inputGesture = "{Dialog.AnimationSpikeErosion}";
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("TargetX", 0.5f, 0.0f, 1.0f);
			effect.AddProperty("TargetY", 0.5f, 0.0f, 1.0f);
			effect.AddProperty("End color", new GrfColor(255, 0, 0, 0), default, default);

			effect.AddProperty("SpikesPerFrame", 80, 1, 200);
			effect.AddProperty("SpikeHalfAngle", 24f, 0f, 180f);
			effect.AddProperty("SpikeRatioLength", 0.1f, 0f, 1f);
			effect.AddProperty("SpikeStart", 0f, 0f, 1f);

			_animationComponent.DefaultSaveData.SetAnimation(4);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.TargetX = effect.GetProperty<float>("TargetX");
			_options.TargetY = effect.GetProperty<float>("TargetY");

			_options.SpikesPerFrame = effect.GetProperty<int>("SpikesPerFrame");
			_options.SpikeHalfAngle = (float)Math.PI / effect.GetProperty<float>("SpikeHalfAngle");
			_options.SpikeRatioLength = effect.GetProperty<float>("SpikeRatioLength");
			_options.SpikeStart = 1f - effect.GetProperty<float>("SpikeStart");
			_options.D0 = t => _options.SpikeStart - t - _options.SpikeRatioLength;
			_options.D1 = t => _options.SpikeStart - t + _options.SpikeRatioLength;

			TargetColor = effect.GetProperty<GrfColor>("End color");
		}

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			base.OnPreviewProcessAction(act, action, aid);
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = animLength <= 0 ? action.Frames.Count : animLength;
			int aid = act.Actions.IndexOf(action);

			_cutMasks = new List<bool[,]>();
			int maxLayerCount = action.Frames.Max(p => p.Layers.Count);

			for (int i = 0; i < maxLayerCount; i++) {
				int w = 0;
				int h = 0;

				for (int j = animStart; j < animStart + animLength; j++) {
					Frame frame = action[j];

					if (i < frame.Layers.Count) {
						var image = act.Sprite.GetImage(frame.Layers[i]);

						if (image != null) {
							w = Math.Max(w, image.Width);
							h = Math.Max(h, image.Height);
						}
					}
				}

				_cutMasks.Add(new bool[w, h]);
			}

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					_index = new ActIndex() { ActionIndex = aid, FrameIndex = i, LayerIndex = layerIndex };

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor.Value);

					ProcessLayer(act, layer, step, animLength);
				}
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			// Todo: align the mask with the layer's position
			bool[,] cutMask = _cutMasks[_index.LayerIndex];

			//int w = img.Width;
			//int h = img.Height;
			int w = Math.Min(img.Width, cutMask.GetLength(0));
			int h = Math.Min(img.Height, cutMask.GetLength(1));

			int targetX = (int)(_options.TargetX * img.Width);
			int targetY = (int)(_options.TargetY * img.Height);

			float t = (float)(step + 1) / (totalSteps + 1);

			float D0 = _options.D0(t);
			float D1 = _options.D1(t);

			var rnd = new Random(_options.RandomSeed + step);

			for (int s = 0; s < _options.SpikesPerFrame; s++) {
				float r = Lerp(D1, D0, (float)rnd.NextDouble());
				float angle = (float)(rnd.NextDouble() * Math.PI * 2);

				float dirX = (float)Math.Cos(angle);
				float dirY = (float)Math.Sin(angle);

				int px = (int)(targetX + dirX * r * w);
				int py = (int)(targetY + dirY * r * h);

				CarveCone(cutMask, px, py, dirX, dirY, _options.SpikeHalfAngle);
			}

			ApplyMask(img, cutMask);
		}

		public float Lerp(float p1, float p2, float rng) {
			if (p1 > p2)
				return Lerp(p2, p1, rng);

			float diff = p2 - p1;
			if (diff == 0)
				return p1;
			diff = rng % diff;
			return p1 + diff;
		}

		public void CarveCone(bool[,] mask, int px, int py, float dirX, float dirY, float halfAngle) {
			int w = mask.GetLength(0);
			int h = mask.GetLength(1);

			float cosLimit = (float)Math.Cos(halfAngle);

			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					if (mask[x, y])
						continue;

					float vx = x - px;
					float vy = y - py;

					float len = (float)Math.Sqrt(vx * vx + vy * vy);
					if (len < 1e-3f)
						continue;

					vx /= len;
					vy /= len;

					float dot = vx * dirX + vy * dirY;

					if (dot > cosLimit) {
						mask[x, y] = true;
					}
				}
			}
		}
	}
}
