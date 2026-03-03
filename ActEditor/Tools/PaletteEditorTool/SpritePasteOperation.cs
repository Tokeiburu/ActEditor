using GRF.Image;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Utilities;
using static ActEditor.Tools.PaletteEditorTool.SelectionTool;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpritePasteOperation {
		private SelectionTool _selectionTool;
		private ImageViewer _imageViewer;
		public GrfImage Image;
		public Point StartMousePosition;
		public Point CurrentMousePosition;
		public Point StartPosition;
		public Point Position;
		public Rect Selection => new Rect(Position.X, Position.Y, Image.Width, Image.Height);
		public SelectionStage Stage = SelectionStage.None;
		public bool IsActive => Stage != SelectionStage.None;
		private MouseButtonState _leftMouse = MouseButtonState.Released;
		private MouseButtonState _rightMouse = MouseButtonState.Released;

		public SpritePasteOperation(SelectionTool selectionTool, ImageViewer imageViewer) {
			_selectionTool = selectionTool;
			_imageViewer = imageViewer;
		}

		public void BeginPaste(SpriteEditorState state) {
			var image = Clipboard.GetImage();

			if (image == null)
				return;

			// Cancel all current tasks before applying the paste
			_selectionTool.Cancel(state);
			Image = ConvertToGrfImage(image, state);

			StartPosition = new Point(0, 0);
			Position = new Point(0, 0);
			UpdateViewer();
			Stage = SelectionStage.ReadyMove;
		}

		public void MovePaste(SpriteEditorState state, int x, int y) {
			if (Mouse.LeftButton != MouseButtonState.Pressed)
				return;

			if (Stage == SelectionStage.ReadyMove) {
				StartMousePosition = new Point(x, y);
				Stage = SelectionStage.Moving;

				if (!state.SpriteViewer.IsMouseCaptured)
					state.SpriteViewer.CaptureMouse();
			}

			if (Stage == SelectionStage.Moving) {
				CurrentMousePosition = new Point(x, y);

				var mouseMove = CurrentMousePosition - StartMousePosition;
				Position = StartPosition + mouseMove;
				UpdateViewer();
			}
		}

		public void EndMovePaste(SpriteEditorState state, int x, int y) {
			StartPosition = Position;
			Stage = SelectionStage.ReadyMove;

			// Check if left mouse button was released
			if (_leftMouse == MouseButtonState.Pressed && !_imageViewer.Selection.IsMouseWithin(x, y)) {
				ApplyPaste(state);
				Cancel();
			}
		}

		public void ApplyPaste(SpriteEditorState state) {
			Stage = SelectionStage.None;

			var selection = Selection;
			var imageClipRect = Selection;
			imageClipRect.Intersect(new Rect(0, 0, _imageViewer.ImageWidth, _imageViewer.ImageHeight));
			
			if (imageClipRect.Width <= 0 || imageClipRect.Height <= 0)
				return;

			var pasteImage = Image.Extract((int)(imageClipRect.X - selection.X), (int)(imageClipRect.Y - selection.Y), (int)imageClipRect.Width, (int)imageClipRect.Height);

			var editingImage = state.SelectedImage.Copy();
			editingImage.SetPixelsUnrestricted((int)imageClipRect.X, (int)imageClipRect.Y, pasteImage, true);

			if (!Methods.ByteArrayCompare(editingImage.Pixels, state.SelectedImage.Pixels)) {
				state.SpriteEditorControl.Sprite.Palette.Commands.StoreAndExecute(new ImageModifiedCommand(state.SpriteEditorControl.Sprite, state.SelectedSpriteIndex, editingImage));
			}

			state.InvalidateImage(editingImage);
		}

		public void Process(SpriteEditorState state, int x, int y) {
			_leftMouse = Mouse.LeftButton;
			_rightMouse = Mouse.RightButton;

			Cursor target = null;

			if (Stage == SelectionStage.ReadyMove) {
				if (_imageViewer.Selection.IsMouseWithin(x, y)) {
					target = Cursors.SizeAll;
					MovePaste(state, x, y);
				}
			}
			else if (Stage == SelectionStage.Moving) {
				MovePaste(state, x, y);
			}

			Mouse.OverrideCursor = target;
		}

		public void UpdateViewer() {
			_imageViewer.Selection.Set(Selection);
			_imageViewer.SetOverlayOperation(Position, Image.Cast<BitmapSource>());
		}

		public GrfImage ConvertToGrfImage(BitmapSource image, SpriteEditorState state) {
			if (image.Format != PixelFormats.Bgra32)
				return null;

			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(image));

			using (MemoryStream stream = new MemoryStream()) {
				encoder.Save(stream);

				stream.Seek(0, SeekOrigin.Begin);
				byte[] imData = new byte[stream.Length];
				stream.Read(imData, 0, imData.Length);

				var grfImage = new GrfImage(imData);

				bool isAllTransparency = true;

				for (int y = 0; y < grfImage.Height; y++) {
					for (int x = 0; x < grfImage.Width; x++) {
						if (!grfImage.IsPixelTransparent(x, y)) {
							isAllTransparency = false;
							break;
						}
					}
				}

				if (isAllTransparency) {
					for (int y = 0; y < grfImage.Height; y++) {
						for (int x = 0; x < grfImage.Width; x++) {
							grfImage.Pixels[4 * (y * grfImage.Width + x) + 3] = 255;
						}
					}
				}

				grfImage.MakeColorTransparent(GrfColors.Pink);
				grfImage.MakeColorTransparent(GrfColor.FromByteArray(state.SpriteEditorControl.Sprite.Palette.BytePalette, 0, GrfImageType.Indexed8));
				grfImage.Convert(GrfImageType.Indexed8, state.SpriteEditorControl.Sprite.Palette.BytePalette);
				return grfImage;
			}
		}

		public void Cancel() {
			Stage = SelectionStage.None;
			_imageViewer.ClearOverlayOperation();
			_imageViewer.Selection.Clear();
		}

		public void MoveSelection(int x, int y) {
			if (Stage == SelectionStage.ReadyMove) {
				Position += new Vector(x, y);
				StartPosition = Position;
				UpdateViewer();
			}
		}
	}
}
