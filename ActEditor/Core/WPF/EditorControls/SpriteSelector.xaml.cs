using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
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
using Utilities.Extension;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for SpriteSelector.xaml
	/// </summary>
	public partial class SpriteSelector : UserControl {
		private readonly List<ImageDraw> _imageDraws = new List<ImageDraw>();
		private readonly Type[] _updateCommandTypes = {typeof (Flip), typeof (SpriteCommand), typeof (RemoveCommand), typeof (Insert), typeof (ChangePalette)};
		private IFrameRendererEditor _editor;
		private int _previousPosition;

		public SpriteSelector() {
			InitializeComponent();

			SizeChanged += delegate { _updateBackground(); };

			_sv.MouseRightButtonUp += new MouseButtonEventHandler(_sv_MouseRightButtonUp);
			DragEnter += new DragEventHandler(_spriteSelector_DragEnter);
			DragOver += new DragEventHandler(_spriteSelector_DragOver);
			DragLeave += new DragEventHandler(_spriteSelector_DragLeave);
			Drop += new DragEventHandler(_spriteSelector_Drop);

			_dp.Background = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteBackgroundColor);

			_sv.ScrollChanged += delegate { _sv.ScrollToHorizontalOffset((int) _sv.HorizontalOffset); };
		}

		private void _spriteSelector_Drop(object sender, DragEventArgs e) {
			try {
				if (_isImageDragged(e)) {
					string[] filesArray = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (filesArray == null) return;

					List<string> files = filesArray.ToList();
					files.Reverse();

					try {
						SpriteManager.SpriteConverterOption = -1;

						try {
							foreach (string file in files.Where(p => p.IsExtension(".bmp", ".tga", ".jpg", ".png"))) {
								byte[] data = File.ReadAllBytes(file);

								GrfImage image = new GrfImage(ref data);

								if (_previousPosition == _childrenCount()) {
									if (_previousPosition == 0)
										_editor.SpriteManager.Execute(0, image, SpriteEditMode.Add);
									else
										_editor.SpriteManager.Execute(_previousPosition - 1, image, SpriteEditMode.After);
								}
								else
									_editor.SpriteManager.Execute(_previousPosition, image, SpriteEditMode.Before);
							}
						}
						catch (OperationCanceledException) {
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err);
						}

						try {
							_editor.Act.Commands.BeginNoDelay();

							foreach (string file in files.Where(p => p.IsExtension(".spr"))) {
								Spr spr = new Spr(file);

								_editor.Act.Commands.Backup(act => { }, "Adding sprite from file");

								try {
									List<GrfImage> images = spr.Images;

									if (_childrenCount() != 0)
										images.Reverse();

									foreach (GrfImage image in images) {
										if (_previousPosition == _childrenCount()) {
											if (_previousPosition == 0)
												_editor.SpriteManager.Execute(0, image, SpriteEditMode.Add);
											else
												_editor.SpriteManager.Execute(_previousPosition - 1, image, SpriteEditMode.After);
										}
										else
											_editor.SpriteManager.Execute(_previousPosition, image, SpriteEditMode.Before);
									}
								}
								catch (OperationCanceledException) {
								}
								catch (Exception err) {
									ErrorHandler.HandleException(err);
								}
							}
						}
						catch (Exception err) {
							_editor.Act.Commands.CancelEdit();
							ErrorHandler.HandleException(err);
						}
						finally {
							_editor.Act.Commands.End();
						}
					}
					catch (OperationCanceledException) {
					}
					finally {
						SpriteManager.SpriteConverterOption = -1;
						_lineMoveLayer.Visibility = Visibility.Hidden;
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
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

			if (_isImageDragged(e)) {
				double offsetX = 0;

				var um = _dp.InputHitTest(e.GetPosition(_dp));

				Border elementUnderMouse = (um is Border ? um : WpfUtilities.FindDirectParentControl<Border>(um as FrameworkElement)) as Border;

				int position;

				if (elementUnderMouse == null) {
					if (_previousPosition < 0) {
						_previousPosition = 0;
					}

					position = _previousPosition;
				}
				else {
					position = _dp.Children.IndexOf(elementUnderMouse);
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
				_lineMoveLayer.Stroke = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteSelectionBorder.ToColor());
				_lineMoveLayer.Margin = new Thickness(offsetX - 2, 0, 0, SystemParameters.HorizontalScrollBarHeight);
			}
		}

		private int _childrenCount() {
			return _dp.Children.Count - 1;
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
			if (_childrenCount() != 0 || _editor.Act == null) {
				e.Handled = true;
			}
		}

		private void _updateBackground() {
			((VisualBrush) _gridBackground.Background).Viewport = new Rect(0, 0, 16d / (_gridBackground.ActualWidth), 16d / (_gridBackground.ActualHeight));
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

		public void PaletteUpdate() {
			int loaded = _childrenCount();

			if (_editor.Act == null) {
				Reset();
				return;
			}

			for (int i = 0; i < loaded; i++) {
				_imageDraws[i].QuickRender(_editor.FrameRenderer);
			}
		}

		public void InternalUpdate() {
			int loaded = _childrenCount();

			if (_editor.Act == null) {
				Reset();
				return;
			}

			while (loaded > _editor.Act.Sprite.NumberOfImagesLoaded) {
				int last = _childrenCount() - 1;

				_dp.Children.RemoveAt(last);
				_imageDraws.RemoveAt(last);
				loaded--;
			}

			for (int i = 0; i < loaded; i++) {
				_imageDraws[i].Render(_editor.FrameRenderer);
			}

			for (int i = loaded, count = _editor.Act.Sprite.NumberOfImagesLoaded; i < count; i++) {
				var img = new ImageDraw(i, _editor) {IsSelectable = true};
				_imageDraws.Add(img);
				img.Render(_editor.FrameRenderer);
				_dp.Children.Insert(_childrenCount(), img.Border); // Add(img.Border);
			}
		}

		public void Update() {
			InternalUpdate();
		}

		public void DeselectAllExcept(ImageDraw imageDraw) {
			foreach (ImageDraw draw in _imageDraws) {
				if (ReferenceEquals(draw, imageDraw))
					continue;
				draw.IsSelected = false;
			}
		}

		public void DeselectAll() {
			DeselectAllExcept(null);
		}

		public void Reset() {
			while (_dp.Children.Count > 1) {
				_dp.Children.RemoveAt(0);
				_imageDraws.RemoveAt(0);
			}
		}

		private void _miAdd_Click(object sender, RoutedEventArgs e) {
			try {
				string[] files = TkPathRequest.OpenFiles<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.Image));

				if (files != null && files.Length > 0) {
					try {
						try {
							_editor.Act.Commands.BeginNoDelay();
							SpriteManager.SpriteConverterOption = -1;

							try {
								List<GrfImage> images = files.Where(p => p.IsExtension(".bmp", ".jpg", ".png", ".tga")).Select(file1 => new GrfImage(file1)).ToList();
								int index = -1;

								foreach (GrfImage image1 in images) {
									if (index < 0) {
										_editor.SpriteManager.Execute(0, image1, SpriteEditMode.Add);
									}
									else {
										_editor.SpriteManager.Execute(index, image1, SpriteEditMode.After);
									}

									index++;
								}
							}
							catch (OperationCanceledException) {
							}
						}
						catch (Exception err) {
							_editor.Act.Commands.CancelEdit();
							ErrorHandler.HandleException(err);
						}
						finally {
							_editor.Act.Commands.End();
							SpriteManager.SpriteConverterOption = -1;
						}
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void Select(int absoluteIndex) {
			DeselectAll();

			if (absoluteIndex < _imageDraws.Count) {
				GrfThread.Start(() => _highlight(_imageDraws[absoluteIndex]));

				double offsetX = 0;

				for (int i = 0; i < absoluteIndex; i++) {
					offsetX += ((FrameworkElement) _dp.Children[i]).ActualWidth;
				}

				offsetX = offsetX + ((FrameworkElement) _dp.Children[absoluteIndex]).ActualWidth / 2d - _sv.ViewportWidth / 2d;
				_sv.ScrollToHorizontalOffset(offsetX);
			}
		}

		private void _highlight(ImageDraw imageDraw) {
			if (imageDraw.IsPreviewing)
				return;

			try {
				imageDraw.IsPreviewing = true;
				const int NumberOfIteration = 20;

				for (int i = 0; i <= NumberOfIteration; i++) {
					double colorFactor = (NumberOfIteration - (double) i) / NumberOfIteration;

					imageDraw.Dispatch(p => p.Overlay.Fill = new SolidColorBrush(Color.FromArgb((byte) (colorFactor * 255), 255, 0, 0)));
					Thread.Sleep(50);
				}
			}
			catch {
			}
			finally {
				imageDraw.Dispatch(p => p.Overlay.Fill = Brushes.Transparent);
				imageDraw.IsPreviewing = false;
			}
		}
	}
}