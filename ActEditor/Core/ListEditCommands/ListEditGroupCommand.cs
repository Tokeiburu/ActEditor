using System;
using System.Collections.Generic;
using TokeiLibrary.WPF;
using Utilities.Commands;

namespace ActEditor.Core.ListEditCommands {
	public class ListEditGroupCommand<T> : IGroupCommand<IListEditCommand<T>>, IListEditCommand<T> {
		private readonly RangeObservableCollection<T> _data;
		private readonly List<IListEditCommand<T>> _commands = new List<IListEditCommand<T>>();
		private readonly bool _executeCommandsOnStore;
		private bool _firstTimeExecuted = true;

		public ListEditGroupCommand(RangeObservableCollection<T> data, bool executeCommandsOnStore = false) {
			_data = data;
			_executeCommandsOnStore = executeCommandsOnStore;
		}

		#region IActCommand Members

		public void Execute(RangeObservableCollection<T> data) {
			if (_executeCommandsOnStore) {
				if (_firstTimeExecuted) {
					_firstTimeExecuted = false;
					return;
				}
			}

			for (int index = 0; index < _commands.Count; index++) {
				var command = _commands[index];
				try {
					command.Execute(data);
				}
				catch (AbstractCommandException) {
					_commands.RemoveAt(index);
					index--;
				}
			}
		}

		public void Undo(RangeObservableCollection<T> data) {
			for (int index = _commands.Count - 1; index >= 0; index--) {
				_commands[index].Undo(data);
			}
		}

		public string CommandDescription => "";

		#endregion

		#region IGroupCommand<IActCommand> Members

		public List<IListEditCommand<T>> Commands {
			get { return _commands; }
		}

		public void Close() {
		}

		public void Processing(IListEditCommand<T> command) {
			if (_executeCommandsOnStore)
				command.Execute(_data);
		}

		public void AddRange(List<IListEditCommand<T>> commands) {
			_commands.AddRange(commands);
		}

		public void Add(IListEditCommand<T> command) {
			throw new NotImplementedException();
		}

		#endregion
	}
}
