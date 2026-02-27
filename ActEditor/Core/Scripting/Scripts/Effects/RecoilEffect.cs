using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using System;

namespace ActEditor.Core.Scripting.Scripts.Effects {

	public class RecoilEffect : ImageProcessingEffect {
		#region IActScript Members
		public class EffectOptions {
			public float Angle;
			public TkVector2 Pivot;
			public int Ease;
			public bool ReverseAnimation;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;

		public RecoilEffect() : base("Recoil effect") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Angle", 10f, 0f, 90f);
			effect.AddProperty("Pivot", new TkVector2(0, 0), new TkVector2(-100, 100), new TkVector2(-100, 100));
			effect.AddProperty("Ease", 50, -50, 50);
			effect.AddProperty("ReverseAnimation", false, false, true);

			_animationComponent.DefaultSaveData.AnimLength = 4;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.SetAnimation(2);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Angle = effect.GetProperty<float>("Angle");
			_options.Pivot = effect.GetProperty<TkVector2>("Pivot");
			_options.Ease = effect.GetProperty<int>("Ease");
			_options.ReverseAnimation = effect.GetProperty<bool>("ReverseAnimation");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / (animLength - 1);

			if (_options.ReverseAnimation) {
				if (t > 0.5f)
					t = (1 - t) * 2;
				else
					t *= 2;
			}

			var angle = _options.Angle;

			if (_status.Aid % 8 >= 4)
				angle *= -1;

			t = _easeMethod(t);
			angle *= t;

			TkVector2 pivot = _options.Pivot * new TkVector2(1f, -1f);
			TkVector2 centerSprite = new TkVector2(layer.OffsetX, layer.OffsetY);

			centerSprite.RotateZ(-angle, pivot);

			layer.OffsetX = (int)centerSprite.X;
			layer.OffsetY = (int)centerSprite.Y;
			layer.Rotation += (int)angle;
		}

		//public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
		//	int aid = act.Actions.IndexOf(action);
		//	int baseAction = aid % 8;
		//
		//	TkVector3[] animation;
		//
		//	TkVector3[] animationBottom = new TkVector3[] {
		//		new TkVector3(5, 0, 7),
		//		new TkVector3(8, 0, 10),
		//		new TkVector3(10, 1, 12),
		//		new TkVector3(12, 2, 14),
		//	};
		//
		//	TkVector3[] animationTop = new TkVector3[] {
		//		new TkVector3(4, 2, 7),
		//		new TkVector3(6, 3, 10),
		//		new TkVector3(7, 4, 12),
		//		new TkVector3(9, 5, 14),
		//	};
		//
		//	for (int i = animStart; i < animStart + animLength; i++) {
		//		Frame frame = action[i];
		//		int step = i - animStart;
		//		float mult = (float)step / (animLength - 1);
		//
		//		for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
		//			if (!IsLayerForProcess(layerIndex))
		//				continue;
		//
		//			var layer = frame[layerIndex];
		//
		//			if (TargetColor != null)
		//				ProcessColor(layer, mult, TargetColor);
		//
		//			if (baseAction <= 1 || baseAction >= 6)
		//				animation = animationBottom;
		//			else
		//				animation = animationTop;
		//
		//			int bAction = Methods.Clamp((int)(mult * animation.Length), 0, animation.Length - 1);
		//			int bActionNext = Methods.Clamp(bAction + 1, 0, animation.Length - 1);
		//
		//			var diff = mult - bAction / (float)animation.Length;
		//
		//			TkVector3 actionVector = (animation[bActionNext] - animation[bAction]) * diff + animation[bAction];
		//
		//			if (baseAction <= 1) {
		//
		//			}
		//			else if (baseAction <= 3)
		//				actionVector = new TkVector3(actionVector.X, actionVector.Y, actionVector.Z);
		//			else if (baseAction <= 5)
		//				actionVector = new TkVector3(-actionVector.X, actionVector.Y, -actionVector.Z);
		//			else
		//				actionVector = new TkVector3(-actionVector.X, actionVector.Y, -actionVector.Z);
		//
		//			layer.OffsetX += (int)(actionVector.X * _options.Scale.X);
		//			layer.OffsetY += (int)(actionVector.Y * _options.Scale.Y);
		//			layer.Rotation += (int)(actionVector.Z);
		//		}
		//	}
		//}

		public override string InputGesture => "{Dialog.AnimationRecoil}";
		public override string Image => "empty.png";
		public override string Group => "Effects/Hit";

		#endregion
	}
}
