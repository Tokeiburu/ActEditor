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
	public class SimpleAttackEffect : ImageProcessingEffect {
		#region IActScript Members
		public class EffectOptions {
			public TkVector2 Scale;
		}

		private EffectOptions _options = new EffectOptions();

		public SimpleAttackEffect() : base("Simple attack [Tokei]") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("ScaleX", 1f, 0f, 10f);
			effect.AddProperty("ScaleY", 1f, 0f, 10f);

			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.SetAnimation(2);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Scale = new TkVector2(effect.GetProperty<float>("ScaleX"), effect.GetProperty<float>("ScaleY"));
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			int aid = act.Actions.IndexOf(action);
			int baseAction = aid % 8;

			EnsureFrameCount(action, animStart, animLength, _loopMissingFrames);

			TkVector2[] animation = new TkVector2[] {
				new TkVector2(-5, 0),
				new TkVector2(-11, -1),
				new TkVector2(-16, -1),
				new TkVector2(-22, -2),
				new TkVector2(-26, -1),
				new TkVector2(-28, 0),
				new TkVector2(-31, 1),
				new TkVector2(-32, 2),
				new TkVector2(-33, 3),
				new TkVector2(-34, 4),
				new TkVector2(-36, 6),
				new TkVector2(-36, 6),
				new TkVector2(-35, 5),
				new TkVector2(-34, 5),
				new TkVector2(-17, 9),
				new TkVector2(-13, 4),
				new TkVector2(-9, 3),
				new TkVector2(-5, 3),
			};

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

					//TkVector2 actionVector = _options.Vector;
					//
					//if (baseAction <= 1)
					//	;
					//else if (baseAction <= 3)
					//	actionVector = new TkVector2(actionVector.X, -actionVector.Y);
					//else if (baseAction <= 5)
					//	actionVector = new TkVector2(-actionVector.X, -actionVector.Y);
					//else
					//	actionVector = new TkVector2(-actionVector.X, actionVector.Y);
					//
					//List<Layer> toAddSub = new List<Layer>();
					//
					//for (int k = 0; k < ColorCount; k++) {
					//	if (_options.Colors[k].A == 0)
					//		continue;
					//
					//	Layer nLayer = new Layer(layer);
					//	nLayer.OffsetX += (int)(actionVector.X * (k + 1));
					//	nLayer.OffsetY -= (int)(actionVector.Y * (k + 1));
					//	nLayer.Color = _options.Colors[k];
					//
					//	toAddSub.Add(nLayer);
					//}
					//
					//toAddSub.Reverse();
					//toAdd.AddRange(toAddSub);
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

		#endregion
	}
}
