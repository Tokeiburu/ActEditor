using ActEditor.Core.WPF.EditorControls;

namespace ActEditor.Core.WPF.Dialogs {
	public class HeadEditorActIndexSelector : IActIndexSelector {
		private readonly HeadEditorDialog _editor;

		public HeadEditorActIndexSelector(HeadEditorDialog editor) {
			_editor = editor;
		}

		public void OnFrameChanged(int actionindex) {
			ActIndexSelector.FrameIndexChangedDelegate handler = FrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		public bool IsPlaying { get { return false; } }
		public event ActIndexSelector.FrameIndexChangedDelegate ActionChanged;

		public void OnActionChanged(int actionindex) {
			ActIndexSelector.FrameIndexChangedDelegate handler = ActionChanged;
			if (handler != null) handler(this, actionindex);
		}

		public event ActIndexSelector.FrameIndexChangedDelegate FrameChanged;
		public event ActIndexSelector.FrameIndexChangedDelegate SpecialFrameChanged;

		public void OnSpecialFrameChanged(int actionindex) {
			ActIndexSelector.FrameIndexChangedDelegate handler = SpecialFrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		public void OnAnimationPlaying(int actionindex) {
		}

		public void SetAction(int index) {
			_editor._listViewHeads.SelectedIndex = index;
		}

		public void SetFrame(int index) {
			// Nothing to do
		}

		public int SelectedAction { get; set; }
		public int SelectedFrame { get; set; }

		public void Play() {
			// Nothing to do
		}

		public void Stop() {
			// Nothing to do
		}

		public void Init(IFrameRendererEditor editor, int actionIndex, int selectedAction) {
			// Nothing to do
		}
	}
}
