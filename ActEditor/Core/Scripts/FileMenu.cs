using ActEditor.Core.WPF.Dialogs;
using GRF.FileFormats.ActFormat;
using TokeiLibrary;

namespace ActEditor.Core.Scripts {
	public class SpriteExport : IActScript {
		public SpriteExport() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		#region IActScript Members

		public object DisplayName {
			get { return "__IndexOverride,12__%Export all sprites..."; }
		}

		public string Group {
			get { return "File"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "export.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			var dialog = new ExportSpriteDialog(ActEditorWindow.Instance);
			dialog.Owner = WpfUtilities.TopWindow;
			dialog.Show();
			IsEnabled = false;
			dialog.Closed += delegate { 
				IsEnabled = true;
				dialog.Owner.Focus();
			};
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return IsEnabled;
		}

		#endregion
	}
}