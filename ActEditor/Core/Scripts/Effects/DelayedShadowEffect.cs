using GRF.FileFormats.ActFormat;
using GRF.Image;
using System;

namespace ActEditor.Core.Scripts.Effects {
	public class DelayedShadowEffect : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public int SyncFrameTarget;
			public GrfColor Color;
		}

		private EffectOptions _options = new EffectOptions();

		public DelayedShadowEffect() : base("Delayed shadow effect") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("SyncFrameTarget", -1, -10, 10);
			effect.AddProperty("Color", new GrfColor(200, 0, 255, 255), default, default);

			_animationComponent.DefaultSaveData.SetAnimation(0, 1);
			_animationComponent.DefaultSaveData.SetLayers(0);
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.SyncFrameTarget = effect.GetProperty<int>("SyncFrameTarget");
			_options.Color = effect.GetProperty<GrfColor>("Color");
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			int targetFrame = _status.Fid + _options.SyncFrameTarget;
			int max = _status.Action.NumberOfFrames;
			targetFrame = (targetFrame % max + max) % max;

			var frame = _status.Action[targetFrame];

			if (_status.Lid >= frame.Layers.Count)
				return;

			Layer copy = new Layer(frame[_status.Lid]);
			copy.Color = _options.Color;

			_layersToInsert.Add((_status.Fid, _status.Lid, copy));
		}

		public override string Group => "Effects/Global";
		public override string InputGesture => "{Dialog.AnimationDelayedShadow}";
		public override string Image => "effect_shadow.png";

		#endregion
	}
}
