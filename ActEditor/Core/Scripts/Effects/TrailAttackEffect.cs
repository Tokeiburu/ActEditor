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
	public class TrailAttackEffect : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public TkVector2 Vector;
			public int TrailCount;
			public bool UseTrailColor;
			public GrfColor TrailColor;
			public float StartOpacity;
		}

		private EffectOptions _options = new EffectOptions();

		public TrailAttackEffect() : base("Trail attack") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("VectorX", -32, -50, 50);
			effect.AddProperty("VectorY", 18, -50, 50);
			effect.AddProperty("TrailCount", 4, 0, 10);
			effect.AddProperty("UseTrailColor", false, false, true);
			effect.AddProperty("TrailColor", new GrfColor(255, 150, 0, 0), null, null);
			effect.AddProperty("StartOpacity", 0.2f, 0.0f, 1.0f);

			_animationComponent.DefaultSaveData.SetAnimation(2);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Vector = new TkVector2(effect.GetProperty<int>("VectorX"), effect.GetProperty<int>("VectorY"));
			_options.TrailCount = effect.GetProperty<int>("TrailCount");
			_options.UseTrailColor = effect.GetProperty<bool>("UseTrailColor");
			_options.TrailColor = effect.GetProperty<GrfColor>("TrailColor");
			_options.StartOpacity = effect.GetProperty<float>("StartOpacity");
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

					layer.OffsetX += (int)actionVector.X;
					layer.OffsetY += (int)actionVector.Y;

					if (mult < 1) {
						List<Layer> toAddSub = new List<Layer>();

						for (int k = 0; k < _options.TrailCount; k++) {
							float mult2 = k / (float)_options.TrailCount;
							float opacity = (1f - _options.StartOpacity) / _options.TrailCount * k + _options.StartOpacity;

							opacity -= _options.StartOpacity * mult;

							Layer nLayer = new Layer(layer);
							nLayer.OffsetX = (int)(layer.OffsetX - (1 - mult) * actionVector.X * (1f - mult2));
							nLayer.OffsetY = (int)(layer.OffsetY - (1 - mult) * actionVector.Y * (1f - mult2));

							if (_options.UseTrailColor)
								nLayer.Color = new GrfColor((byte)(_options.TrailColor.A * opacity), _options.TrailColor.R, _options.TrailColor.G, _options.TrailColor.B);
							else
								nLayer.Color = new GrfColor((byte)(nLayer.Color.A * opacity), nLayer.Color.R, nLayer.Color.G, nLayer.Color.B);

							toAddSub.Add(nLayer);
						}

						if (baseAction >= 2 && baseAction <= 5) {
							toAddSub.Reverse();
						}

						toAdd.AddRange(toAddSub);
					}
				}

				int insertIndex = -1;

				if (baseAction >= 2 && baseAction <= 5) {
					for (int layerIndex = frame.Layers.Count - 1; layerIndex >= 0; layerIndex--) {
						if (!IsLayerForProcess(layerIndex))
							continue;
						insertIndex = layerIndex + 1;
						break;
					}
				}
				else {
					for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
						if (!IsLayerForProcess(layerIndex))
							continue;
						insertIndex = layerIndex;
						break;
					}
				}
				
				if (toAdd.Count > 0) {
					frame.Layers.InsertRange(insertIndex, toAdd);
				}
			}
		}

		public override string Group => "Effects/Attack";
		public override string InputGesture => "{Dialog.AnimationAttack}";
		public override string Image => "effect_hit.png";

		#endregion
	}
}
