using System;
using System.Collections.Generic;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace Scripts {
    public class Script : IActScript {
		public object DisplayName {
			get { return "Change selected layers' color"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return "Ctrl-Shift-W"; }
		}
		
		public string Image {
			get { return "pal.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			var frame = act[selectedActionIndex, selectedFrameIndex];
			
			selectedLayerIndexes = _getSelection(frame, selectedLayerIndexes);
			
			if (selectedLayerIndexes.Length == 0) {
				ErrorHandler.HandleException("No layers found.", ErrorLevel.Warning);
				return;
			}
			
			GrfColor startColor = act[selectedActionIndex, selectedFrameIndex, selectedLayerIndexes[0]].Color;
			ColorPicker.PickerDialog picker = new ColorPicker.PickerDialog(startColor.ToColor(), ColorPicker.Core.ColorMode.Hue);
			picker.Owner = WpfUtilities.TopWindow;
			
			var initialLayers = _backupLayers(frame, selectedLayerIndexes);
			
			picker.PickerControl.ColorChanged += (s, args) => _previewUpdate(act, frame, selectedLayerIndexes, args.Value.ToGrfColor());

			picker.ShowDialog();
			
			_restoreLayers(frame, selectedLayerIndexes, initialLayers);

			if (picker.DialogResult) {
				try {
					act.Commands.Begin();
					
					GrfColor newColor = picker.PickerControl.SelectedColor.ToGrfColor();

					foreach (int layerIndex in selectedLayerIndexes) {
						act.Commands.SetColor(selectedActionIndex, selectedFrameIndex, layerIndex, newColor);
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
			
			act.InvalidateVisual();
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
		
		private int[] _getSelection(Frame frame, int[] selectedLayerIndexes) {
			if (selectedLayerIndexes.Length == 0) {
				selectedLayerIndexes = new int[frame.NumberOfLayers];
				
				for (int i = 0; i < selectedLayerIndexes.Length; i++) {
					selectedLayerIndexes[i] = i;
				}
			}
			
			return selectedLayerIndexes;
		}
		
		private Dictionary<int, Layer> _backupLayers(Frame frame, int[] selectedLayerIndexes) {
			var initialLayers = new Dictionary<int, Layer>();
			
			foreach (int layerIndex in selectedLayerIndexes) {
				initialLayers[layerIndex] = new Layer(frame[layerIndex]);
			}
			
			return initialLayers;
		}
		
		private void _previewUpdate(Act act, Frame frame, int[] selectedLayerIndexes, GrfColor color) {
			foreach (int layerIndex in selectedLayerIndexes) {
				frame[layerIndex].Color = color;
			}
			
			act.InvalidateVisual();
		}
		
		private void _restoreLayers(Frame frame, int[] selectedLayerIndexes, Dictionary<int, Layer> initialLayers) {
			foreach (int layerIndex in selectedLayerIndexes) {
				frame.Layers[layerIndex] = initialLayers[layerIndex];
			}
		}
	}
}
