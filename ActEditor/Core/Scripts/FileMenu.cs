using System;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities.Services;

namespace ActEditor.Core.Scripts {
	public class SpriteExportNormal : IActScript {
		public SpriteExportNormal() {
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
			if (act == null) return;

			try {
				string path = PathRequest.FolderExtract();

				if (path != null) {
					string name = "image_{0:0000}";

					int i = 0;
					int numberOfImages = act.Sprite.NumberOfImagesLoaded;

					TaskManager.DisplayTaskC("Export", "Exporting sprites...", () => i, numberOfImages, isCancelling => {
						int count = act.Sprite.NumberOfImagesLoaded;

						for (; i < count; i++) {
							var im = act.Sprite.Images[i];

							if (im.GrfImageType == GrfImageType.Indexed8) {
								im.Save(GrfPath.Combine(path, String.Format(name, i) + ".bmp"));
							}
							else {
								im.Save(GrfPath.Combine(path, String.Format(name, i) + ".png"));
							}
						}
					});

					OpeningService.FileOrFolder(path);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class SpriteExport : IActScript {
		public SpriteExport() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		#region IActScript Members

		public object DisplayName {
			get { return "__IndexOverride,13__%Export all sprites (adv)..."; }
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