using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GRF.Image.Decoders;
using GRF.Graphics;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;

namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Remove unused sprites"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return "Ctrl-Shift-D"; }
		}
		
		public string Image {
			get { return "delete.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			int count = 0;
			
			try {
				act.Commands.Begin();
				act.Commands.Backup(_ => {
					count = act.Sprite.Images.Count;
					
					for (int i = act.Sprite.Images.Count - 1; i >= 0 ; i--) {
						if (act.FindUsageOf(i).Count == 0) {
							act.Sprite.Remove(i, act, EditOption.AdjustIndexes);
						}
					}
				}, "Remove unused sprites", true);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
				
				count = count - act.Sprite.Images.Count;
				
				if (count == 0) {
					ErrorHandler.HandleException("No sprites were removed.", ErrorLevel.NotSpecified);
				}
				else {
					ErrorHandler.HandleException("Removed " + count + " sprite(s).", ErrorLevel.NotSpecified);
				}
			}
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
