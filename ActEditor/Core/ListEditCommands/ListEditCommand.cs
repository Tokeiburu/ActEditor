using System.Collections.Generic;
using System.Linq;
using TokeiLibrary.WPF;

namespace ActEditor.Core.ListEditCommands {
	public enum EditCommandType {
		Remove,
		Insert,
		MoveAndInsert,
		CopyAndInsert,
	}

	public class ListEditCommand : IListEditCommand<IEditData> {
		private EditCommandType _commandType;
		private List<IEditData> _items;
		private int _index;
		private int _length;
		private List<IEditData> _oldSelection;

		public string CommandDescription => "";
		public EditCommandType CommandType => _commandType;
		public List<IEditData> Selection = new List<IEditData>();
		private List<IEditData> _commandTempValues = new List<IEditData>();

		public ListEditCommand(EditCommandType commandType, int index, int length) {
			_commandType = commandType;
			_index = index;
			_length = length;
		}

		public ListEditCommand(EditCommandType commandType, int index, List<IEditData> items, List<IEditData> oldSelection = null) {
			_commandType = commandType;
			_index = index;
			_items = items;
			_oldSelection = oldSelection;
		}

		public void Execute(RangeObservableCollection<IEditData> data) {
			IEditData target;
			int startIndex;
			Selection.Clear();

			switch (_commandType) {
				case EditCommandType.Remove:
					_commandTempValues.Clear();

					data.Disable();
					for (int i = 0; i < _length; i++) {
						_commandTempValues.Add(data[_index]);
						data.RemoveAt(_index);
					}

					var selectTarget = data.FirstOrDefault(p => p.Index == _index + 1);

					if (selectTarget != null)
						Selection.Add(selectTarget);
					break;
				case EditCommandType.MoveAndInsert:
					data.Disable();
					_commandTempValues = new List<IEditData>(data);
					target = _index >= data.Count ? null : data[_index];

					foreach (var item in _items) {
						data.Remove(item);
					}

					startIndex = target == null ? data.Count : data.IndexOf(target);

					for (int i = 0; i < _items.Count; i++) {
						data.Insert(startIndex + i, _items[i]);
					}

					Selection.AddRange(_items);
					break;
				case EditCommandType.CopyAndInsert:
					data.Disable();

					_commandTempValues.Clear();
					target = _index >= data.Count ? null : data[_index];
					startIndex = target == null ? data.Count : data.IndexOf(target);

					for (int i = 0; i < _items.Count; i++) {
						var copy = _items[i].Copy();
						data.Insert(startIndex + i, copy);
						_commandTempValues.Add(copy);
					}

					Selection.AddRange(_commandTempValues);
					break;
			}
		}

		public void Undo(RangeObservableCollection<IEditData> data) {
			Selection.Clear();

			switch (_commandType) {
				case EditCommandType.Remove:
					data.Disable();
					for (int i = _length - 1; i >= 0; i--) {
						data.Insert(_index, _commandTempValues[i]);
					}

					Selection.AddRange(_commandTempValues);
					break;
				case EditCommandType.MoveAndInsert:
					data.Disable();
					foreach (var item in _items) {
						data.Remove(item);
					}

					for (int i = 0; i < _commandTempValues.Count; i++) {
						if (data.Count <= i || _commandTempValues[i] != data[i]) {
							data.Insert(i, _commandTempValues[i]);
						}
					}

					Selection.AddRange(_items);
					break;
				case EditCommandType.CopyAndInsert:
					data.Disable();
					foreach (var item in _commandTempValues) {
						data.Remove(item);
					}

					Selection.AddRange(_oldSelection);
					break;
			}
		}
	}
}
