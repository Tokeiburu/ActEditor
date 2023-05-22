using System.Windows;

namespace ActEditor.Core.WPF.FrameEditor {
	public class ReadonlyFrameRenderer : FrameRenderer {
		public ReadonlyFrameRenderer() {
			_gridZoom.HorizontalAlignment = HorizontalAlignment.Left;
			_gridZoom.VerticalAlignment = VerticalAlignment.Bottom;
		}

		public override void Init(IFrameRendererEditor editor) {
			base.Init(editor);

			Edit.EnableEdit = false;
		}
	}
}
