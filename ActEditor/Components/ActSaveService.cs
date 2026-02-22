using ActEditor.ApplicationConfiguration;
using ActEditor.Core;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using GRF.GrfSystem;
using GRF.Image;
using GrfToWpfBridge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;
using Utilities.Extension;
using Imaging = ActImaging.Imaging;

namespace ActEditor.Components {
	public class ActSaveService {
		public class SaveFormat {
			public string Name;
			public string Filter;
			public SaveMode Mode;
			public string RequiredExtension;
			public string AnyExtension;

			public string[] GetRequiredExtensions() {
				return RequiredExtension.Replace("*", "").Split(';');
			}

			public string[] GetAnyExtensions() {
				return AnyExtension.Replace("*", "").Split(';');
			}
		}

		public class SaveContext {
			public TabAct Tab;
			public string FilePath;
			public SaveMode Mode;
		}

		public class SaveResult {
			public string NewFilePath;
			public bool AddToRecentFiles;
			public bool IsNewCleared;
		}

		public enum SaveMode {
			ActAndSpr,
			ActSprPal,
			ActOnly,
			PaletteOnly,
			SpriteOnly,
			Gif,
			Image
		}

		public string ResolveInitialPath(TabAct tab) {
			var fileName = ActEditorConfiguration.AppLastPath;

			if (Path.GetFileNameWithoutExtension(fileName) != Path.GetFileNameWithoutExtension(tab.Act.LoadedPath)) {
				fileName = tab.Act.LoadedPath;
			}

			return fileName;
		}

		private List<SaveFormat> _saveFormats = new List<SaveFormat> {
			new SaveFormat { Name = "Act and Spr files", Filter = "*.act;*.spr", Mode = SaveMode.ActAndSpr, RequiredExtension = "*.act;*.spr" },
			new SaveFormat { Name = "Act, Spr and Pal files", Filter = "*.act;*.spr;*.pal", Mode = SaveMode.ActSprPal, RequiredExtension = "*.act" },
			new SaveFormat { Name = "Animation files", Filter = "*.act", Mode = SaveMode.ActOnly, RequiredExtension = "*.act", AnyExtension = "*.act" },
			new SaveFormat { Name = "Palette files", Filter = "*.pal", Mode = SaveMode.PaletteOnly, RequiredExtension = "*.pal", AnyExtension = "*.pal" },
			new SaveFormat { Name = "Sprite files", Filter = "*.spr", Mode = SaveMode.SpriteOnly, RequiredExtension = "*.spr", AnyExtension = "*.spr" },
			new SaveFormat { Name = "Gif files", Filter = "*.gif", Mode = SaveMode.Gif, RequiredExtension = "*.gif", AnyExtension = "*.gif" },
			new SaveFormat { Name = "Image files", Filter = "*.bmp;*.png;*.jpg;*.tga", Mode = SaveMode.Image, RequiredExtension = "*.bmp;*.png;*.jpg;*.tga", AnyExtension = "*.bmp;*.png;*.jpg;*.tga" },
		};

		public SaveContext CreateSaveContext(TabAct tab) {
			if (tab.Act == null)
				return null;

			string fileName = ResolveInitialPath(tab);

			string file = TkPathRequest.SaveFile<ActEditorConfiguration>("AppLastPath",
				"fileName", fileName,
				"filter", Methods.Aggregate(_saveFormats.Select(p => p.Name + "|" + p.Filter).ToList(), "|"));
			if (file == null) return null;

			var dialog = TkPathRequest.LatestSaveFileDialog;

			return new SaveContext {
				Tab = tab,
				FilePath = file,
				Mode = ResolveSaveMode(file, dialog.FilterIndex - 1)
			};
		}

		public SaveMode ResolveSaveMode(string file, int filterIndex) {
			if (filterIndex < 0 || filterIndex >= _saveFormats.Count) {
				throw new Exception("Unable to find a matching save mode.");
			}

			var format = _saveFormats[filterIndex];

			if (file.IsExtension(format.GetRequiredExtensions())) {
				return format.Mode;
			}

			// If not a direct match, fallback to extension rather than the selected filter index
			for (int i = 0; i < _saveFormats.Count; i++) {
				format = _saveFormats[i];

				if (string.IsNullOrEmpty(format.AnyExtension))
					continue;
				if (file.IsExtension(format.GetAnyExtensions()))
					return format.Mode;
			}

			throw new Exception("File extension does not match the save mode.");
		}

		public SaveResult ExecuteSave(SaveContext sc) {
			switch (sc.Mode) {
				case SaveMode.ActAndSpr:
					return _saveActAndSpr(sc);
				case SaveMode.ActSprPal:
					return _saveActSprPal(sc);
				case SaveMode.ActOnly:
					return _saveActOnly(sc);
				case SaveMode.PaletteOnly:
					return _savePalOnly(sc);
				case SaveMode.SpriteOnly:
					return _saveSprOnly(sc);
				case SaveMode.Gif:
					return _saveGif(sc);
				case SaveMode.Image:
					return _saveImage(sc);
				default:
					throw new InvalidOperationException("Unknown save mode.");
			}
		}

		private SaveResult _saveActAndSpr(SaveContext sc) {
			var act = sc.Tab.Act;
			var actPath = sc.FilePath.ReplaceExtension(".act");

			act.SaveWithSprite(actPath);
			act.LoadedPath = actPath;
			act.Commands.SaveCommandIndex();

			return new SaveResult {
				AddToRecentFiles = true,
				IsNewCleared = true,
				NewFilePath = actPath,
			};
		}

		private SaveResult _saveActSprPal(SaveContext sc) {
			var act = sc.Tab.Act;
			var actPath = sc.FilePath.ReplaceExtension(".act");

			act.SaveWithSprite(actPath);
			File.WriteAllBytes(actPath.ReplaceExtension(".pal"), act.Sprite.Palette.BytePalette);
			act.LoadedPath = actPath;
			act.Commands.SaveCommandIndex();

			return new SaveResult {
				AddToRecentFiles = true,
				IsNewCleared = true,
				NewFilePath = actPath,
			};
		}

		private SaveResult _saveActOnly(SaveContext sc) {
			var act = sc.Tab.Act;
			var actPath = sc.FilePath.ReplaceExtension(".act");

			act.Save(actPath);

			return new SaveResult {
				AddToRecentFiles = true,
				NewFilePath = actPath,
			};
		}

		private SaveResult _savePalOnly(SaveContext sc) {
			var act = sc.Tab.Act;
			File.WriteAllBytes(sc.FilePath, act.Sprite.Palette.BytePalette);

			return new SaveResult();
		}

		private SaveResult _saveSprOnly(SaveContext sc) {
			var act = sc.Tab.Act;
			act.Sprite.Save(sc.FilePath.ReplaceExtension(".spr"));

			return new SaveResult();
		}

		private SaveResult _saveGif(SaveContext sc) {
			var act = sc.Tab.Act;
			var tab = sc.Tab;

			try {
				for (int i = 0; i < act.Sprite.NumberOfIndexed8Images; i++) {
					act.Sprite.Images[i].Palette[3] = 0;
				}

				GifSavingDialog dialog = new GifSavingDialog(act, tab.SelectedAction);
				dialog.Owner = WpfUtilities.TopWindow;

				if (ActEditorConfiguration.ActEditorGifHideDialog || dialog.ShowDialog() == true) {
					TaskDialog task = new TaskDialog("Saving as gif...", "app.ico", "Processing frames, please wait...");
					task.ShowFooter(true);
					task.Start(isCancelling => {
						try {
							List<Act> back = new List<Act>();
							List<Act> front = new List<Act>();

							foreach (var reference in tab.References.Where(reference => reference.ShowReference)) {
								if (reference.Mode == ZMode.Back)
									back.Insert(0, reference.Act);
								else
									front.Add(reference.Act);
							}

							Imaging.SaveAsGif(sc.FilePath, Act.MergeAct(back.ToArray(), act, front.ToArray()), tab.SelectedAction, task, dialog.Dispatch(() => dialog.Extra));
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err);
						}
					}, () => task.Progress);
					task.ShowDialog();
				}
			}
			finally {
				for (int i = 0; i < act.Sprite.NumberOfIndexed8Images; i++) {
					act.Sprite.Images[i].Palette[3] = 255;
				}
			}

			return new SaveResult();
		}

		private SaveResult _saveImage(SaveContext sc) {
			var act = sc.Tab.Act;
			var tab = sc.Tab;

			var imgSource = Imaging.GenerateImage(act, tab.SelectedAction, tab.SelectedFrame);
			PngBitmapEncoder encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(Imaging.ForceRender(imgSource, BitmapScalingMode.NearestNeighbor)));

			using (MemoryStream stream = new MemoryStream()) {
				encoder.Save(stream);

				byte[] data = new byte[stream.Length];
				stream.Seek(0, SeekOrigin.Begin);
				stream.Read(data, 0, data.Length);

				GrfImage grfImage = new GrfImage(data);
				grfImage.Save(sc.FilePath);
			}

			return new SaveResult();
		}

		private bool _isInGrf(Act act) {
			TkPath path = new TkPath(act.LoadedPath);

			return !string.IsNullOrEmpty(path.RelativePath);
		}

		public SaveResult Save(Act act) {
			SaveResult result;

			if (_isInGrf(act)) {
				result = _saveToGrf(act);
			}
			else {
				result = _saveToFileSystem(act);
			}

			if (act.Sprite.RleSaveError) {
				if (ActEditorConfiguration.ShowErrorRleDowngrade) {
					try {
						WindowProvider.WindowOpened += _errorWindowOpened;
						ErrorHandler.HandleException("Some of your sprite image sizes are too large and will not load properly. The SPR file format has been downgraded to 0x200 to avoid losing data, however these sprites will not load properly ingame.\r\n\r\nTo fix this issue, reduce your sprite image sizes. You can also convert your images as Bgra32 instead as this format has no size restriction.");
					}
					finally {
						WindowProvider.WindowOpened -= _errorWindowOpened;
					}
				}
			}

			return new SaveResult {
				IsNewCleared = true,
			};
		}

		private SaveResult _saveToGrf(Act act) {
			TkPath path = new TkPath(act.LoadedPath);

			if (Methods.IsFileLocked(path.FilePath)) {
				throw new Exception("The file " + path.FilePath + " is locked by another process. Try closing other GRF applicactions or use the 'Save as...' option.");
			}

			using (GrfHolder grf = new GrfHolder(path.FilePath)) {
				string temp = TemporaryFilesManager.GetTemporaryFilePath("to_grf_{0:0000}");

				act.Sprite.Save(temp + ".spr");
				act.Save(temp + ".act");

				grf.Commands.AddFile(path.RelativePath.ReplaceExtension(".act"), File.ReadAllBytes(temp + ".act"));
				grf.Commands.AddFile(path.RelativePath.ReplaceExtension(".spr"), File.ReadAllBytes(temp + ".spr"));

				grf.QuickSave();

				if (!grf.CancelReload) {
					act.Commands.SaveCommandIndex();
				}
			}

			return new SaveResult {
				IsNewCleared = true,
			};
		}

		private SaveResult _saveToFileSystem(Act act) {
			act.Sprite.Save(act.LoadedPath.ReplaceExtension(".spr"));
			act.Save();
			act.Commands.SaveCommandIndex();

			return new SaveResult {
				IsNewCleared = true,
			};
		}

		private void _errorWindowOpened(TkWindow window) {
			ErrorDialog dialog = window as ErrorDialog;

			if (dialog == null)
				return;

			dialog.Loaded += delegate {
				var grid = (Grid)dialog.Content;
				var footer = (Grid)((Grid)grid.Children[grid.Children.Count - 1]).Children[0];
				var cb = new CheckBox { Content = "Do not show again", Margin = new Thickness(3) };
				WpfUtilities.AddMouseInOutUnderline(cb);
				cb.SetValue(Grid.ColumnProperty, 1);
				cb.HorizontalAlignment = HorizontalAlignment.Left;
				cb.VerticalAlignment = VerticalAlignment.Center;
				footer.Children.Add(cb);

				Binder.Bind(cb, () => !ActEditorConfiguration.ShowErrorRleDowngrade, v => ActEditorConfiguration.ShowErrorRleDowngrade = !v, null, true);
			};
		}
	}
}
