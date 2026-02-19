using GRF.FileFormats.ActFormat;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts.Effects {
	public class StrokeSilhouetteEffect : ImageProcessingEffect {
		#region IActScript Members
		public class EffectOptions {
			public float ScaleX;
			public float ScaleY;
			public GrfColor Color;
		}

		private EffectOptions _options = new EffectOptions();

		public StrokeSilhouetteEffect() : base("Stroke silhouette [Palooza]") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("ScaleX", 0.082f, 0f, 1f);
			effect.AddProperty("ScaleY", 0.035f, 0f, 1f);
			effect.AddProperty("Color", new GrfColor("0xFFAD5500"), default, default);

			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.ScaleX = effect.GetProperty<float>("ScaleX");
			_options.ScaleY = effect.GetProperty<float>("ScaleY");
			_options.Color = effect.GetProperty<GrfColor>("Color");
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = animLength <= 0 ? action.Frames.Count : animLength;

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);
				List<(int Index, Layer Layer)> toInsert = new List<(int, Layer)>();

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor.Value);

					var nLayer = new Layer(layer);
					nLayer.ScaleX += _options.ScaleX;
					nLayer.ScaleY += _options.ScaleY;
					nLayer.SetColor(_options.Color);
					toInsert.Add((layerIndex, nLayer));
				}

				foreach (var insert in toInsert.OrderByDescending(p => p.Index)) {
					frame.Layers.Insert(insert.Index, insert.Layer);
				}
			}
		}

		public override string InputGesture => "{Dialog.AnimationStrokeSilhouette}";
		public override string Image => "effect_stroke.png";
		public override string Group => "Effects/Global";

		#endregion
	}
}
