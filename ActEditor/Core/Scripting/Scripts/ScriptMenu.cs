using System;
using System.IO;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using TokeiLibrary;
using Utilities.Services;

namespace ActEditor.Core.Scripting.Scripts {
	public class ScriptRunnerMenu : IActScript {
		public ScriptRunnerMenu() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			var dialog = new ScriptRunnerDialog();
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

		public object DisplayName {
			get { return "Script Runner..."; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "{ActEditor.OpenScriptRunner|Ctrl-R}"; }
		}

		public string Image {
			get { return "dos.png"; }
		}

		#endregion
	}

	public class OpenScriptsFolder : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				if (!Directory.Exists(path))
					Directory.CreateDirectory(path);

				OpeningService.FileOrFolder(path);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return true;
		}

		public object DisplayName {
			get { return "Open scripts folder"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "newFolder.png"; }
		}

		#endregion
	}

	public class ReloadScripts : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				ActEditor.ScriptLoader.RecompileScripts();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return true;
		}

		public object DisplayName {
			get { return "Reload scripts"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "refresh.png"; }
		}

		#endregion
	}

	public class BatchScriptMenu : IActScript {
		public BatchScriptMenu() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			var dialog = new BatchScriptDialog(ActEditorWindow.Instance);
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

		public object DisplayName {
			get { return "Batch script..."; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "empty.png"; }
		}

		#endregion
	}
}