using TokeiLibrary.WPF;

namespace ActEditor.Core.ActionEditCommands {
	public interface IActionEditCommand<T> {
		string CommandDescription { get; }
		void Execute(RangeObservableCollection<T> data);
		void Undo(RangeObservableCollection<T> data);
	}
}
