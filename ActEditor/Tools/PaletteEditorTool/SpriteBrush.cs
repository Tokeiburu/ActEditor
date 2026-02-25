namespace ActEditor.Tools.PaletteEditorTool {
	public class SpriteBrush {
		public int[,] Data;
		private int _brushSize = -1;
		public int Size => _brushSize;

		public void UpdateBrush(int size) {
			if (size == _brushSize)
				return;

			_brushSize = size;
			_generateBrush();
		}

		private void _setBrushPixel(int x, int y) {
			if (y < 0 || y >= 2 * _brushSize + 1)
				return;
			if (x < 0 || x >= 2 * _brushSize + 1)
				return;

			Data[y, x] = 1;
		}

		private void _setBrushPixel(int cx, int cy, int x, int y) {
			_horizontalLine(cx - x, cy + y, cx + x);
			if (y != 0)
				_horizontalLine(cx - x, cy - y, cx + x);
		}

		private void _horizontalLine(int x0, int y0, int x1) {
			for (int x = x0; x <= x1; ++x)
				_setBrushPixel(x, y0);
		}

		private void _generateBrush() {
			Data = new int[2 * _brushSize + 1, 2 * _brushSize + 1];

			int radius = _brushSize;

			int error = -radius;
			int x = radius;
			int y = 0;
			int x0 = _brushSize;
			int y0 = _brushSize;

			while (x >= y) {
				int lastY = y;

				error += y;
				++y;
				error += y;

				_setBrushPixel(x0, y0, x, lastY);

				if (error >= 0) {
					if (x != lastY)
						_setBrushPixel(x0, y0, lastY, x);

					error -= x;
					--x;
					error -= x;
				}
			}
		}
	}
}
