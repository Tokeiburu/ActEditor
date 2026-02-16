using System;
using System.Globalization;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using TokeiLibrary;
using TokeiLibrary.WPF;
using ActEditor.Core.Scripts;

namespace Scripts {
	public class Script : IActScript {
		public const int Magnify = 2;

		public object DisplayName {
			get { return "Magnify"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "Ctrl-Shift-M"; }
		}

		public string Image {
			get { return "scale.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			var effect = new EffectConfiguration("Magnify script");
			var animationComponent = new AnimationEditComponent(act, AnimationEditTypes.TargetOnly);
			animationComponent.DefaultSaveData.AddEmptyFrame = false;
			animationComponent.DefaultSaveData.LoopFrames = false;
			animationComponent.DefaultSaveData.AllLayers = true;
			animationComponent.DefaultSaveData.AllAnimations = true;
			effect.AddProperty("AnimationData", animationComponent, null, null);
			animationComponent.SetEffectProperty(effect.Properties["AnimationData"]);
			animationComponent.LoadProperty();
			effect.AddProperty("ScaleX", 2f, 0, 10f);
			effect.AddProperty("ScaleY", 2f, 0, 10f);
			effect.AddProperty("Anchors", true, false, true);

			effect.Apply(actInput => {
				float scaleX = effect.GetProperty<float>("ScaleX");
				float scaleY = effect.GetProperty<float>("ScaleY");
				bool anchors = effect.GetProperty<bool>("Anchors");
				var animationsIndexes = animationComponent.SaveData.Animations;
				var layersIndexes = animationComponent.SaveData.Layers;

				actInput.AllActions((action, aid) => {
					if (!animationsIndexes.Contains(aid / 8))
						return;

					foreach (var frame in action) {
						for (int i = 0; i < frame.Layers.Count; i++) {
							if (!layersIndexes.Contains(i))
								continue;

							var layer = frame[i];
							layer.OffsetX = (int)Math.Round(layer.OffsetX * scaleX, MidpointRounding.AwayFromZero);
							layer.OffsetY = (int)Math.Round(layer.OffsetY * scaleY, MidpointRounding.AwayFromZero);
							layer.ScaleX *= scaleX;
							layer.ScaleY *= scaleY;
						}

						if (anchors) {
							foreach (var anchor in frame.Anchors) {
								anchor.OffsetX = (int)Math.Round(anchor.OffsetX * scaleX, MidpointRounding.AwayFromZero);
								anchor.OffsetY = (int)Math.Round(anchor.OffsetY * scaleY, MidpointRounding.AwayFromZero);
							}
						}
					}
				});
			});
			effect.Display(act, selectedActionIndex);
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
