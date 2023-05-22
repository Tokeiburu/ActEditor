using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.Scripts;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.FrameEditor {
	public class PrimaryFrameRenderer : FrameRenderer {
		public event DrawingComponent.DrawingComponentDelegate Selected;

		public void OnSelected(int index, bool selected) {
			DrawingComponent.DrawingComponentDelegate handler = Selected;
			if (handler != null) handler(this, index, selected);
		}

		public PrimaryFrameRenderer() {
			_createContextMenu();

			_zoomEngine.ZoomInMultiplier = () => ActEditorConfiguration.ActEditorZoomInMultiplier;

			Drop += new DragEventHandler(_renderer_Drop);
		}

		public override void Init(IFrameRendererEditor editor) {
			base.Init(editor);

			_drawingModules.Add(AnchorModule);
			_drawingModules.Add(new DefaultDrawModule(() => Editor.References.Where(p => p.ShowReference && p.Mode == ZMode.Back).Select(p => (DrawingComponent)new ActDraw(p.Act, Editor)).ToList(), DrawingPriorityValues.Back, false));
			_drawingModules.Add(new DefaultDrawModule(() => Editor.References.Where(p => p.ShowReference && p.Mode == ZMode.Front).Select(p => (DrawingComponent)new ActDraw(p.Act, Editor)).ToList(), DrawingPriorityValues.Front, false));
			_drawingModules.Add(new DefaultDrawModule(delegate {
				if (Editor.Act != null) {
					var primary = new ActDraw(Editor.Act, Editor);
					primary.Selected += new DrawingComponent.DrawingComponentDelegate(_primary_Selected);
					return new List<DrawingComponent> { primary };
				}

				return new List<DrawingComponent>();
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

		private void _renderer_Drop(object sender, DragEventArgs e) {
			object imageIndexObj = e.Data.GetData("ImageIndex");

			if (imageIndexObj == null) return;

			int imageIndex = (int)imageIndexObj;

			Editor.Act.Commands.LayerAdd(Editor.SelectedAction, Editor.SelectedFrame, imageIndex);

			int lastIndex = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame].NumberOfLayers - 1;
			Layer layer = Editor.Act[Editor.SelectedAction, Editor.SelectedFrame, lastIndex];
			_posFrameToAct(e.GetPosition(this), out layer.OffsetX, out layer.OffsetY);
			Editor.FrameSelector.OnFrameChanged(Editor.SelectedFrame);
			Editor.SelectionEngine.SetSelection(lastIndex);

			Keyboard.Focus(Editor.GridPrimary);
		}

		private void _posFrameToAct(Point pos, out int posX, out int posY) {
			posX = (int)((pos.X - _relativeCenter.X * ActualWidth) / ZoomEngine.Scale);
			posY = (int)((pos.Y - _relativeCenter.Y * ActualHeight) / ZoomEngine.Scale);
		}

		private void _primary_Selected(object sender, int index, bool selected) {
			if (selected) {
				Editor.SelectionEngine.AddSelection(index);
			}
			else {
				Editor.SelectionEngine.RemoveSelection(index);
			}

			OnSelected(index, selected);
		}

		protected override void _updateBackground() {
			try {
				if (_gridBackground.Background is VisualBrush) {
					base._updateBackground();
				}
				else {
					ImageBrush brush = _gridBackground.Background as ImageBrush;

					if (brush != null) {
						brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
						double width = ((BitmapSource)brush.ImageSource).PixelWidth;
						double height = ((BitmapSource)brush.ImageSource).PixelHeight;

						brush.Viewport = new Rect(RelativeCenter.X + width / (_gridBackground.ActualWidth / ZoomEngine.Scale) / 2f, RelativeCenter.Y + height / (_gridBackground.ActualHeight / ZoomEngine.Scale) / 2f, width / (_gridBackground.ActualWidth / ZoomEngine.Scale), height / (_gridBackground.ActualHeight / ZoomEngine.Scale));
					}
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
