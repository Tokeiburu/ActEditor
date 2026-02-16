using ActEditor.Core.WPF.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TokeiLibrary.WPF;
using Utilities.Commands;

namespace ActEditor.Core.ActionEditCommands {
	public interface IEditData {
		int Index { get; set; }
		IEditData Copy();
	}

	public class CommandsHolder : AbstractCommand<IActionEditCommand<IEditData>> {
		private RangeObservableCollection<IEditData> _data;

		public CommandsHolder(RangeObservableCollection<IEditData> data) {
			_data = data;
		}

		protected override void _execute(IActionEditCommand<IEditData> command) {
			command.Execute(_data);
		}

		protected override void _undo(IActionEditCommand<IEditData> command) {
			command.Undo(_data);
		}

		protected override void _redo(IActionEditCommand<IEditData> command) {
			command.Execute(_data);
		}

		public void Remove(int startIndex, int length) {
			StoreAndExecute(new EditCommand(EditCommandType.Remove, startIndex, length));
		}

		public void RemoveAt(int index) {
			StoreAndExecute(new EditCommand(EditCommandType.Remove, index, 1));
		}

		public void MoveAndInsert(int index, List<IEditData> items) {
			foreach (var item in items) {
				if (item.Index == index)
					return;
			}

			StoreAndExecute(new EditCommand(EditCommandType.MoveAndInsert, index, items));
		}

		public void CopyAndInsert(int index, List<IEditData> items, List<IEditData> oldSelection) {
			//foreach (var item in items) {
			//	if (item.Index == index)
			//		return;
			//}

			StoreAndExecute(new EditCommand(EditCommandType.CopyAndInsert, index, items, oldSelection));
		}

		/// <summary>
		/// Begins the commands stack grouping.
		/// </summary>
		public void Begin() {
			BeginEdit(new ActionEditGroupCommand<IEditData>(_data, false));
		}

		/// <summary>
		/// Begins the commands stack grouping and apply commands as soon as they're received.
		/// </summary>
		public void BeginNoDelay() {
			BeginEdit(new ActionEditGroupCommand<IEditData>(_data, true));
		}

		/// <summary>
		/// Ends the commands stack grouping.
		/// </summary>
		public void End() {
			EndEdit();
		}
	}
}
