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
			if (selectedLayerIndexes.Length == 0) {
				selectedLayerIndexes = new int[act[selectedActionIndex, selectedFrameIndex].NumberOfLayers];
				
				for (int i = 0; i < selectedLayerIndexes.Length; i++) {
					selectedLayerIndexes[i] = i;
				}
			}
			
			if (act == null) return;
			
			var effect = new EffectConfiguration("AddFrames");
			effect.AddProperty("Magnitude", 2f, 0f, 10f);
			
			effect.Apply(actInput => {
				float magnitude = effect.GetProperty<float>("Magnitude");
				
				try {
					actInput.Commands.Begin();
					
					for (int layerIndex = 0; layerIndex < selectedLayerIndexes.Length; layerIndex++) {
						Layer layer = actInput[selectedActionIndex, selectedFrameIndex, selectedLayerIndexes[layerIndex]];
						actInput.Commands.SetOffsets(selectedActionIndex, selectedFrameIndex, selectedLayerIndexes[layerIndex],
							(int) (layer.OffsetX * magnitude), 
							(int) (layer.OffsetY * magnitude));
					}
				}
				catch (Exception err) {
					actInput.Commands.CancelEdit();
					ErrorHandler.HandleException(err, ErrorLevel.Warning);
				}
				finally {
					actInput.Commands.End();
				}
			});
			effect.ActIndexSelectorReadonly = true;
			effect.AutoPlay = false;
			effect.Display(act, selectedActionIndex);
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
