using GRF.FileFormats.ActFormat;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ActEditor.Core.Scripting {
	public class ScriptLoaderResult {
		public List<MenuItem> AddedMenuItems = new List<MenuItem>();
		public List<LoadScriptResult> Errors = new List<LoadScriptResult>();
	}

	public class LoadScriptResult {
		public bool Success { get; set; }
		public string ErrorMessage { get; set; }
		public EmitResult EmitResult { get; set; }
		public IActScript ActScript { get; set; }
		public string ScriptPath { get; set; }
		public string OriginalScriptPath { get; set; }
		public string OriginalDllPath { get; set; }

		public static LoadScriptResult Fail(string message, string scriptPath) {
			LoadScriptResult result = new LoadScriptResult();
			result.Success = false;
			result.ErrorMessage = message;
			result.ScriptPath = scriptPath;
			return result;
		}

		public static LoadScriptResult Fail(EmitResult emitResult, string scriptPath) {
			LoadScriptResult result = new LoadScriptResult();
			result.Success = false;
			result.ErrorMessage = "Failed to compile script.";
			result.EmitResult = emitResult;
			result.ScriptPath = scriptPath;
			return result;
		}
	}

	public class AddScriptResult {
		public bool Success { get; set; }
		public string ErrorMessage { get; set; }
		public MenuItem AddedMenuItem { get; set; }
	}
}
