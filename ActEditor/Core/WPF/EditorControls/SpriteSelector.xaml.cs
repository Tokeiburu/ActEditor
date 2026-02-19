using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat.Commands;
using GRF.FileFormats.SprFormat;
using GRF.FileFormats.SprFormat.Commands;
using GRF.Image;
using GRF.Threading;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Paths;
using Utilities;
using Utilities.Extension;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for SpriteSelector.xaml
	/// </summary>
	public partial class SpriteSelector : UserControl {
		private readonly Type[] _updateCommandTypes = { typeof(Flip), typeof(SpriteCommand), typeof(RemoveCommand), typeof(Insert), typeof(ChangePalette) };
		private IFrameRendererEditor _editor;
		private int _previousPosition;
		private SpriteImageControl _lastSelectedSpriteControl;
		private UsageDialog _usageDialog;

		public SpriteSelector() {
			InitializeComponent();

			SizeChanged += delegate { _updateBackground(); };

			_sv.PreviewMouseRightButtonUp += new MouseButtonEventHandler(_sv_MouseRightButtonUp);
			DragEnter += new DragEventHandler(_spriteSelector_DragEnter);
			DragOver += new DragEventHandler(_spriteSelector_DragOver);
			DragLeave += new DragEventHandler(_spriteSelector_DragLeave);
			Drop += new DragEventHandler(_spriteSelector_Drop);

			_dp.Background = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteBackgroundColor);

			if (_dp.Background.CanFreeze)
				_dp.Background.Freeze();

			_sv.ScrollChanged += delegate {
				_sv.ScrollToHorizontalOffset((int)_sv.HorizontalOffset);
				UpdateVisibleOnly();
			};
			_sv.PreviewMouseWheel += _sv_MouseWheel;

			ActEditorConfiguration.ActEditorSpriteSelectionBorder.PropertyChanged += delegate {
				SpriteImageControl.SpriteBorderBrush = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteSelectionBorder.Get().ToColor());

				if (SpriteImageControl.SpriteBorderBrush.CanFreeze)
					SpriteImageControl.SpriteBorderBrush.Freeze();
			};
		}

		private void _sv_MouseWheel(object sender, MouseWheelEventArgs e) {
			_sv.ScrollToHorizontalOffset((e.Delta < 0 ? 60 : -60) + _sv.HorizontalOffset);
			e.Handled = true;
		}

		private void _spriteSelector_Drop(object sender, DragEventArgs e) {
			try {
				if (_isImageDragged(e)) {
					string[] filesArray = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (filesArray == null) return;

					List<string> files = filesArray.ToList();
					files.Reverse();

					this.Dispatcher.BeginInvoke(new Action(() => {
						_spriteSelector_Dropped(files);
					}));
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_lineMoveLayer.Visibility = Visibility.Hidden;
			}
		}

		private void _spriteSelector_Dropped(List<string> files) {
			try {
				_editor.SpriteManager.InsertImages(_previousPosition, files);
			}
			catch (OperationCanceledException) {
			}
			finally {
				_lineMoveLayer.Visibility = Visibility.Hidden;
			}
		}

		private void _spriteSelector_DragLeave(object sender, DragEventArgs e) {
			_lineMoveLayer.Visibility = Visibility.Hidden;
			_previousPosition = -1;
		}

		private bool _isImageDragged(DragEventArgs e) {
			if (_editor.Act != null && e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
				var files = e.Data.GetData(DataFormats.FileDrop, true) as string[];
				if (files != null && files.Any(p => p.IsExtension(".bmp", ".tga", ".jpg", ".png", ".spr"))) {
					if (e.Data.GetDataPresent("ImageIndex")) return false;
					return true;
				}
			}

			return false;
		}

		private void _spriteSelector_DragOver(object sender, DragEventArgs e) {
			if (!_isImageDragged(e)) return;
			
			double offsetX = 0;

			var spriteControl = _sv.GetObjectAtPoint<SpriteImageControl>(e.GetPosition(_sv));

			if (spriteControl == null)
				return;
			
			int position;
			
			if (spriteControl == null) {
				if (_previousPosition < 0) {
					_previousPosition = 0;
				}
			
				position = _previousPosition;
			}
			else {
				position = spriteControl.SpriteIndex;
				_previousPosition = position;
			}
			
			if (position < 0) {
				_lineMoveLayer.Visibility = Visibility.Hidden;
				return;
			}
			
			for (int i = 0; i < position; i++) {
				offsetX += ((FrameworkElement) _dp.Children[i]).ActualWidth;
			}
			
			offsetX -= _sv.HorizontalOffset;
			
			_lineMoveLayer.Visibility = Visibility.Visible;
			_lineMoveLayer.Stroke = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteSelectionBorder.Get().ToColor());
			_lineMoveLayer.Margin = new Thickness(offsetX - 2, 0, 0, SystemParameters.HorizontalScrollBarHeight);
		}

		private int _childrenCount() {
			return _dp.Children.OfType<SpriteImageControl>().Count(p => p.Visibility == Visibility.Visible);
		}

		private void _spriteSelector_DragEnter(object sender, DragEventArgs e) {
			try {
				if (_isImageDragged(e)) {
					_previousPosition = _childrenCount();
					e.Effects = DragDropEffects.All;
					return;
				}

				e.Effects = DragDropEffects.None;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _sv_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
			if (_editor.Act == null) {
				e.Handled = true;
			}
			else {
				Point position = e.GetPosition(_sv);

				if (position.X < 0 || position.Y < 0 || position.X >= _sv.ViewportWidth || position.Y >= _sv.ViewportHeight) {
					e.Handled = true;
				}

				_lastSelectedSpriteControl = WpfUtilities.GetObjectAtPoint<SpriteImageControl>(_sv, position);
				var menuItems = _contextMenuImages.Items.OfType<UIElement>().ToList();

				if (_lastSelectedSpriteControl != null && _lastSelectedSpriteControl.Visibility == Visibility.Visible) {
					_miAdd.Visibility = Visibility.Collapsed;

					for (int i = 1; i < menuItems.Count; i++) {
						menuItems[i].Visibility = Visibility.Visible;
					}
				}
				else {
					_miAdd.Visibility = Visibility.Visible;

					for (int i = 1; i < menuItems.Count; i++) {
						menuItems[i].Visibility = Visibility.Collapsed;
					}

					UpdateAllHoverExcept(null);
				}
			}
		}

		private void _updateBackground() {
			((VisualBrush)_gridBackground.Background).Viewport = new Rect(0, 0, 16d / (_gridBackground.ActualWidth), 16d / (_gridBackground.ActualHeight));
		}

		public void Init(IFrameRendererEditor editor) {
			_editor = editor;

			_editor.ActLoaded += new ActEditorWindow.ActEditorEventDelegate(_actEditor_ActLoaded);
		}

		private void _actEditor_ActLoaded(object sender) {
			if (_editor.Act == null) return;

			_editor.Act.Commands.CommandExecuted += _commandChanged;
			_editor.Act.Commands.CommandRedo += _commandChanged;
			_editor.Act.Commands.CommandUndo += _commandChanged;
			_editor.Act.SpriteVisualInvalidated += e => _commandChanged(null, null);
			_editor.Act.SpritePaletteInvalidated += e => _commandChanged(null, null);

			Update();
		}

		private void _commandChanged(object sender, IActCommand command) {
			var cmd = command as ActGroupCommand;

			if (cmd != null) {
				if (_updateCommandTypes.Any(type => cmd.Commands.Any(c => type == c.GetType()))) {
					InternalUpdate();
				}
			}
			else if (command != null && _updateCommandTypes.Any(type => command.GetType() == type)) {
				InternalUpdate();
			}
			else if (command == null) {
				InternalUpdate();
			}
		}

		public void UpdateVisibleOnly() {
			double offset = 0;
			var spr = _editor.Act.Sprite;
			var sprites = _dp.Children.OfType<SpriteImageControl>().ToList();

			for (int i = 0; i < spr.NumberOfImagesLoaded && i < sprites.Count; i++) {
				SpriteImageControl spc = sprites[i];

				if (offset + spc.ContentWidth >= _sv.HorizontalOffset && offset < _sv.HorizontalOffset + _sv.ViewportWidth) {
					sprites[i].UpdateImage();
				}

				offset += spc.ContentWidth;
			}
		}

		public void PaletteUpdate() {
			if (_editor.Act == null) {
				Reset();
				return;
			}

			UpdateVisibleOnly();
		}

		public void InternalUpdate() {
			this.Dispatcher.BeginInvoke(new Action(() => {
				_internalUpdate();
			}), System.Windows.Threading.DispatcherPriority.Render);
		}

		private void _internalUpdate() {
			if (_editor.Act == null) {
				Reset();
				return;
			}

			var act = _editor.Act;
			var spr = act.Sprite;
			var sprites = _dp.Children.OfType<SpriteImageControl>().ToList();

			for (int i = spr.NumberOfImagesLoaded; i < sprites.Count; i++) {
				sprites[i].Visibility = Visibility.Collapsed;
			}

			for (int i = sprites.Count; i < spr.NumberOfImagesLoaded; i++) {
				SpriteImageControl spc = new SpriteImageControl(_editor, this, i);
				_dp.Children.Insert(i, spc);
				sprites.Insert(i, spc);
			}

			for (int i = 0; i < spr.NumberOfImagesLoaded; i++) {
				SpriteImageControl spc = sprites[i];
				if (spc.Visibility != Visibility.Visible)
					spc.Visibility = Visibility.Visible;
				sprites[i].UpdateSize();
			}

			UpdateVisibleOnly();
		}

		public void Update() {
			InternalUpdate();
		}

		public void UpdateAllHoverExcept(SpriteImageControl element) {
			foreach (var imageControl in _dp.Children.OfType<SpriteImageControl>()) {
				if (imageControl == element)
					continue;

				imageControl.ShowSelected(false);
			}
		}

		public void UpdateAllHover() {
			UpdateAllHoverExcept(null);
		}

		public void Reset() {
			foreach (var sprite in _dp.Children.OfType<SpriteImageControl>()) {
				sprite.Visibility = Visibility.Collapsed;
			}
		}

		private void _miAdd_Click(object sender, RoutedEventArgs e) {
			try {
				string[] files = TkPathRequest.OpenFiles<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.Image));
				_editor.SpriteManager.InsertImages(0, files.ToList());
			}
			catch (OperationCanceledException) {
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private SpriteImageControl _getSpriteControl(int index) {
			return this.Dispatch(() => _dp.Children[index] as SpriteImageControl);
		}

		public void Select(int absoluteIndex) {
			UpdateAllHover();

			if (absoluteIndex < _childrenCount()) {
				Task.Run(() => _highlight(_getSpriteControl(absoluteIndex)));

				double offsetX = 0;

				for (int i = 0; i < absoluteIndex; i++) {
					offsetX += ((FrameworkElement)_dp.Children[i]).ActualWidth;
				}

				offsetX = offsetX + ((FrameworkElement)_dp.Children[absoluteIndex]).ActualWidth / 2d - _sv.ViewportWidth / 2d;
				_sv.ScrollToHorizontalOffset(offsetX);
			}
		}

		private void _highlight(SpriteImageControl imageDraw) {
			if (imageDraw.IsPreviewing)
				return;

			try {
				imageDraw.IsPreviewing = true;
				const int NumberOfIteration = 20;

				for (int i = 0; i <= NumberOfIteration; i++) {
					double colorFactor = (NumberOfIteration - (double)i) / NumberOfIteration;

					imageDraw.Dispatch(p => p._overlay.Fill = new SolidColorBrush(Color.FromArgb((byte)(colorFactor * 255), 255, 0, 0)));
					Thread.Sleep(50);
				}
			}
			catch {
			}
			finally {
				imageDraw.Dispatch(p => p._overlay.Fill = Brushes.Transparent);
				imageDraw.IsPreviewing = false;
			}
		}

		private void _miAddAfter_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Add);
		private void _miAddBefore_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Before);
		private void _miRemove_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Remove);
		private void _miReplace_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Replace);
		private void _miFlipHorizontal_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.ReplaceFlipHorizontal);
		private void _miFlipVertical_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.ReplaceFlipVertical);
		private void _miConvert_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Convert);
		private void _miExport_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Export);
		private void _miFindUsage_Click(object sender, RoutedEventArgs e) => Execute(SpriteEditMode.Usage);

		public void Execute(SpriteEditMode mode) {
			if (_lastSelectedSpriteControl == null) return;

			if (_editor.SpriteManager == null)
				throw new Exception("SpriteManager not set in the FrameRenderer.");

			if (_editor.SpriteManager.IsModeDisabled(mode)) {
				ErrorHandler.HandleException("This feature is disabled.");
				return;
			}

			int index = _lastSelectedSpriteControl.SpriteIndex;

			switch (mode) {
				case SpriteEditMode.Remove:
				case SpriteEditMode.Export:
				case SpriteEditMode.Convert:
				case SpriteEditMode.ReplaceFlipHorizontal:
				case SpriteEditMode.ReplaceFlipVertical:
					_editor.SpriteManager.ApplyCommand(SpriteIndex.FromAbsoluteIndex(index, _editor.Act.Sprite), null, mode);
					break;
				case SpriteEditMode.Before:
				case SpriteEditMode.After:
				case SpriteEditMode.Replace:
					string[] files = TkPathRequest.OpenFiles<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.Image));

					if (files != null && files.Length > 0) {
						try {
							_editor.Act.Commands.ActEditBegin("Sprite: " + mode);
							SpriteManager.SpriteConverterOption = -1;

							List<GrfImage> images = files.Where(p => p.IsExtension(".bmp", ".jpg", ".png", ".tga")).Select(file1 => new GrfImage(file1)).ToList();
							int index2 = index;

							foreach (GrfImage image1 in images) {
								_editor.SpriteManager.ApplyCommand(SpriteIndex.FromAbsoluteIndex(index2, _editor.Act.Sprite), image1, mode);
								index2++;
							}
						}
						catch (OperationCanceledException) {
						}
						catch (Exception err) {
							_editor.Act.Commands.ActCancelEdit();
							ErrorHandler.HandleException(err);
						}
						finally {
							_editor.Act.Commands.ActEditEnd();
							SpriteManager.SpriteConverterOption = -1;
						}
					}
					break;
				case SpriteEditMode.Usage:
					var res = _editor.Act.FindUsageOf(index);

					if (_usageDialog == null) {
						_usageDialog = new UsageDialog(_editor, res);
						_usageDialog.Show();
						_usageDialog.Closed += delegate {
							_usageDialog = null;
						};
					}
					else {
						_usageDialog.UpdateUsage(_editor, res);
					}

					break;
			}
		}
	}
}