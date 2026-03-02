using GRF.Image;
using PaletteEditor;
using System.Collections.Generic;
using Utilities;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteEditorState {
		public int SelectedSpriteIndex;
		public GrfImage SelectedImage => SpriteViewer.GetImage(SelectedSpriteIndex);
		public GrfImage LoadedImage => SpriteViewer.LoadedImage;
		public SpriteBrush Brush;
		public SpriteViewer SpriteViewer;
		public GrfImage EditingImage;
		public bool IsModified;
		public bool IsEditing;
		public SingleColorEditControl SingleEditor;
		public GradientColorEditControl GradientEditor;
		public bool IsGradientEditorSelected;
		public bool IsSingleEditorSelected;
		public List<int> StampLock = new List<int>();

		public delegate void InvalidateImageEventHandler(GrfImage image);

		public event InvalidateImageEventHandler ImageInvalidated;

		public void InvalidateImage(GrfImage image) {
			ImageInvalidated?.Invoke(image);
		}

		public void ResetImage() {
			if (SelectedImage != null)
				ImageInvalidated?.Invoke(SelectedImage);
		}
	}
}
