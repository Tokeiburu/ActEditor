using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.FrameEditor;
using ActEditor.Core.WPF.InteractionComponent;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for HeadEditorDialog.xaml
	/// </summary>
	public partial class HeadEditorDialog : TkWindow, IFrameRendererEditor {
		private TabAct _editor;
		private Act _actSource;
		private Act _actHeadReference;
		private Act _actBodyReference;
		private Spr _sprReference;
		private readonly SelectionEngine _selectionEngine;
		private readonly List<ReferenceControl> _references = new List<ReferenceControl>();
		public HeadEditorActIndexSelector _actIndexSelector;
		public SpriteManager _spriteManager;
		private Act _actOriginal;
		private Act _actReferenceOriginal;
		private int _flag;

		public class ActReferenceView {
			public int Index { get; set; }
			private readonly string _name;

			public bool Default {
				get { return true; }
			}

			public ActReferenceView(string name, int index) {
				Index = index;
				_name = name;
			}

			public string DisplayName {
				get { return _name; }
			}

			public override string ToString() {
				return _name;
			}
		}

		public HeadEditorDialog(int flag)
			: base(flag == 0 ? "Setup Headgear" : (flag == 1 ? "Setup Head" : "Setup Garment"), "advanced.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
			ShowInTaskbar = true;
			_flag = flag;

			_selectionEngine = new SelectionEngine();
			_actIndexSelector = new HeadEditorActIndexSelector(this);
			_selectionEngine.Init(this);
			_spriteManager = new SpriteManager();
			_spriteManager.AddDisabledMode(SpriteEditMode.Add);
			_spriteManager.AddDisabledMode(SpriteEditMode.After);
			_spriteManager.AddDisabledMode(SpriteEditMode.Before);
			_spriteManager.AddDisabledMode(SpriteEditMode.Convert);
			_spriteManager.AddDisabledMode(SpriteEditMode.Remove);
			_spriteManager.AddDisabledMode(SpriteEditMode.Replace);
			_spriteManager.AddDisabledMode(SpriteEditMode.ReplaceFlipHorizontal);
			_spriteManager.AddDisabledMode(SpriteEditMode.ReplaceFlipVertical);
			_spriteManager.AddDisabledMode(SpriteEditMode.Usage);

			_listViewHeads.PreviewKeyDown += new KeyEventHandler(_listViewHeads_PreviewKeyDown);
			_rendererPrimary.PreviewDrop += new DragEventHandler(_rendererPrimary_PreviewDrop);

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listViewHeads, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.GeneralColumnInfo { Header = "File name", DisplayExpression = "DisplayName", SearchGetAccessor = "DisplayName", IsFill = true, TextAlignment = TextAlignment.Left, ToolTipBinding = "DisplayName" }
			}, new DefaultListViewComparer<ActReferenceView>(), new string[] { "Default", "{DynamicResource TextForeground}" }, "generateHeader", "true", "overrideSizeRedraw", "true");

			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Z", "HeadEditor.Undo"), () => {
				if (Act != null) {
					Act.Commands.Undo();
					_selectionEngine.AddSelection(0);
				}
			}, this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Y", "HeadEditor.Redo"), () => {
				if (Act != null) {
					Act.Commands.Redo();
					_selectionEngine.AddSelection(0);
				}
			}, this);

			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Shift-A", "HeadEditor.ShowAdjacentFrames"), () => {
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
					_createPreviewGrid(true);
					_createPreviewGrid(false);
					_col1.Width = new GridLength(1, GridUnitType.Star);
				}
			}, this);
		}

		private void _listViewHeads_PreviewKeyDown(object sender, KeyEventArgs e) {
			_rendererPrimary.Edit.Renderer_KeyDown(sender, e);
			e.Handled = true;
		}

		private void _rendererPrimary_PreviewDrop(object sender, DragEventArgs e) {
			object imageIndexObj = e.Data.GetData("ImageIndex");

			if (imageIndexObj == null) return;

			int imageIndex = (int)imageIndexObj;
			Point mousePosition = e.GetPosition(_rendererPrimary);

			Act.Commands.BeginNoDelay();
			Act.Commands.SetAbsoluteSpriteId(SelectedAction, SelectedFrame, 0, imageIndex);
			Act.Commands.SetOffsets(SelectedAction, SelectedFrame, 0, (int)((mousePosition.X - _rendererPrimary.RelativeCenter.X * _rendererPrimary.ActualWidth) / _rendererPrimary.ZoomEngine.Scale), (int)((mousePosition.Y - _rendererPrimary.RelativeCenter.Y * _rendererPrimary.ActualHeight) / _rendererPrimary.ZoomEngine.Scale));
			Act.Commands.End();

			IndexSelector.OnFrameChanged(SelectedFrame);
			SelectionEngine.SetSelection(0);

			Keyboard.Focus(GridPrimary);

			e.Handled = true;
		}

		private void _appendSprite2(int i, Act actHeadReference, Act actBodyReference, Act actOriginal, Act act) {
			ActIndex actIndex;
			
			switch(i) {
				case 0:
					actIndex = new ActIndex() { ActionIndex = 0, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 1:
					actIndex = new ActIndex() { ActionIndex = 1, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 2:
					actIndex = new ActIndex() { ActionIndex = 2, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 3:
					actIndex = new ActIndex() { ActionIndex = 3, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 4:
					actIndex = new ActIndex() { ActionIndex = 4, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 5:
					actIndex = new ActIndex() { ActionIndex = 24, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 6:
					actIndex = new ActIndex() { ActionIndex = 24, FrameIndex = 1, LayerIndex = 0 };
					break;
				case 7:
					actIndex = new ActIndex() { ActionIndex = 26, FrameIndex = 0, LayerIndex = 0 };
					break;
				case 8:
					actIndex = new ActIndex() { ActionIndex = 26, FrameIndex = 1, LayerIndex = 0 };
					break;
				default:
					actIndex = new ActIndex() { ActionIndex = 0, FrameIndex = 0, LayerIndex = 0 };
					break;
			}

			{
				Action action = new Action();
				Frame frame = new Frame();
				frame.Anchors.AddRange(_actHeadReference[actIndex.ActionIndex, actIndex.FrameIndex].Anchors);
				action.Frames.Add(frame);
				frame.Layers.Add(_actHeadReference[actIndex]);
				actHeadReference.AddAction(action);
			}
			{
				// Body
				Action action = new Action();
				Frame frame = new Frame();
				action.Frames.Add(frame);
				bool found = false;

				var sourceFrame = _actBodyReference.TryGetFrame(actIndex.ActionIndex, actIndex.FrameIndex);

				if (sourceFrame != null && sourceFrame.NumberOfLayers > 0) {
					var sourceLayer = sourceFrame.Layers.FirstOrDefault(p => p.SpriteIndex > -1);

					if (sourceLayer != null) {
						frame.Anchors.AddRange(sourceFrame.Anchors);
						Layer newLayer = new Layer(sourceLayer);
						frame.Layers.Add(newLayer);
						found = true;
					}
				}

				if (!found) {
					frame.Anchors.AddRange(_actHeadReference[actIndex.ActionIndex, actIndex.FrameIndex].Anchors);
					Layer dummyLayer = new Layer(0, _sprReference);
					dummyLayer.SpriteIndex = -1;
					frame.Layers.Add(dummyLayer);
				}

				actBodyReference.AddAction(action);
			}
			{
				Action action = new Action();
				Frame frame = new Frame();
				action.Frames.Add(frame);
				bool found = false;

				var sourceFrame = actOriginal.TryGetFrame(actIndex.ActionIndex, actIndex.FrameIndex);

				if (sourceFrame != null && sourceFrame.NumberOfLayers > 0) {
					var sourceLayer = sourceFrame.Layers.FirstOrDefault(p => p.SpriteIndex > -1);

					if (sourceLayer != null) {
						frame.Anchors.AddRange(_actHeadReference[actIndex.ActionIndex, actIndex.FrameIndex].Anchors);
						Layer newLayer = new Layer(sourceLayer);
						frame.Layers.Add(newLayer);
						found = true;
					}
				}

				if (!found) {
					// Add dummy frame
					frame.Anchors.AddRange(_actHeadReference[actIndex.ActionIndex, actIndex.FrameIndex].Anchors);
					Layer dummyLayer = new Layer(0, _sprReference);
					dummyLayer.SpriteIndex = -1;
					frame.Layers.Add(dummyLayer);
				}

				act.AddAction(action);
			}
		}

		private void _appendSprite1(int i, Act actHeadReference, Act actBodyReference, Act actOriginal, Act act) {
			var actUsageReference = _actHeadReference.FindUsageOf(i).Where(p => !_actHeadReference[p].Mirror).ToList();

			if (actUsageReference.Count > 0) {
				{
					Action action = new Action();
					Frame frame = new Frame();
					frame.Anchors.AddRange(_actHeadReference[actUsageReference[0].ActionIndex, actUsageReference[0].FrameIndex].Anchors);
					action.Frames.Add(frame);
					frame.Layers.Add(_actHeadReference[actUsageReference[0]]);
					actHeadReference.AddAction(action);
				}
				{
					// Body
					Action action = new Action();
					Frame frame = new Frame();
					action.Frames.Add(frame);
					bool found = false;

					foreach (var actIndex in actUsageReference) {
						var sourceFrame = _actBodyReference.TryGetFrame(actIndex.ActionIndex, actIndex.FrameIndex);

						if (sourceFrame != null && sourceFrame.NumberOfLayers > 0) {
							var sourceLayer = sourceFrame.Layers.FirstOrDefault(p => p.SpriteIndex > -1);

							if (sourceLayer != null) {
								frame.Anchors.AddRange(sourceFrame.Anchors);
								Layer newLayer = new Layer(sourceLayer);
								frame.Layers.Add(newLayer);
								found = true;
								break;
							}
						}
					}

					if (!found) {
						frame.Anchors.AddRange(_actHeadReference[actUsageReference[0].ActionIndex, actUsageReference[0].FrameIndex].Anchors);
						Layer dummyLayer = new Layer(0, _sprReference);
						dummyLayer.SpriteIndex = -1;
						frame.Layers.Add(dummyLayer);
					}

					actBodyReference.AddAction(action);
				}
				{
					Action action = new Action();
					Frame frame = new Frame();
					action.Frames.Add(frame);
					bool found = false;

					foreach (var actIndex in actUsageReference) {
						var sourceFrame = actOriginal.TryGetFrame(actIndex.ActionIndex, actIndex.FrameIndex);

						if (sourceFrame != null && sourceFrame.NumberOfLayers > 0) {
							var sourceLayer = sourceFrame.Layers.FirstOrDefault(p => p.SpriteIndex > -1);

							if (sourceLayer != null) {
								frame.Anchors.AddRange(_actHeadReference[actUsageReference[0].ActionIndex, actUsageReference[0].FrameIndex].Anchors);
								Layer newLayer = new Layer(sourceLayer);
								frame.Layers.Add(newLayer);
								found = true;
								break;
							}
						}
					}

					if (!found) {
						// Add dummy frame
						frame.Anchors.AddRange(_actHeadReference[actUsageReference[0].ActionIndex, actUsageReference[0].FrameIndex].Anchors);
						Layer dummyLayer = new Layer(0, _sprReference);
						dummyLayer.SpriteIndex = -1;
						frame.Layers.Add(dummyLayer);
					}

					act.AddAction(action);
				}
			}
		}

		public void Init(TabAct editor, Act actOriginal) {
			_actOriginal = actOriginal;
			_actSource = new Act(actOriginal);
			_actSource.IsSelectable = true;
			_editor = editor;

			var refHead = _editor.References.First(p => p.ReferenceName == "Head");
			var refBody = _editor.References.First(p => p.ReferenceName == "Body");

			refHead.MakeAct();
			refBody.MakeAct();

			_actHeadReference = new Act(refHead.Act);
			_sprReference = new Spr(refHead.Spr);

			if (_flag == 2) {
				_actReferenceOriginal = new Act(new Act(refBody.Act));
			}
			else {
				_actReferenceOriginal = new Act(new Act(refHead.Act));
			}

			_actBodyReference = new Act(refBody.Act);

			Act act = new Act(actOriginal.Sprite);
			Act actHeadReference = new Act(_sprReference);
			Act actBodyReference = new Act(refBody.Spr);

			if (_flag == 2) {
				for (int i = 0; i < _actSource.Sprite.NumberOfIndexed8Images; i++) {
					_appendSprite2(i, actHeadReference, actBodyReference, actOriginal, act);
				}
			}
			else {
				for (int i = 0; i < _sprReference.NumberOfIndexed8Images; i++) {
					_appendSprite1(i, actHeadReference, actBodyReference, actOriginal, act);
				}
			}

			_actSource = act;
			_actSource.Commands.CommandIndexChanged += delegate {
				_buttonOk.IsEnabled = _actSource.Commands.IsModified;
			};

			if (_flag == 2) {
				_actHeadReference = actHeadReference;
				_actHeadReference.Name = "Head";
				_actHeadReference.AnchoredTo = _actBodyReference;

				_actBodyReference = actBodyReference;
				_actBodyReference.Name = "Body";
				_actBodyReference.AnchoredTo = _actSource;
			}
			else {
				_actHeadReference = actHeadReference;
				_actHeadReference.Name = "Head";
				_actHeadReference.AnchoredTo = _actSource;

				_actBodyReference = actBodyReference;
				_actBodyReference.Name = "Body";
				_actBodyReference.AnchoredTo = _actHeadReference;
			}

			List<ActReferenceView> items = new List<ActReferenceView>(_sprReference.NumberOfIndexed8Images);

			if (_flag == 2) {
				for (int i = 0; i < _actSource.Sprite.NumberOfIndexed8Images; i++) {
					items.Add(new ActReferenceView(i + " - Garment", i));
				}
			}
			else {	
				for (int i = 0; i < _sprReference.NumberOfIndexed8Images; i++) {
					items.Add(new ActReferenceView(i + " - Head", i));
				}
			}

			_listViewHeads.SelectionChanged += new SelectionChangedEventHandler(_listViewHeads_SelectionChanged);

			_spriteSelector.Init(this);

			_rendererPrimary.Init(this);
			_rendererPrimary.InteractionEngine = new HeadInteraction(_rendererPrimary, this);

			_rendererPrimary.DrawingModules.Clear();
			_rendererPrimary.DrawingModules.Add(new DefaultDrawModule(delegate {
				List<DrawingComponent> components = new List<DrawingComponent>();
				_actSource.IsSelectable = true;

				if (_flag == 2) {
					switch(_rendererPrimary.SelectedAction) {
						case 0:
						case 1:
						case 2:
							components.Add(new ActDraw(_actSource, this));
							components.Add(new ActDraw(_actBodyReference, this));
							components.Add(new ActDraw(_actHeadReference, this));
							break;
						default:
							components.Add(new ActDraw(_actBodyReference, this));
							components.Add(new ActDraw(_actHeadReference, this));
							components.Add(new ActDraw(_actSource, this));
							break;
					}
				}
				else {
					components.Add(new ActDraw(_actBodyReference, this));
					components.Add(new ActDraw(_actHeadReference, this));
					components.Add(new ActDraw(_actSource, this));
				}

				return components;
			}, DrawingPriorityValues.Normal, false));

			_listViewHeads.ItemsSource = items;
			_listViewHeads.SelectedIndex = 0;
			ActLoaded?.Invoke(this);
		}

		private void _listViewHeads_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			_actIndexSelector.OnActionChanged(SelectedAction);
			_actIndexSelector.OnFrameChanged(SelectedFrame);
			_selectionEngine.AddSelection(0);
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			try {
				if (_createHeadSprite()) {
					_buttonOk.IsEnabled = false;
					Act.Commands.SaveCommandIndex();
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private int _getReferenceFrameIndex(int actionIndex, int frameIndex, Act act) {
			if (actionIndex >= act.NumberOfActions) return -1;
			if (act.Name == "Head" || act.Name == "Body") {

				if (act[actionIndex].NumberOfFrames == 3 &&
					(0 <= actionIndex && actionIndex < 8) ||
					(16 <= actionIndex && actionIndex < 24)) {
					if (_actOriginal != null) {
						Act editorAct = _actOriginal;

						int group = editorAct[actionIndex].NumberOfFrames / 3;

						if (group != 0) {
							if (frameIndex < group) {
								return 0;
							}
							if (frameIndex < 2 * @group) {
								return 1;
							}
							if (frameIndex < 3 * @group) {
								return 2;
							}
							return 2;
						}
					}
				}
			}

			if (frameIndex >= act[actionIndex].NumberOfFrames) {
				if (act[actionIndex].NumberOfFrames > 0)
					return frameIndex % act[actionIndex].NumberOfFrames;

				return 0;
			}

			return frameIndex;
		}

		private int _getRerenceSpriteIndex(int aid, int fid, Act act) {
			var layer = act[aid, fid].Layers.FirstOrDefault(p => p.SpriteIndex > -1);

			if (layer == null)
				return -1;

			return layer.SpriteIndex;
		}

		private int _getSourceSpriteIndex(int referenceSpriteIndex) {
			Frame frameSource = _actSource[referenceSpriteIndex, 0];

			if (frameSource.Layers.Count <= 0)
				return -1;

			return frameSource.Layers[0].SpriteIndex;
		}

		private bool _createHeadSprite() {
			return _editor.Element.Dispatch(delegate {
				try {
					_actOriginal.Commands.Begin();
					_actOriginal.Commands.Backup(act => {
						if (_flag == 2) {
							for (int aid = 0; aid < _actOriginal.Actions.Count && aid < _actReferenceOriginal.Actions.Count; aid++) {
								var action = _actOriginal.Actions[aid];

								for (int fid = 0; fid < action.Frames.Count; fid++) {
									_cleanUpFrame(aid, fid);	// There is always 1 layer per frame
									int referenceFrameIndex = _getReferenceFrameIndex(aid, fid, _actReferenceOriginal);
									int referenceSpriteIndex = _getRerenceSpriteIndex(aid, referenceFrameIndex, _actReferenceOriginal);

									if (referenceSpriteIndex < 0)
										continue;

									if (_flag == 2 && referenceSpriteIndex >= 5)
										continue;

									int sourceSpriteIndex = _getSourceSpriteIndex(referenceSpriteIndex);

									if (sourceSpriteIndex < 0)
										continue;

									Layer currentLayer = _actOriginal[aid, fid, 0];
									currentLayer.SetAbsoluteSpriteId(sourceSpriteIndex, _actOriginal.Sprite);

									_adjustLayerCoordinates(aid, fid, referenceFrameIndex, currentLayer, referenceSpriteIndex);
								}
							}
						}
						else {
							for (int aid = 0; aid < _actOriginal.Actions.Count && aid < _actReferenceOriginal.Actions.Count; aid++) {
								var action = _actOriginal.Actions[aid];

								for (int fid = 0; fid < action.Frames.Count; fid++) {
									_cleanUpFrame(aid, fid);	// There is always 1 layer per frame
									int referenceFrameIndex = _getReferenceFrameIndex(aid, fid, _actReferenceOriginal);
									int referenceSpriteIndex = _getRerenceSpriteIndex(aid, referenceFrameIndex, _actReferenceOriginal);

									if (referenceSpriteIndex < 0)
										continue;

									int sourceSpriteIndex = _getSourceSpriteIndex(referenceSpriteIndex);

									if (sourceSpriteIndex < 0)
										continue;

									Layer currentLayer = _actOriginal[aid, fid, 0];
									currentLayer.SetAbsoluteSpriteId(sourceSpriteIndex, _actOriginal.Sprite);

									_adjustLayerCoordinates(aid, fid, referenceFrameIndex, currentLayer, referenceSpriteIndex);

									if (_flag == 1) {
										_actOriginal[aid, fid].Layers.Insert(0, new Layer(_actOriginal[aid, fid, 0]));
										_actOriginal[aid, fid, 0].SpriteIndex = -1;
									}
								}
							}
						}
					}, (_flag == 2 ? "Garment sprite generation" : "Head sprite generation"));
				}
				catch (Exception err) {
					_actOriginal.Commands.CancelEdit();
					ErrorHandler.HandleException(err);
					return false;
				}
				finally {
					_actOriginal.Commands.End();
					_actOriginal.InvalidateVisual();
					_actOriginal.InvalidateSpriteVisual();
				}

				return true;
			});
		}

		private void _adjustLayerCoordinates(int aid, int fid, int referenceFrameIndex, Layer layerSource, int referenceSpriteIndex) {
			Frame frameSource = _actOriginal[aid, fid];
			Frame frameReference = _actReferenceOriginal[aid, referenceFrameIndex];
			Layer layerReference = frameReference.Layers.FirstOrDefault(p => p.SpriteIndex > -1);

			if (layerReference == null)
				return;

			Frame modelFrameSource = _actSource[referenceSpriteIndex, 0];
			Frame modelFrameReference;

			if (_flag == 2) {
				modelFrameReference = _actBodyReference[referenceSpriteIndex, 0];
			}
			else {
				modelFrameReference = _actHeadReference[referenceSpriteIndex, 0];
			}

			Layer modelLayerSource = modelFrameSource.Layers[0];
			Layer modelLayerReference = modelFrameReference.Layers[0];

			layerSource.Mirror = layerReference.Mirror;

			// Ignore anchors for now...
			// Ignore mirrors
			int x0 = modelLayerSource.OffsetX;
			int y0 = modelLayerSource.OffsetY;
			int x1 = modelLayerReference.OffsetX;
			int y1 = modelLayerReference.OffsetY;

			if (frameSource.Anchors.Count > 0 && frameReference.Anchors.Count > 0) {
				frameSource.Anchors[0].OffsetX = frameReference.Anchors[0].OffsetX;
				frameSource.Anchors[0].OffsetY = frameReference.Anchors[0].OffsetY;
			}

			layerSource.OffsetX = layerReference.OffsetX;
			layerSource.OffsetY = layerReference.OffsetY;
			layerSource.ScaleX = modelLayerSource.ScaleX;
			layerSource.ScaleY = modelLayerSource.ScaleY;
			layerSource.Rotation = modelLayerSource.Rotation;

			var vectorX = x0 - x1;
			var vectorY = y0 - y1;

			if (layerSource.Mirror) {
				vectorX *= -1;

				if (_flag != 2) {
					var widthSource = _actOriginal.Sprite.GetImage(layerSource).Width % 2;
					var widthReference = _actHeadReference.Sprite.GetImage(layerReference).Width % 2;

					if (widthSource != widthReference) {
						if (widthSource % 2 == 0) {
							vectorX--;
						}
						if (widthReference % 2 == 0) {
							vectorX++;
						}
					}
				}

				if (layerSource.Rotation > 0) {
					int rotation = 360 - layerSource.Rotation;
					layerSource.Rotation = rotation < 0 ? rotation + 360 : rotation;
				}
			}

			layerSource.Translate(vectorX, vectorY);
		}

		private void _cleanUpFrame(int aid, int fid) {
			var frame = _actOriginal[aid].Frames[fid];

			while (frame.Layers.Count > 1) {
				frame.Layers.RemoveAt(1);
			}

			if (frame.Layers.Count == 0) {
				// Need to create a new layer, we do not care about the sprite index for now
				Layer layer = new Layer(0, _actOriginal.Sprite);
				frame.Layers.Add(layer);
			}
		}

		private void _createPreviewGrid(bool left) {
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

			renderer.Init(editor);
			renderer.ZoomEngine.ZoomInMultiplier = () => _rendererPrimary.ZoomEngine.ZoomInMultiplier();

			renderer.DrawingModules.Add(new AnchorDrawModule(renderer, editor));
			renderer.DrawingModules.Add(new DefaultDrawModule(() => new List<DrawingComponent> { new ActDraw(_actBodyReference, editor), new ActDraw(_actHeadReference, editor) }, DrawingPriorityValues.Back, false));
			renderer.DrawingModules.Add(new BufferedDrawModule(delegate {
				if (Act != null) {
					var primary = new ActDraw(Act, editor);
					return (true, new List<DrawingComponent> { primary });
				}

				return (false, new List<DrawingComponent>());
			}, DrawingPriorityValues.Normal, false));

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

			_actIndexSelector.ActionChanged += delegate {
				renderer.Update();
			};

			_actIndexSelector.FrameChanged += delegate {
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

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		public int SelectedAction {
			get {
				if (_listViewHeads.SelectedItem == null)
					return 0;

				return ((ActReferenceView)_listViewHeads.SelectedItem).Index;
			}
		}

		public void OnReferencesChanged(object sender) {
			ReferencesChanged?.Invoke(sender);
		}

		public UIElement Element => this;
		public Act Act => _actSource;
		public event ActEditorWindow.ActEditorEventDelegate ReferencesChanged;
		public event ActEditorWindow.ActEditorEventDelegate ActLoaded;
		public int SelectedFrame => 0;
		public SelectionEngine SelectionEngine => _selectionEngine;
		public List<ReferenceControl> References => _references;
		public IActIndexSelector IndexSelector => _actIndexSelector;
		public Grid GridPrimary => _gridPrimary;
		public LayerEditor LayerEditor => null;
		public SpriteSelector SpriteSelector => _spriteSelector;
		public FrameRenderer FrameRenderer => _rendererPrimary;
		public SpriteManager SpriteManager => _spriteManager;
	}
}
