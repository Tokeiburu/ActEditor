using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GrfToWpfBridge.Application;
using PaletteEditor;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
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
		private bool _gradientSelection = false;
		private CancellationTokenSource _animToken;
		private readonly float[] _pixelGlow = new float[256];
		private SpriteBrush _brush = new SpriteBrush();
		private SpriteLoadAndSaveService _spriteLoadSaveService = new SpriteLoadAndSaveService();
		public Spr Sprite => _spr;

		private SpriteEditorTool _selectTool;
		private SpriteEditorTool _penTool;
		private SpriteEditorTool _pickerTool;
		private SpriteEditorTool _bucketTool;
		private SpriteEditorTool _eraserTool;
		private SpriteEditorTool _stampTool;
		private SpriteEditorTool _stampSpecialTool;
		private SpriteEditorTool _rectangleTool;
		private SpriteEditorTool _lineTool;
		private SpriteEditorTool _ellipseTool;
		private SpriteEditorTool _currentTool = null;

		private SpriteEditorState _state = new SpriteEditorState();

		public SpriteEditorControl() {
			InitializeComponent();

			_recentFiles = new WpfRecentFiles(Configuration.ConfigAsker, 6, _menuItemOpenRecent, "Sprite editor");
			_recentFiles.FileClicked += f => Load(new TkPath(f));
			
			_sce.PaletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;
			_gceControl.PaletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;

			_initializeShortcuts();
			_initializeSpriteEditorState();
			
			Loaded += delegate {
				Keyboard.Focus(_focusDummy);
				_focusDummy.Focus();

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
			};

			Unloaded += delegate {
				Debug.Ignore(() => _animToken?.Cancel());
			};

			_initializeBrushTools();
		}

		private void _initializeSpriteEditorState() {
			_state.SpriteViewer = _spriteViewer;
			_state.SingleEditor = _sce;
			_state.GradientEditor = _gceControl;
			_state.Brush = _brush;

			_state.ImageInvalidated += image => {
				_spriteViewer.Dispatch(p => p.LoadImage(image));
			};
		}

		private void _initializeBrushTools() {
			_selectTool = new SelectTool(_buttonSelection);
			_penTool = new PenTool(_buttonPen);
			_lineTool = new LineTool(_buttonLine);
			_ellipseTool = new EllipseTool(_buttonEllipse);
			_pickerTool = new PickerTool(null);
			_bucketTool = new BucketTool(_buttonBucket);
			_eraserTool = new EraserTool(_buttonEraser);
			_stampTool = new StampTool(_buttonStamp);
			_stampSpecialTool = new StampSpecialTool(_buttonStamp2);
			_rectangleTool = new RectangleTool(_buttonRectangle);
			_currentTool = _selectTool;
		}

		private void _initializeShortcuts() {
			ApplicationShortcut.Link(ApplicationShortcut.Save, () => _menuItemSave_Click(null, null), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Q", "SpriteEditor.Select"), () => SetTool(_selectTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-B", "SpriteEditor.Bucket"), () => SetTool(_bucketTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-T", "SpriteEditor.Stamp"), () => SetTool(_stampTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-E", "SpriteEditor.Eraser"), () => SetTool(_eraserTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-P", "SpriteEditor.Pen"), () => SetTool(_penTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-R", "SpriteEditor.Rectangle"), () => SetTool(_rectangleTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-L", "SpriteEditor.Line"), () => SetTool(_lineTool), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-I", "SpriteEditor.Circle"), () => SetTool(_ellipseTool), this);
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
			ActEditorConfiguration.BrushSize = Methods.Clamp(ActEditorConfiguration.BrushSize + amount, 0, 15);
			_brush.UpdateBrush(ActEditorConfiguration.BrushSize);
			UpdateTool();
		}

		private void UpdateTool() {
			try {
				Point imagePoint = Mouse.GetPosition(_spriteViewer._imageSprite);
				imagePoint.X = (imagePoint.X) / (_spriteViewer._imageSprite.Width);
				imagePoint.Y = (imagePoint.Y) / (_spriteViewer._imageSprite.Height);

				int x = (int)(_spriteViewer.Bitmap.PixelWidth * imagePoint.X);
				int y = (int)(_spriteViewer.Bitmap.PixelHeight * imagePoint.Y);

				_currentTool?.OnPixelMoved(_getCurrentState(), this, x, y);
			}
			catch {
			}
		}

		private void _spriteEditorControl_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (ApplicationShortcut.IsCommandActive())
				return;

			if (e.SystemKey == Key.LeftAlt) {
				e.Handled = true;

				if (_spriteViewer.IsMouseOver) {
					Mouse.OverrideCursor = e.IsDown ? _pickerTool?.Cursor : _currentTool?.Cursor;
				}
			}
			else {
				if (_spriteViewer.IsMouseOver) {
					Mouse.OverrideCursor = _currentTool?.Cursor;
				}
			}
		}

		private void _spriteViewer_LostMouseCapture(object sender, MouseEventArgs e) {
			EndEdit();
		}

		private void _spriteEditorControl_Drop(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
				string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

				if (files != null && files.Length == 1 && files[0].IsExtension(".spr", ".pal")) {
					try {
						Load(new TkPath(files[0]));
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

				if (files != null && files.Length == 1 && files[0].IsExtension(".spr", ".pal")) {
					e.Effects = DragDropEffects.Move;
					return;
				}
			}

			e.Effects = DragDropEffects.None;
		}

		private void _spriteViewer_PixelClicked(object sender, int x, int y, bool isWithin) {
			try {
				if ((_currentTool != _selectTool) && !Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt)) {
					_currentTool.OnPixelMoved(_getCurrentState(), null, x, y);
					return;
				}

				if (isWithin) {
					GrfImage image = _spr.Images[_cbSpriteId.SelectedIndex];

					_sce.PaletteSelector.SelectedItem = image.Pixels[y * image.Width + x];
					_gceControl.PaletteSelector.SelectedItems.Clear();
					_gceControl.PaletteSelector.AddSelection(image.Pixels[y * image.Width + x]);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _spriteViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			EndEdit();
		}

		public void EndEdit() {
			if (_spr == null)
				return;
			
			if (_spriteViewer.IsMouseCaptured)
				_spriteViewer.ReleaseMouseCapture();

			if (_state.IsEditing) {
				if (!Methods.ByteArrayCompare(_state.EditingImage.Pixels, _state.SelectedImage.Pixels)) {
					_spr.Palette.Commands.StoreAndExecute(new ImageModifiedCommand(_spr, _cbSpriteId.SelectedIndex, _state.EditingImage));
				}

				_state.EditingImage = null;
				_state.IsEditing = false;
			}

			_spr.Palette.Commands.End();
		}

		private void _spriteViewer_MouseMove(object sender, MouseEventArgs e) {
			if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released && !_spriteViewer.IsMouseCaptured && Keyboard.IsKeyDown(Key.LeftAlt)) {
				Mouse.OverrideCursor = _pickerTool?.Cursor;
			}
			else if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released && !_spriteViewer.IsMouseCaptured && !Keyboard.IsKeyDown(Key.LeftAlt)) {
				Mouse.OverrideCursor = _currentTool?.Cursor;
			}

			UpdateTool();
		}

		private SpriteEditorState _getCurrentState() {
			_state.SelectedImage = _spr.Images[_state.SelectedSpriteIndex];
			_state.IsGradientEditorSelected = _gradientSelection;
			_state.IsSingleEditorSelected = !_gradientSelection;
			return _state;
		}

		private void _cbSpriteId_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbSpriteId.SelectedIndex < 0) {
				_spriteViewer.Clear();
				_state.SelectedImage = null;
			}
			else {
				_spriteViewer.LoadIndexed8(_cbSpriteId.SelectedIndex);
				_state.SelectedImage = _spr.Images[_cbSpriteId.SelectedIndex];
			}

			_state.SelectedSpriteIndex = _cbSpriteId.SelectedIndex;
		}

		private void _set(Spr spr) {
			_spr = spr;
			_spriteViewer.SetSpr(spr);

			_cbSpriteId.ItemsSource = Enumerable.Range(0, _spr.NumberOfIndexed8Images).ToList();
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

			if (args.Action != ObservableListEventType.Removed) {
				foreach (int index in args.Items)
					if (_pixelGlow[index] <= 0.5f)
						_pixelGlow[index] = 1f;

				StartPixelAnimator();

				if (_gradientSelection)
					_gceControl.FocusGrid();
				else
					_sce.FocusGrid();
			}
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

		public bool Load(TkPath file) {
			try {
				var result = _spriteLoadSaveService.Load(file);
				
				if (result.AddToRecentFiles)
					_recentFiles.AddRecentFile(result.FilePath);
				if (result.RemoveToRecentFiles)
					_recentFiles.RemoveRecentFile(result.FilePath);
				if (result.ErrorMessage != null)
					ErrorHandler.HandleException(result.ErrorMessage);
				if (!result.Success)
					return false;

				if (result.UpdatePalette) {
					_spr.Palette.Commands.SetPalette(result.LoadedPal.BytePalette);
				}
				else {
					_set(result.LoadedSpr);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			return true;
		}

		private void _menuItemOpen_Click(object sender, RoutedEventArgs e) {
			try {
				string file = TkPathRequest.OpenFile(new Setting(v => Configuration.ConfigAsker["[ActEditor - App recent]"] = v.ToString(), () => Configuration.ConfigAsker["[ActEditor - App recent]", "C:\\"]), "filter", "All files|*.pal;*.spr;*.grf;*.gpf;*.thor");

				if (file != null) {
					Load(new TkPath(file));
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void SaveAs(string file) {
			try {
				var result = _spriteLoadSaveService.Save(file, _spr);

				if (result.ErrorMessage != null)
					ErrorHandler.HandleException(result.ErrorMessage);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _menuItemClose_Click(object sender, RoutedEventArgs e) {
			WpfUtilities.FindParentControl<Window>(this).Close();
		}

		private void _menuItemSave_Click(object sender, RoutedEventArgs e) => SaveAs(_spr?.LoadedPath);

		private void _menuItemSaveAs_Click(object sender, RoutedEventArgs e) {
			SaveAs(TkPathRequest.SaveFile(new Setting(v => Configuration.ConfigAsker["[ActEditor - App recent]"] = v.ToString(), () => Configuration.ConfigAsker["[ActEditor - App recent]", "C:\\"]), "filter", "Sprite and Palette Files|*.spr;*.pal|Sprite Files|*.spr|Palette Files|*.pal"));
		}

		private void _menuItemSwapPaletteColors_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGradientChange(_spr, _getCurrentState(), GradientOperation.SwapPaletteColors);
		private void _menuItemSwapSpriteIndexes_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGradientChange(_spr, _getCurrentState(), GradientOperation.SwapSpriteIndexes);
		private void _menuItemSwapColorsAndIndexes_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGradientChange(_spr, _getCurrentState(), GradientOperation.SwapSpriteIndexesAndPaletteColors);
		private void _menuItemRedirectTo_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGradientChange(_spr, _getCurrentState(), GradientOperation.Redirect);

		private void _menuItemStampLock_Click(object sender, RoutedEventArgs e) {
			_menuItemStampLock.IsChecked = _menuItemStampLock.IsChecked != true;
			_stampTool?.StampLock(_getCurrentState(), _menuItemStampLock.IsChecked == true);
		}

		public void SetTool(SpriteEditorTool tool) {
			try {
				if (tool == _currentTool)
					return;

				_brush.UpdateBrush(ActEditorConfiguration.BrushSize);
				_currentTool?.Unselect(_getCurrentState());
				_currentTool = tool;
				_currentTool.Select(_getCurrentState());
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonBucket_Click(object sender, RoutedEventArgs e) => SetTool(_bucketTool);
		private void _buttonPen_Click(object sender, RoutedEventArgs e) => SetTool(_penTool);
		private void _buttonSelection_Click(object sender, RoutedEventArgs e) => SetTool(_selectTool);
		private void _buttonStamp_Click(object sender, RoutedEventArgs e) => SetTool(_stampTool);
		private void _buttonStamp2_Click(object sender, RoutedEventArgs e) => SetTool(_stampSpecialTool);
		private void _buttonEraser_Click(object sender, RoutedEventArgs e) => SetTool(_eraserTool);
		private void _rectangleTool_Click(object sender, RoutedEventArgs e) => SetTool(_rectangleTool);
		private void _buttonLine_Click(object sender, RoutedEventArgs e) => SetTool(_lineTool);
		private void _buttonEllipse_Click(object sender, RoutedEventArgs e) => SetTool(_ellipseTool);

		private void _menuItemBrushPlus_Click(object sender, RoutedEventArgs e) => _brushIncrease(1);
		private void _menuItemBrushMinus_Click(object sender, RoutedEventArgs e) => _brushIncrease(-1);

		private void _menuItemPaletteSelector_Click(object sender, RoutedEventArgs e) {
			WindowProvider.Show(new PalettePreset(), _menuItemPaletteSelector, WpfUtilities.FindDirectParentControl<Window>(this));
		}

		private void _spriteViewer_MouseEnter(object sender, MouseEventArgs e) => Mouse.OverrideCursor = _currentTool?.Cursor;
		private void _spriteViewer_MouseLeave(object sender, MouseEventArgs e) => Mouse.OverrideCursor = null;

		private void _menuItemGrayscaleMaxValue_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGrayscale(_spr, _getCurrentState(), GrayscaleMode.MaxValue);
		private void _menuItemGrayscaleAverage_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGrayscale(_spr, _getCurrentState(), GrayscaleMode.Average);
		private void _menuItemGrayscaleLightness_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGrayscale(_spr, _getCurrentState(), GrayscaleMode.Lightness);
		private void _menuItemGrayscaleLuminosity_Click(object sender, RoutedEventArgs e) => SpriteOperation.ApplyGrayscale(_spr, _getCurrentState(), GrayscaleMode.Luminosity);
	}
}