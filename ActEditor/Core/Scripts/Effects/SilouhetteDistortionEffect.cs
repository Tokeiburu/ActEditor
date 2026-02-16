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
	public class SilouhetteDistortionEffect : ImageProcessingEffect {
		#region IActScript Members
		public class EffectOptions {
			public TkVector2 Vector;
			public GrfColor[] Colors;
		}

		private EffectOptions _options = new EffectOptions();
		public const int ColorCount = 3;

		public SilouhetteDistortionEffect() : base("Silouhette distortion [Tokei]") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("VectorX", 1, -5, 5);
			effect.AddProperty("VectorY", 1, -5, 5);
			effect.AddProperty("Color1", new GrfColor(180, 255, 0, 0), null, null);
			effect.AddProperty("Color2", new GrfColor(0, 0, 255, 0), null, null);
			effect.AddProperty("Color3", new GrfColor(0, 0, 0, 255), null, null);

			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Vector = new TkVector2(effect.GetProperty<int>("VectorX"), effect.GetProperty<int>("VectorY"));
			_options.Colors = new GrfColor[ColorCount];

			for (int i = 1; i <= ColorCount; i++) {
				_options.Colors[i - 1] = effect.GetProperty<GrfColor>("Color" + i);
			}
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			int aid = act.Actions.IndexOf(action);
			int baseAction = aid % 8;

			EnsureFrameCount(action, animStart, animLength, _loopMissingFrames);

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);
				List<Layer> toAdd = new List<Layer>();

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor);

					TkVector2 actionVector = _options.Vector;

					if (baseAction <= 1) {

					}
					else if (baseAction <= 3)
						actionVector = new TkVector2(actionVector.X, -actionVector.Y);
					else if (baseAction <= 5)
						actionVector = new TkVector2(-actionVector.X, -actionVector.Y);
					else
						actionVector = new TkVector2(-actionVector.X, actionVector.Y);

					List<Layer> toAddSub = new List<Layer>();

					for (int k = 0; k < ColorCount; k++) {
						if (_options.Colors[k].A == 0)
							continue;

						Layer nLayer = new Layer(layer);
						nLayer.OffsetX += (int)(actionVector.X * (k + 1));
						nLayer.OffsetY -= (int)(actionVector.Y * (k + 1));
						nLayer.Color = _options.Colors[k];

						toAddSub.Add(nLayer);
					}

					toAddSub.Reverse();
					toAdd.AddRange(toAddSub);
				}

				int insertIndex = -1;

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;
					insertIndex = layerIndex;
					break;
				}

				if (toAdd.Count > 0) {
					frame.Layers.InsertRange(insertIndex, toAdd);
				}
			}
		}

		public override string InputGesture => "{Dialog.AnimationStrokeSilouhette}";
		public override string Image => "effect_stroke.png";
		public override string Group => "Effects/Global";

		#endregion
	}
}
