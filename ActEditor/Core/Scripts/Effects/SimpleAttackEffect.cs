using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
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

			TkVector2[] animation;

			TkVector2[] animationBottom = new TkVector2[] {
				new TkVector2(-5, 0),
				new TkVector2(-11, -2),
				new TkVector2(-16, -2),
				new TkVector2(-22, -5),
				new TkVector2(-24, -5),
				new TkVector2(-26, -4),
				new TkVector2(-28, -3),
				new TkVector2(-32, 2),
				new TkVector2(-33, 3),
				new TkVector2(-34, 4),
				new TkVector2(-35, 5),
				new TkVector2(-37, 7),
				new TkVector2(-37, 7),
				new TkVector2(-36, 6),
				new TkVector2(-35, 6),
				new TkVector2(-33, 4),
				new TkVector2(-31, 3),
				new TkVector2(-26, 3),
				new TkVector2(-22, 6),
				new TkVector2(-17, 6),
				new TkVector2(-13, 1),
				new TkVector2(-9, 0),
				new TkVector2(0, 0),
			};

			TkVector2[] animationTop = new TkVector2[] {
				new TkVector2(-6, -4),
				new TkVector2(-12, -10),
				new TkVector2(-18, -15),
				new TkVector2(-25, -20),
				new TkVector2(-27, -21),
				new TkVector2(-30, -22),
				new TkVector2(-33, -24),
				new TkVector2(-36, -25),
				new TkVector2(-36, -25),
				new TkVector2(-37, -25),
				new TkVector2(-38, -25),
				new TkVector2(-38, -25),
				new TkVector2(-38, -25),
				new TkVector2(-39, -25),
				new TkVector2(-39, -25),
				new TkVector2(-39, -25),
				new TkVector2(-37, -26),
				new TkVector2(-34, -25),
				new TkVector2(-27, -19),
				new TkVector2(-19, -8),
				new TkVector2(-16, -12),
				new TkVector2(-11, -11),
				new TkVector2(-7, -6),
			};

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor);

					if (baseAction <= 1 || baseAction >= 6)
						animation = animationBottom;
					else
						animation = animationTop;

					int bAction = Methods.Clamp((int)(mult * animation.Length), 0, animation.Length - 1);
					int bActionNext = Methods.Clamp(bAction + 1, 0, animation.Length - 1);

					var diff = mult - bAction / (float)animation.Length;

					TkVector2 actionVector = (animation[bActionNext] - animation[bAction]) * diff + animation[bAction];

					if (baseAction <= 1) {

					}
					else if (baseAction <= 3)
						actionVector = new TkVector2(actionVector.X, actionVector.Y);
					else if (baseAction <= 5)
						actionVector = new TkVector2(-actionVector.X, actionVector.Y);
					else
						actionVector = new TkVector2(-actionVector.X, actionVector.Y);

					layer.OffsetX += (int)(actionVector.X * _options.Scale.X);
					layer.OffsetY += (int)(actionVector.Y * _options.Scale.Y);
				}
			}
		}

		public override string InputGesture => "{Dialog.AnimationSimpleAttack}";
		public override string Image => "empty.png";
		public override string Group => "Effects/Attack";

		#endregion
	}
}
