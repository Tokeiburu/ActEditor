using TokeiLibrary.Paths;
using Utilities;

namespace ActEditor.ApplicationConfiguration {
	/// <summary>
	/// Class imported from GrfEditor
	/// </summary>
	public static class PathRequest {
		public static Setting ExtractSetting {
			get { return new Setting(null, typeof (ActEditorConfiguration).GetProperty("ExtractingServiceLastPath")); }
		}

		public static Setting SaveAdvancedSetting {
			get { return new Setting(null, typeof (ActEditorConfiguration).GetProperty("SaveAdvancedLastPath")); }
		}

		public static string SaveFileEditor(params string[] extra) {
			return TkPathRequest.SaveFile(new Setting(null, typeof (ActEditorConfiguration).GetProperty("AppLastPath")), extra);
		}

		public static string SaveFolderGarment(params string[] extra) {
			return TkPathRequest.SaveFile(new Setting(null, typeof(ActEditorConfiguration).GetProperty("GarmentSavePath")), extra);
		}

		public static string SaveFileExtract(params string[] extra) {
			return TkPathRequest.SaveFile(ExtractSetting, extra);
		}

		public static string OpenFileEditor(params string[] extra) {
			return TkPathRequest.OpenFile(new Setting(null, typeof (ActEditorConfiguration).GetProperty("AppLastPath")), extra);
		}

		public static string OpenGrfFile(params string[] extra) {
			return TkPathRequest.OpenFile(new Setting(null, typeof (ActEditorConfiguration).GetProperty("AppLastGrfPath")), extra);
		}

		public static string OpenFileExtract(params string[] extra) {
			return TkPathRequest.OpenFile(ExtractSetting, extra);
		}

		public static string[] OpenFilesExtract(params string[] extra) {
			return TkPathRequest.OpenFiles(ExtractSetting, extra);
		}

		public static string FolderEditor(params string[] extra) {
			return TkPathRequest.Folder(new Setting(null, typeof (ActEditorConfiguration).GetProperty("AppLastPath")), extra);
		}

		public static string FolderExtract(params string[] extra) {
			return TkPathRequest.Folder(ExtractSetting);
		}

		public static string FolderSaveAdvanced(params string[] extra) {
			return TkPathRequest.Folder(SaveAdvancedSetting);
		}
	}
}