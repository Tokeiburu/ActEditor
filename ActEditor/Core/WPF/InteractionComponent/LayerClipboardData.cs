using GRF.FileFormats.ActFormat;
using System;
using System.Diagnostics;
using System.Linq;
using Utilities.Extension;

namespace ActEditor.Core.WPF.InteractionComponent {
	[Serializable]
	public class LayerClipboardData {
		public Layer[] Layers;
		public string SourceActPath;
		public string SourceSprPath;
		public int ProcessId;

		public LayerClipboardData(Layer[] layers, Act act) {
			Layers = layers.Select(p => new Layer(p)).ToArray();
			SourceActPath = act.LoadedPath;
			SourceSprPath = act.LoadedPath.ReplaceExtension(".spr");
			ProcessId = Process.GetCurrentProcess().Id;
		}
	}
}
