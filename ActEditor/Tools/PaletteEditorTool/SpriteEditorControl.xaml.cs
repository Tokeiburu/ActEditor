using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Tools.GrfShellExplorer;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats;
using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.Threading;
using GrfToWpfBridge.Application;
using PaletteEditor;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Controls;
using Utilities.Extension;

namespace ActEditor.Tools.PaletteEditorTool {
	/// <summary>
	/// Interaction logic for SpriteEditorControl.xaml
	/// </summary>
	public partial class SpriteEditorControl : UserControl {
		private readonly WpfRecentFiles _recentFiles;
		private byte[] _palette = new byte[1024];
		private Spr _spr;
		private GrfImage _imageEditing;
		private EditMode _editMode = EditMode.Select;
		private bool _gradientSelection = false;
		private CancellationTokenSource _animToken;
		private readonly float[] _pixelGlow = new float[256];

		private Cursor CursorBucket = null;
		private Cursor CursorEraser = null;
		private Cursor CursorStamp = null;
		private Cursor CursorEyedrop = null;
		private Cursor CursorPen = null;
		private int[,] _brush;
		private GrfImage _specialImage;
		private List<int> _stampLock = new List<int>();

		public enum EditMode {
			Select,
			Bucket,
			EyeDrop,
			Brush,
			Stamp,
			StampSpecial,
			Eraser,
			Pen,
		}

		public SpriteEditorControl() {
			InitializeComponent();

			_cbSpriteId.Margin = new Thickness(100, 0, 0, 0);
			_cbSpriteId.SelectionChanged += new SelectionChangedEventHandler(_cbSpriteId_SelectionChanged);
			_spriteViewer.PixelClicked += new ImageViewer.ImageViewerEventHandler(_spriteViewer_PixelClicked);
			_spriteViewer.PixelMoved += new ImageViewer.ImageViewerEventHandler(_spriteViewer_PixelMoved);
			_recentFiles = new WpfRecentFiles(Configuration.ConfigAsker, 6, _menuItemOpenRecent, "Sprite editor");
			_recentFiles.FileClicked += f => _openFile(new TkPath(f));
			
			_mainGrid.IsEnabled = false;

			AllowDrop = true;

			_spriteViewer.DragEnter += new DragEventHandler(_spriteEditorControl_DragEnter);
			_spriteViewer.DragOver += new DragEventHandler(_spriteEditorControl_DragEnter);
			_spriteViewer.Drop += new DragEventHandler(_spriteEditorControl_Drop);
			_spriteViewer.MouseMove += _spriteViewer_MouseMove;
			_spriteViewer.MouseEnter += delegate {
				_setCursor(_editMode);
			};
			_spriteViewer.MouseLeave += delegate {
				Mouse.OverrideCursor = null;
			};
			_spriteViewer.LostMouseCapture += new MouseEventHandler(_spriteViewer_LostMouseCapture);
			_spriteViewer.MouseLeftButtonUp += _spriteViewer_MouseLeftButtonUp;
			PreviewKeyDown += _spriteEditorControl_PreviewKeyDown;
			PreviewKeyUp += _spriteEditorControl_PreviewKeyDown;
			_spriteViewer.PreviewKeyDown += _spriteEditorControl_PreviewKeyDown;
			_spriteViewer.PreviewKeyUp += _spriteEditorControl_PreviewKeyDown;

			_mainGrid.SizeChanged += new SizeChangedEventHandler(_mainTabControl_SizeChanged);
			_sce.PaletteSelector.SelectionChanged += new ObservableList.ObservableListEventHandler(_paletteSelector_SelectionChanged);
			_gceControl.PaletteSelector.SelectionChanged += new ObservableList.ObservableListEventHandler(_paletteSelector_SelectionChanged);

			ApplicationShortcut.Link(ApplicationShortcut.Save, () => _menuItemSave_Click(null, null), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Q", "SpriteEditor.Select"), () => _buttonSelection_Click(_buttonSelection, null), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-B", "SpriteEditor.Bucket"), () => _buttonBucket_Click(_buttonBucket, null), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-T", "SpriteEditor.Stamp"), () => _buttonStamp_Click(_buttonStamp, null), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-E", "SpriteEditor.Eraser"), () => _buttonEraser_Click(_buttonEraser, null), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-P", "SpriteEditor.Pen"), () => _buttonPen_Click(_buttonPen, null), this);

			Loaded += delegate {
				var parent = WpfUtilities.FindParentControl<Window>(this);

				if (parent != null) {
					parent.StateChanged += (e, a) => _mainTabControl_SizeChanged(null, null);
				}

				Keyboard.Focus(_focusDummy);
				_focusDummy.Focus();
				_buttonSelection.IsPressed = true;

				_sce.PaletteSelector.Margin = new Thickness(270, 5, 2, 2);
				var parentGrid = (Grid)_sce.PaletteSelector.Parent;
				parentGrid.Children.Remove(_sce.PaletteSelector);
				parentGrid.Children.Remove(_sce.PickerControl);
				_gceControl.PrimaryGrid.Children.Add(_sce.PaletteSelector);
				_gceControl.PrimaryGrid.Children.Add(_sce.PickerControl);
				_gceControl.PickerControl.Visibility = Visibility.Visible;
				_gceControl.Panel.Visibility = Visibility.Visible;
				_gceControl.GradientGrid.Visibility = Visibility.Visible;

				_sce.PaletteSelector.GotFocus += (s, e) => SelectSingleColorEditControl();
				_gceControl.PaletteSelector.GotFocus += (s, e) => SelectGradientColorEditControl();
				SelectSingleColorEditControl();

				_brushIncrease(0);
			};

			Unloaded += delegate {
				try {
					_animToken?.Cancel();
				}
				catch { }
			};
		}

		public void SelectSingleColorEditControl() {
			_gradientSelection = false;
			_sce.PickerControl.Visibility = Visibility.Visible;
			_gceControl.PickerControl.Visibility = Visibility.Hidden;
			_gceControl.Panel.Visibility = Visibility.Hidden;
			_gceControl.GradientGrid.Visibility = Visibility.Hidden;
		}

		public void SelectGradientColorEditControl() {
			_gradientSelection = true;
			_sce.PickerControl.Visibility = Visibility.Hidden;
			_gceControl.PickerControl.Visibility = Visibility.Visible;
			_gceControl.Panel.Visibility = Visibility.Visible;
			_gceControl.GradientGrid.Visibility = Visibility.Visible;
		}

		private void _brushIncrease(int amount) {
			try {
				ActEditorConfiguration.BrushSize += amount;

				if (ActEditorConfiguration.BrushSize > 15)
					ActEditorConfiguration.BrushSize = 15;

				if (ActEditorConfiguration.BrushSize < 0)
					ActEditorConfiguration.BrushSize = 0;

				_generateBrush();

				Point imagePoint = Mouse.GetPosition(_spriteViewer._imageSprite);
				imagePoint.X = (imagePoint.X) / (_spriteViewer._imageSprite.Width);
				imagePoint.Y = (imagePoint.Y) / (_spriteViewer._imageSprite.Height);

				int x = (int)(_spriteViewer.Bitmap.PixelWidth * imagePoint.X);
				int y = (int)(_spriteViewer.Bitmap.PixelHeight * imagePoint.Y);

				_spriteViewer_PixelMoved(this, x, y, true);
			}
			catch {
			}
		}

		private void _spriteViewer_PixelMoved(object sender, int x0, int y0, bool isWithin) {
			switch (_editMode) {
				case EditMode.Eraser:
					{
						GrfImage image = _spr.Images[_cbSpriteId.Dispatch(p => p.SelectedIndex)];
						image = image.Copy();

						for (int bx = 0; bx < 2 * ActEditorConfiguration.BrushSize + 1; bx++) {
							for (int by = 0; by < 2 * ActEditorConfiguration.BrushSize + 1; by++) {
								if (_brush[bx, by] == 0)
									continue;

								int ix = bx - ActEditorConfiguration.BrushSize + x0;
								int iy = by - ActEditorConfiguration.BrushSize + y0;

								if (ix < 0 || ix >= image.Width ||
									iy < 0 || iy >= image.Height)
									continue;

								int pixelOffset = ix + image.Width * iy;

								image.Pixels[pixelOffset] = 0;
							}
						}

						_spriteViewer.Dispatch(p => p.LoadImage(image));
					}

					break;
				case EditMode.Stamp:
					if (_gradientSelection && _getGce().PaletteSelector.SelectedItem != null) {
						GrfImage image = _spr.Images[_cbSpriteId.Dispatch(p => p.SelectedIndex)];
						image = image.Copy();

						for (int bx = 0; bx < 2 * ActEditorConfiguration.BrushSize + 1; bx++) {
							for (int by = 0; by < 2 * ActEditorConfiguration.BrushSize + 1; by++) {
								if (_brush[bx, by] == 0)
									continue;

								int ix = bx - ActEditorConfiguration.BrushSize + x0;
								int iy = by - ActEditorConfiguration.BrushSize + y0;

								if (ix < 0 || ix >= image.Width ||
									iy < 0 || iy >= image.Height)
									continue;

								// ReSharper disable once PossibleInvalidOperationException
								int selected = _getGce().PaletteSelector.SelectedItem.Value / 8;
								int pixelOffset = ix + image.Width * iy;
								int pixel = image.Pixels[pixelOffset];

								image.Pixels[pixelOffset] = (byte)(selected * 8 + (pixel % 8));
							}
						}

						_spriteViewer.Dispatch(p => p.LoadImage(image));

					}

					break;
				case EditMode.StampSpecial:
					if (_gradientSelection && _getGce().PaletteSelector.SelectedItem != null) {
						GrfImage image = _spr.Images[_cbSpriteId.Dispatch(p => p.SelectedIndex)];
						image = image.Copy();

						if (_specialImage == null)
							return;

						int left = x0 - _specialImage.Width / 2;
						int top = y0 - _specialImage.Height / 2;

						for (int x = 0; x < _specialImage.Width; x++) {
							for (int y = 0; y < _specialImage.Height; y++) {
								int targetX = x + left;
								int targetY = y + top;

								if (targetX < 0 || targetX >= image.Width || targetY < 0 || targetY >= image.Height)
									continue;

								try {
									byte p = _specialImage.Pixels[x + y * _specialImage.Width];

									if (p == 0)
										continue;

									image.Pixels[targetX + targetY * image.Width] = p;
								}
								catch (Exception err) {
									ErrorHandler.HandleException(err);
								}
							}
						}

						_spriteViewer.Dispatch(p => p.LoadImage(image));

					}

					break;
			}
		}

		private void _spriteEditorControl_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (ApplicationShortcut.IsCommandActive())
				return;

			if (e.SystemKey == Key.LeftAlt) {
				e.Handled = true;

				if (_spriteViewer.IsMouseOver) {
					if (e.IsDown) {
						_setCursor(EditMode.EyeDrop);
					}
					else {
						_setCursor(_editMode);
					}
				}
			}
			else {
				if (_spriteViewer.IsMouseOver) {
					_setCursor(_editMode);
				}
			}
		}

		private void _spriteViewer_LostMouseCapture(object sender, MouseEventArgs e) {
			if (_spr == null)
				return;

			if (_imageEditing != null) {
				_spr.Palette.Commands.StoreAndExecute(new ImageModifiedCommand(_spr, _cbSpriteId.SelectedIndex, _imageEditing));
				_imageEditing = null;
			}

			_spr.Palette.Commands.End();
		}

		public Spr Sprite {
			get { return _spr; }
		}

		private void _mainTabControl_SizeChanged(object sender, SizeChangedEventArgs e) {
			int absoluteTopOffset = 0;
			int top = (int)((_mainGrid.ActualHeight - 550) / 2) - absoluteTopOffset;

			_sce.Margin = new Thickness(0, top, 0, 0);
			_gceControl.Margin = new Thickness(0, top, 0, 0);
		}

		private void _spriteEditorControl_Drop(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
				string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

				if (files != null && files.Length == 1 && files[0].IsExtension(".spr")) {
					try {
						_openFile(new TkPath(files[0]));
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
				}
			}
		}

		private void _spriteEditorControl_DragEnter(object sender, DragEventArgs e) {
			e.Handled = true;

			if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
				string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

				if (files != null && files.Length == 1 && files[0].IsExtension(".spr")) {
					e.Effects = DragDropEffects.Move;
					return;
				}
			}

			e.Effects = DragDropEffects.None;
		}

		private void _spriteViewer_PixelClicked(object sender, int x, int y, bool isWithin) {
			if ((_editMode != EditMode.Select) && !Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt)) {
				_draw(x, y);
				return;
			}

			if (isWithin) {
				GrfImage image = _spr.Images[_cbSpriteId.SelectedIndex];

				_sce.PaletteSelector.SelectedItem = image.Pixels[y * image.Width + x];
				_gceControl.PaletteSelector.SelectedItems.Clear();
				_gceControl.PaletteSelector.AddSelection(image.Pixels[y * image.Width + x]);
			}
		}

		private void _draw(int x, int y) {
			try {
				if (_editMode == EditMode.Eraser) {

				}
				else if (_editMode == EditMode.StampSpecial) {
					if (_getGce().PaletteSelector.SelectedItem == null)
						throw new Exception("You must select 1 gradient to use the Special Stamp tool.");

					if (!_gradientSelection)
						throw new Exception("Please select a gradient for the Special Stamp tool.");
				}
				else if (_editMode == EditMode.Stamp) {
					if (_getGce().PaletteSelector.SelectedItem == null)
						throw new Exception("You must select 1 gradient to use the Stamp tool.");

					if (!_gradientSelection)
						throw new Exception("Please select a gradient for the Stamp tool.");
				}
				else {
					if (_getSce().PaletteSelector.SelectedItems.Count != 1) {
						throw new Exception("You must select 1 color to use the pen.");
					}
				}

				_spriteViewer.CaptureMouse();
				_spriteViewer_MouseMove(null, null);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _spriteViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			if (_spriteViewer.IsMouseCaptured)
				_spriteViewer.ReleaseMouseCapture();

			if (_imageEditing != null) {
				_spr.Palette.Commands.StoreAndExecute(new ImageModifiedCommand(_spr, _cbSpriteId.SelectedIndex, _imageEditing));
				_imageEditing = null;
			}
		}

		private void _spriteViewer_MouseMove(object sender, MouseEventArgs e) {
			if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released && !_spriteViewer.IsMouseCaptured && Keyboard.IsKeyDown(Key.LeftAlt)) {
				_setCursor(EditMode.EyeDrop);
			}
			else if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released && !_spriteViewer.IsMouseCaptured && !Keyboard.IsKeyDown(Key.LeftAlt)) {
				_setCursor(_editMode);
			}
			if (!_spriteViewer.IsMouseCaptured || 
				(_editMode == EditMode.Select) ||
				Mouse.LeftButton != MouseButtonState.Pressed) return;

			Point imagePoint = Mouse.GetPosition(_spriteViewer._imageSprite);
			imagePoint.X = (imagePoint.X) / (_spriteViewer._imageSprite.Width);
			imagePoint.Y = (imagePoint.Y) / (_spriteViewer._imageSprite.Height);

			int x = (int)(_spriteViewer.Bitmap.PixelWidth * imagePoint.X);
			int y = (int)(_spriteViewer.Bitmap.PixelHeight * imagePoint.Y);

			if (imagePoint.X >= 0 && imagePoint.X < 1 &&
			    imagePoint.Y >= 0 && imagePoint.Y < 1) {

				if (_editMode == EditMode.Bucket) {
					if (_imageEditing == null) {
						_imageEditing = _spr.Images[_cbSpriteId.SelectedIndex].Copy();
					}

					if (_imageEditing.Pixels[y * _imageEditing.Width + x] == (byte)_getSce().PaletteSelector.SelectedItems[0])
						return;

					_colorConnected(_imageEditing, x, y, _imageEditing.Pixels[y * _imageEditing.Width + x], (byte)_getSce().PaletteSelector.SelectedItems[0]);
					_spriteViewer.ForceUpdatePreview(_imageEditing.Cast<BitmapSource>());
				}
				else if (_editMode == EditMode.Pen) {
					if (_imageEditing == null) {
						_imageEditing = _spr.Images[_cbSpriteId.SelectedIndex].Copy();
					}

					if (_imageEditing.Pixels[y * _imageEditing.Width + x] == (byte)_getSce().PaletteSelector.SelectedItems[0])
						return;

					_imageEditing.Pixels[y * _imageEditing.Width + x] = (byte)_getSce().PaletteSelector.SelectedItems[0];
					_spriteViewer.ForceUpdatePreview(_imageEditing.Cast<BitmapSource>());
				}
			}

			if (_editMode == EditMode.Stamp) {
				if (!_gradientSelection || _getGce().PaletteSelector.SelectedItem == null) {
					throw new Exception("Please select a gradient for the Stamp tool.");
				}

				if (_imageEditing == null) {
					_imageEditing = _spr.Images[_cbSpriteId.SelectedIndex].Copy();
				}

				bool edited = false;

				for (int bx = 0; bx < 2 * ActEditorConfiguration.BrushSize + 1; bx++) {
					for (int by = 0; by < 2 * ActEditorConfiguration.BrushSize + 1; by++) {
						if (_brush[bx, by] == 0)
							continue;

						int ix = bx - ActEditorConfiguration.BrushSize + x;
						int iy = by - ActEditorConfiguration.BrushSize + y;

						if (ix < 0 || ix >= _imageEditing.Width ||
							iy < 0 || iy >= _imageEditing.Height)
							continue;

						// ReSharper disable once PossibleInvalidOperationException
						int selected = _getGce().PaletteSelector.SelectedItem.Value / 8;
						int pixelOffset = ix + _imageEditing.Width * iy;
						int pixel = _imageEditing.Pixels[pixelOffset];
						int newPixel = (byte)(selected * 8 + (pixel % 8));

						if (pixel == 0)
							continue;

						if (_stampLock.Count > 0 && !_stampLock.Contains(_imageEditing.Pixels[pixelOffset]))
							continue;

						if (_imageEditing.Pixels[pixelOffset] != newPixel) {
							edited = true;
						}

						_imageEditing.Pixels[pixelOffset] = (byte)(selected * 8 + (pixel % 8));
					}
				}

				if (!edited && sender != null)
					return;
				
				_spriteViewer.ForceUpdatePreview(_imageEditing.Cast<BitmapSource>());
			}
			else if (_editMode == EditMode.StampSpecial) {
				if (!_gradientSelection || _getGce().PaletteSelector.SelectedItem == null) {
					throw new Exception("Please select a gradient for the Special Stamp tool.");
				}

				if (_imageEditing == null) {
					_imageEditing = _spr.Images[_cbSpriteId.SelectedIndex].Copy();
				}

				bool edited = false;

				int left = x - _specialImage.Width / 2;
				int top = y - _specialImage.Height / 2;

				for (int xx = 0; xx < _specialImage.Width; xx++) {
					for (int yy = 0; yy < _specialImage.Height; yy++) {
						int targetX = xx + left;
						int targetY = yy + top;

						if (targetX < 0 || targetX >= _imageEditing.Width || targetY < 0 || targetY >= _imageEditing.Height)
							continue;

						byte p = _specialImage.Pixels[xx + yy * _specialImage.Width];

						if (p == 0)
							continue;

						if (_imageEditing.Pixels[targetX + targetY * _imageEditing.Width] != p) {
							edited = true;
						}

						_imageEditing.Pixels[targetX + targetY * _imageEditing.Width] = p;
					}
				}

				if (!edited && sender != null)
					return;

				_spriteViewer.ForceUpdatePreview(_imageEditing.Cast<BitmapSource>());
			}

			if (_editMode == EditMode.Eraser) {
				if (_imageEditing == null) {
					_imageEditing = _spr.Images[_cbSpriteId.SelectedIndex].Copy();
				}

				bool edited = false;

				for (int bx = 0; bx < 2 * ActEditorConfiguration.BrushSize + 1; bx++) {
					for (int by = 0; by < 2 * ActEditorConfiguration.BrushSize + 1; by++) {
						if (_brush[bx, by] == 0)
							continue;

						int ix = bx - ActEditorConfiguration.BrushSize + x;
						int iy = by - ActEditorConfiguration.BrushSize + y;

						if (ix < 0 || ix >= _imageEditing.Width ||
							iy < 0 || iy >= _imageEditing.Height)
							continue;

						// ReSharper disable once PossibleInvalidOperationException
						int pixelOffset = ix + _imageEditing.Width * iy;
						int pixel = _imageEditing.Pixels[pixelOffset];

						if (pixel == 0)
							continue;

						if (_imageEditing.Pixels[pixelOffset] != 0) {
							edited = true;
						}

						_imageEditing.Pixels[pixelOffset] = 0;
					}
				}

				if (!edited && sender != null)
					return;

				_spriteViewer.ForceUpdatePreview(_imageEditing.Cast<BitmapSource>());
			}
		}

		private void _colorConnected(GrfImage image, int x, int y, byte target, byte newIndex) {
			if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
				return;

			if (image.Pixels[y * image.Width + x] != target)
				return;

			image.Pixels[y * image.Width + x] = newIndex;
			_colorConnected(image, x - 1, y, target, newIndex);
			_colorConnected(image, x + 1, y, target, newIndex);
			_colorConnected(image, x, y - 1, target, newIndex);
			_colorConnected(image, x, y + 1, target, newIndex);
		}

		private void _cbSpriteId_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbSpriteId.SelectedIndex < 0) {
				_spriteViewer.Clear();
			}
			else {
				_spriteViewer.LoadIndexed8(_cbSpriteId.SelectedIndex);
			}
		}

		private bool _openFromFile(string file) {
			try {
				if (file.IsExtension(".spr")) {
					Spr spr = new Spr(file);

					if (spr.NumberOfIndexed8Images <= 0) {
						throw new Exception("The sprite file does not contain a palette (probably because it doesn't have any Indexed8 images). You must add one for a palette to be created.");
					}

					_recentFiles.AddRecentFile(file);
					_set(spr);
				}
				else if (file.IsExtension(".pal")) {
					Pal pal = new Pal(File.ReadAllBytes(file));
					pal.BytePalette[3] = 0;

					if (_spr == null)
						throw new Exception("No sprite has been loaded yet.");

					_recentFiles.AddRecentFile(file);
					_spr.Palette.Commands.SetPalette(pal.BytePalette);
				}
				else {
					return false;
				}

				_mainGrid.IsEnabled = true;

				return true;
			}
			catch (Exception err) {
				_recentFiles.RemoveRecentFile(file);
				ErrorHandler.HandleException(err);
			}

			_mainGrid.IsEnabled = false;
			return false;
		}

		private void _set(Spr spr) {
			_spr = spr;
			_spriteViewer.SetSpr(spr);

			_cbSpriteId.Items.Clear();

			for (int i = 0; i < _spr.NumberOfIndexed8Images; i++) {
				_cbSpriteId.Items.Add(i);
			}

			_cbSpriteId.SelectedIndex = 0;

			if (_spr.Palette == null) {
				_spr.Palette = new Pal();
			}

			_tmbUndo.SetUndo(_spr.Palette.Commands);
			_tmbRedo.SetRedo(_spr.Palette.Commands);

			_spr.Palette.PaletteChanged += new Pal.PalEventHandler(_pal_PaletteChanged);

			_sce.SetPalette(_spr.Palette);
			_gceControl.SetPalette(_spr.Palette);
		}

		private void _pal_PaletteChanged(object sender) {
			_spriteViewer.LoadIndexed8(_cbSpriteId.SelectedIndex);
		}

		private void _paletteSelector_SelectionChanged(object sender, ObservabableListEventArgs args) {
			if (args.Items.Count == 0)
				return;

			bool valid = false;

			if ((_gradientSelection && args.Items.Count > 1) ||
				(!_gradientSelection && args.Items.Count == 1)) {
				valid = true;
			}

			if (!valid)
				return;

			foreach (int index in args.Items)
				if (_pixelGlow[index] <= 0.5f)
					_pixelGlow[index] = 1f;

			StartPixelAnimator();

			if (_gradientSelection)
				_gceControl.FocusGrid();
			else
				_sce.FocusGrid();
		}

		public void StartPixelAnimator() {
			if (_animToken != null)
				return;

			_animToken = new CancellationTokenSource();
			_ = _animateAsync(_animToken.Token);
		}

		private async Task _animateAsync(CancellationToken token) {
			const int delay = 50;
			const float decay = 0.08f;

			while (!token.IsCancellationRequested) {
				bool anyAlive = false;

				for (int i = 0; i < _pixelGlow.Length; i++) {
					if (_pixelGlow[i] > 0f) {
						_pixelGlow[i] -= decay;
						if (_pixelGlow[i] < 0f)
							_pixelGlow[i] = 0f;
						anyAlive = true;
					}
				}

				if (!anyAlive)
					break;

				RenderGlowFrame();

				await Task.Delay(delay);
			}

			_animToken = null;
		}

		public void RenderGlowFrame() {
			var image = _spr.Images[_cbSpriteId.Dispatch(p => p.SelectedIndex)].Copy();

			Buffer.BlockCopy(_spr.Palette.BytePalette, 0, _palette, 0, 1024);

			for (int i = 0; i < _pixelGlow.Length; i++) {
				float g = _pixelGlow[i];
				if (g <= 0f)
					continue;

				int idx = i * 4;

				_palette[idx] = (byte)(_palette[idx] + g * (255 - _palette[idx]));
				_palette[idx + 1] = (byte)(_palette[idx + 1] * (1f - g));
				_palette[idx + 2] = (byte)(_palette[idx + 2] * (1f - g));
			}

			image.SetPalette(ref _palette);
			_spriteViewer.Dispatch(p => p.LoadImage(image));
		}

		private bool _openFile(TkPath file) {
			try {
				if (String.IsNullOrEmpty(file.RelativePath)) {
					return _openFromFile(file.FilePath);
				}

				if (!File.Exists(file.FilePath)) {
					_recentFiles.RemoveRecentFile(file.GetFullPath());
					return false;
				}

				_recentFiles.AddRecentFile(file.GetFullPath());

				TkPath imPath = new TkPath(file);

				byte[] data = null;

				using (GrfHolder grf = new GrfHolder(file.FilePath)) {
					if (grf.FileTable.ContainsFile(file.RelativePath))
						data = grf.FileTable[file.RelativePath].GetDecompressedData();
				}

				if (data == null) {
					ErrorHandler.HandleException("File not found: " + file);
					return false;
				}

				if (imPath.RelativePath.IsExtension(".spr")) {
					Spr spr = new Spr(data);
					spr.LoadedPath = imPath.ToString();

					if (spr.NumberOfIndexed8Images <= 0) {
						throw new Exception("The sprite file does not contain a palette (probably because it doesn't have any Indexed8 images). You must add one for a palette to be created.");
					}

					_recentFiles.AddRecentFile(imPath.ToString());
					_set(spr);
					_mainGrid.IsEnabled = true;
					return true;
				}
				else if (imPath.RelativePath.IsExtension(".pal")) {
					Pal pal = new Pal(data);
					pal.BytePalette[3] = 0;

					_recentFiles.AddRecentFile(imPath.ToString());
					//_spr.Palette.Commands.SetPalette(pal.BytePalette);
					_spr.Palette.Commands.SetRawBytesInPalette(0, pal.BytePalette);
					return true;
				}
				else {
					ErrorHandler.HandleException("File format not supported: " + file);
					return false;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			return false;
		}

		private void _miOpenFromGrf(string grfPath) {
			try {
				string file = grfPath ?? TkPathRequest.OpenFile<ActEditorConfiguration>("AppLastGrfPath", "filter", FileFormat.MergeFilters(Format.AllContainers, Format.Grf, Format.Gpf, Format.Thor));

				if (file != null) {
					GrfExplorer dialog = new GrfExplorer(file, SelectMode.Pal);
					dialog.Owner = WpfUtilities.TopWindow;
					IsEnabled = false;

					try {
						if (dialog.ShowDialog() == true) {
							string relativePath = dialog.SelectedItem;

							if (relativePath == null) return;

							if (!relativePath.IsExtension(".pal", ".spr")) {
								throw new Exception("Only PAL or SPR files can be selected.");
							}

							_openFile(new TkPath(file, relativePath));
						}
					}
					finally {
						IsEnabled = true;
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _menuItemOpen_Click(object sender, RoutedEventArgs e) {
			try {
				string file = TkPathRequest.OpenFile(new Setting(v => Configuration.ConfigAsker["[ActEditor - App recent]"] = v.ToString(), () => Configuration.ConfigAsker["[ActEditor - App recent]", "C:\\"]), "filter", "All files|*.pal;*.spr;*.grf;*.gpf;*.thor");

				if (file != null) {
					if (file.IsExtension(".grf", ".thor", ".gpf")) {
						_miOpenFromGrf(file);
						return;
					}

					_openFile(new TkPath(file));
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void SaveAs(string file) {
			try {
				if (_spr == null) return;
				if (file != null) {
					if (file.Contains("?")) throw new Exception("The file couldn't be saved because of an invalid location (you cannot save inside a GRF).");

					if (file.IsExtension(".spr")) {
						try {
							_spr.Palette.EnableRaiseEvents = false;
							_spr.Palette.MakeFirstColorUnique();
							_spr.Palette[3] = 255;

							_spr.Save(file.ReplaceExtension(".spr"));
							_spr.Palette[3] = 0;
						}
						finally {
							_spr.Palette.EnableRaiseEvents = true;
						}
					}
					else {
						try {
							_spr.Palette.EnableRaiseEvents = false;
							_spr.Palette.MakeFirstColorUnique();
							_spr.Palette[3] = 255;

							_spr.Palette.Save(file.ReplaceExtension(".pal"));
							_spr.Palette[3] = 0;
						}
						finally {
							_spr.Palette.EnableRaiseEvents = true;
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _menuItemSave_Click(object sender, RoutedEventArgs e) {
			Save();
		}

		private void _menuItemClose_Click(object sender, RoutedEventArgs e) {
			WpfUtilities.FindParentControl<Window>(this).Close();
		}

		private void _menuItemSaveAs_Click(object sender, RoutedEventArgs e) {
			SaveAs(TkPathRequest.SaveFile(new Setting(v => Configuration.ConfigAsker["[ActEditor - App recent]"] = v.ToString(), () => Configuration.ConfigAsker["[ActEditor - App recent]", "C:\\"]),
										  "filter", "Sprite and Palette Files|*.spr;*.pal|Sprite Files|*.spr|Palette Files|*.pal"));
		}

		public void Save() {
			try {
				if (_spr == null) return;
				SaveAs(_spr.LoadedPath);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool Open(string file) {
			try {
				Spr spr = new Spr(file);

				if (spr.NumberOfIndexed8Images <= 0) {
					throw new Exception("The sprite file does not contain a palette (probably because it doesn't have any Indexed8 images). You must add one for a palette to be created.");
				}

				//_recentFiles.AddRecentFile(file);
				_set(spr);
				_mainGrid.IsEnabled = true;
				return true;
			}
			catch (Exception err) {
				_recentFiles.RemoveRecentFile(file);
				ErrorHandler.HandleException(err);
			}

			_mainGrid.IsEnabled = false;
			return false;
		}

		private SingleColorEditControl _getSce() {
			return _sce;
		}

		private GradientColorEditControl _getGce() {
			return _gceControl;
		}

		private void _menuItemSwitchGradient_Click(object sender, RoutedEventArgs e) {
			try {
				bool fixIndex = sender == _menuItemSwitchGradient1 || sender == _menuItemSwitchGradient2;
				bool fixGradient = sender == _menuItemSwitchGradient1 || sender == _menuItemSwitchGradient3;
				
				if (_gradientSelection == false) {
					if (_getSce().PaletteSelector.SelectedItems.Count != 2) {
						throw new Exception("You must select two colors to switch them.");
					}

					try {
						_spr.Palette.Commands.BeginNoDelay();

						Spr oldSprite = new Spr(_spr);
						Spr newSprite = new Spr(_spr);

						byte p1 = (byte) _getSce().PaletteSelector.SelectedItems[0];
						byte p2 = (byte) _getSce().PaletteSelector.SelectedItems[1];

						for (int i = 0; i < newSprite.NumberOfIndexed8Images; i++) {
							var image = newSprite.Images[i];

							for (int k = 0; k < image.Pixels.Length; k++) {
								if (image.Pixels[k] == p1) {
									image.Pixels[k] = p2;
								}
								else if (image.Pixels[k] == p2) {
									image.Pixels[k] = p1;
								}
							}
						}

						var d1 = new byte[4];
						var d2 = new byte[4];

						Buffer.BlockCopy(_spr.Palette.BytePalette, p1 * 4, d1, 0, 4);
						Buffer.BlockCopy(_spr.Palette.BytePalette, p2 * 4, d2, 0, 4);

						if (fixGradient) {
							_spr.Palette.Commands.SetRawBytesInPalette(p1 * 4, d2);
							_spr.Palette.Commands.SetRawBytesInPalette(p2 * 4, d1);
						}
						if (fixIndex) {
							_spr.Palette.Commands.StoreAndExecute(new SpriteModifiedCommand(_spr, oldSprite, newSprite));
						}
					}
					finally {
						_spr.Palette.Commands.End();
					}
				}
				else if (_gradientSelection == true) {
					if (_getGce().PaletteSelector.SelectedItems.Count != 16) {
						throw new Exception("You must select two gradients to switch them.");
					}

					try {
						_spr.Palette.Commands.BeginNoDelay();

						Spr oldSprite = new Spr(_spr);
						Spr newSprite = new Spr(_spr);

						byte p1 = (byte)_getGce().PaletteSelector.SelectedItems[0];
						byte p2 = (byte)_getGce().PaletteSelector.SelectedItems[8];

						for (int i = 0; i < newSprite.NumberOfIndexed8Images; i++) {
							var image = newSprite.Images[i];

							for (int k = 0; k < image.Pixels.Length; k++) {
								if (image.Pixels[k] >= p1 && image.Pixels[k] < p1 + 8) {
									image.Pixels[k] = (byte)(p2 + image.Pixels[k] - p1);
								}
								else if (image.Pixels[k] >= p2 && image.Pixels[k] < p2 + 8) {
									image.Pixels[k] = (byte)(p1 + image.Pixels[k] - p2);
								}
							}
						}

						var d1 = new byte[4 * 8];
						var d2 = new byte[4 * 8];

						Buffer.BlockCopy(_spr.Palette.BytePalette, p1 * 4, d1, 0, 4 * 8);
						Buffer.BlockCopy(_spr.Palette.BytePalette, p2 * 4, d2, 0, 4 * 8);

						if (fixGradient) {
							_spr.Palette.Commands.SetRawBytesInPalette(p1 * 4, d2);
							_spr.Palette.Commands.SetRawBytesInPalette(p2 * 4, d1);
						}
						if (fixIndex) {
							_spr.Palette.Commands.StoreAndExecute(new SpriteModifiedCommand(_spr, oldSprite, newSprite));
						}
					}
					finally {
						_spr.Palette.Commands.End();
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _menuItemStampLock_Click(object sender, RoutedEventArgs e) {
			try {
				if (_menuItemStampLock.IsChecked) {
					_stampLock.Clear();
					_menuItemStampLock.IsChecked = false;
					return;
				}

				_stampLock = _getGce().PaletteSelector.SelectedItems.ToList();
				_menuItemStampLock.IsChecked = true;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _menuItemSwitchGradient4_Click(object sender, RoutedEventArgs e) {
			try {
				if (_gradientSelection == false) {
					if (_getSce().PaletteSelector.SelectedItems.Count != 2) {
						throw new Exception("You must select two colors to switch them.");
					}

					try {
						_spr.Palette.Commands.BeginNoDelay();

						Spr oldSprite = new Spr(_spr);
						Spr newSprite = new Spr(_spr);

						byte p1 = (byte)_getSce().PaletteSelector.SelectedItems[0];
						byte p2 = (byte)_getSce().PaletteSelector.SelectedItems[1];

						for (int i = 0; i < newSprite.NumberOfIndexed8Images; i++) {
							var image = newSprite.Images[i];

							for (int k = 0; k < image.Pixels.Length; k++) {
								if (image.Pixels[k] == p1) {
									image.Pixels[k] = p2;
								}
							}
						}

						_spr.Palette.Commands.StoreAndExecute(new SpriteModifiedCommand(_spr, oldSprite, newSprite));
					}
					finally {
						_spr.Palette.Commands.End();
					}
				}
				else {
					if (_getGce().PaletteSelector.SelectedItems.Count != 16) {
						throw new Exception("You must select two gradients to switch them.");
					}

					try {
						_spr.Palette.Commands.BeginNoDelay();

						Spr oldSprite = new Spr(_spr);
						Spr newSprite = new Spr(_spr);

						byte p1 = (byte)_getGce().PaletteSelector.SelectedItems[0];
						byte p2 = (byte)_getGce().PaletteSelector.SelectedItems[8];

						for (int i = 0; i < newSprite.NumberOfIndexed8Images; i++) {
							var image = newSprite.Images[i];

							for (int k = 0; k < image.Pixels.Length; k++) {
								if (image.Pixels[k] >= p1 && image.Pixels[k] < p1 + 8) {
									image.Pixels[k] = (byte)(p2 + image.Pixels[k] - p1);
								}
							}
						}

						_spr.Palette.Commands.StoreAndExecute(new SpriteModifiedCommand(_spr, oldSprite, newSprite));
					}
					finally {
						_spr.Palette.Commands.End();
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonBucket_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.Bucket);
		}

		private void _setEditMode(EditMode mode) {
			_editMode = mode;
			_generateBrush();

			switch(_editMode) {
				case EditMode.StampSpecial:
					_specialImage = _spr.Images[_cbSpriteId.SelectedIndex].Copy();
					int total = _specialImage.Height * _specialImage.Width;
					int selected = _getGce().PaletteSelector.SelectedItem.Value;

					for (int i = 0; i < total; i++) {
						if (_specialImage.Pixels[i] < selected || _specialImage.Pixels[i] >= selected + 8) {
							_specialImage.Pixels[i] = 0;
						}
					}

					break;
				case EditMode.Pen:
				case EditMode.Bucket:
				case EditMode.Select:
					GrfImage image = _spr.Images[_cbSpriteId.Dispatch(p => p.SelectedIndex)];
					image = image.Copy();
					_spriteViewer.Dispatch(p => p.LoadImage(image));
					break;
			}
		}

		private void _setCursor(EditMode mode) {
			switch (mode) {
				case EditMode.EyeDrop:
					if (CursorEyedrop == null)
						CursorEyedrop = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_eyedrop.png"), Width = 16, Height = 16 }, new Point() { X = 2, Y = 14 });

					if (CursorEyedrop != null) {
						Mouse.OverrideCursor = CursorEyedrop;
					}

					break;
				case EditMode.Pen:
					if (CursorPen == null)
						CursorPen = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_pen.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });

					if (CursorPen != null) {
						Mouse.OverrideCursor = CursorPen;
					}

					break;
				case EditMode.Bucket:
					if (CursorBucket == null)
						CursorBucket = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_bucket.png"), Width = 16, Height = 16 }, new Point() { X = 14, Y = 15 });

					if (CursorBucket != null) {
						Mouse.OverrideCursor = CursorBucket;
					}

					break;
				case EditMode.StampSpecial:
				case EditMode.Stamp:
					if (CursorStamp == null)
						CursorStamp = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_brush.png"), Width = 16, Height = 16 }, new Point() { X = 9, Y = 8 });

					if (CursorStamp != null) {
						Mouse.OverrideCursor = CursorStamp;
					}

					break;
				case EditMode.Eraser:
					if (CursorEraser == null)
						CursorEraser = CursorHelper.CreateCursor(new Image() { Source = ApplicationManager.PreloadResourceImage("cs_eraser.png"), Width = 16, Height = 16 }, new Point() { X = 8, Y = 8 });

					if (CursorEraser != null) {
						Mouse.OverrideCursor = CursorEraser;
					}

					break;
				case EditMode.Select:
					Mouse.OverrideCursor = null;
					break;
			}
		}

		private void _setPixel(int x, int y) {
			if (y < 0 || y >= 2 * ActEditorConfiguration.BrushSize + 1)
				return;
			if (x < 0 || x >= 2 * ActEditorConfiguration.BrushSize + 1)
				return;

			_brush[y, x] = 1;
		}

		private void _setBrushPixel(int cx, int cy, int x, int y) {
			_horizontalLine(cx - x, cy + y, cx + x);
			if (y != 0)
				_horizontalLine(cx - x, cy - y, cx + x);
		}

		private void _horizontalLine(int x0, int y0, int x1) {
			for (int x = x0; x <= x1; ++x)
				_setPixel(x, y0);
		}

		private void _generateBrush() {
			_brush = new int[2 * ActEditorConfiguration.BrushSize + 1, 2 * ActEditorConfiguration.BrushSize + 1];

			int radius = ActEditorConfiguration.BrushSize;

			int error = -radius;
			int x = radius;
			int y = 0;
			int x0 = ActEditorConfiguration.BrushSize;
			int y0 = ActEditorConfiguration.BrushSize;

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

		private void _buttonSelect(FancyButton exceptionButton) {
			FancyButton[] buttons = new FancyButton[] {
				_buttonPen,
				_buttonSelection,
				_buttonBucket,
				_buttonStamp,
				_buttonStamp2,
				_buttonEraser,
			};


			foreach (var button in buttons) {
				if (button == exceptionButton)
					continue;

				button.IsPressed = false;
			}

			if (exceptionButton == _buttonStamp2 && _buttonStamp2.IsPressed) {
				_buttonStamp2.IsPressed = false;
				_buttonSelection.IsPressed = true;
				_setEditMode(EditMode.Select);
				return;
			}

			exceptionButton.IsPressed = true;
		}

		private void _buttonPen_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.Pen);
		}

		private void _buttonSelection_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.Select);
		}

		private void _buttonBrush_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.Brush);
		}

		private void _buttonStamp_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.Stamp);
		}

		private void _buttonStamp2_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.StampSpecial);
		}

		private void _buttonEraser_Click(object sender, RoutedEventArgs e) {
			_buttonSelect((FancyButton)sender);
			_setEditMode(EditMode.Eraser);
		}

		private void _menuItemBrushPlus_Click(object sender, RoutedEventArgs e) {
			_brushIncrease(1);
		}

		private void _menuItemBrushMinus_Click(object sender, RoutedEventArgs e) {
			_brushIncrease(-1);
		}

		private void _menuItemPaletteSelector_Click(object sender, RoutedEventArgs e) {
			WindowProvider.Show(new PalettePreset(), _menuItemPaletteSelector, WpfUtilities.FindDirectParentControl<Window>(this));
		}
	}
}