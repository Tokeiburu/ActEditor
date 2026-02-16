using System;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Image;

namespace Scripts {
    public class Script : IActScript {
		public object DisplayName {
			get { return "Sample"; }
		}
		
		public string Group {
			get { return "Custom Scripts"; }
		}
		
		public string InputGesture {
			get { return "Ctrl-Alt-Shift-A"; }
		}
		
		public string Image {
			get { return "settings.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			try {
				act.Commands.Begin();
				act.Commands.Backup(_ => {
					act.AllLayers(layer => {
						layer.OffsetX += 0;
						layer.OffsetY += 0;
						layer.ScaleX *= 1;
						layer.ScaleY *= 1;
						layer.SpriteIndex = layer.SpriteIndex;
						layer.SpriteType = layer.SpriteType;
						layer.Color = new GrfColor(layer.Color.A, layer.Color.R, layer.Color.G, layer.Color.B);
						layer.Mirror = layer.Mirror;
						layer.Rotation = layer.Rotation;
					});
				}, "Sample script", true);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
