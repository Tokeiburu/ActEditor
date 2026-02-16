using GRF.FileFormats.ActFormat;
using System;

namespace ActEditor.Core.Scripts.Effects {
	public class FloatingEffect : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public int Height;
			public int Ease;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;

		public FloatingEffect() : base("Floating effect") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Height", 15, 0, 100);
			effect.AddProperty("Ease", 10, -50, 50);

			_animationComponent.DefaultSaveData.SetAnimation(0, 1);
			_animationComponent.DefaultSaveData.SetLayers(0);
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Height = effect.GetProperty<int>("Height");
			_options.Ease = effect.GetProperty<int>("Ease");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / animLength;

			if (t > 0.5f)
				t = (1 - t) * 2;
			else
				t *= 2;

			t = _easeMethod(t);

			layer.OffsetY -= (int)(_options.Height * t);
		}

		public override string Group => "Effects/Idle";
		public override string InputGesture => "{Dialog.AnimationFloating}";
		public override string Image => "effect_float.png";

		#endregion
	}
}
