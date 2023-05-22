using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.Scripts;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.FrameEditor;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SoundEditDialog.xaml
	/// </summary>
	public partial class InterpolateDialog : TkWindow {
		#region EditMode enum

		public static float EaseRange = 100;

		public enum EditMode {
			None,
			Frame,
			Layers
		}

		#endregion

		public EditMode Mode {
			get {
				return
					this.Dispatch(() => _mode0.IsChecked == true ? EditMode.Frame : _mode1.IsChecked == true ? EditMode.Layers : EditMode.None
				);
			}
			set {
				switch (value) {
					case EditMode.Frame:
						_mode0.IsChecked = true;
						_frameMode();
						break;
					case EditMode.Layers:
						_mode1.IsChecked = true;
						_layersMode();
						break;
				}
			}
		}

		private void _layersMode() {
			_setAllVisible();

			_b3.Visibility = Visibility.Collapsed;
			_labelRange.Visibility = Visibility.Collapsed;
		}

		private void _frameMode() {
			_setAllVisible();

			_b2.Visibility = Visibility.Collapsed;
			_b4.Visibility = Visibility.Collapsed;
			_labelEndIndex.Visibility = Visibility.Collapsed;
			_labelLayerIndexes.Visibility = Visibility.Collapsed;
			_asIndexEnd.Visibility = Visibility.Collapsed;
		}

		private void _setAllVisible() {
			_labelStartIndex.Visibility = Visibility.Visible;
			_labelEndIndex.Visibility = Visibility.Visible;
			_labelLayerIndexes.Visibility = Visibility.Visible;
			_labelRange.Visibility = Visibility.Visible;

			_b1.Visibility = Visibility.Visible;
			_b2.Visibility = Visibility.Visible;
			_b3.Visibility = Visibility.Visible;
			_b4.Visibility = Visibility.Visible;

			_asIndexStart.Visibility = Visibility.Visible;
			_asIndexEnd.Visibility = Visibility.Visible;
		}

		private readonly Act _act;
		public int ActionIndex { get; set; }

		public int StartIndex {
			get {
				int index;

				if (Int32.TryParse(_tbIndexStart.Dispatch(p => p.Text), out index)) {
					return index;
				}

				return -1;
			}
			set { _tbIndexStart.Text = value.ToString(CultureInfo.InvariantCulture); }
		}

		public int EndIndex {
			get {
				int index;

				if (Int32.TryParse(_tbIndexEnd.Dispatch(p => p.Text), out index)) {
					return index;
				}

				return -1;
			}
			set {
				_tbIndexEnd.Text = value.ToString(CultureInfo.InvariantCulture);

				if (Int32.Parse(_tbIndexEnd.Text) != value) {
					_tbIndexEnd.Text = value.ToString(CultureInfo.InvariantCulture);
				}
			}
		}

		public int Range {
			get {
				int index;

				if (Int32.TryParse(_tbRange.Dispatch(p => p.Text), out index)) {
					return index;
				}

				return -1;
			}
			set {
				_tbRange.Text = value.ToString(CultureInfo.InvariantCulture);

				if (Int32.Parse(_tbRange.Text) != value) {
					_tbRange.Text = value.ToString(CultureInfo.InvariantCulture);
				}
			}
		}

		public string LayerIndexes {
			get {
				return _tbLayerIndexes.Dispatch(p => p.Text);
			}
			set {
				_tbLayerIndexes.Text = value;
			}
		}

		public InterpolateDialog() : base("Interpolation", "advanced.png") {
			InitializeComponent();

			Binder.Bind(_cbOffsets, () => ActEditorConfiguration.InterpolateOffsets, v => ActEditorConfiguration.InterpolateOffsets = v, _updatePreview);
			Binder.Bind(_cbAngle, () => ActEditorConfiguration.InterpolateAngle, v => ActEditorConfiguration.InterpolateAngle = v, _updatePreview);
			Binder.Bind(_cbScale, () => ActEditorConfiguration.InterpolateScale, v => ActEditorConfiguration.InterpolateScale = v, _updatePreview);
			Binder.Bind(_cbColor, () => ActEditorConfiguration.InterpolateColor, v => ActEditorConfiguration.InterpolateColor = v, _updatePreview);
			Binder.Bind(_cbMirror, () => ActEditorConfiguration.InterpolateMirror, v => ActEditorConfiguration.InterpolateMirror = v, _updatePreview);

			Binder.Bind((TextBox)_tbRange, () => ActEditorConfiguration.InterpolateRange, v => ActEditorConfiguration.InterpolateRange = v, _updatePreview);

			WpfUtilities.AddFocus(_tbIndexStart, _tbIndexEnd, _tbLayerIndexes, _tbRange, _tbEase, _tbTolerance);
			WpfUtilities.PreviewLabel(_tbLayerIndexes, "Example : 1,2,5-9;12;");

			_asIndexStart.FrameChanged += new ActIndexSelector.FrameIndexChangedDelegate(_asIndexStart_ActionChanged);
			_asIndexEnd.FrameChanged += new ActIndexSelector.FrameIndexChangedDelegate(_asIndexEnd_ActionChanged);

			_tbIndexEnd.TextChanged += delegate {
				int ival;

				if (Int32.TryParse(_tbIndexEnd.Text, out ival)) {
					_asIndexEnd.SelectedFrame = ival;
					_updatePreview();
				}
			};

			_tbIndexStart.TextChanged += delegate {
				int ival;

				if (Int32.TryParse(_tbIndexStart.Text, out ival)) {
					_asIndexStart.SelectedFrame = ival;
					_updatePreview();
				}
			};

			bool eventsEnabled = true;

			_gpEase.ValueChanged += delegate {
				((LinearGradientBrush)_gpEase.GradientBackground).GradientStops[1].Offset = _gpEase.Position;
				if (!eventsEnabled) return;

				_tbEase.Text = ((int)(_gpEase.Position * (2 * EaseRange) - EaseRange)).ToString(CultureInfo.InvariantCulture);
			};

			_tbEase.TextChanged += delegate {
				eventsEnabled = false;

				int ival;

				if (Int32.TryParse(_tbEase.Text, out ival)) {
					_gpEase.SetPosition((ival + EaseRange) / (2 * EaseRange), false);

					_labelEaseInOrOut.Content = ival < 0 ? "In" : ival > 0 ? "Out" : "";

					ActEditorConfiguration.InterpolateEase = ival;
					_updatePreview();
				}

				eventsEnabled = true;
			};

			_gpEase.Loaded += delegate {
				_tbEase.Text = ActEditorConfiguration.InterpolateEase.ToString(CultureInfo.InvariantCulture);
			};

			_gpTolerance.ValueChanged += delegate {
				ActEditorConfiguration.InterpolateTolerance = _gpTolerance.Position;
				_updatePreview();
			};

			_gpTolerance.SetPosition(ActEditorConfiguration.InterpolateTolerance, true);
		}

		public InterpolateDialog(Act act, int actionIndex) : this() {
			ActionIndex = actionIndex;
			_asIndexStart.Set(act, actionIndex);
			_asIndexEnd.Set(act, actionIndex);
			_act = act;
			_rps.Init(_act, ActionIndex);

			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = () => _rps.Act;
			editor.Element = this;
			editor.FrameSelector = _rps.ToActIndexSelector();
			editor.SelectedActionFunc = () => _rps.SelectedAction;
			editor.SelectedFrameFunc = () => _rps.SelectedFrame;

			_rfp.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (editor.Act != null) {
					return new List<DrawingComponent> { new ActDraw(editor.Act, editor) };
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			_rfp.Init(editor);

			_updatePreview();
		}

		private void _updatePreview() {
			if (_act == null) return;

			LazyAction.Execute(delegate {
				Act act = new Act(_act.Sprite);

				foreach (var action in _act) {
					act.AddAction(new Action(action));
				}

				if (CanExecute(act)) {
					Execute(act, true);
					this.BeginDispatch(() => _rps.SelectedFrame = StartIndex);
					_rps.Play();
				}
				else {
					_rps.Stop();
				}

				_rps.Init(act, ActionIndex);
			}, GetHashCode());
		}

		private void _asIndexEnd_ActionChanged(object sender, int actionindex) {
			_tbIndexEnd.Text = actionindex.ToString(CultureInfo.InvariantCulture);
		}

		private void _asIndexStart_ActionChanged(object sender, int actionindex) {
			_tbIndexStart.Text = actionindex.ToString(CultureInfo.InvariantCulture);
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				DialogResult = true;
			}
			base.GRFEditorWindowKeyDown(sender, e);
		}

		protected override void OnClosing(CancelEventArgs e) {
			if (DialogResult == true) {
				if (!CanExecute(_act)) {
					DialogResult = null;
					e.Cancel = true;
				}
			}

			if (!e.Cancel) {
				_rps.Stop();
			}
		}

		public bool CanExecute(Act act) {
			if (Mode == EditMode.None) return false;
			try {
				Execute(act, false);
				return true;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				return false;
			}
		}

		public void Execute(Act act, bool executeCommands = true) {
			var easeMethod = InterpolationAnimation.GetEaseMethod(ActEditorConfiguration.InterpolateEase);

			switch (Mode) {
				case EditMode.Frame:
					if (StartIndex < 0) {
						StartIndex = 0;
					}

					if (StartIndex >= act[ActionIndex].NumberOfFrames) {
						StartIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (Range <= 0) {
						Range = 1;
					}

					if (Range > 100) {
						throw new Exception("The number of frames must be below 100.");
					}

					var startIndex = StartIndex;
					var actionIndex = ActionIndex;
					var range = Range;

					if (executeCommands) {
						try {
							act.Commands.Begin();
							act.Commands.Backup(actInput => {
								var endFrameIndex = startIndex + 1;

								if (endFrameIndex >= act[actionIndex].Frames.Count)
									endFrameIndex = 0;

								// Ensures there are enough frames
								Frame startFrame = act[actionIndex, startIndex];
								Frame finalFrame = act[actionIndex, endFrameIndex];
								List<Layer> finalLayers = finalFrame.Layers;

								List<Frame> newFrames = new List<Frame>();
								HashSet<int> endLayersProcessed = new HashSet<int>();
								int ival = range;
								InterpolationAnimation.UseInterpolateSettings = true;

								for (int i = 0; i < ival; i++) {
									Frame frame = new Frame(startFrame);
									List<Layer> startLayers = frame.Layers;

									float degree = (i + 1f) / (ival + 1f);

									for (int layer = 0; layer < frame.NumberOfLayers; layer++) {
										if (layer < finalLayers.Count) {
											if (finalLayers[layer].SpriteIndex == startLayers[layer].SpriteIndex ||
												_similarEnough(act, finalLayers[layer], startLayers[layer])) {
												startLayers[layer] = InterpolationAnimation.Interpolate(startLayers[layer], finalLayers[layer], degree, easeMethod);
												endLayersProcessed.Add(layer);
											}
											else {
												if (startLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1 &&
												    finalLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1) {
													int index = finalLayers.FindIndex(p => p.SpriteIndex == startLayers[layer].SpriteIndex);
													Layer final = finalLayers[index];
													startLayers[layer] = InterpolationAnimation.Interpolate(startLayers[layer], final, degree, easeMethod);
													endLayersProcessed.Add(index);
												}
												else {
													startLayers[layer] = InterpolationAnimation.Interpolate(startLayers[layer], null, degree, easeMethod);
												}
											}
										}
										else {
											if (startLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1 &&
											    finalLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1) {
												int index = finalLayers.FindIndex(p => p.SpriteIndex == startLayers[layer].SpriteIndex);
												Layer final = finalLayers[index];
												startLayers[layer] = InterpolationAnimation.Interpolate(startLayers[layer], final, degree, easeMethod);
												endLayersProcessed.Add(index);
											}
											else {
												startLayers[layer] = InterpolationAnimation.Interpolate(startLayers[layer], null, degree, easeMethod);
											}
										}
									}

									for (int layer = 0; layer < finalLayers.Count; layer++) {
										if (!endLayersProcessed.Contains(layer)) {
											startLayers.Add(InterpolationAnimation.Interpolate(null, finalLayers[layer], degree, easeMethod));
										}
									}

									newFrames.Add(frame);
								}

								act[actionIndex].Frames.InsertRange(startIndex + 1, newFrames);
							}, "Interpolate frames");
						}
						catch (Exception err) {
							act.Commands.CancelEdit();
							ErrorHandler.HandleException(err);
						}
						finally {
							act.Commands.End();
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.Layers:
					if (StartIndex < 0) {
						StartIndex = 0;
					}

					if (StartIndex >= act[ActionIndex].NumberOfFrames) {
						StartIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (EndIndex < 0) {
						EndIndex = 0;
					}

					if (EndIndex >= act[ActionIndex].NumberOfFrames) {
						EndIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (EndIndex > act[ActionIndex].NumberOfFrames) {
						EndIndex = act[ActionIndex].NumberOfFrames;
					}

					Frame start = act[ActionIndex, StartIndex];
					HashSet<int> layerIndexes = new HashSet<int>();

					foreach (var layerIndex in Methods.GetRange(LayerIndexes, start.NumberOfLayers)) {
						if (layerIndex > -1 && layerIndex < start.NumberOfLayers)
							layerIndexes.Add(layerIndex);
					}

					if (layerIndexes.Count == 0) {
						for (int i = 0; i < start.NumberOfLayers; i++)
							layerIndexes.Add(i);
					}

					if (executeCommands) {
						try {
							var endIndex = EndIndex;
							var startIndex2 = StartIndex;
							var actionIndex2 = ActionIndex;

							act.Commands.Begin();
							act.Commands.Backup(actInput => {
								Frame finalFrame = act[actionIndex2, endIndex];
								InterpolationAnimation.UseInterpolateSettings = true;

								foreach (int selected in layerIndexes) {
									var startSub = start.Layers[selected];
									Layer layerFinal = null;

									if (selected < finalFrame.NumberOfLayers && finalFrame.Layers[selected].SpriteIndex == startSub.SpriteIndex) {
										layerFinal = finalFrame.Layers[selected];
									}
									else {
										for (int i = 0; i < finalFrame.NumberOfLayers; i++) {
											if (finalFrame.Layers[i].SpriteIndex == startSub.SpriteIndex) {
												layerFinal = finalFrame.Layers[i];
											}
										}
									}

									InterpolationAnimation.Interpolate(act, actionIndex2, selected, startSub, layerFinal, startIndex2, endIndex, easeMethod);
								}
							}, "Interpolate selected layers");
						}
						catch (Exception err) {
							act.Commands.CancelEdit();
							ErrorHandler.HandleException(err);
						}
						finally {
							act.Commands.End();
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.None:
					throw new Exception("No command selected.");
			}
		}

		private bool _similarEnough(Act act, Layer finalLayer, Layer startLayer) {
			if (ActEditorConfiguration.InterpolateTolerance >= 1d)
				return false;

			var im1 = startLayer.GetImage(act.Sprite);
			var im2 = finalLayer.GetImage(act.Sprite);

			if (im1 != null && im2 != null) {
				if (im1.SimilarityWith(im2) > ActEditorConfiguration.InterpolateTolerance) {
					return true;
				}
			}

			return false;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _mode_Checked(object sender, RoutedEventArgs e) {
			EditMode mode = EditMode.None;

			if (sender == _mode0)
				mode = EditMode.Frame;
			else if (sender == _mode1)
				mode = EditMode.Layers;

			Mode = mode;

			_updatePreview();
		}

		private void _buttonApply_Click(object sender, RoutedEventArgs e) {
			if (CanExecute(_act)) {
				Execute(_act, true);

				_asIndexStart.Set(_act, ActionIndex);
				_asIndexEnd.Set(_act, ActionIndex);
			}
		}
	}
}