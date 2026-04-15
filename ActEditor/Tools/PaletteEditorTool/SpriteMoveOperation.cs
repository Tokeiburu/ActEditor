using GRF.Image;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Utilities;
using static ActEditor.Tools.PaletteEditorTool.SelectionTool;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteMoveOperation {
		private SelectionTool _selectionTool;
		private ImageViewer _imageViewer;
		public SelectionStage Stage = SelectionStage.None;
		public Point StartMousePosition;
		public Point CurrentMousePosition;
		public Rect StartSelection;
		public Rect Selection;
		public Rect StartImageOffsetRect;
		public Rect ImageOffsetRect;
		private MouseButtonState _leftMouse;
		private MouseButtonState _rightMouse;
		public GrfImage SelectionImage;
		public GrfImage OutputImage;
		public bool IsActive => Stage != SelectionStage.None;

		public SpriteMoveOperation(SelectionTool selectionTool, ImageViewer imageViewer) {
			_selectionTool = selectionTool;
			_imageViewer = imageViewer;
		}

		public void BeginSelect(SpriteEditorState state, int x, int y) {
			if (_leftMouse != MouseButtonState.Pressed)
				return;

			if (!state.SpriteViewer.IsMouseCaptured)
				state.SpriteViewer.CaptureMouse();

			StartMousePosition = new Point(x, y);
			CurrentMousePosition = StartMousePosition;

			Selection = new Rect(StartMousePosition, CurrentMousePosition);
			Stage = SelectionStage.Select;
		}

		public void SetupSelect(SpriteEditorState state, int x, int y) {
			if (_leftMouse != MouseButtonState.Pressed)
				return;

			CurrentMousePosition = new Point(x, y);
			Selection = new Rect(StartMousePosition, CurrentMousePosition);
			Selection.Width++;
			Selection.Height++;
			UpdateViewer();
		}

		public void OnMouseUp(SpriteEditorState state, int x, int y) {
			if (_leftMouse != MouseButtonState.Pressed)
				return;

			if (Stage == SelectionStage.ReadyMove && !_imageViewer.Selection.IsMouseWithin(x, y)) {
				Apply(state);
				SelectionImage = null;
				Cancel(state);
				return;
			}

			if (Stage == SelectionStage.Select) {
				if (Selection.Width > 0 && Selection.Height > 0)
					Stage = SelectionStage.ReadyMove;
				else
					Stage = SelectionStage.None;
			}

			if (Stage == SelectionStage.Moving) {
				Stage = SelectionStage.ReadyMove;
			}

			StartSelection = Selection;
			StartImageOffsetRect = ImageOffsetRect;
		}

		public void Apply(SpriteEditorState state) {
			// The image has never been moved...
			if (SelectionImage == null)
				return;

			var selectionImage = SelectionImage;
			SelectionImage = null;

			// Clip selection image to image area
			var imageOffsetRect = ImageOffsetRect;
			imageOffsetRect.Intersect(new Rect(0, 0, _imageViewer.ImageWidth, _imageViewer.ImageHeight));

			var outputImage = OutputImage;

			if (imageOffsetRect.Width > 0 && imageOffsetRect.Height > 0) {
				var pasteImage = selectionImage.Extract((int)(imageOffsetRect.X - ImageOffsetRect.X), (int)(imageOffsetRect.Y - ImageOffsetRect.Y), (int)imageOffsetRect.Width, (int)imageOffsetRect.Height);
				outputImage.SetPixelsUnrestricted((int)imageOffsetRect.X, (int)imageOffsetRect.Y, pasteImage, true);
			}

			if (!Methods.ByteArrayCompare(outputImage.Pixels, state.SelectedImage.Pixels)) {
				state.SpriteEditorControl.Sprite.Palette.Commands.StoreAndExecute(new ImageModifiedCommand(state.SpriteEditorControl.Sprite, state.SelectedSpriteIndex, outputImage));
			}

			state.InvalidateImage(outputImage);
		}

		public void Process(SpriteEditorState state, int x, int y) {
			_leftMouse = Mouse.LeftButton;
			_rightMouse = Mouse.RightButton;

			Cursor target = null;

			if (Stage == SelectionStage.None) {
				BeginSelect(state, x, y);
			}
			else if (Stage == SelectionStage.Select) {
				SetupSelect(state, x, y);
			}
			if (Stage == SelectionStage.ReadyMove) {
				if (_imageViewer.Selection.IsMouseWithin(x, y)) {
					target = Cursors.SizeAll;
					BeginMoveSelection(state, x, y);
				}
				else if (_leftMouse == MouseButtonState.Pressed) {
					_selectionTool.Cancel(state);
				}
			}
			else if (Stage == SelectionStage.Moving) {
				MoveSelection(state, x, y);
			}

			Mouse.OverrideCursor = target;
		}

		public void MoveSelection(SpriteEditorState state, int x, int y) {
			if (_leftMouse != MouseButtonState.Pressed)
				return;

			CurrentMousePosition = new Point(x, y);

			var diff = CurrentMousePosition - StartMousePosition;
			Selection = new Rect(StartSelection.Left + diff.X, StartSelection.Top + diff.Y, StartSelection.Width, StartSelection.Height);
			ImageOffsetRect = new Rect(StartImageOffsetRect.Left + diff.X, StartImageOffsetRect.Top + diff.Y, StartImageOffsetRect.Width, StartImageOffsetRect.Height);
			UpdateViewer();
		}

		public void BeginMoveSelection(SpriteEditorState state, int x, int y) {
			if (_leftMouse != MouseButtonState.Pressed)
				return;

			StartMousePosition = new Point(x, y);
			CurrentMousePosition = StartMousePosition;

			// The moving layer hasn't been created yet, make it
			if (SelectionImage == null) {
				var image = state.SelectedImage.Copy();
				ImageOffsetRect = Selection;
				ImageOffsetRect.Intersect(new Rect(0, 0, _imageViewer.ImageWidth, _imageViewer.ImageHeight));

				SelectionImage = _imageViewer.Selection.GetClippedImage(image);
				OutputImage = state.SelectedImage.Copy();

				for (int yy = 0; yy < SelectionImage.Height; yy++) {
					for (int xx = 0; xx < SelectionImage.Width; xx++) {
						OutputImage.SetPixelTransparent(xx + (int)ImageOffsetRect.X, yy + (int)ImageOffsetRect.Y);
					}
				}

				_imageViewer.SetOverlayOperation(ImageOffsetRect.TopLeft, SelectionImage.Cast<BitmapSource>());
				state.InvalidateImage(OutputImage);
			}

			StartImageOffsetRect = ImageOffsetRect;
			StartSelection = Selection;
			Stage = SelectionStage.Moving;
		}

		public void Cancel(SpriteEditorState state) {
			if (SelectionImage != null) {
				Apply(state);
			}

			_imageViewer.ClearOverlayOperation();
			_imageViewer.Selection.Clear();
			SelectionImage = null;
			OutputImage = null;
			Stage = SelectionStage.None;
		}

		public void UpdateViewer() {
			_imageViewer.Selection.Set(Selection);

			if (Stage == SelectionStage.Moving)
				_imageViewer.SetOverlayOperation(ImageOffsetRect.TopLeft, null);
		}

		public void DoMoveSelection(SpriteEditorState state, int x, int y) {
			if (Stage == SelectionStage.ReadyMove) {
				_leftMouse = MouseButtonState.Pressed;
				BeginMoveSelection(state, 0, 0);
				MoveSelection(state, x, y);
				OnMouseUp(state, x, y);
				_leftMouse = MouseButtonState.Released;
				UpdateViewer();
			}
		}

		public void Delete(SpriteEditorState state) {
			if (SelectionImage == null) {
				_leftMouse = MouseButtonState.Pressed;
				BeginMoveSelection(state, 0, 0);
				_leftMouse = MouseButtonState.Released;
			}

			var outputImage = OutputImage.Copy();
			SelectionImage = null;

			if (!Methods.ByteArrayCompare(outputImage.Pixels, state.SelectedImage.Pixels)) {
				state.SpriteEditorControl.Sprite.Palette.Commands.StoreAndExecute(new ImageModifiedCommand(state.SpriteEditorControl.Sprite, state.SelectedSpriteIndex, outputImage));
			}

			state.InvalidateImage(outputImage);
			Cancel(state);
		}
	}
}
