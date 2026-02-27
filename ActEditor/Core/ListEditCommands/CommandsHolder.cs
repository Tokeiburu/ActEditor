using System.Collections.Generic;
using TokeiLibrary.WPF;
using Utilities.Commands;

namespace ActEditor.Core.ListEditCommands {
	public interface IEditData {
		int Index { get; set; }
		IEditData Copy();
	}

	public class CommandsHolder : AbstractCommand<IListEditCommand<IEditData>> {
		private RangeObservableCollection<IEditData> _data;

		public CommandsHolder(RangeObservableCollection<IEditData> data) {
			_data = data;
		}

		protected override void _execute(IListEditCommand<IEditData> command) {
			command.Execute(_data);
		}

		protected override void _undo(IListEditCommand<IEditData> command) {
			command.Undo(_data);
		}

		protected override void _redo(IListEditCommand<IEditData> command) {
			command.Execute(_data);
		}

		public void Remove(int startIndex, int length) {
			StoreAndExecute(new ListEditCommand(EditCommandType.Remove, startIndex, length));
		}

		public void RemoveAt(int index) {
			StoreAndExecute(new ListEditCommand(EditCommandType.Remove, index, 1));
		}

		public void MoveAndInsert(int index, List<IEditData> items) {
			foreach (var item in items) {
				if (item.Index == index)
					return;
			}

			StoreAndExecute(new ListEditCommand(EditCommandType.MoveAndInsert, index, items));
		}

		public void CopyAndInsert(int index, List<IEditData> items, List<IEditData> oldSelection) {
			StoreAndExecute(new ListEditCommand(EditCommandType.CopyAndInsert, index, items, oldSelection));
		}

		/// <summary>
		/// Begins the commands stack grouping.
		/// </summary>
		public void Begin() {
			BeginEdit(new ListEditGroupCommand<IEditData>(_data, false));
		}

		/// <summary>
		/// Begins the commands stack grouping and apply commands as soon as they're received.
		/// </summary>
		public void BeginNoDelay() {
			BeginEdit(new ListEditGroupCommand<IEditData>(_data, true));
		}

		/// <summary>
		/// Ends the commands stack grouping.
		/// </summary>
		public void End() {
			EndEdit();
		}
	}
}
