using ActEditor.Core.ActionEditCommands;
using ErrorManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TokeiLibrary.Shortcuts;

namespace ActEditor.Core.WPF.Dialogs {
	public class ActEditListInteraction {
		private CommandsHolder _commands;
		private UIElement _window;
		private ListBox _listBox;
		private SelectableListBoxExtension _listBoxExtension;

		public ActEditListInteraction(CommandsHolder commands, FrameworkElement window, ListBox listBox, SelectableListBoxExtension listBoxExtension) {
			_commands = commands;
			_window = window;
			_listBox = listBox;
			_listBoxExtension = listBoxExtension;
			_listBoxExtension.ListItemsDropped += (idx) => ItemsDrop(idx);
			_listBoxExtension.ClipboardItemsDropped += (idx) => PasteEnd(idx);

			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Z", "ListData.Undo"), Undo, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Y", "ListData.Redo"), Redo, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Delete", "ListData.Remove"), Remove, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-C", "ListData.Copy"), Copy, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-T", "ListData.MoveAt"), MoveAt, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-V", "ListData.Paste"), Paste, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-X", "ListData.Cut"), Cut, window);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Shift-V", "ListData.InsertAt"), InsertAt, window);
		}

		public void Cut() {
			Copy();
			Remove();
		}

		public void Copy() {
			_listBoxExtension.Copy();
		}

		public void MoveAt() {
			_listBoxExtension.MoveAt();
		}
		public void InsertAt() {
			_listBoxExtension.InsertAt();
		}

		public void Paste() {
			PasteEnd(_listBoxExtension.GetInsertIndexFromSelectedItem());
		}

		public void PasteEnd(int insertIndex) {
			try {
				if (insertIndex < 0)
					return;

				var data = _listBoxExtension.GetClipboardData();

				if (data == null)
					return;

				_commands.BeginNoDelay();

				var items = data.OrderBy(p => p.Index);

				_commands.CopyAndInsert(insertIndex, items.ToList(), _listBox.SelectedItems.OfType<IEditData>().ToList());
			}
			catch (Exception err) {
				_commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_commands.End();
			}
		}

		public void ItemsDrop(int insertIndex) {
			try {
				_commands.BeginNoDelay();

				var items = _listBox.SelectedItems.OfType<IEditData>().OrderBy(p => p.Index);

				_commands.MoveAndInsert(insertIndex, items.ToList());
			}
			catch (Exception err) {
				_commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_commands.End();
			}
		}

		public void Undo() {
			try {
				_commands.Undo();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void Redo() {
			try {
				_commands.Redo();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void Remove() {
			try {
				_commands.BeginNoDelay();

				var items = _listBox.SelectedItems.OfType<IEditData>().OrderByDescending(p => p.Index);

				foreach (var item in items) {
					_commands.RemoveAt(item.Index);
				}
			}
			catch (Exception err) {
				_commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_commands.End();
			}
		}

		public void New(IEditData item) {
			try {
				var insertIndex = _listBoxExtension.GetInsertIndexFromSelectedItem();

				if (insertIndex < 0)
					return;

				_commands.BeginNoDelay();
				_commands.CopyAndInsert(insertIndex, new List<IEditData>() { item }, _listBox.SelectedItems.OfType<IEditData>().ToList());
			}
			catch (Exception err) {
				_commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_commands.End();
			}
		}
	}
}
