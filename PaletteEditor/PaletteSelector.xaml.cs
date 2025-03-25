using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ErrorManager;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using Utilities.Controls;
using Utilities.Extension;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for PaletteSelector.xaml
	/// </summary>
	public partial class PaletteSelector : UserControl {
		private readonly ObservableList<int> _selectedIndexes = new ObservableList<int>();
		private Pal _pal;
		private bool[] _selectedColors = new bool[256];

		public delegate void PaletteSelectorPaletteChangedEvent(object sender);

		public event PaletteSelectorPaletteChangedEvent PaletteSelectorPaletteChanged;

		protected virtual void OnPaletteSelectorPaletteChanged() {
			PaletteSelectorPaletteChangedEvent handler = PaletteSelectorPaletteChanged;
			if (handler != null) handler(this);
		}

		public PaletteSelector() {
			InitializeComponent();

			_selectedIndexes.CollectionChanged += new ObservableList.ObservableListEventHandler(_selectedIndexes_CollectionChanged);

			_paletteOverlay.Visibility = Visibility.Hidden;

			_borderSelector.MouseEnter += new MouseEventHandler(_borderSelector_MouseEnter);
			_borderSelector.MouseLeave += new MouseEventHandler(_borderSelector_MouseLeave);
			_borderSelector.MouseMove += new MouseEventHandler(_borderSelector_MouseMove);
			_borderSelector.MouseDown += new MouseButtonEventHandler(_borderSelector_MouseDown);
			_borderSelector.MouseLeftButtonUp += new MouseButtonEventHandler(_borderSelector_MouseLeftButtonUp);
			_borderSelector.DragEnter += new DragEventHandler(_borderSelector_DragEnter);
			_borderSelector.Drop += new DragEventHandler(_borderSelector_Drop);

			Loaded += delegate {
				Window parent = WpfUtilities.FindParentControl<Window>(this);

				if (parent != null) {
					parent.PreviewKeyDown += new KeyEventHandler(_paletteSelector_PreviewKeyDown);
					parent.KeyDown += new KeyEventHandler(_paletteSelector_KeyDown);
				}
			};
		}

		private void _borderSelector_Drop(object sender, DragEventArgs e) {
			try {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".pal"))) {
						var palette = File.ReadAllBytes(files.First(p => p.IsExtension(".pal")));

						if (palette.Length < 1024)
							return;

						// Fix the alpha channel
						for (int i = 3; i < 1024; i += 4) {
							palette[i] = 255;
						}

						palette[3] = 0;

						Palette.Commands.SetRawBytesInPalette(0, palette);
						OnPaletteSelectorPaletteChanged();
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _borderSelector_DragEnter(object sender, DragEventArgs e) {
			try {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".pal"))) {
						e.Effects = DragDropEffects.All;
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public Grid GridFocus {
			get { return _gridFocus; }
		}

		public Pal Palette {
			get { return _pal; }
			set { SetPalette(value); }
		}

		public bool IsMultipleColorsSelectable { get; set; }

		public IList<int> SelectedItems {
			get { return _selectedIndexes; }
		}

		public bool UseLargeSelector { get; set; }

		public IList<bool> AllSelectedColors {
			get { return _selectedColors; }
		}

		public int? SelectedItem {
			get {
				if (_selectedIndexes.Count > 0)
					return _selectedIndexes[0];

				return null;
			}
			set {
				if (value == null) {
					_selectedIndexes.Clear();
				}
				else {
					if (_selectedIndexes.Count > 1)
						_selectedIndexes.Clear();
					if (_selectedIndexes.Count == 0)
						_selectedIndexes.Add(value.Value);
					else
						_selectedIndexes[0] = value.Value;
				}
			}
		}

		public event ObservableList.ObservableListEventHandler SelectionChanged;
		public event ObservableList.ObservableListEventHandler ColorHighlightRequested;

		public void OnColorHighlightRequested(ObservabableListEventArgs args) {
			ObservableList.ObservableListEventHandler handler = ColorHighlightRequested;
			if (handler != null) handler(this, args);
		}

		public void OnSelectionChanged(ObservabableListEventArgs args) {
			ObservableList.ObservableListEventHandler handler = SelectionChanged;
			if (handler != null) handler(this, args);
		}

		public void Copy() {
			List<GrfColor> colors = _pal.GetColors(_selectedIndexes.OrderBy(p => p));

			if (colors.Count <= 0) return;

			string colorsString = colors.Aggregate("", (current, color) => current + "," + String.Format("0x{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B));

			if (colorsString.StartsWith(",")) {
				colorsString = colorsString.Remove(0, 1);
			}

			Clipboard.SetText(colorsString);
		}

		public void Paste() {
			try {
				string colorsString = Clipboard.GetText();
				List<GrfColor> colors = colorsString.Split(',').Select(p => new GrfColor(p)).ToList();

				if (_selectedIndexes.Count <= 0) return;

				int startIndex = _selectedIndexes[0];
				byte[] colorsData = new byte[4 * colors.Count];

				byte[] colorData = new byte[4];

				for (int i = 0; i < colors.Count; i++) {
					colorData[0] = colors[i].R;
					colorData[1] = colors[i].G;
					colorData[2] = colors[i].B;
					colorData[3] = colors[i].A;
					Buffer.BlockCopy(colorData, 0, colorsData, i * 4, 4);
				}

				_pal.Commands.SetRawBytesInPalette(startIndex * 4, colorsData);
				OnPaletteSelectorPaletteChanged();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _paletteSelector_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (ApplicationShortcut.Is(ApplicationShortcut.Copy)) {
				if (!_gridFocus.IsFocused)
					return;

				Copy();
				e.Handled = true;
			}
			else if (ApplicationShortcut.Is(ApplicationShortcut.Paste)) {
				if (!_gridFocus.IsFocused)
					return;

				Paste();
				e.Handled = true;
			}
		}

		private void _paletteSelector_KeyDown(object sender, KeyEventArgs e) {
			if (ApplicationShortcut.Is(ApplicationShortcut.Copy)) {
				Copy();
			}
			else if (ApplicationShortcut.Is(ApplicationShortcut.Paste)) {
				Paste();
			}
		}

		private void _borderSelector_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			_borderSelector.ReleaseMouseCapture();
			e.Handled = true;
		}

		private void _selectedIndexes_CollectionChanged(object sender, ObservabableListEventArgs args) {
			OnSelectionChanged(args);
			_selectedColors = new bool[256];

			foreach (int index in _selectedIndexes)
				_selectedColors[index] = true;

			_generateSelectionArea();
		}

		private void _generateSelectionArea() {
			int colorIndex;
			const int HalfStride = 8 * 16 * 4;
			const int CellStride = 16 * 4;
			byte[] imagePixels = new byte[256 * 256 * 4];

			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					colorIndex = y * 16 + x;

					if (_selectedColors[colorIndex]) {
						byte[] data = _getSelectionFor(colorIndex, Color.FromArgb(255, 255, 0, 0));
						for (int j = 0; j < 16; j++) {
							Buffer.BlockCopy(data, j * CellStride, imagePixels, y * 2 * HalfStride * 16 + x * CellStride + j * (2 * HalfStride), CellStride);
						}
					}
				}
			}
			
			GrfImage image = new GrfImage(ref imagePixels, 256, 256, GrfImageType.Bgra32);
			_imageSelectionPalette.Source = image.Cast<BitmapSource>();
		}

		public void GenerateUsedPalette(bool[] used) {
			int colorIndex;
			const int HalfStride = 8 * 16 * 4;
			const int CellStride = 16 * 4;
			byte[] imagePixels = new byte[256 * 256 * 4];

			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					colorIndex = y * 16 + x;

					if (used[colorIndex]) {
						byte[] data = _getSelectionFor2(used, colorIndex, Color.FromArgb(128, 255, 255, 255));
						for (int j = 0; j < 16; j++) {
							Buffer.BlockCopy(data, j * CellStride, imagePixels, y * 2 * HalfStride * 16 + x * CellStride + j * (2 * HalfStride), CellStride);
						}
					}
				}
			}
			
			GrfImage image = new GrfImage(ref imagePixels, 256, 256, GrfImageType.Bgra32);
			_imageUsedPalette.Source = image.Cast<BitmapSource>();
		}

		private byte[] _getSelectionFor(int realIndex, Color color) {
			bool hasOneOnTheLeft = false;
			bool hasOneOnTheRight = false;
			bool hasOneOnTop = false;
			bool hasOneOnBottom = false;

			if (realIndex > 15 && _selectedColors[realIndex - 16]) {
				hasOneOnTop = true;
			}

			if (realIndex % 16 != 15 && _selectedColors[realIndex + 1]) {
				hasOneOnTheRight = true;
			}

			if (realIndex % 16 != 0 && _selectedColors[realIndex - 1]) {
				hasOneOnTheLeft = true;
			}

			if (realIndex < 240 && _selectedColors[realIndex + 16]) {
				hasOneOnBottom = true;
			}

			byte[] data = new byte[256 * 4];

			int index;

			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					index = 4 * (y * 16 + x);

					if ((!hasOneOnBottom && (y == 14 || y == 15)) ||
					    (!hasOneOnTop && (y == 0 || y == 1)) ||
					    (!hasOneOnTheLeft && (x == 0 || x == 1)) ||
					    (!hasOneOnTheRight && (x == 14 || x == 15))) {
						data[index] = color.B;
						data[index + 1] = color.G;
						data[index + 2] = color.R;
						data[index + 3] = color.A;
					}
					else if (x == y) {
						data[index] = color.B;
						data[index + 1] = color.G;
						data[index + 2] = color.R;
						data[index + 3] = 80;
					}
				}
			}

			return data;
		}

		private byte[] _getSelectionFor2(bool[] used, int realIndex, Color color) {
			bool hasOneOnTheLeft = false;
			bool hasOneOnTheRight = false;
			bool hasOneOnTop = false;
			bool hasOneOnBottom = false;

			if (realIndex > 15 && used[realIndex - 16]) {
				hasOneOnTop = true;
			}

			if (realIndex % 16 != 15 && used[realIndex + 1]) {
				hasOneOnTheRight = true;
			}

			if (realIndex % 16 != 0 && used[realIndex - 1]) {
				hasOneOnTheLeft = true;
			}

			if (realIndex < 240 && used[realIndex + 16]) {
				hasOneOnBottom = true;
			}

			byte[] data = new byte[256 * 4];

			int index;

			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					index = 4 * (y * 16 + x);

					if ((!hasOneOnBottom && (y == 15)) ||
						(!hasOneOnTop && (y == 0)) ||
						(!hasOneOnTheLeft && (x == 0)) ||
						(!hasOneOnTheRight && (x == 15))) {
						data[index] = color.B;
						data[index + 1] = color.G;
						data[index + 2] = color.R;
						data[index + 3] = color.A;
					}
					else if (x == y) {
						data[index] = color.B;
						data[index + 1] = color.G;
						data[index + 2] = color.R;
						data[index + 3] = 80;
					}
				}
			}

			return data;
		}

		//private byte[] _getSelectionFor(int realIndex, Color color, SelectionPaletteBrush brush = null) {
		//	bool[] location = new bool[9];
		//
		//	if (brush == null) {
		//		brush = new SelectionPaletteBrush();
		//		brush.Margin = 3;
		//		brush.Thickness = 2;
		//		brush.ShowDiagonale = true;
		//	}
		//
		//	// Check up row
		//	if (realIndex > 15) {
		//		location[1] = _selectedColors[realIndex - 16];
		//
		//		// Check left and right	
		//		if (realIndex % 16 != 15) location[2] = _selectedColors[realIndex - 16 + 1];
		//		if (realIndex % 16 !=  0) location[0] = _selectedColors[realIndex - 16 - 1];
		//	}
		//
		//	// Check left and right
		//	if (realIndex % 16 != 15) location[3] = _selectedColors[realIndex + 1];
		//	if (realIndex % 16 !=  0) location[5] = _selectedColors[realIndex - 1];
		//
		//	// Check down row
		//	if (realIndex < 240) {
		//		location[7] = _selectedColors[realIndex + 16];
		//
		//		// Check left and right	
		//		if (realIndex % 16 != 15) location[8] = _selectedColors[realIndex + 16 + 1];
		//		if (realIndex % 16 !=  0) location[6] = _selectedColors[realIndex + 16 - 1];
		//	}
		//
		//	byte[] data = new byte[256 * 4];
		//
		//	int index;
		//
		//	bool[] cells = new bool[9];
		//
		//	for (int i = 0; i < cells.Length; i++)
		//		cells[i] = true;
		//
		//	if (!location[0]) {
		//		lines[]
		//	}
		//	// Drawing left bar
		//	if (location[0] || location[3] || location[6]) {
		//		if (location[0] && location[3] && location[6]) {
		//			
		//		}
		//	}
		//
		//	if (_selectedColors[realIndex] && brush.ShowDiagonale) {
		//		for (int y = 0; y < 16; y++) {
		//			for (int x = 0; x < 16; x++) {
		//				index = 4 * (y * 16 + x);
		//
		//				if (x == y) {
		//					data[index] = color.B;
		//					data[index + 1] = color.G;
		//					data[index + 2] = color.R;
		//					data[index + 3] = 80;
		//				}
		//			}
		//		}
		//	}
		//
		//	//if (hasOn)
		//	//for (int y = 0; y < 16; y++) {
		//	//	for (int x = 0; x < 16; x++) {
		//	//		index = 4 * (y * 16 + x);
		//	//
		//	//		if (!hasOneOnTheLeft) {
		//	//			
		//	//		}
		//	//
		//	//		if ((!hasOneOnBottom && (y == 14 - margin || y == 15 - margin)) ||
		//	//			(!hasOneOnTop && (y == 0 + margin || y == 1 + margin)) ||
		//	//			(!hasOneOnTheLeft && (x == 0 + margin || x == 1 + margin)) ||
		//	//			(!hasOneOnTheRight && (x == 14 - margin || x == 15 - margin))) {
		//	//			data[index] = color.B;
		//	//			data[index + 1] = color.G;
		//	//			data[index + 2] = color.R;
		//	//			data[index + 3] = color.A;
		//	//		}
		//	//		else if (x == y && (x >= margin && x <= 15 - margin && y >= margin && y <= 15 - margin)) {
		//	//			data[index] = color.B;
		//	//			data[index + 1] = color.G;
		//	//			data[index + 2] = color.R;
		//	//			data[index + 3] = 80;
		//	//		}
		//	//	}
		//	//}
		//
		//	return data;
		//}

		private void _borderSelector_MouseDown(object sender, MouseButtonEventArgs e) {
			Keyboard.Focus(_gridFocus);
			_gridFocus.Focus();

			if (e.LeftButton == MouseButtonState.Pressed) {
				_borderSelector.CaptureMouse();
			}

			_borderSelector_MouseMove(sender, e);

			e.Handled = true;
		}

		public void AddSelection(int index) {
			if (index > 255)
				throw new Exception("Invalid palette selection index (must be lower than 256).");

			if (UseLargeSelector) {
				int baseIndex = index / 8 * 8;
				int lastIndex = baseIndex + 8;

				List<int> indexes = new List<int>();
				for (int i = baseIndex; i < lastIndex; i++) {
					indexes.Add(i);
				}

				_selectedIndexes.AddRange(indexes);
			}
			else {
				SelectedItems.Add(index);
			}
		}

		public void AddSelection(int row, int column) {
			if (row > 15 || column > 15)
				throw new Exception("Row or column index must be below 16.");

			AddSelection(16 * column + row);
		}

		public void SetPalette(Pal pal) {
			if (_pal != null)
				_pal.PaletteChanged -= _pal_PaletteChanged;

			_pal = pal;
			_imageBackPalette.Source = pal.Image.Cast<BitmapSource>();
			_pal.PaletteChanged += _pal_PaletteChanged;
		}

		private void _pal_PaletteChanged(object sender) {
			_imageBackPalette.Source = _pal.Image.Cast<BitmapSource>();
		}

		private void _borderSelector_MouseMove(object sender, MouseEventArgs e) {
			Point position = _getClippedPosition(e.GetPosition(_borderSelector));

			if (UseLargeSelector) {
				_largePaletteOverlay.Margin = new Thickness((int) (position.X / 128) * 128, (int) (position.Y / 16) * 16, 0, 0);
			}
			else {
				_paletteOverlay.Margin = new Thickness((int) (position.X / 16) * 16, (int) (position.Y / 16) * 16, 0, 0);
			}

			if (e.LeftButton == MouseButtonState.Pressed) {
				if (IsMultipleColorsSelectable && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
					AddSelection(_getIndexFromPosition(position));
				}
				else if (IsMultipleColorsSelectable && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
					if (SelectedItems.Count == 0) {
						AddSelection(_getIndexFromPosition(position));
					}
					else {
						int latestIndex = SelectedItems.Last();
						int currentIndex = _getIndexFromPosition(position);

						if (latestIndex < currentIndex) {
							int temp = currentIndex;
							currentIndex = latestIndex;
							latestIndex = temp;
						}

						if (UseLargeSelector) {
							currentIndex = currentIndex / 8 * 8;
							latestIndex = latestIndex / 8 * 8 + 7;
						}

						List<int> indexes = new List<int>();
						for (int i = currentIndex; i <= latestIndex; i++) {
							indexes.Add(i);
						}

						_selectedIndexes.AddRange(indexes);
					}
				}
				else {
					ClearSelection();
					AddSelection(_getIndexFromPosition(position));
				}
			}

			e.Handled = true;
		}

		public void ClearSelection() {
			_selectedIndexes.Clear();
		}

		private Point _getClippedPosition(Point position) {
			if (position.X >= 256)
				position.X = 255;

			if (position.X < 0)
				position.X = 0;

			if (position.Y < 0)
				position.Y = 0;

			if (position.Y >= 256)
				position.Y = 255;

			return position;
		}

		private int _getIndexFromPosition(Point position) {
			return (int) (position.Y / 16) * 16 + (int) (position.X / 16);
		}

		private void _borderSelector_MouseLeave(object sender, MouseEventArgs e) {
			if (UseLargeSelector) {
				_largePaletteOverlay.Visibility = Visibility.Hidden;
			}
			else {
				_paletteOverlay.Visibility = Visibility.Hidden;
			}
			e.Handled = true;
		}

		private void _borderSelector_MouseEnter(object sender, MouseEventArgs e) {
			if (UseLargeSelector) {
				_largePaletteOverlay.Visibility = Visibility.Visible;
			}
			else {
				_paletteOverlay.Visibility = Visibility.Visible;
			}
			e.Handled = true;
		}

		#region Show selection

		#endregion
	}
}