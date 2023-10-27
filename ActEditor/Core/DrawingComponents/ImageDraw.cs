using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using GRF.Image;
using GRF.System;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Paths;
using Utilities.Extension;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for an image in the Sprite Editor.
	/// Todo: This class should not actually be here; it's not used
	/// Todo: by the frame preview.
	/// </summary>
	public class ImageDraw : DrawingComponent {
		private readonly IFrameRendererEditor _editor;
		private readonly int _imageIndex;
		private Image _image;
		private string _toolTip = "";
		private static UsageDialog _usageDialog;

		public ImageDraw(int imageIndex, IFrameRendererEditor editor) {
			_imageIndex = imageIndex;
			_editor = editor;
			Selected += _imageDraw_Selected;
		}

		public bool IsPreviewing { get; set; }

		public Rectangle Overlay { get; private set; }
		public Border Border { get; private set; }

		private void _imageDraw_Selected(object sender, int index, bool selected) {
			_initBorder();
			Border.BorderBrush = new SolidColorBrush((selected ? ActEditorConfiguration.ActEditorSpriteSelectionBorder : GrfColor.FromArgb(0, 0, 0, 0)).ToColor());
		}

		public override void Render(IFrameRenderer renderer) {
			_initBorder();

			Act act = renderer.Act;
			GrfImage img = act.Sprite.Images[_imageIndex];
			int usage = _editor.Act.FindUsageOf(_imageIndex).Count;

			string toolTip = "Image #" + _imageIndex + "\r\nWidth = " + img.Width + "\r\nHeight = " + img.Height + "\r\nFormat = " + (img.GrfImageType == GrfImageType.Indexed8 ? "Indexed8" : "Bgra32") + "\r\nUsed in " + usage + " layer(s)";

			if (toolTip != _toolTip) {
				_toolTip = toolTip;

				if (Border.ToolTip == null) {
					Border.ToolTip = new TextBlock();
				}

				TextBlock block = (TextBlock) Border.ToolTip;
				block.Text = _toolTip;
			}

			if (img.GrfImageType == GrfImageType.Indexed8) {
				img = img.Copy();
				img.Palette[3] = 0;
			}

			_image.Source = img.Cast<BitmapSource>();
		}

		public override void QuickRender(IFrameRenderer renderer) {
			Act act = renderer.Act;
			GrfImage img = act.Sprite.Images[_imageIndex];

			if (img.GrfImageType == GrfImageType.Indexed8) {
				img = img.Copy();
				img.Palette[3] = 0;
			}
			else
				return;

			_image.Source = img.Cast<BitmapSource>();
		}

		public override void Remove(IFrameRenderer renderer) {
			if (Border != null)
				renderer.Canva.Children.Remove(Border);
		}

		private void _initBorder() {
			if (Border == null) {
				Border = new Border();
				Border.Background = Brushes.Transparent;
				Border.BorderThickness = new Thickness(1);
				Border.Margin = new Thickness(0);
				Border.BorderBrush = new SolidColorBrush(Colors.Transparent);
				//_border.SnapsToDevicePixels = true;
				_initImage();

				Grid grid = new Grid();
				grid.Margin = new Thickness(1);
				Border.Child = grid;
				grid.RowDefinitions.Add(new RowDefinition {Height = new GridLength(-1, GridUnitType.Auto)});
				grid.RowDefinitions.Add(new RowDefinition());
				grid.Children.Add(_image);

				_image.VerticalAlignment = VerticalAlignment.Top;
				_image.HorizontalAlignment = HorizontalAlignment.Left;
				_image.SetValue(Grid.RowProperty, 1);
				_image.Stretch = Stretch.None;
				_image.SnapsToDevicePixels = true;

				grid.SizeChanged += delegate { _image.Margin = new Thickness((int) ((grid.ActualWidth - _image.ActualWidth) / 2d), (int) ((grid.ActualHeight - _image.ActualHeight) / 2d), 0, 0); };

				Binding binding = new Binding("ViewportHeight");
				binding.Source = _editor.SpriteSelector._sv;
				Border.SetBinding(HeightProperty, binding);
				Border.SnapsToDevicePixels = true;

				Label label = new Label();
				label.Content = _imageIndex;
				label.HorizontalAlignment = HorizontalAlignment.Center;
				label.VerticalAlignment = VerticalAlignment.Bottom;
				label.FontWeight = FontWeights.Bold;
				label.FontSize = 13;

				Label label2 = new Label();
				label2.Content = _imageIndex;
				label2.HorizontalAlignment = HorizontalAlignment.Center;
				label2.VerticalAlignment = VerticalAlignment.Bottom;
				label2.Effect = new BlurEffect();
				label2.Foreground = Brushes.White;
				label2.FontWeight = FontWeights.Bold;
				label2.FontSize = 13;

				grid.Children.Add(label2);
				grid.Children.Add(label);
				label.SetValue(Grid.RowProperty, 1);
				label2.SetValue(Grid.RowProperty, 1);

				Overlay = new Rectangle();
				Overlay.Fill = Brushes.Transparent;
				Overlay.SetValue(IsHitTestVisibleProperty, false);
				Overlay.SetValue(Grid.RowSpanProperty, 2);
				grid.Children.Add(Overlay);
			}

			if (!IsSelectable) {
				IsSelected = false;
				IsHitTestVisible = false;

				if (_image != null) {
					_image.IsHitTestVisible = false;
				}
			}
		}

		private void _initImage() {
			if (_image == null) {
				_image = new Image();

				if (!IsSelectable) {
					_image.IsHitTestVisible = false;
				}
				else {
					Border.MouseDown += delegate(object sender, MouseButtonEventArgs e) {
						if (e.LeftButton == MouseButtonState.Pressed) {
							IsSelected = true;
						}

						_editor.SpriteSelector.DeselectAllExcept(this);
					};
					Border.MouseMove += _image_MouseMove;
					Border.MouseEnter += delegate {
						IsSelected = true;

						_editor.SpriteSelector.DeselectAllExcept(this);
					};
					Border.MouseLeave += delegate(object sender, MouseEventArgs e) {
						if (e.LeftButton == MouseButtonState.Released)
							IsSelected = false;

						if (_image.ContextMenu != null && _image.ContextMenu.IsOpen)
							IsSelected = true;
					};
					Border.MouseUp += delegate(object sender, MouseButtonEventArgs e) {
						if (e.ChangedButton == MouseButton.Right) {
							if (_image.ContextMenu == null) {
								ContextMenu cm = new ContextMenu();

								MenuItem menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("forward.png") };
								menuItem.Header = "Add after...";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.After);
								cm.Items.Add(menuItem);

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("backward.png") };
								menuItem.Header = "Add before...";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.Before);
								cm.Items.Add(menuItem);

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("delete.png") };
								menuItem.Header = "Remove";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.Remove);
								cm.Items.Add(menuItem);

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("refresh.png") };
								menuItem.Header = "Replace...";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.Replace);
								cm.Items.Add(menuItem);

								cm.Items.Add(new Separator());

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("flip2.png") };
								menuItem.Header = "Flip horizontal";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.ReplaceFlipHorizontal);
								cm.Items.Add(menuItem);

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("flip.png") };
								menuItem.Header = "Flip vertical";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.ReplaceFlipVertical);
								cm.Items.Add(menuItem);

								cm.Items.Add(new Separator());

								var menuItem2 = new MenuItem();
								menuItem2.Icon = new Image { Source = ApplicationManager.GetResourceImage("convert.png") };
								menuItem2.Header = "Convert to ";
								menuItem2.Click += (s, args) => _doAction(SpriteEditMode.Convert);
								cm.Opened += delegate { menuItem2.Header = "Convert to " + (_editor.Act.Sprite.Images[_imageIndex].GrfImageType == GrfImageType.Indexed8 ? "Bgra32" : "Indexed8"); };
								cm.Items.Add(menuItem2);

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("export.png") };
								menuItem.Header = "Export...";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.Export);
								cm.Items.Add(menuItem);

								cm.Items.Add(new Separator());

								menuItem = new MenuItem();
								menuItem.Icon = new Image { Source = ApplicationManager.GetResourceImage("help.png") };
								menuItem.Header = "Find usage...";
								menuItem.Click += (s, args) => _doAction(SpriteEditMode.Usage);
								cm.Items.Add(menuItem);

								_image.ContextMenu = cm;
							}

							_image.ContextMenu.IsOpen = true;
							e.Handled = true;
						}
					};
				}
			}
		}

		/// <summary>
		/// This method redirects the calls made from the menu item to 
		/// the SpriteManager class. It is simply used to add additionnal
		/// information/work.
		/// </summary>
		/// <param name="mode">The mode.</param>
		private void _doAction(SpriteEditMode mode) {
			if (_editor.SpriteManager == null)
				throw new Exception("SpriteManager not set in the IFrameRenderer.");

			if (_editor.SpriteManager.IsModeDisabled(mode)) {
				ErrorHandler.HandleException("This feature is disabled.");
				return;
			}

			switch (mode) {
				case SpriteEditMode.Remove:
				case SpriteEditMode.Export:
				case SpriteEditMode.Convert:
				case SpriteEditMode.ReplaceFlipHorizontal:
				case SpriteEditMode.ReplaceFlipVertical:
					_editor.SpriteManager.Execute(_imageIndex, null, mode);
					break;
				case SpriteEditMode.Before:
				case SpriteEditMode.After:
				case SpriteEditMode.Replace:
					string[] files = TkPathRequest.OpenFiles<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.Image));

					if (files != null && files.Length > 0) {
						try {
							try {
								_editor.Act.Commands.BeginNoDelay();
								SpriteManager.SpriteConverterOption = -1;

								try {
									List<GrfImage> images = files.Where(p => p.IsExtension(".bmp", ".jpg", ".png", ".tga")).Select(file1 => new GrfImage(file1)).ToList();
									int index = _imageIndex;

									foreach (GrfImage image1 in images) {
										_editor.SpriteManager.Execute(index, image1, mode);
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
					break;
				case SpriteEditMode.Usage:
					var res = _editor.Act.FindUsageOf(_imageIndex);

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

		private void _image_MouseMove(object sender, MouseEventArgs e) {
			if (!_valideMouseOperation()) return;

			if (e.LeftButton == MouseButtonState.Pressed) {
				IsSelected = true;

				_editor.SpriteSelector.DeselectAllExcept(this);

				DataObject data = new DataObject();

				try {
					if (_imageIndex > -1 && _imageIndex < _editor.Act.Sprite.NumberOfImagesLoaded) {
						GrfImage image = _editor.Act.Sprite.Images[_imageIndex];
						string file = GrfPath.Combine(Settings.TempPath, String.Format("image_{0:000}", _imageIndex) + (image.GrfImageType == GrfImageType.Indexed8 ? ".bmp" : ".png"));
						image.Save(file);
						data.SetData(DataFormats.FileDrop, new string[] {file});
					}
				}
				catch {
				}

				data.SetData("ImageIndex", _imageIndex);
				DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
				IsSelected = false;

				if (_editor.LayerEditor != null)
					_editor.LayerEditor._lineMoveLayer.Visibility = Visibility.Hidden;

				e.Handled = true;
			}
		}

		private bool _valideMouseOperation() {
			if (!IsSelectable) {
				IsSelected = false;
				return false;
			}

			return true;
		}
	}
}