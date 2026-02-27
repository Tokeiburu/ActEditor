using ActEditor.Tools.GrfShellExplorer;
using GRF.Core;
using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.GrfSystem;
using System.IO;
using TokeiLibrary;
using Utilities;
using Utilities.Extension;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteLoadAndSaveService {
		public class LoadResult {
			public bool AddToRecentFiles;
			public bool RemoveToRecentFiles;
			public bool UpdatePalette;
			public string FilePath;
			public bool Success;
			public string ErrorMessage;
			public Spr LoadedSpr;
			public Pal LoadedPal;

			public static LoadResult Fail(string message, string path) {
				LoadResult result = new LoadResult();
				result.Success = false;
				result.ErrorMessage = message;
				result.FilePath = path;
				result.RemoveToRecentFiles = true;
				return result;
			}
		}

		public LoadResult Load(TkPath file) {
			if (file.IsFile && file.FilePath.IsExtension(".grf", ".gpf", ".thor")) {
				return _loadFromGrf2(file);
			}

			if (file.IsContainer) {
				return _loadFromGrf(file);
			}
			else {
				return _loadFromFileSystem(file.FilePath);
			}
		}

		private LoadResult _loadFromFileSystem(string file) {
			if (!file.IsExtension(".pal", ".spr"))
				return LoadResult.Fail("Invalid file extension; only .pal or .spr files are allowed.", file);

			if (!File.Exists(file))
				return LoadResult.Fail("File not found: " + file, file);

			return _loadFromData(file, file, File.ReadAllBytes(file));
		}

		private LoadResult _loadFromGrf2(TkPath file) {
			GrfExplorer dialog = new GrfExplorer(file.FilePath, SelectMode.Pal);
			dialog.Owner = WpfUtilities.TopWindow;

			LoadResult result = new LoadResult();
			
			if (dialog.ShowDialog() == true) {
				string relativePath = dialog.SelectedItem;
				byte[] data = dialog.SelectedData;

				if (relativePath == null || data == null) {
					result.Success = false;
					return result;
				}

				if (!relativePath.IsExtension(".pal", ".spr")) {
					return LoadResult.Fail("Only PAL or SPR files can be selected.", file);
				}

				return _loadFromData(new TkPath(file, relativePath), relativePath, data);
			}

			result.Success = false;
			return result;
		}

		public LoadResult _loadFromGrf(TkPath file) {
			if (!File.Exists(file.FilePath))
				return LoadResult.Fail("GRF path not found.", file);

			byte[] data = null;

			using (GrfHolder grf = new GrfHolder(file.FilePath)) {
				if (grf.FileTable.ContainsFile(file.RelativePath))
					data = grf.FileTable[file.RelativePath].GetDecompressedData();
			}

			if (data == null)
				return LoadResult.Fail("File not found: " + file, file);

			return _loadFromData(file, file.RelativePath, data);
		}

		private LoadResult _loadFromData(TkPath file, string filePath, byte[] data) {
			LoadResult result = new LoadResult();
			result.FilePath = file.GetFullPath();
			result.AddToRecentFiles = true;

			if (file.IsFile) {
				if (file.FilePath.StartsWith(Settings.TempPath))
					result.AddToRecentFiles = false;
			}

			if (filePath.IsExtension(".spr")) {
				result.LoadedSpr = new Spr(data);

				if (result.LoadedSpr.NumberOfIndexed8Images <= 0)
					return LoadResult.Fail("The sprite file does not contain a palette (probably because it doesn't have any Indexed8 images). You must add one for a palette to be created.", file);

				result.Success = true;
				return result;
			}
			else if (filePath.IsExtension(".pal")) {
				result.LoadedPal = new Pal(data);
				result.LoadedPal.BytePalette[3] = 0;
				result.UpdatePalette = true;
				result.Success = true;
				return result;
			}
			else {
				return LoadResult.Fail("File format not supported: " + file, file);
			}
		}

		public class SaveResult {
			public string FilePath;
			public bool Success;
			public string ErrorMessage;

			public static SaveResult Fail(string message, string path) {
				SaveResult result = new SaveResult();
				result.Success = false;
				result.ErrorMessage = message;
				result.FilePath = path;
				return result;
			}
		}

		public SaveResult Save(string file, Spr spr) {
			if (spr == null)
				return SaveResult.Fail("Cannot save while there is no sprite loaded.", file);

			switch(file.GetExtension()) {
				case ".pal":
					_saveAsPal(file, spr.Palette);
					break;
				case ".spr":
					_saveAsSpr(file, spr);
					break;
				default:
					return SaveResult.Fail("Unknown type format requested: " + file.GetExtension(), file);
			}

			SaveResult result = new SaveResult();
			result.Success = true;
			return result;
		}

		private void _saveAsSpr(string file, Spr spr) {
			try {
				spr.Palette.EnableRaiseEvents = false;
				spr.Palette.MakeFirstColorUnique();
				spr.Palette[3] = 255;

				spr.Save(file);
				spr.Palette[3] = 0;
			}
			finally {
				spr.Palette.EnableRaiseEvents = true;
			}
		}

		private void _saveAsPal(string file, Pal pal) {
			try {
				pal.EnableRaiseEvents = false;
				pal.MakeFirstColorUnique();
				pal[3] = 255;

				pal.Save(file.ReplaceExtension(".pal"));
				pal[3] = 0;
			}
			finally {
				pal.EnableRaiseEvents = true;
			}
		}
	}
}
