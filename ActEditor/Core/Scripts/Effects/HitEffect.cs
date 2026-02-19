using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts.Effects {
	public class HitEffect : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public float ScaleDistance;
			public float ScaleEffect;
			public int Distortion;
		}

		private EffectOptions _options = new EffectOptions();

		public HitEffect() : base("Classic hit animation") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("ScaleDistance", 1, 0f, 5f);
			effect.AddProperty("ScaleEffect", 1, 0f, 5f);
			effect.AddProperty("Distortion", 1, 1, 5);

			_animationComponent.DefaultSaveData.SetAnimation(3);
			_animationComponent.DefaultSaveData.SetLayers(0);
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.ScaleDistance = effect.GetProperty<float>("ScaleDistance");
			_options.ScaleEffect = effect.GetProperty<float>("ScaleEffect");
			_options.Distortion = effect.GetProperty<int>("Distortion");
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = 5;
			int aid = act.Actions.IndexOf(action);
			int baseAction = aid % 8;
			float[] scales = { 1f, 1.15f, 1.09f, 1.03f, 1f };

			if (action.Frames.Count == 1 && animStart == 0) {
				TkVector2[] offsets = new TkVector2[8] {
					new TkVector2(4, -9),
					new TkVector2(4, -9),
					new TkVector2(4, -4),
					new TkVector2(4, -4),
					new TkVector2(-4, -4),
					new TkVector2(-4, -4),
					new TkVector2(-4, -9),
					new TkVector2(-4, -9)
				};

				var offset = offsets[baseAction] * _options.ScaleDistance;

				Frame frame0 = new Frame(action.Frames[0]);

				frame0.Translate((int)offset.X, (int)offset.Y);
				action.Frames.Add(new Frame(frame0));
				action.Frames.Add(new Frame(frame0));
				action.Frames.Add(new Frame(frame0));
				action.Frames.Add(new Frame(frame0));
			}

			EnsureFrameCount(action, animStart, animLength, _loopMissingFrames);

			for (int i = animStart + 1; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);
				List<Layer> toAdd = new List<Layer>();

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor.Value);

					Layer nLayer = null;

					for (int k = 0; k < _options.Distortion; k++) {
						if (nLayer == null)
							nLayer = new Layer(layer);
						else
							nLayer = new Layer(nLayer);

						nLayer.Scale(scales[i - animStart] * _options.ScaleEffect, scales[i - animStart] * _options.ScaleEffect);
						nLayer.Color = new GrfColor((byte)(0.58d * nLayer.Color.A), nLayer.Color.R, nLayer.Color.G, nLayer.Color.B);

						toAdd.Add(nLayer);
					}
				}

				foreach (var layer in toAdd)
					frame.Layers.Add(layer);
			}
		}

		public override string Group => "Effects/Hit";
		public override string InputGesture => "{Dialog.AnimationReceivingHit}";
		public override string Image => "effect_hit.png";

		#endregion
	}
}
