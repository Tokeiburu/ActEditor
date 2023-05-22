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

namespace ActEditor.Core.Scripts {
	public class ActionCopy : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Copy action"; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.CopyAction|Alt-C}"; }
		}

		public string Image {
			get { return "copy.png"; }
		}

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

		public object DisplayName {
			get { return "Paste action"; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.PasteAction|Alt-V}"; }
		}

		public string Image {
			get { return "paste.png"; }
		}

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

		public object DisplayName {
			get { return "Delete action"; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.DeleteAction|Alt-Delete}"; }
		}

		public string Image {
			get { return "delete.png"; }
		}

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

	public class ActionInsertAt : IActScript {
		#region IActScript Members

		public object DisplayName {
			get { return "Add action to..."; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{Dialog.AddActionTo|Alt-T}"; }
		}

		public string Image {
			get { return "add.png"; }
		}

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

		public object DisplayName {
			get { return "Switch action to..."; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{Dialog.SwitchActionTo|Alt-M}"; }
		}

		public string Image {
			get { return "refresh.png"; }
		}

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

		public object DisplayName {
			get { return "Copy action and replace to..."; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{Dialog.OverwriteActionTo|Alt-G}"; }
		}

		public string Image {
			get { return "convert.png"; }
		}

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

		public object DisplayName {
			get { return "Advanced edit..."; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{Dialog.AdvancedEdit|Alt-E}"; }
		}

		public string Image {
			get { return "advanced.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				ActionInsertDialog dialog = new ActionInsertDialog(act);
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

		public string InputGesture {
			get { return "{FrameEditor.MirrorAction|Alt-X}"; }
		}

		public string Image {
			get { return "convert.png"; }
		}

		public string Group {
			get { return "Action"; }
		}

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

			public string CommandDescription { get { return "Selection changed..."; } }
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

		public string Group {
			get { return "Action"; }
		}

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

		public object DisplayName {
			get { return "Mirror vertical"; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.FrameMirrorVertical|Ctrl-L}"; }
		}

		public string Image {
			get { return "flip2.png"; }
		}

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

		public object DisplayName {
			get { return "Mirror horizontal"; }
		}

		public string Group {
			get { return "Frame"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.FrameMirrorHorizontal|Ctrl-Shift-L}"; }
		}

		public string Image {
			get { return "flip.png"; }
		}

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

		public object DisplayName {
			get { return "Mirror vertical"; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.ActionMirrorVertical|Alt-L}"; }
		}

		public string Image {
			get { return "flip2.png"; }
		}

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

		public object DisplayName {
			get { return "Mirror horizontal"; }
		}

		public string Group {
			get { return "Action"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.ActionMirrorHorizontal|Alt-Shift-L}"; }
		}

		public string Image {
			get { return "flip.png"; }
		}

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