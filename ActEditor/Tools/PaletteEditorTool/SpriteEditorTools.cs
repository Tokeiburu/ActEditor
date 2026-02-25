using ErrorManager;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;

namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteEditorTool {
		public Cursor Cursor;
		public FancyButton ToggleButton;

		public SpriteEditorTool(FancyButton button) {
			ToggleButton = button;
		}

		public SpriteEditorTool() {

		}

		public virtual void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			
		}

		public void BeginEdit(SpriteEditorState state, ref GrfImage image) {
			// Start editing image
			if (state.EditingImage == null)
				state.EditingImage = image.Copy();

			image = state.EditingImage;
			state.IsEditing = true;

			if (!state.SpriteViewer.IsMouseCaptured)
				state.SpriteViewer.CaptureMouse();
		}

		public bool IsWithin(GrfImage image, int x, int y) {
			return x >= 0 && x < image.Width && y >= 0 && y < image.Height;
		}

		public virtual void Unselect(SpriteEditorState state) {
			if (ToggleButton != null)
				ToggleButton.IsPressed = false;
		}

		public virtual void Select(SpriteEditorState state) {
			if (ToggleButton != null)
				ToggleButton.IsPressed = true;
		}

		public void StampLock(SpriteEditorState state, bool isLocked) {
			try {
				if (!isLocked) {
					state.StampLock.Clear();
					state.GradientEditor.PaletteSelector.GenerateUsedPalette(new bool[256]);
					return;
				}

				state.StampLock = state.GradientEditor.PaletteSelector.SelectedItems.ToList();

				bool[] used = new bool[256];
				for (int i = 0; i < 256; i++) {
					if (!state.StampLock.Contains(i))
						used[i] = true;
				}
				state.GradientEditor.PaletteSelector.GenerateUsedPalette(used);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}
	}

	public class BucketTool : SpriteEditorTool {
		public BucketTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_bucket.png"), Width = 16, Height = 16 }, new Point() { X = 14, Y = 15 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			GrfImage image = state.SelectedImage;

			if (Mouse.LeftButton != MouseButtonState.Pressed)
				return;

			if (state.SingleEditor.PaletteSelector.SelectedItems.Count != 1)
				throw new Exception("You must select 1 color to use the bucket tool.");

			if (!IsWithin(image, x, y))
				return;

			BeginEdit(state, ref image);

			byte nColor = (byte)state.SingleEditor.PaletteSelector.SelectedItems[0];

			if (image.Pixels[y * image.Width + x] == nColor)
				return;

			_colorConnected(image, x, y, image.Pixels[y * image.Width + x], nColor);

			state.InvalidateImage(image);
		}

		private void _colorConnected(GrfImage image, int x, int y, byte target, byte newIndex) {
			bool[,] processed = new bool[image.Width, image.Height];

			Queue<(int X, int Y)> points = new Queue<(int X, int Y)>();
			int width = image.Width;
			int height = image.Height;

			if (image.Width == 0 || image.Height == 0)
				return;

			points.Enqueue((x, y));
			processed[x, y] = true;

			while (points.Count > 0) {
				var point = points.Dequeue();
				
				int imageIndex = point.X + point.Y * width;

				if (image.Pixels[imageIndex] != target)
					continue;

				image.Pixels[imageIndex] = newIndex;

				if (point.X > 0 && !processed[point.X - 1, point.Y]) {
					processed[point.X - 1, point.Y] = true;
						
					if (image.Pixels[imageIndex - 1] == target)
						points.Enqueue((point.X - 1, point.Y));
				}
				if (point.X < width - 1 && !processed[point.X + 1, point.Y]) {
					processed[point.X + 1, point.Y] = true;

					if (image.Pixels[imageIndex + 1] == target)
						points.Enqueue((point.X + 1, point.Y));
				}
				if (point.Y > 0 && !processed[point.X, point.Y - 1]) {
					processed[point.X, point.Y - 1] = true;

					if (image.Pixels[imageIndex - width] == target)
						points.Enqueue((point.X, point.Y - 1));
				}
				if (point.Y < height - 1 && !processed[point.X, point.Y + 1]) {
					processed[point.X, point.Y + 1] = true;

					if (image.Pixels[imageIndex + width] == target)
						points.Enqueue((point.X, point.Y + 1));
				}
			}
		}
	}

	public class SelectTool : SpriteEditorTool {
		public SelectTool(FancyButton button) : base(button) {
			Cursor = null;
		}
	}

	public class EraserTool : SpriteEditorTool {
		public EraserTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_eraser.png"), Width = 16, Height = 16 }, new Point() { X = 8, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			GrfImage image = state.SelectedImage;

			if (Mouse.LeftButton == MouseButtonState.Pressed) {
				BeginEdit(state, ref image);
			}
			else {
				// Preview only
				image = image.Copy();
			}
			
			for (int bx = 0; bx < 2 * state.Brush.Size + 1; bx++) {
				for (int by = 0; by < 2 * state.Brush.Size + 1; by++) {
					if (state.Brush.Data[bx, by] == 0)
						continue;

					int ix = bx - state.Brush.Size + x;
					int iy = by - state.Brush.Size + y;

					if (ix < 0 || ix >= image.Width ||
						iy < 0 || iy >= image.Height)
						continue;

					int pixelOffset = ix + image.Width * iy;

					image.Pixels[pixelOffset] = 0;
				}
			}

			state.InvalidateImage(image);
		}

		public override void Unselect(SpriteEditorState state) {
			base.Unselect(state);

			state.ResetImage();
		}
	}

	public class StampTool : SpriteEditorTool {
		public StampTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_brush.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			GrfImage image = state.SelectedImage;

			if (state.GradientEditor.PaletteSelector.SelectedItem == null)
				throw new Exception("You must select 1 gradient to use the Stamp tool.");

			if (!state.IsGradientEditorSelected)
				throw new Exception("Please select a gradient for the Stamp tool.");

			if (Mouse.LeftButton == MouseButtonState.Pressed) {
				BeginEdit(state, ref image);
			}
			else {
				image = image.Copy();
			}

			for (int bx = 0; bx < 2 * state.Brush.Size + 1; bx++) {
				for (int by = 0; by < 2 * state.Brush.Size + 1; by++) {
					if (state.Brush.Data[bx, by] == 0)
						continue;

					int ix = bx - state.Brush.Size + x;
					int iy = by - state.Brush.Size + y;

					if (ix < 0 || ix >= image.Width ||
						iy < 0 || iy >= image.Height)
						continue;

					// ReSharper disable once PossibleInvalidOperationException
					int selected = state.GradientEditor.PaletteSelector.SelectedItem.Value / 8;
					int pixelOffset = ix + image.Width * iy;
					int pixel = image.Pixels[pixelOffset];
					int newPixel = (byte)(selected * 8 + (pixel % 8));

					if (pixel == 0)
						continue;

					if (state.StampLock.Count > 0 && !state.StampLock.Contains(image.Pixels[pixelOffset]))
						continue;

					image.Pixels[pixelOffset] = (byte)(selected * 8 + (pixel % 8));
				}
			}

			state.InvalidateImage(image);
		}

		public override void Unselect(SpriteEditorState state) {
			base.Unselect(state);

			state.ResetImage();
		}
	}

	public class StampSpecialTool : SpriteEditorTool {
		private GrfImage _specialImage;
		private bool _eventSet;

		public StampSpecialTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_brush.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			GrfImage image = state.SelectedImage;

			if (state.GradientEditor.PaletteSelector.SelectedItem == null) {
				if (Mouse.LeftButton == MouseButtonState.Pressed)
					throw new Exception("You must select 1 gradient to use the Special Stamp tool.");
				return;
			}

			if (!state.IsGradientEditorSelected) {
				if (Mouse.LeftButton == MouseButtonState.Pressed)
					throw new Exception("Please select a gradient for the Special Stamp tool.");
				return;
			}

			if (Mouse.LeftButton == MouseButtonState.Pressed) {
				BeginEdit(state, ref image);
			}
			else {
				image = image.Copy();
			}

			if (_specialImage == null) {
				_specialImage = state.SelectedImage.Copy();
				int total = _specialImage.Height * _specialImage.Width;
				int selected = state.GradientEditor.PaletteSelector.SelectedItem.Value;

				for (int i = 0; i < total; i++) {
					if (_specialImage.Pixels[i] < selected || _specialImage.Pixels[i] >= selected + 8) {
						_specialImage.Pixels[i] = 0;
					}
				}
			}

			int left = x - _specialImage.Width / 2;
			int top = y - _specialImage.Height / 2;

			for (int xx = 0; xx < _specialImage.Width; xx++) {
				for (int yy = 0; yy < _specialImage.Height; yy++) {
					int targetX = xx + left;
					int targetY = yy + top;

					if (targetX < 0 || targetX >= image.Width || targetY < 0 || targetY >= image.Height)
						continue;

					byte p = _specialImage.Pixels[xx + yy * _specialImage.Width];

					if (p == 0)
						continue;

					image.Pixels[targetX + targetY * image.Width] = p;
				}
			}

			state.InvalidateImage(image);
		}

		public override void Select(SpriteEditorState state) {
			base.Select(state);
			_specialImage = null;
			
			if (!_eventSet) {
				_eventSet = true;
				state.GradientEditor.PaletteSelector.SelectionChanged += (s, args) => _paletteSelector_SelectionChanged(s, args, state);
			}
		}

		public override void Unselect(SpriteEditorState state) {
			base.Unselect(state);

			state.ResetImage();
		}

		private void _paletteSelector_SelectionChanged(object sender, Utilities.Controls.ObservabableListEventArgs args, SpriteEditorState state) {
			try {
				Select(state);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}
	}

	public class PenTool : SpriteEditorTool {
		public PenTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_pen.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			GrfImage image = state.SelectedImage;

			if (Mouse.LeftButton != MouseButtonState.Pressed)
				return;

			if (state.SingleEditor.PaletteSelector.SelectedItems.Count != 1)
				throw new Exception("You must select 1 color to use the pen tool.");

			if (!IsWithin(image, x, y))
				return;

			BeginEdit(state, ref image);

			byte nColor = (byte)state.SingleEditor.PaletteSelector.SelectedItems[0];

			if (image.Pixels[y * image.Width + x] == nColor)
				return;

			image.Pixels[y * image.Width + x] = nColor;
			state.InvalidateImage(image);
		}
	}

	public class RectangleTool : SpriteEditorTool {
		private (int X, int Y) Start;

		public RectangleTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_pen.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x, int y) {
			GrfImage image = state.SelectedImage;

			if (Mouse.LeftButton != MouseButtonState.Pressed)
				return;

			if (state.SingleEditor.PaletteSelector.SelectedItems.Count != 1)
				throw new Exception("You must select 1 color to use the pen tool.");

			if (state.EditingImage == null) {
				Start = (x, y);
			}

			BeginEdit(state, ref image);

			Buffer.BlockCopy(state.SelectedImage.Pixels, 0, image.Pixels, 0, image.Pixels.Length);
			byte nColor = (byte)state.SingleEditor.PaletteSelector.SelectedItems[0];

			int x0 = Math.Min(Start.X, x);
			int x1 = Math.Max(Start.X, x);

			int y0 = Math.Min(Start.Y, y);
			int y1 = Math.Max(Start.Y, y);

			for (int xx = Math.Max(0, x0); xx <= Math.Min(image.Width - 1, x1); xx++) {
				if (y0 >= 0 && y0 < image.Height) {
					image.Pixels[y0 * image.Width + xx] = nColor;
				}
				if (y1 >= 0 && y1 < image.Height) {
					image.Pixels[y1 * image.Width + xx] = nColor;
				}
			}

			for (int yy = Math.Max(0, y0); yy <= Math.Min(image.Height - 1, y1); yy++) {
				if (x0 >= 0 && x0 < image.Width) {
					image.Pixels[yy * image.Width + x0] = nColor;
				}
				if (x1 >= 0 && x1 < image.Width) {
					image.Pixels[yy * image.Width + x1] = nColor;
				}
			}

			state.InvalidateImage(image);
		}
	}

	public class LineTool : SpriteEditorTool {
		private (int X, int Y) Start;

		public LineTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_pen.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x1, int y1) {
			GrfImage image = state.SelectedImage;

			if (Mouse.LeftButton != MouseButtonState.Pressed)
				return;

			if (state.SingleEditor.PaletteSelector.SelectedItems.Count != 1)
				throw new Exception("You must select 1 color to use the pen tool.");

			if (state.EditingImage == null) {
				Start = (x1, y1);
			}

			BeginEdit(state, ref image);

			Buffer.BlockCopy(state.SelectedImage.Pixels, 0, image.Pixels, 0, image.Pixels.Length);
			byte nColor = (byte)state.SingleEditor.PaletteSelector.SelectedItems[0];

			int x0 = Start.X;
			int y0 = Start.Y;

			// Bresenham's Line Algorithm
			int dx = Math.Abs(x1 - x0);
			int dy = -Math.Abs(y1 - y0);
			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;

			while (true) {
				if (x0 >= 0 && x0 < image.Width && y0 >= 0 && y0 < image.Height) {
					image.Pixels[y0 * image.Width + x0] = nColor;
				}

				if (x0 == x1 && y0 == y1) break;

				int e2 = 2 * err;
				
				if (e2 >= dy) {
					err += dy;
					x0 += sx;
				}
				if (e2 <= dx) {
					err += dx;
					y0 += sy;
				}
			}

			state.InvalidateImage(image);
		}
	}

	public class EllipseTool : SpriteEditorTool {
		private (int X, int Y) Start;

		public EllipseTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_pen.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });
		}

		public override void OnPixelMoved(SpriteEditorState state, object sender, int x1, int y1) {
			GrfImage image = state.SelectedImage;

			if (Mouse.LeftButton != MouseButtonState.Pressed)
				return;

			if (state.SingleEditor.PaletteSelector.SelectedItems.Count != 1)
				throw new Exception("You must select 1 color to use the pen tool.");

			if (state.EditingImage == null) {
				Start = (x1, y1);
			}

			BeginEdit(state, ref image);

			Buffer.BlockCopy(state.SelectedImage.Pixels, 0, image.Pixels, 0, image.Pixels.Length);
			byte nColor = (byte)state.SingleEditor.PaletteSelector.SelectedItems[0];

			int x0 = Start.X;
			int y0 = Start.Y;

			// Bresenham's Line Algorithm
			int a = Math.Abs(x1 - x0);
			int b = Math.Abs(y1 - y0);
			int b1 = b & 1;

			long dx = 4 * (1 - a) * b * b;
			long dy = 4 * (b1 + 1) * a * a;
			long err = dx + dy + b1 * a * a;
			long e2;

			if (x0 > x1) { x0 = x1; x1 += a; }
			if (y0 > y1) y0 = y1;

			y0 += (b + 1) / 2;
			y1 = y0 - b1;
			a *= 8 * a;
			b1 = 8 * b * b;

			do {
				image.SetColor(x1, y0, nColor);
				image.SetColor(x0, y0, nColor);
				image.SetColor(x0, y1, nColor);
				image.SetColor(x1, y1, nColor);

				e2 = 2 * err;
				if (e2 <= dy) { y0++; y1--; err += dy += a; }  /* y step */
				if (e2 >= dx || 2 * err > dy) { x0++; x1--; err += dx += b1; } /* x step */
			} while (x0 <= x1);

			while (y0 - y1 <= b) {  /* too early stop of flat ellipses a=1 */
				image.SetColor(x0 - 1, y0, nColor);
				image.SetColor(x1 + 1, y0++, nColor);
				image.SetColor(x0 - 1, y1, nColor);
				image.SetColor(x1 + 1, y1--, nColor);
			}

			state.InvalidateImage(image);
		}
	}

	public class PickerTool : SpriteEditorTool {
		public PickerTool(FancyButton button) : base(button) {
			Cursor = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_eyedrop.png"), Width = 16, Height = 16 }, new Point() { X = 2, Y = 14 });
		}

	}
}
