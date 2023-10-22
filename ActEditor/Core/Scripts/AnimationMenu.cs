using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Image;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.Scripts {
	public class FrameCopyBBr : IActScript {
		#region IActScript Members

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add("Rotation copy from ");
				txt.Inlines.Add(new Bold(new Run("bottom")));
				txt.Inlines.Add(" to ");
				txt.Inlines.Add(new Bold(new Run("bottom right")));

				return txt;
			}
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "bbr.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.Begin();

				int baseActionIndex = selectedActionIndex / 8 * 8;
				int next;

				for (int actionIndex = baseActionIndex; actionIndex < act.NumberOfActions && actionIndex < baseActionIndex + 8; actionIndex += 2) {
					next = actionIndex == baseActionIndex ? 7 : -1;
					act.Commands.FrameDeleteRange(actionIndex + next, 0, act[actionIndex + next].NumberOfFrames);

					for (int frameIndex = 0; frameIndex < act[actionIndex].NumberOfFrames; frameIndex++) {
						act.Commands.FrameCopyTo(actionIndex, frameIndex, actionIndex + next, frameIndex);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && (selectedActionIndex / 8 * 8 + 7) < act.NumberOfActions;
		}

		#endregion
	}

	public class FrameCopyBrB : IActScript {
		#region IActScript Members

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add("Rotation copy from ");
				txt.Inlines.Add(new Bold(new Run("bottom right")));
				txt.Inlines.Add(" to ");
				txt.Inlines.Add(new Bold(new Run("bottom")));
				txt.Inlines.Add(" (common)");

				return txt;
			}
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "brb.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.Begin();

				int baseActionIndex = selectedActionIndex / 8 * 8;
				int next;

				for (int actionIndex = baseActionIndex + 1; actionIndex < act.NumberOfActions && actionIndex < baseActionIndex + 8; actionIndex += 2) {
					next = actionIndex == baseActionIndex + 7 ? -7 : 1;
					act.Commands.FrameDeleteRange(actionIndex + next, 0, act[actionIndex + next].NumberOfFrames);

					for (int frameIndex = 0; frameIndex < act[actionIndex].NumberOfFrames; frameIndex++) {
						act.Commands.FrameCopyTo(actionIndex, frameIndex, actionIndex + next, frameIndex);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && (selectedActionIndex / 8 * 8 + 7) < act.NumberOfActions;
		}

		#endregion
	}

	public class FrameCopyBlB : IActScript {
		#region IActScript Members

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add("Rotation copy from ");
				txt.Inlines.Add(new Bold(new Run("bottom left")));
				txt.Inlines.Add(" to ");
				txt.Inlines.Add(new Bold(new Run("bottom")));

				return txt;
			}
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "blb.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.Begin();

				int baseActionIndex = selectedActionIndex / 8 * 8;

				for (int actionIndex = baseActionIndex + 1; actionIndex < act.NumberOfActions && actionIndex < baseActionIndex + 8; actionIndex += 2) {
					act.Commands.FrameDeleteRange(actionIndex - 1, 0, act[actionIndex - 1].NumberOfFrames);

					for (int frameIndex = 0; frameIndex < act[actionIndex].NumberOfFrames; frameIndex++) {
						act.Commands.FrameCopyTo(actionIndex, frameIndex, actionIndex - 1, frameIndex);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && (selectedActionIndex / 8 * 8 + 7) < act.NumberOfActions;
		}

		#endregion
	}

	public class FrameCopyBBl : IActScript {
		#region IActScript Members

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add("Rotation copy from ");
				txt.Inlines.Add(new Bold(new Run("bottom")));
				txt.Inlines.Add(" to ");
				txt.Inlines.Add(new Bold(new Run("bottom left")));
				txt.Inlines.Add(" (common)");

				return txt;
			}
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "bbl.png"; }
		}

		public string Group {
			get { return "Animation"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.Begin();

				int baseActionIndex = selectedActionIndex / 8 * 8;

				for (int actionIndex = baseActionIndex; actionIndex < act.NumberOfActions && actionIndex < baseActionIndex + 8; actionIndex += 2) {
					act.Commands.FrameDeleteRange(actionIndex + 1, 0, act[actionIndex + 1].NumberOfFrames);

					for (int frameIndex = 0; frameIndex < act[actionIndex].NumberOfFrames; frameIndex++) {
						act.Commands.FrameCopyTo(actionIndex, frameIndex, actionIndex + 1, frameIndex);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && (selectedActionIndex / 8 * 8 + 7) < act.NumberOfActions;
		}

		#endregion
	}

	public class FrameCopyHead : IActScript {
		private readonly ActEditorWindow _editor;
		private bool _canUse = true;

		#region IActScript Members

		public object DisplayName {
			get { return "Setup Headgear..."; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "empty.png"; }
		}

		public string Group {
			get { return "Animation"; }
		}

		public FrameCopyHead(ActEditorWindow editor) {
			_editor = editor;
			_canUse = true;
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			HeadEditorDialog diag2 = new HeadEditorDialog(0);
			diag2.Init(ActEditorWindow.Instance.GetCurrentTab2(), act);
			diag2.Owner = _editor;
			_canUse = false;
			diag2.Show();
			diag2.Closed += delegate {
				_editor.Focus();
				_canUse = true;
			};
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && _canUse;
		}

		#endregion
	}

	public class FrameCopyHead2 : IActScript {
		private readonly ActEditorWindow _editor;
		private bool _canUse = true;

		#region IActScript Members

		public object DisplayName {
			get { return "Setup Head..."; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "empty.png"; }
		}

		public string Group {
			get { return "Animation"; }
		}

		public FrameCopyHead2(ActEditorWindow editor) {
			_editor = editor;
			_canUse = true;
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			HeadEditorDialog diag2 = new HeadEditorDialog(1);
			diag2.Init(ActEditorWindow.Instance.GetCurrentTab2(), act);
			diag2.Owner = _editor;
			_canUse = false;
			diag2.Show();
			diag2.Closed += delegate {
				_editor.Focus();
				_canUse = true;
			};
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && _canUse;
		}

		#endregion
	}

	public class FrameCopyGarment : IActScript {
		private readonly ActEditorWindow _editor;
		private bool _canUse = true;

		#region IActScript Members

		public object DisplayName {
			get { return "Setup Garment..."; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "empty.png"; }
		}

		public string Group {
			get { return "Animation"; }
		}

		public FrameCopyGarment(ActEditorWindow editor) {
			_editor = editor;
			_canUse = true;
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			HeadEditorDialog diag2 = new HeadEditorDialog(2);
			diag2.Init(ActEditorWindow.Instance.GetCurrentTab2(), act);
			diag2.Owner = _editor;
			_canUse = false;
			diag2.Show();
			diag2.Closed += delegate {
				_editor.Focus();
				_canUse = true;
			};
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			//return act != null && _canUse;
			// TODO: Disabled the Setup Garment feature,
			// it's just too buggy to begin with. File > Save as Garment... is way better.
			return false;
		}

		#endregion
	}

	public class ReverseAnimation : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.Begin();
				act.Commands.StoreAndExecute(new BackupCommand(_ => act[selectedActionIndex].Frames.Reverse(), "Reverse animation") {CopyMode = CopyStructureMode.Actions});
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

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex].NumberOfFrames > 1;
		}

		public object DisplayName {
			get { return "Reverse animation"; }
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "reverse.png"; }
		}

		#endregion
	}

	public class InterpolationAnimation : IActScript {
		public static bool UseInterpolateSettings;

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				InputDialog dialog = new InputDialog("Interpolating frame " + selectedFrameIndex + " to frame " + (selectedFrameIndex + 1) + ". On how many frames should the animation be?", "Interpolation animation", Configuration.ConfigAsker["[ActEditor - Interpolation value]", "5"], false, false);
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() != true)
					return;

				int ival;

				if (Int32.TryParse(dialog.Input, out ival)) {
					if (ival <= 0 || ival > 50) {
						ErrorHandler.HandleException("The number of frames must be between 0 and 50.");
						return;
					}
				}
				else {
					ErrorHandler.HandleException("Invalid integer format.");
					return;
				}

				Configuration.ConfigAsker["[ActEditor - Interpolation value]"] = dialog.Input;

				act.Commands.Begin();
				act.Commands.Backup(actInput => {
					var endFrameIndex = selectedFrameIndex + 1;

					if (endFrameIndex >= act[selectedActionIndex].Frames.Count)
						endFrameIndex = 0;

					// Ensures there are enough frames
					Frame startFrame = act[selectedActionIndex, selectedFrameIndex];
					Frame finalFrame = act[selectedActionIndex, endFrameIndex];
					List<Layer> finalLayers = finalFrame.Layers;

					List<Frame> newFrames = new List<Frame>();
					HashSet<int> endLayersProcessed = new HashSet<int>();
					var easeMethod = GetEaseMethod(0);
					UseInterpolateSettings = false;

					for (int i = 0; i < ival; i++) {
						Frame frame = new Frame(startFrame);
						List<Layer> startLayers = frame.Layers;

						float degree = (i + 1f) / (ival + 1f);

						for (int layer = 0; layer < frame.NumberOfLayers; layer++) {
							if (layer < finalLayers.Count) {
								if (finalLayers[layer].SpriteIndex == startLayers[layer].SpriteIndex) {
									startLayers[layer] = Interpolate(startLayers[layer], finalLayers[layer], degree, easeMethod);
									endLayersProcessed.Add(layer);
								}
								else {
									if (startLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1 &&
									    finalLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1) {
										int index = finalLayers.FindIndex(p => p.SpriteIndex == startLayers[layer].SpriteIndex);
										Layer final = finalLayers[index];
										startLayers[layer] = Interpolate(startLayers[layer], final, degree, easeMethod);
										endLayersProcessed.Add(index);
									}
									else {
										startLayers[layer] = Interpolate(startLayers[layer], null, degree, easeMethod);
									}
								}
							}
							else {
								if (startLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1 &&
								    finalLayers.Count(p => p.SpriteIndex == startLayers[layer].SpriteIndex) == 1) {
									int index = finalLayers.FindIndex(p => p.SpriteIndex == startLayers[layer].SpriteIndex);
									Layer final = finalLayers[index];
									startLayers[layer] = Interpolate(startLayers[layer], final, degree, easeMethod);
									endLayersProcessed.Add(index);
								}
								else {
									startLayers[layer] = Interpolate(startLayers[layer], null, degree, easeMethod);
								}
							}
						}

						for (int layer = 0; layer < finalLayers.Count; layer++) {
							if (!endLayersProcessed.Contains(layer)) {
								startLayers.Add(Interpolate(null, finalLayers[layer], degree, easeMethod));
							}
						}

						newFrames.Add(frame);
					}

					act[selectedActionIndex].Frames.InsertRange(selectedFrameIndex + 1, newFrames);
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

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex].NumberOfFrames > 1;
		}

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add(new Bold(new Run("Interpolate")));
				txt.Inlines.Add(" frames");

				return txt;
			}
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return "{Dialog.InterpolateAdvanced|Ctrl-I}"; }
		}

		public string Image {
			get { return "interpolate.png"; }
		}

		#endregion

		public static Layer Interpolate(Layer sub1, Layer sub2, float degree, Func<float, float> easeFunc) {
			degree = easeFunc(degree);

			if (sub1 == null) {
				Layer layer = new Layer(sub2);
				layer.Color = new GrfColor((byte) (layer.Color.A * degree), layer.Color.R, layer.Color.G, layer.Color.B);
				return layer;
			}
			if (sub2 == null) {
				Layer layer = new Layer(sub1);
				layer.Color = new GrfColor((byte) (layer.Color.A * (1f - degree)), layer.Color.R, layer.Color.G, layer.Color.B);
				return layer;
			}
			else {
				Layer layer = new Layer(sub1);

				if (!UseInterpolateSettings || (UseInterpolateSettings && ActEditorConfiguration.InterpolateOffsets)) {
					layer.Translate((int) ((sub2.OffsetX - sub1.OffsetX) * degree),
					                (int) ((sub2.OffsetY - sub1.OffsetY) * degree));
				}

				if (!UseInterpolateSettings || (UseInterpolateSettings && ActEditorConfiguration.InterpolateAngle)) {
					int lengthLeft = sub2.Rotation < sub1.Rotation ? sub2.Rotation + 360 - sub1.Rotation : sub2.Rotation - sub1.Rotation;
					int lengthRight = sub2.Rotation > sub1.Rotation ? sub1.Rotation + 360 - sub2.Rotation : sub1.Rotation - sub2.Rotation;

					if (lengthLeft < lengthRight) {
						layer.Rotate((int) (lengthLeft * degree));
					}
					else {
						layer.Rotate((int) (-lengthRight * degree));
					}
				}

				if (!UseInterpolateSettings || (UseInterpolateSettings && ActEditorConfiguration.InterpolateScale)) {
					layer.ScaleX = (sub2.ScaleX - sub1.ScaleX) * degree + sub1.ScaleX;
					layer.ScaleY = (sub2.ScaleY - sub1.ScaleY) * degree + sub1.ScaleY;
				}

				if (!UseInterpolateSettings || (UseInterpolateSettings && ActEditorConfiguration.InterpolateColor)) {
					layer.Color = new GrfColor(
						(byte) (((sub2.Color.A - sub1.Color.A) * degree) + sub1.Color.A),
						(byte) (((sub2.Color.R - sub1.Color.R) * degree) + sub1.Color.R),
						(byte) (((sub2.Color.G - sub1.Color.G) * degree) + sub1.Color.G),
						(byte) (((sub2.Color.B - sub1.Color.B) * degree) + sub1.Color.B));
				}

				if (!UseInterpolateSettings || (UseInterpolateSettings && ActEditorConfiguration.InterpolateMirror)) {
					if (sub1.Mirror != sub2.Mirror) {
						layer.ScaleX *= -(degree * (2f * layer.Width) - layer.Width) / layer.Width; // ((1f - degree) - 0.5f);
					}
				}

				return layer;
			}
		}

		public enum EaseMode {
			InOrOut,
			InAndOut
		}

		public static Func<float, float> GetEaseMethod(int ease) {
			if (ease == 0)
				return v => v;

			var p = Math.Abs(ease / 25f);

			if (ease < 0)
				return v => (float) Math.Pow(v, 1f + p);
			return v => 1 - (float) Math.Pow(1 - v, 1f + p);
		}

		public static void Interpolate(Act act, int selectedActionIndex, int selected, Layer subStart, Layer subEnd, int from, int to, Func<float, float> easeFunc) {
			int interpolateLength;
			
			if (to >= from)
				interpolateLength = to - from - 1;
			else
				interpolateLength = to + (act[selectedActionIndex].NumberOfFrames - from) - 1;

			for (int i = 0; i < interpolateLength; i++) {
				Frame frame = act[selectedActionIndex, (from + i + 1) % act[selectedActionIndex].NumberOfFrames];
				float degree = (i + 1f) / (interpolateLength + 1f);
				int destIndex = -1;

				if (selected < frame.NumberOfLayers && frame.Layers[selected].SpriteIndex == subStart.SpriteIndex) {
					destIndex = selected;
				}
				else {
					for (int j = 0; j < frame.NumberOfLayers; j++) {
						if (frame.Layers[j].SpriteIndex == subStart.SpriteIndex) {
							destIndex = j;
						}
					}
				}

				if (destIndex >= 0) {
					frame.Layers[destIndex] = Interpolate(subStart, subEnd, degree, easeFunc);
				}
				else {
					Layer final = Interpolate(subStart, subEnd, degree, easeFunc);
					frame.Layers.Add(final);
				}
			}
		}
	}

	public class LayerInterpolationAnimation : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null || selectedLayerIndexes.Length == 0) return;

			try {
				InputDialog dialog = new InputDialog("Interpolating layer(s) (" + string.Join(",", selectedLayerIndexes.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray()) + "). What is the target frame index?", "Interpolation animation", (act[selectedActionIndex].NumberOfFrames - 1).ToString(CultureInfo.InvariantCulture), false, false);
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() != true)
					return;

				int ival;

				if (Int32.TryParse(dialog.Input, out ival)) {
					if (ival < 0) {
						ErrorHandler.HandleException("The target frame index must be above 0");
						return;
					}

					if (ival == selectedFrameIndex) {
						ErrorHandler.HandleException("The target frame index must not be equal to " + selectedFrameIndex);
						return;
					}

					if (ival >= act[selectedActionIndex].NumberOfFrames) {
						ErrorHandler.HandleException("The target frame index must be below " + act[selectedActionIndex].NumberOfFrames);
						return;
					}
				}
				else {
					ErrorHandler.HandleException("Invalid integer format.");
					return;
				}

				act.Commands.Begin();
				act.Commands.Backup(actInput => {
					Frame startFrame = act[selectedActionIndex, selectedFrameIndex];
					Frame finalFrame = act[selectedActionIndex, ival];
					InterpolationAnimation.UseInterpolateSettings = false;

					foreach (int selected in selectedLayerIndexes) {
						var startSub = startFrame.Layers[selected];
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

						InterpolationAnimation.Interpolate(act, selectedActionIndex, selected, startSub, layerFinal, selectedFrameIndex, ival, InterpolationAnimation.GetEaseMethod(0));
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

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && selectedLayerIndexes.Length > 0;
		}

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add(new Bold(new Run("Interpolate")));
				txt.Inlines.Add(" selected layers");

				return txt;
			}
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return "{Dialog.InterpolateLayers|Ctrl-Alt-I}"; }
		}

		public string Image {
			get { return "interpolate.png"; }
		}

		#endregion
	}

	public class InterpolationAnimationAdv : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				var dialog = new InterpolateDialog(act, selectedActionIndex);
				dialog.StartIndex = selectedFrameIndex;
				dialog.EndIndex = act[selectedActionIndex].NumberOfFrames - 1;
				dialog.Mode = InterpolateDialog.EditMode.Frame;
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					dialog.Execute(act, true);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex].NumberOfFrames > 1;
		}

		public object DisplayName {
			get { return "Advanced interpolation"; }
		}

		public string Group {
			get { return "Animation"; }
		}

		public string InputGesture {
			get { return "Alt-I"; }
		}

		public string Image {
			get { return "advanced.png"; }
		}

		#endregion
	}
}