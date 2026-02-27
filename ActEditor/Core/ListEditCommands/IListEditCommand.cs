using TokeiLibrary.WPF;

namespace ActEditor.Core.ListEditCommands {
	public interface IListEditCommand<T> {
		string CommandDescription { get; }
		void Execute(RangeObservableCollection<T> data);
		void Undo(RangeObservableCollection<T> data);
	}
}
