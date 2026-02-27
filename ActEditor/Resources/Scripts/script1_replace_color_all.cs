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
			get { return "Change all layers' color"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return "{Scripts.ChangeAllLayersColors}"; }
		}
		
		public string Image {
			get { return "pal.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			var frame = act[selectedActionIndex, selectedFrameIndex];
			
			GrfColor startColor;
			
			if (frame.Layers.Count == 0) {
				startColor = GrfColor.White;
			}
			else {
				startColor = act[selectedActionIndex, selectedFrameIndex, 0].Color;
			}
			
			ColorPicker.PickerDialog picker = new ColorPicker.PickerDialog(startColor.ToColor(), ColorPicker.Core.ColorMode.Hue);
			picker.Owner = WpfUtilities.TopWindow;
			
			var initialLayers = _backupLayers(frame);
			
			picker.PickerControl.ColorChanged += (s, args) => _previewUpdate(act, frame, args.Value.ToGrfColor());
			
			if (EffectConfiguration.SkipAndRememberInput > 0) {
				picker.Loaded += delegate {
					if (EffectConfiguration.SkipAndRememberInput == 1) {
						EffectConfiguration.SkipAndRememberInput = 2;
					}
					else {
						picker.PickerControl.SelectColor(new GrfColor(Configuration.ConfigAsker["[ActEditor - ReplaceColor - Color]"]).ToColor(), ColorPicker.Core.ColorMode.Current);
						picker.DialogResult = true;
						picker.Close();
					}
				};
			}
			
			picker.ShowDialog();
			
			_restoreLayers(frame, initialLayers);

			if (picker.DialogResult) {
				try {
					act.Commands.Begin();
					GrfColor newColor = picker.PickerControl.SelectedColor.ToGrfColor();
					Configuration.ConfigAsker["[ActEditor - ReplaceColor - Color]"] = newColor.ToHexString();
					act.Commands.SetColor(newColor);
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
		
		private List<Layer> _backupLayers(Frame frame) {
			var initialLayers = new List<Layer>();
			
			for (int i = 0; i < frame.Layers.Count; i++) {
				initialLayers.Add(new Layer(frame[i]));
			}
			
			return initialLayers;
		}
		
		private void _previewUpdate(Act act, Frame frame, GrfColor color) {
			foreach (Layer layer in frame) {
				layer.Color = color;
			}
			
			act.InvalidateVisual();
		}
		
		private void _restoreLayers(Frame frame, List<Layer> initialLayers) {
			for (int i = 0; i < frame.Layers.Count; i++) {
				frame.Layers[i] = initialLayers[i];
			}
		}
	}
}
