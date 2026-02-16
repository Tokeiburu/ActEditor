using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using ActEditor.Core.Scripts;

namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Expand"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "Ctrl-Shift-E"; }
		}

		public string Image {
			get { return "expand.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			var effect = new EffectConfiguration("Expand");
			var animationComponent = new AnimationEditComponent(act, AnimationEditTypes.TargetOnly);
			animationComponent.DefaultSaveData.AddEmptyFrame = false;
			animationComponent.DefaultSaveData.LoopFrames = false;
			animationComponent.DefaultSaveData.AllLayers = true;
			animationComponent.DefaultSaveData.AllAnimations = true;
			effect.AddProperty("AnimationData", animationComponent, null, null);
			animationComponent.SetEffectProperty(effect.Properties["AnimationData"]);
			animationComponent.LoadProperty();
			effect.AddProperty("Magnitude", 2f, 0f, 10f);

			effect.Apply(actInput => {
				float magnitude = effect.GetProperty<float>("Magnitude");
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
							layer.OffsetX = (int)(layer.OffsetX * magnitude);
							layer.OffsetY = (int)(layer.OffsetY * magnitude);
						}
					}
				});
			});
			effect.AutoPlay = false;
			effect.Display(act, selectedActionIndex);
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
