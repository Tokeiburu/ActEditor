using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for TabAct.xaml
	/// </summary>
	public partial class TabAct : TabItem, IFrameRendererEditor {
		private readonly SelectionEngine _selectionEngine = new SelectionEngine();
		private readonly SpriteManager _spriteManager = new SpriteManager();
		private List<ReferenceControl> _references = new List<ReferenceControl>();
		private Act _act;
		private bool _isNew;
		private readonly ActEditorWindow _actEditor;
		private readonly Grid _gridRenderer;
		public bool IsActLoaded { get; set; }

		public delegate void NewStateChangedEventHandler(object sender);

		public event NewStateChangedEventHandler NewStateChanged;

		public ActEditorWindow ActEditor => _actEditor;

		public TabAct(ActEditorWindow editor) {
			InitializeComponent();

			_actEditor = editor;
			_gridRenderer = _rendererPrimary._gridBackground;

			_gridRenderer.Loaded += delegate {
				LoadBackground(ActEditorConfiguration.BackgroundPath);
			};

			_frameSelector.Init(this, -1, -1);
			_rendererPrimary.Init(this);
			_layerEditor.Init(this);
			_selectionEngine.Init(this);
			_spriteSelector.Init(this);
			_spriteManager.Init(this);

			_initEvents();
		}

		private void _initEvents() {
			_references.Add(new ReferenceControl(this, "ref_body_m", "ref_body_f", "Body", false));
			_references.Add(new ReferenceControl(this, "ref_head_m", "ref_head_f", "Head", false));
			_references.Add(new ReferenceControl(this, "ref_body_m", "ref_body_f", "Other", false));
			_references.Add(new ReferenceControl(this, "ref_body_f", "ref_body_f", "Nearby", true));
			_stackPanelReferences.Children.Add(_references[0]);
			_stackPanelReferences.Children.Add(_references[1]);
			_stackPanelReferences.Children.Add(_references[2]);
			_stackPanelReferences.Children.Add(_references[3]);
			_references.ForEach(p => p.Init());
		}

		public void CreatePreviewGrid(bool left) {
			if (left && _rendererLeft.IsHitTestVisible == false && (string)_rendererLeft.Tag == "created") {
				_rendererLeft.Visibility = Visibility.Visible;
				_rendererLeft.IsHitTestVisible = true;
				_col0.Width = new GridLength(1, GridUnitType.Star);
				return;
			}

			if (!left && _rendererRight.IsHitTestVisible == false && (string)_rendererRight.Tag == "created") {
				_rendererRight.Visibility = Visibility.Visible;
				_rendererRight.IsHitTestVisible = true;
				_col2.Width = new GridLength(1, GridUnitType.Star);
				return;
			}

			var renderer = left ? _rendererLeft : _rendererRight;

			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = () => Act;
			editor.Element = this;
			editor.IndexSelector = IndexSelector;
			renderer.Editor = editor;
			editor.SelectionEngine = new SelectionEngine();
			editor.FrameRenderer = renderer;
			editor.SelectionEngine.Init(editor);
			editor.SelectedActionFunc = delegate {
				if (left) {
					int action = SelectedAction;

					if (Act[SelectedAction].Frames.Count <= 1) {
						if (SelectedAction < 0)
							action = 0;
						else
							action = action - 1;

						if (action < 0)
							action = 0;
					}

					return action;
				}
				else {
					int action = SelectedAction;

					if (Act[SelectedAction].Frames.Count <= 1) {
						if (SelectedAction >= Act.NumberOfActions - 1)
							action = Act.NumberOfActions - 1;
						else
							action = action + 1;

						if (action >= Act.NumberOfActions)
							action = Act.NumberOfActions - 1;
					}

					return action;
				}
			};
			editor.SelectedFrameFunc = delegate {
				if (left)
					return SelectedFrame - 1 < 0 ? Act[SelectedAction].Frames.Count - 1 : SelectedFrame - 1;

				return SelectedFrame + 1 >= Act[SelectedAction].Frames.Count ? 0 : SelectedFrame + 1;
			};

			Act.Commands.CommandIndexChanged += delegate {
				renderer.Update();
			};

			renderer.DrawingModules.Add(new AnchorDrawModule(renderer, editor));
			renderer.DrawingModules.Add(new DefaultDrawModule(() => _references.Where(p => p.ShowReference && p.Mode == ZMode.Back).Select(p => (DrawingComponent)new ActDraw(p.Act, editor)).ToList(), DrawingPriorityValues.Back, false));
			renderer.DrawingModules.Add(new DefaultDrawModule(() => _references.Where(p => p.ShowReference && p.Mode == ZMode.Front).Select(p => (DrawingComponent)new ActDraw(p.Act, editor)).ToList(), DrawingPriorityValues.Front, false));
			renderer.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (Act != null) {
					var primary = new ActDraw(Act, editor);
					return new List<DrawingComponent> { primary };
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			renderer.Init(editor);
			renderer.ZoomEngine.ZoomInMultiplier = () => _rendererPrimary.ZoomEngine.ZoomInMultiplier();

			renderer._cbZoom.Visibility = Visibility.Collapsed;
			FancyButton button = new FancyButton();

			button.HorizontalAlignment = HorizontalAlignment.Right;
			button.VerticalAlignment = VerticalAlignment.Top;
			button.Height = 18;
			button.Width = 18;
			button.Opacity = 0.8;
			button.Background = (Brush)this.TryFindResource("TabItemBackground");
			button.ImagePath = "reset.png";
			renderer.FrameMouseUp += (s, e) => {
				if (renderer.GetObjectAtPoint<FancyButton>(e.GetPosition(renderer)) != button)
					return;

				if (left)
					_col0.Width = new GridLength(0);
				else
					_col2.Width = new GridLength(0);

				_col1.Width = new GridLength(2, GridUnitType.Star);

				renderer.Visibility = Visibility.Collapsed;
				renderer.IsHitTestVisible = false;
			};

			if (left)
				_col0.Width = new GridLength(1, GridUnitType.Star);
			else
				_col2.Width = new GridLength(1, GridUnitType.Star);

			renderer.Visibility = Visibility.Visible;
			renderer.IsHitTestVisible = true;

			renderer._gridBackground.Children.Add(button);

			_rendererPrimary.ZoomChanged += (e, scale) => {
				renderer.ZoomEngine.SetZoom(scale);
				renderer._cbZoom.Text = renderer.ZoomEngine.ScaleText;
				renderer.RelativeCenter = _rendererPrimary.RelativeCenter;
				renderer.SizeUpdate();
			};

			_rendererPrimary.ViewerMoved += (e, position) => {
				renderer.RelativeCenter = position;
				renderer.SizeUpdate();
			};

			_frameSelector.ActionChanged += delegate {
				renderer.Update();
			};

			_frameSelector.FrameChanged += delegate {
				renderer.Update();
			};

			Act.Commands.CommandIndexChanged += delegate {
				renderer.SizeUpdate();
			};

			renderer.ZoomEngine.SetZoom(_rendererPrimary.ZoomEngine.Scale);
			renderer.RelativeCenter = _rendererPrimary.RelativeCenter;
			renderer.Update();
			renderer.Tag = "created";
		}

		public Frame Frame => Act[_frameSelector.SelectedAction, _frameSelector.SelectedFrame];

		public Act Act {
			get => _act;
			set {
				_act = value;

				if (_act != null) {
					_act.AllActions(a => {
						if (a.Frames.Count == 0) {
							a.Frames.Add(new Frame());
						}
					});
				}
			}
		}

		public LayerEditor LayerEditor => _layerEditor;
		public SpriteSelector SpriteSelector => _spriteSelector;
		public FrameRenderer FrameRenderer => _rendererPrimary;
		public event ActEditorWindow.ActEditorEventDelegate ReferencesChanged;
		public event ActEditorWindow.ActEditorEventDelegate ActLoaded;
		public Grid GridPrimary => _gridPrimary;

		public void OnActLoaded() {
			IsActLoaded = true;
			ActEditorWindow.ActEditorEventDelegate handler = ActLoaded;
			if (handler != null) handler(this);
		}

		public void OnReferencesChanged() {
			ActEditorWindow.ActEditorEventDelegate handler = ReferencesChanged;
			if (handler != null) handler(this);
		}

		public SelectionEngine SelectionEngine => _selectionEngine;
		public SpriteManager SpriteManager => _spriteManager;
		public int SelectedAction => _frameSelector.SelectedAction;

		public int SelectedFrame {
			get => _frameSelector.SelectedFrame;
			set {
				value = value < 0 ? 0 : value;
				_frameSelector.SelectedFrame = value;
			}
		}

		public List<ReferenceControl> References { get => _references; set => _references = value; }
		public IActIndexSelector IndexSelector => _frameSelector;
		public UIElement Element => this;

		public bool IsNew { 
			get => _isNew; 
			set {
				if (_isNew != value)
					NewStateChanged?.Invoke(this);

				_isNew = value;
			}
		}

		public void ResetBackground() {
			if (_rendererPrimary._gridBackground != null) {
				VisualBrush brush = new VisualBrush { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.RelativeToBoundingBox, Viewport = new Rect(0, 0, 0.5f, 0.5f) };
				Image img = new Image { Source = ApplicationManager.PreloadResourceImage("background2.png"), Width = 256, Height = 256, SnapsToDevicePixels = true };
				img.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
				brush.Visual = img;
				_rendererPrimary._gridBackground.Background = brush;

				if (File.Exists(ActEditorConfiguration.BackgroundPath)) {
					ActEditorConfiguration.BackgroundPath = ActEditorConfiguration.BackgroundPath.Replace(ActEditorConfiguration.BackgroundPath.GetExtension() ?? "", "");
				}

				GrfColor color = new GrfColor((Configuration.ConfigAsker["[ActEditor - Background preview color]", GrfColor.ToHex(150, 0, 0, 0)]));
				_rendererPrimary.IsUsingImageBackground = false;
				_gridRenderer.Children.OfType<Canvas>().First().Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
				_rendererPrimary.SizeUpdate();
			}
		}

		public void Close() {
			_rendererPrimary.Unload();
		}

		public void LoadBackground(string path) {
			if (path != null && _gridRenderer != null) {
				_gridRenderer.Dispatch(delegate {
					try {
						if (!File.Exists(path)) return;

						GrfImage image = new GrfImage(path);
						ImageBrush imBrush = new ImageBrush { ImageSource = image.Cast<BitmapSource>(), TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, image.Width, image.Height) };
						//imBrush.TileMode = TileMode.FlipXY;	// Comment this line for regular tile
						_gridRenderer.Background = imBrush;
						_rendererPrimary.IsUsingImageBackground = true;
						_gridRenderer.Children.OfType<Canvas>().First().Background = Brushes.Transparent;
						_rendererPrimary.SizeUpdate();
					}
					catch {
						ResetBackground();
					}
				});
			}
		}

		public void SetAnchorIndex(int anchorIndex) {
			_rendererPrimary?.SetAnchorIndex(anchorIndex);
		}

		public void Copy() => _rendererPrimary.Copy();
		public void Paste() => _rendererPrimary.Paste();
		public void Cut() => _rendererPrimary.Cut();
		public void UpdatePrimary() => _rendererPrimary.Update();

		public void UpdateAll() {
			_rendererLeft.Update();
			_rendererPrimary.Update();
			_rendererRight.Update();
		}

		public void ShowOrDisablePreviewFrames() {
			if (_rendererLeft.IsHitTestVisible || _rendererRight.IsHitTestVisible) {
				_col0.Width = new GridLength(0);
				_col2.Width = new GridLength(0);

				_rendererLeft.Visibility = Visibility.Collapsed;
				_rendererLeft.IsHitTestVisible = false;
				_rendererRight.Visibility = Visibility.Collapsed;
				_rendererRight.IsHitTestVisible = false;
				_col1.Width = new GridLength(1, GridUnitType.Star);
			}
			else {
				CreatePreviewGrid(true);
				CreatePreviewGrid(false);
				_col1.Width = new GridLength(1, GridUnitType.Star);
			}
		}

		public void ReverseAnchorChecked() {
			if (Act != null) {
				foreach (var reference in References) {
					if (reference.Act != null && reference.Act.Name == "Body") {
						Act.AnchoredTo = reference.Act;
						reference.Act.AnchoredTo = null;
						break;
					}
				}
			}

			UpdateAll();
		}

		public void ReverseAnchorUnchecked() {
			if (Act != null) {
				Act.AnchoredTo = null;

				foreach (var reference in References) {
					if (reference.Act != null && reference.Act.Name == "Body") {
						reference.RefreshSelection();
						break;
					}
				}
			}

			UpdateAll();
		}

		public void Undo() {
			Act.Commands.Undo();
		}

		public void Redo() {
			Act.Commands.Redo();
		}

		public void FrameMove(int amount) {
			_frameSelector.SelectedFrame += amount;
		}

		public void ActionMove(int amount) {
			_frameSelector.SelectedAction += amount;
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();

			var closeButton = GetTemplateChild("_borderButton") as Border;

			if (closeButton == null)
				return;

			LinkHeader(closeButton);
		}

		public void LinkHeader(Border closeButton) {
			var headerGrid = closeButton.Parent as Grid;

			if (headerGrid == null)
				return;

			headerGrid.ContextMenu = BuildContextMenu();
			headerGrid.ToolTip = BuildToolTip();

			headerGrid.MouseDown += (s, e) => {
				if (e.MiddleButton == MouseButtonState.Pressed) {
					_actEditor.TabEngine.CloseAct(this);
				}
			};

			closeButton.PreviewMouseLeftButtonDown += (s, e) => e.Handled = true;
			closeButton.PreviewMouseLeftButtonUp += (s, e) => {
				_actEditor.TabEngine.CloseAct(this);
			};
		}

		public ToolTip BuildToolTip() {
			ToolTip tt = new ToolTip();
			tt.Content = _act.LoadedPath;
			return tt;
		}

		public ContextMenu BuildContextMenu() {
			var menu = new ContextMenu();

			TkMenuItem miClose = new TkMenuItem();
			miClose.Click += delegate {
				_actEditor.TabEngine.CloseAct(this);
			};
			miClose.Header = "Close Act";
			miClose.InputGestureText = "Ctrl-Q";
			miClose.Icon = GetImage("delete.png");

			TkMenuItem miSelect = new TkMenuItem();
			miSelect.Click += delegate {
				_actEditor.TabEngine.Select(this);
			};
			miSelect.Header = "Select Act";
			miSelect.Icon = GetImage("arrowdown.png");

			TkMenuItem miSave = new TkMenuItem();
			miSave.Click += delegate {
				_actEditor.TabEngine.Save(this);
			};
			miSave.Header = "Save";
			miSave.InputGestureText = "Ctrl-S";
			miSave.Icon = GetImage("save.png");

			TkMenuItem miSaveAs = new TkMenuItem();
			miSaveAs.Click += delegate {
				_actEditor.TabEngine.SaveAs(this);
			};
			miSaveAs.Header = "Save as...";

			TkMenuItem miCloseAllBut = new TkMenuItem();
			miCloseAllBut.Click += delegate {
				var tabs = _actEditor.TabEngine.GetTabs();

				foreach (var tabS in tabs) {
					if (!tabS.Act.Commands.IsModified && tabS != this) {
						if (!_actEditor.TabEngine.CloseAct(tabS)) {
							return;
						}
					}
				}
			};
			miCloseAllBut.Header = "Close all but this";

			menu.Items.Add(miSave);
			menu.Items.Add(miSaveAs);
			menu.Items.Add(new Separator());
			menu.Items.Add(miCloseAllBut);
			menu.Items.Add(new Separator());
			menu.Items.Add(miClose);
			menu.Items.Add(miSelect);

			return menu;
		}

		private Image GetImage(string imagePath) {
			Image image = new Image { Source = ApplicationManager.PreloadResourceImage(imagePath) };
			image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
			return image;
		}
	}
}
