using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;

namespace ActEditor.Core.Scripts {
	public class FrameDelete : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Delete frame"; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.DeleteFrame|Ctrl-Delete}"; }
		}

		public string Image {
			get { return "delete.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				act.Commands.FrameDelete(selectedActionIndex, selectedFrameIndex);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex].NumberOfFrames > 1;
		}

		#endregion
	}

	public class FrameAdd : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Add frame"; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.AddFrame|Ctrl-Enter}"; }
		}

		public string Image {
			get { return "add.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				act.Commands.FrameInsertAt(selectedActionIndex, selectedFrameIndex + 1);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class FrameInsertAt : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Add frame to..."; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{Dialog.AddFrameTo|Ctrl-T}"; }
		}

		public string Image {
			get { return "add.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				var dialog = new FrameInsertDialog(act, selectedActionIndex);
				dialog.StartIndex = selectedFrameIndex;
				dialog.EndIndex = selectedFrameIndex + 1;
				dialog.Mode = FrameInsertDialog.EditMode.Insert;
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
			return act != null;
		}

		#endregion
	}

	public class FrameSwitchSelected : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Switch frame to..."; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{Dialog.SwitchFrameTo|Ctrl-M}"; }
		}

		public string Image {
			get { return "refresh.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				var dialog = new FrameInsertDialog(act, selectedActionIndex);
				dialog.StartIndex = selectedFrameIndex;
				dialog.EndIndex = selectedFrameIndex + 1;
				dialog.Mode = FrameInsertDialog.EditMode.Switch;
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
			return act != null;
		}

		#endregion
	}

	public class FrameCopyAt : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Copy frame and replace to..."; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{Dialog.OverwriteFrameTo|Ctrl-G}"; }
		}

		public string Image {
			get { return "convert.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				var dialog = new FrameInsertDialog(act, selectedActionIndex);
				dialog.StartIndex = selectedFrameIndex;
				dialog.EndIndex = selectedFrameIndex + 1;
				dialog.Mode = FrameInsertDialog.EditMode.Replace;
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
			return act != null;
		}

		#endregion
	}

	public class FrameAdvanced : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Edit frames..."; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{Dialog.FrameAdvancedEdit|Ctrl-E}"; }
		}

		public string Image {
			get { return "advanced.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				var dialog = new FrameEditDialog(act, selectedActionIndex, selectedFrameIndex);
				dialog.ShowDialog();
				//var dialog = new FrameInsertDialog(act, selectedActionIndex);
				//dialog.StartIndex = selectedFrameIndex;
				//dialog.EndIndex = selectedFrameIndex + 1;
				//dialog.Mode = FrameInsertDialog.EditMode.None;
				//dialog.Owner = WpfUtilities.TopWindow;
				//
				//if (dialog.ShowDialog() == true) {
				//	dialog.Execute(act, true);
				//}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class FrameDuplicate : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Duplicate frames..."; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{Dialog.DuplicateFrames|Ctrl-W}"; }
		}

		public string Image {
			get { return "copy.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				InputDialog dialog = new InputDialog("Number of times to copy the frames.", "Frame duplication", "2");
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() != true) return;

				int val;

				if (Int32.TryParse(dialog.Input, out val) && val > 0) {
					try {
						act.Commands.BeginNoDelay();

						int count = act[selectedActionIndex].NumberOfFrames;

						for (int i = 0; i < val; i++) {
							for (int j = 0; j < count; j++) {
								act.Commands.FrameCopy(selectedActionIndex, j);
							}
						}
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
				else {
					ErrorHandler.HandleException("The input value was not in the correct format. Integer value must be greater than 0.");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex].NumberOfFrames > 0;
		}

		#endregion
	}

	public class FrameAddLayerToAllFrames : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Add sprite to all frames..."; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{Dialog.AddSpriteToAllFrame}"; }
		}

		public string Image {
			get { return "empty.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				var effect = new EffectConfiguration("AddSpriteToAllFrames");
				effect.AddProperty("OffsetX", 0, -100, 100);
				effect.AddProperty("OffsetY", 0, -150, 150);
				effect.AddProperty("Back/Front", 1, 0, 1);
				effect.AddProperty("Scale", 1f, 0f, 5f);
				effect.AddProperty("Color", new GrfColor(255, 255, 255, 255), null, null);
				effect.AddProperty("Animation", "0;1;2;3;4;5;6;7;8;9;10;11;12", "", "");
				effect.AddProperty("Sprite Index", 0, 0, act.Sprite.Images.Count - 1);

				effect.InvalidateSprite = true;
				effect.Apply(actInput => {
					int offsetX = effect.GetProperty<int>("OffsetX");
					int offsetY = effect.GetProperty<int>("OffsetY");
					int frontBack = effect.GetProperty<int>("Back/Front");
					float scale = effect.GetProperty<float>("Scale");
					GrfColor color = effect.GetProperty<GrfColor>("Color");
					string animation = effect.GetProperty<string>("Animation");
					int spriteIndex = effect.GetProperty<int>("Sprite Index");

					// Only process the animation indexes provided by the animation variable; QueryIndexProvider provides index for the format such as 1-5;7;8
					var animIndexes = new HashSet<int>(new Utilities.IndexProviders.QueryIndexProvider(animation).GetIndexes());

					// Copy effect from actEffect
					actInput.Commands.Backup(_ => actInput.AllFrames((frame, aid, fid) => {
						int animIndex = aid / 8;

						if (!animIndexes.Contains(animIndex))
							return;

						var layer = new Layer(spriteIndex, act.Sprite);

						layer.OffsetX = offsetX;
						layer.OffsetY = offsetY;
						layer.Color = color;
						layer.ScaleX = scale;
						layer.ScaleY = scale;

						if (frontBack == 1) {
							frame.Layers.Add(layer);
						}
						else {
							frame.Layers.Insert(0, layer);
						}
					}), "Add sprite to all frames");
				});
				effect.Display(act, selectedActionIndex);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}
}