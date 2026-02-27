using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.Scripting.Scripts;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.FrameEditor {
	public class PrimaryFrameRenderer : FrameRenderer {
		private Point _lastDragPoint = new Point(-1, -1);
		private DispatcherTimer _autoRemovalTimer = new DispatcherTimer();
		private GRF.FileFormats.ActFormat.Frame _autoRemoveFrame;
		private Func<bool> _autoRemovalHoverCheck;
		public bool IsUsingImageBackground { get; set; }

		public PrimaryFrameRenderer() {
			_autoRemovalTimer.Interval = TimeSpan.FromMilliseconds(100);
			_autoRemovalTimer.Tick += delegate {
				_removalCheck();
			};

			_createContextMenu();

			_zoomEngine.ZoomInMultiplier = () => ActEditorConfiguration.ActEditorZoomInMultiplier;

			Drop += new DragEventHandler(_renderer_Drop);
			DragOver += new DragEventHandler(_renderer_DragOver);
			DragLeave += delegate {
				_removalCheck();
			};
		}

		public override void Init(IFrameRendererEditor editor) {
			base.Init(editor);

			_drawingModules.Add(AnchorModule);
			_drawingModules.Add(new ReferenceDrawModule(Editor, DrawingPriorityValues.Back, false));
			_drawingModules.Add(new ReferenceDrawModule(Editor, DrawingPriorityValues.Front, false));
			_drawingModules.Add(new BufferedDrawModule(delegate {
				if (Editor.Act != null) {
					Editor.Act.IsSelectable = true;
					var primary = new ActDraw(Editor.Act, Editor);
					//var primary = new ActDraw2(Editor.Act, Editor);
					return (true, new List<DrawingComponent> { primary });
				}
			
				return (false, new List<DrawingComponent>());
			}, DrawingPriorityValues.Normal, false));
		}

		private void _createContextMenu() {
			ContextMenu = new ContextMenu();
			var menuItem = new TkMenuItem { HeaderText = "Delete layer", ShortcutCmd = "LayerEditor.DeleteSelected", IconPath = "delete.png" };
			menuItem.Click += delegate {
				if (Editor.LayerEditor != null)
					Editor.LayerEditor.Delete();
			};
			ContextMenu.Items.Add(menuItem);
			ContextMenu.Items.Add(new Separator());

			menuItem = new TkMenuItem { HeaderText = "Invert selection", ShortcutCmd = "LayerEditor.InvertSelection" };
			menuItem.Click += (e, a) => Editor.SelectionEngine.SelectReverse();
			ContextMenu.Items.Add(menuItem);

			menuItem = new TkMenuItem { HeaderText = "Bring to front", ShortcutCmd = "LayerEditor.BringToFront", IconPath = "front.png" };
			menuItem.Click += delegate {
				if (Editor.LayerEditor != null)
					Editor.LayerEditor.BringToFront();
			};
			ContextMenu.Items.Add(menuItem);

			menuItem = new TkMenuItem { HeaderText = "Bring to back", ShortcutCmd = "LayerEditor.BringToBack", IconPath = "back.png" };
			menuItem.Click += delegate {
				if (Editor.LayerEditor != null)
					Editor.LayerEditor.BringToBack();
			};
			ContextMenu.Items.Add(menuItem);
			ContextMenu.Items.Add(new Separator());

			menuItem = new TkMenuItem { HeaderText = "Bring one up", InputGestureText = "Alt-F", IconPath = "front.png" };
			menuItem.Click += delegate {
				var alm = new ActionLayerMove(ActionLayerMove.MoveDirection.Down, Editor);
				if (alm.CanExecute(Act, SelectedAction, SelectedFrame, Editor.SelectionEngine.SelectedItems.ToArray())) {
					alm.Execute(Act, SelectedAction, SelectedFrame, Editor.SelectionEngine.SelectedItems.ToArray());
				}
			};
			ContextMenu.Items.Add(menuItem);
			ContextMenu.Items.Add(new Separator());

			menuItem = new TkMenuItem { HeaderText = "Mirror vertical", ShortcutCmd = "FrameEditor.LayerMirrorVertical", Shortcut = "NA", IconPath = "flip2.png" };
			menuItem.Click += delegate {
				if (Editor.LayerEditor != null)
					Editor.LayerEditor.MirrorFromOffset(FlipDirection.Vertical);
			};
			ContextMenu.Items.Add(menuItem);

			menuItem = new TkMenuItem { HeaderText = "Mirror horizontal", IconPath = "flip.png", ShortcutCmd = "FrameEditor.LayerMirrorHorizontal", Shortcut = "NA" };
			menuItem.Click += delegate {
				if (Editor.LayerEditor != null)
					Editor.LayerEditor.MirrorFromOffset(FlipDirection.Horizontal);
			};
			ContextMenu.Items.Add(menuItem);
			ContextMenu.Items.Add(new Separator());

			menuItem = new TkMenuItem { HeaderText = "Copy", InputGestureText = "Ctrl-C", IconPath = "copy.png" };
			menuItem.Click += (e, s) => Copy();
			ContextMenu.Items.Add(menuItem);

			menuItem = new TkMenuItem { HeaderText = "Cut", InputGestureText = "Ctrl-X", IconPath = "cut.png" };
			menuItem.Click += (e, s) => Cut();
			ContextMenu.Items.Add(menuItem);
			ContextMenu.Items.Add(new Separator());

			menuItem = new TkMenuItem { HeaderText = "Select in sprite list", CanExecute = _imageExists, IconPath = "arrowdown.png" };
			menuItem.Click += delegate {
				var main = Editor.SelectionEngine.Main;

				if (main != null) {
					int latestSelected = Editor.SelectionEngine.LatestSelected;

					if (latestSelected > -1 && latestSelected < main.Components.Count) {
						Layer layer = ((LayerDraw)main.Components[latestSelected]).Layer;

						if (Editor.Act.Sprite.GetImage(layer) != null) {
							Editor.SpriteSelector.Select(layer.GetAbsoluteSpriteId(Editor.Act.Sprite));
						}
					}
				}
			};
			ContextMenu.Items.Add(menuItem);
		}

		public void Copy() {
			InteractionEngine.Copy();
		}

		public void Paste() {
			InteractionEngine.Paste();
		}

		public void Cut() {
			InteractionEngine.Cut();
		}

		private void _removalCheck() {
			if (_autoRemoveFrame == null)
				return;

			var screenPos = System.Windows.Forms.Control.MousePosition; // screen coords
			Point controlPos = this.PointFromScreen(new Point(screenPos.X, screenPos.Y));
			bool clear = true;

			if (controlPos.X >= 0 &&
				controlPos.X < ActualWidth &&
				controlPos.Y >= 0 &&
				controlPos.Y < ActualHeight) {
				clear = false;
			}
			else if (Editor.LayerEditor != null) {
				Point controlPos2 = Editor.LayerEditor.PointFromScreen(new Point(screenPos.X, screenPos.Y));

				if (controlPos2.X >= 0 &&
					controlPos2.X < Editor.LayerEditor.ActualWidth &&
					controlPos2.Y >= 0 &&
					controlPos2.Y < Editor.LayerEditor.ActualHeight) {
					clear = false;
				}
			}

			if (clear) {
				_clearPreviewLayers(_autoRemoveFrame);
			}

			if (!_autoRemovalHoverCheck()) {
				_clearPreviewLayers(_autoRemoveFrame);
				_autoRemovalTimer.Stop();
			}
		}

		public void AutoRemovePreviewLayer(Func<bool> isDragged) {
			_autoRemoveFrame = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame];
			_autoRemovalHoverCheck = isDragged;
			_autoRemovalTimer.Start();
		}

		private void _renderer_DragOver(object sender, DragEventArgs e) {
			var dragPoint = e.GetPosition(this);

			if (dragPoint.X == _lastDragPoint.X && dragPoint.Y == _lastDragPoint.Y) {
				return;
			}

			object imageIndexObj = e.Data.GetData("ImageIndex");

			if (imageIndexObj == null) return;

			int imageIndex = (int)imageIndexObj;

			var frame = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame];
			var layer = frame.Layers.Where(p => p.Preview).FirstOrDefault();

			int layerIndex = -1;

			if (layer != null) {
				layerIndex = frame.Layers.IndexOf(layer);

				if (layerIndex != frame.Layers.Count - 1) {
					_clearPreviewLayers(frame, false);
					layer = null;
				}
			}

			if (layer == null) {
				// Check if last index
				GrfImage grfImage = Editor.Act.Sprite.Images[imageIndex];
				layer = new Layer((grfImage.GrfImageType == GrfImageType.Indexed8) ? imageIndex : (imageIndex - Editor.Act.Sprite.NumberOfIndexed8Images), grfImage);
				_posFrameToAct(e.GetPosition(this), out layer.OffsetX, out layer.OffsetY);
				layer.Preview = true;
				layerIndex = frame.Layers.Count;
				frame.Layers.Insert(layerIndex, layer);
				Update();
			}
			else {
				_posFrameToAct(e.GetPosition(this), out layer.OffsetX, out layer.OffsetY);
				Update(layerIndex);
			}

			_lastDragPoint = dragPoint;
		}

		private void _renderer_Drop(object sender, DragEventArgs e) {
			object imageIndexObj = e.Data.GetData("ImageIndex");

			if (imageIndexObj == null) return;

			int imageIndex = (int)imageIndexObj;

			_clearPreviewLayers(null, false);

			Editor.Act.Commands.LayerAdd(Editor.SelectedAction, Editor.SelectedFrame, imageIndex);

			int lastIndex = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame].NumberOfLayers - 1;
			Layer layer = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame, lastIndex];
			_posFrameToAct(e.GetPosition(this), out layer.OffsetX, out layer.OffsetY);
			Editor.IndexSelector.OnFrameChanged(Editor.SelectedFrame);
			Editor.SelectionEngine.SetSelection(lastIndex);

			Keyboard.Focus(Editor.GridPrimary);
		}

		private void _clearPreviewLayers(GRF.FileFormats.ActFormat.Frame frame, bool update = true) {
			try {
				if (frame == null)
					frame = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame];

				bool removed = false;

				for (int i = 0; i < frame.Layers.Count; i++) {
					if (frame[i].Preview) {
						frame.Layers.RemoveAt(i);
						removed = true;
						i--;
					}
				}

				if (removed && update) {
					Update();
				}

				_lastDragPoint = new Point(-1, -1);
			}
			catch {
			}
		}

		private void _posFrameToAct(Point pos, out int posX, out int posY) {
			posX = (int)((pos.X - _relativeCenter.X * ActualWidth) / ZoomEngine.Scale);
			posY = (int)((pos.Y - _relativeCenter.Y * ActualHeight) / ZoomEngine.Scale);
		}

		protected override void _updateBackground() {
			try {
				if (IsUsingImageBackground && _gridBackground.Background is ImageBrush brush) {
					brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
					double width = ((BitmapSource)brush.ImageSource).PixelWidth;
					double height = ((BitmapSource)brush.ImageSource).PixelHeight;

					brush.Viewport = new Rect(RelativeCenter.X + width / (_gridBackground.ActualWidth / ZoomEngine.Scale) / 2f, RelativeCenter.Y + height / (_gridBackground.ActualHeight / ZoomEngine.Scale) / 2f, width / (_gridBackground.ActualWidth / ZoomEngine.Scale), height / (_gridBackground.ActualHeight / ZoomEngine.Scale));
				}
				else {
					base._updateBackground();
				}
			}
			catch {
			}
		}

		private bool _imageExists() {
			var main = Editor.SelectionEngine.Main;

			if (main != null) {
				int latestSelected = Editor.SelectionEngine.LatestSelected;

				if (latestSelected > -1 && latestSelected < main.Components.Count) {
					Layer layer = ((LayerDraw)main.Components[latestSelected]).Layer;

					return Editor.Act.Sprite.GetImage(layer) != null;
				}
			}

			return false;
		}
	}
}
