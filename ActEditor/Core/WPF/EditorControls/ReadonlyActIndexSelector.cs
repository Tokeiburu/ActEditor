using System;

namespace ActEditor.Core.WPF.EditorControls {
	public class ReadonlyActIndexSelector : IActIndexSelector {
		private Func<bool> _isPlaying;

		public ReadonlyActIndexSelector(ReadonlyPlaySelector rps) {
			_isPlaying = rps.IsPlaying;

			rps.FrameChanged += (sender, actionIndex) => OnFrameChanged(actionIndex);
			rps.ActionChanged += (sender, actionIndex) => OnActionChanged(actionIndex);
			rps.SpecialFrameChanged += (sender, actionIndex) => OnSpecialFrameChanged(actionIndex);
		}

		public ReadonlyActIndexSelector(CompactActIndexSelector rps) {
			_isPlaying = rps.IsPlaying;

			rps.FrameChanged += (sender, actionIndex) => OnFrameChanged(actionIndex);
			rps.ActionChanged += (sender, actionIndex) => OnActionChanged(actionIndex);
			rps.SpecialFrameChanged += (sender, actionIndex) => OnSpecialFrameChanged(actionIndex);
		}

		public void OnFrameChanged(int actionindex) {
			ActIndexSelector.FrameIndexChangedDelegate handler = FrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		public bool IsPlaying {
			get { return _isPlaying(); }
		}

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
			// Nothing to do
		}

		public void SetFrame(int index) {
			// Nothing to do
		}
	}
}
