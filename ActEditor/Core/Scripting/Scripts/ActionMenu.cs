using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Image;
using TokeiLibrary;
using Utilities.Extension;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.Scripting.Scripts {
	public class ActionCopy : IActScript {
		#region IActScript Members

		public object DisplayName => "Copy action";
		public string Group => "Action";
		public string InputGesture => "{FrameEditor.CopyAction|Alt-C}";
		public string Image => "copy.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			Clipboard.SetDataObject(new DataObject("Action", act[selectedActionIndex]));
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && selectedActionIndex > -1 && selectedActionIndex < act.NumberOfActions;
		}

		#endregion
	}

	public class ActionPaste : IActScript {
		#region IActScript Members

		public object DisplayName => "Paste action";
		public string Group => "Action";
		public string InputGesture => "{FrameEditor.PasteAction|Alt-V}";
		public string Image => "paste.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			var dataObj = Clipboard.GetDataObject();

			if (dataObj == null) return;

			var obj = dataObj.GetData("Action");

			Action action = obj as Action;

			if (action == null) return;

			try {
				act.Commands.BeginNoDelay();
				act.Commands.Backup(_ => {
					act.DeleteActions(selectedActionIndex, 1);
					act.AddAction(action, selectedActionIndex);
				}, "Paste actions");
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && selectedActionIndex > -1 && selectedActionIndex < act.NumberOfActions;
		}

		#endregion
	}

	public class ActionDelete : IActScript {
		#region IActScript Members

		public object DisplayName => "Delete action";
		public string Group => "Action";
		public string InputGesture => "{FrameEditor.DeleteAction|Alt-Delete}";
		public string Image => "delete.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null || act.NumberOfActions <= 1) return;

			try {
				act.Commands.ActionDelete(selectedActionIndex);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act.NumberOfActions > 1;
		}

		#endregion
	}

	public class ActionAdd : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Add action"; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.AddAction|Alt-Enter}"; }
		}

		public string Image {
			get { return "add.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				act.Commands.ActionInsertAt(selectedActionIndex + 1);
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

	public class ActionInsertAt : IActScript {
		#region IActScript Members

		public object DisplayName => "Add action to...";
		public string Group => "Action";
		public string InputGesture => "{Dialog.AddActionTo|Alt-T}";
		public string Image => "add.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				ActionInsertDialog dialog = new ActionInsertDialog(act);
				dialog.Mode = ActionInsertDialog.EditMode.Insert;
				dialog.StartIndex = selectedActionIndex;
				dialog.EndIndex = selectedActionIndex + 1;
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					dialog.Execute(act);
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

	public class ActionSwitchSelected : IActScript {
		#region IActScript Members

		public object DisplayName => "Switch action to...";
		public string Group => "Action";
		public string InputGesture => "{Dialog.SwitchActionTo|Alt-M}";
		public string Image => "refresh.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				try {
					ActionInsertDialog dialog = new ActionInsertDialog(act);
					dialog.Mode = ActionInsertDialog.EditMode.Switch;
					dialog.StartIndex = selectedActionIndex;
					dialog.EndIndex = selectedActionIndex + 1;
					dialog.Owner = WpfUtilities.TopWindow;

					if (dialog.ShowDialog() == true) {
						dialog.Execute(act);
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err, ErrorLevel.Warning);
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

	public class ActionCopyAt : IActScript {
		#region IActScript Members

		public object DisplayName => "Copy action and replace to...";
		public string Group => "Action";
		public string InputGesture => "{Dialog.OverwriteActionTo|Alt-G}";
		public string Image => "convert.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				try {
					ActionInsertDialog dialog = new ActionInsertDialog(act);
					dialog.Mode = ActionInsertDialog.EditMode.Replace;
					dialog.StartIndex = selectedActionIndex;
					dialog.EndIndex = selectedActionIndex + 1;
					dialog.Owner = WpfUtilities.TopWindow;

					if (dialog.ShowDialog() == true) {
						dialog.Execute(act);
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err, ErrorLevel.Warning);
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

	public class ActionAdvanced : IActScript {
		#region IActScript Members

		public object DisplayName => "Edit actions...";
		public string Group => "Action";
		public string InputGesture => "{Dialog.AdvancedEdit|Alt-E}";
		public string Image => "advanced.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				var dialog = new ActionEditDialog(act, selectedActionIndex);
				dialog.Owner = WpfUtilities.TopWindow;
				dialog.ShowDialog();
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

	public class ActionCopyMirror : IActScript {
		#region IActScript Members

		public object DisplayName {
			get {
				TextBlock txt = new TextBlock();

				txt.Inlines.Add("Mirror action from\r\n");
				txt.Inlines.Add(new Bold(new Run("left/right")));
				txt.Inlines.Add(" to ");
				txt.Inlines.Add(new Bold(new Run("right/left")));

				return txt;
			}
		}

		public string InputGesture => "{FrameEditor.MirrorAction|Alt-X}";
		public string Image => "convert.png";
		public string Group => "Action";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				int baseAction = selectedActionIndex % 8;
				int baseZeroAction = selectedActionIndex / 8 * 8;

				if (baseAction == 0 || baseAction == 4) {
					ErrorHandler.HandleException("Cannot mirror the frame. You must select an action which is neither the bottom or top one.");
					return;
				}

				int newActionIndex = 0;

				if (baseAction == 1) newActionIndex = 7;
				if (baseAction == 7) newActionIndex = 1;
				if (baseAction == 2) newActionIndex = 6;
				if (baseAction == 6) newActionIndex = 2;
				if (baseAction == 3) newActionIndex = 4;
				if (baseAction == 4) newActionIndex = 3;

				newActionIndex += baseZeroAction;

				if (newActionIndex >= act.NumberOfActions) {
					ErrorHandler.HandleException("Cannot mirror the frame because the action " + newActionIndex + " doesn't exist.");
					return;
				}

				act.Commands.Backup(new Action<Act>(action => _reverse(act, selectedActionIndex, newActionIndex)), "Frame copy");
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && selectedActionIndex % 8 != 0 && selectedActionIndex % 8 != 4;
		}

		#endregion

		private void _reverse(Act act, int selectedActionIndex, int newSelectedActionIndex) {
			var newAction = new Action(act[selectedActionIndex]);

			foreach (Frame frame in newAction.Frames) {
				foreach (Layer layer in frame.Layers) {
					layer.OffsetX *= -1;
					int rotation = 360 - layer.Rotation;
					layer.Rotation = rotation < 0 ? rotation + 360 : rotation;
					layer.Mirror = !layer.Mirror;
				}

				foreach (var anchor in frame.Anchors) {
					anchor.OffsetX *= -1;
				}
			}

			act.SetAction(newSelectedActionIndex, newAction);
		}
	}

	public class ActionLayerMove : IActScript {
		public class LayerGenericCommand : IActCommand {
			private readonly Action<bool> _callback;

			public LayerGenericCommand(Action<bool> callback) {
				_callback = callback;
			}

			public void Execute(Act act) {
				_callback(true);
			}

			public void Undo(Act act) {
				_callback(false);
			}

			public string CommandDescription => "Selection changed...";
		}

		private readonly MoveDirection _dir;
		private readonly IFrameRendererEditor _editor;

		public enum MoveDirection {
			Up, Down
		}

		public object DisplayName {
			get { return _dir == MoveDirection.Up ? "Move layers to back" : "Move layers to front"; }
		}

		public string InputGesture {
			get { return _dir == MoveDirection.Up ? "{LayerEditor.LayerMoveDown|Alt-B}" : "{LayerEditor.LayerMoveUp|Alt-F}"; }
		}

		public string Image {
			get { return _dir == MoveDirection.Up ? "back.png" : "front.png"; }
		}

		public string Group => "Action";

		public ActionLayerMove(MoveDirection dir, IFrameRendererEditor editor) {
			_dir = dir;
			_editor = editor;
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				var newSelection = new HashSet<int>();
				selectedLayerIndexes = selectedLayerIndexes.OrderBy(p => p).ToArray();
				act.Commands.BeginNoDelay();

				for (int fi = 0; fi < act[selectedActionIndex].NumberOfFrames; fi++) {
					if (_dir == MoveDirection.Down) {
						for (int i = selectedLayerIndexes.Length - 1, j = 0; i >= 0; i--, j++) {
							if (selectedLayerIndexes[i] >= act[selectedActionIndex, fi].NumberOfLayers) continue;
							if (selectedLayerIndexes[i] != act[selectedActionIndex, fi].NumberOfLayers - j - 1) {
								if (selectedLayerIndexes[i] + 2 <= act[selectedActionIndex, fi].NumberOfLayers)
									act.Commands.LayerSwitchRange(selectedActionIndex, fi, selectedLayerIndexes[i], 1, selectedLayerIndexes[i] + 2);

								if (fi == selectedFrameIndex)
									newSelection.Add(selectedLayerIndexes[i] + 1);
							}
							else {
								if (fi == selectedFrameIndex)
									newSelection.Add(selectedLayerIndexes[i]);
							}
						}
					}
					else {
						for (int i = 0, j = 0; i < selectedLayerIndexes.Length; i++, j++) {
							if (selectedLayerIndexes[i] >= act[selectedActionIndex, fi].NumberOfLayers) continue;
							if (selectedLayerIndexes[i] != j) {
								if (selectedLayerIndexes[i] - 1 >= 0)
									act.Commands.LayerSwitchRange(selectedActionIndex, fi, selectedLayerIndexes[i], 1, selectedLayerIndexes[i] - 1);

								if (fi == selectedFrameIndex)
									newSelection.Add(selectedLayerIndexes[i] - 1);
							}
							else {
								if (fi == selectedFrameIndex)
									newSelection.Add(selectedLayerIndexes[i]);
							}
						}
					}
				}

				var cmd = new LayerGenericCommand(redo => {
					if (_editor == null) {
						var editor = ActEditorWindow.Instance.GetCurrentTab2();

						if (editor.SelectedAction == selectedActionIndex) {
							editor.SelectionEngine.SetSelection(redo ? newSelection.ToHashSet() : selectedLayerIndexes.ToHashSet());
						}
					}
					else {	
						if (_editor.SelectedAction == selectedActionIndex) {
							_editor.SelectionEngine.SetSelection(redo ? newSelection.ToHashSet() : selectedLayerIndexes.ToHashSet());
						}
					}
				});

				act.Commands.StoreAndExecute(cmd);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null || selectedLayerIndexes.Length <= 0)
				return false;

			selectedLayerIndexes = selectedLayerIndexes.OrderBy(p => p).ToArray();

			if (_dir == MoveDirection.Down) {
				for (int i = selectedLayerIndexes.Length - 1, j = 0; i >= 0; i--, j++)
					if (selectedLayerIndexes[i] != act[selectedActionIndex, selectedFrameIndex].NumberOfLayers - j - 1) return true;
			}
			else {
				for (int i = 0, j = 0; i < selectedLayerIndexes.Length; i++, j++)
					if (selectedLayerIndexes[i] != j) return true;
			}

			return false;
		}
	}

	public class FrameMirrorVertical : IActScript {
		#region IActScript Members

		public object DisplayName => "Mirror vertical";
		public string Group => "Frame";
		public string InputGesture => "{FrameEditor.FrameMirrorVertical|Ctrl-L}";
		public string Image => "flip2.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.MirrorFromOffset(selectedActionIndex, selectedFrameIndex, 0, FlipDirection.Vertical);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}	
			finally {
				act.InvalidateVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class FrameMirrorHorizontal : IActScript {
		#region IActScript Members

		public object DisplayName => "Mirror horizontal";
		public string Group => "Frame";
		public string InputGesture => "{FrameEditor.FrameMirrorHorizontal|Ctrl-Shift-L}";
		public string Image => "flip.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.MirrorFromOffset(selectedActionIndex, selectedFrameIndex, 0, FlipDirection.Horizontal);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				act.InvalidateVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class ActionMirrorVertical : IActScript {
		#region IActScript Members

		public object DisplayName => "Mirror vertical";
		public string Group => "Action";
		public string InputGesture => "{FrameEditor.ActionMirrorVertical|Alt-L}";
		public string Image => "flip2.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.MirrorFromOffset(selectedActionIndex, 0, FlipDirection.Vertical);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				act.InvalidateVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class ActionMirrorHorizontal : IActScript {
		#region IActScript Members

		public object DisplayName => "Mirror horizontal";
		public string Group => "Action";
		public string InputGesture => "{FrameEditor.ActionMirrorHorizontal|Alt-Shift-L}";
		public string Image => "flip.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.MirrorFromOffset(selectedActionIndex, 0, FlipDirection.Horizontal);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				act.InvalidateVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}
}