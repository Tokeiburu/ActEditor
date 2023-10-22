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
using GRF.Core;
using GRF.IO;
using GRF.System;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Utilities;
using Utilities.Extension;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;

namespace Scripts {
    public class Script : IActScript {
		public object DisplayName {
			get { return "Character palette sheet"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return "{Scripts.PaletteSheet}"; }
		}
		
		public string Image {
			get { return "busy.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			var dialog = new ActEditor.Tools.PaletteSheetGenerator.PreviewSheetDialog();
			dialog.Owner = WpfUtilities.TopWindow;
			dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			dialog.Closed += delegate {
				dialog.Owner.Focus();
			};
			dialog.ShowDialog();
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return true;
		}
	}
}
