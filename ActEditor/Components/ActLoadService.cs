using GRF.Core;
using GRF.FileFormats.ActFormat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using Utilities.Extension;

namespace ActEditor.Components {
	public class ActLoadService {
		public class LoadResult {
			public bool AddToRecentFiles;
			public bool RemoveToRecentFiles;
			public string FilePath;
			public bool Success;
			public string ErrorMessage;
			public Act LoadedAct;

			public static LoadResult Fail(string message, string path) {
				LoadResult result = new LoadResult();
				result.Success = false;
				result.ErrorMessage = message;
				result.FilePath = path;
				result.RemoveToRecentFiles = true;
				return result;
			}
		}

		public void LoadFromGrf(TkPath file) {

		}

		public LoadResult Load(TkPath file) {
			if (file.FilePath.IsExtension(".act") || String.IsNullOrEmpty(file.RelativePath)) {
				return _loadFromFileSystem(file.FilePath);
			}
			else {
				return _loadFromGrf(file);
			}
		}

		private LoadResult _loadFromFileSystem(string file) {
			if (!file.IsExtension(".act"))
				return LoadResult.Fail("Invalid file extension; only .act files are allowed.", file);

			if (!File.Exists(file))
				return LoadResult.Fail("File not found while trying to open the Act.\r\n\r\n" + file, file);

			if (!File.Exists(file.ReplaceExtension(".spr")))
				return LoadResult.Fail("File not found: " + file.ReplaceExtension(".spr"), file);

			LoadResult result = new LoadResult();
			result.FilePath = file;
			result.AddToRecentFiles = true;
			result.LoadedAct = new Act(file, file.ReplaceExtension(".spr"));
			result.LoadedAct.LoadedPath = file;
			result.Success = true;
			return result;
		}

		private LoadResult _loadFromGrf(TkPath file) {
			if (!File.Exists(file.FilePath))
				return LoadResult.Fail("GRF path not found.", file);

			LoadResult result = new LoadResult();
			result.FilePath = file.GetFullPath();
			result.AddToRecentFiles = true;

			TkPath sprPath = new TkPath(file);
			sprPath.RelativePath = sprPath.RelativePath.ReplaceExtension(".spr");

			byte[] dataAct = null;
			byte[] dataSpr = null;

			using (GrfHolder grf = new GrfHolder(file.FilePath)) {
				if (grf.FileTable.ContainsFile(file.RelativePath))
					dataAct = grf.FileTable[file.RelativePath].GetDecompressedData();

				if (grf.FileTable.ContainsFile(file.RelativePath.ReplaceExtension(".spr")))
					dataSpr = grf.FileTable[file.RelativePath.ReplaceExtension(".spr")].GetDecompressedData();
			}

			if (dataAct == null)
				return LoadResult.Fail("File not found: " + file, file);

			if (dataSpr == null)
				return LoadResult.Fail("File not found: " + sprPath, file);

			result.LoadedAct = new Act(dataAct, dataSpr);
			result.LoadedAct.LoadedPath = file.GetFullPath();
			result.Success = true;
			return result;
		}
	}
}
