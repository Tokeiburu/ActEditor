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
			
			var dialog = new InputDialog("Enter the expand magnitude.", "Expand script", Configuration.ConfigAsker["[ActEditor - Explode value]", "2"]);
			dialog.Owner = WpfUtilities.TopWindow;

			if (dialog.ShowDialog() != true) {
				return;
			}

			float explode;

			if (!float.TryParse(dialog.Input.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out explode)) {
				ErrorHandler.HandleException("The expand value is not valid. Only float values are allowed.", ErrorLevel.Warning);
				return;
			}
			
			Configuration.ConfigAsker["[ActEditor - Explode value]"] = dialog.Input;
			
			try {
				act.Commands.Begin();
				
				for (int layerIndex = 0; layerIndex < selectedLayerIndexes.Length; layerIndex++) {
					Layer layer = act[selectedActionIndex, selectedFrameIndex, selectedLayerIndexes[layerIndex]];
					act.Commands.SetOffsets(selectedActionIndex, selectedFrameIndex, selectedLayerIndexes[layerIndex],
						(int) (layer.OffsetX * explode), 
						(int) (layer.OffsetY * explode));
				}
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
			}
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
