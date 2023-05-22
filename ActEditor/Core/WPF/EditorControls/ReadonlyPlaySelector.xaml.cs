using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Threading;
using TokeiLibrary;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for FrameSelector.xaml
	/// </summary>
	public partial class ReadonlyPlaySelector : UserControl, ISelector {
		#region Delegates

		public delegate void FrameIndexChangedDelegate(object sender, int actionIndex);

		#endregion

		private bool _eventsEnabled = true;
		private bool _frameChangedEventEnabled = true;
		private bool _handlersEnabled = true;
		private int _frameIndex;

		public int SelectedFrame {
			get { return _frameIndex; }
			set {
				_frameIndex = value;

				if (_frameIndex >= Act[SelectedAction].NumberOfFrames)
					_frameIndex = 0;

				if (!_eventsEnabled) return;
				if (Act == null) return;

				OnFrameChanged(value);
			}
		}

		public Act Act { get; private set; }

		public ReadonlyPlaySelector() {
			InitializeComponent();

			try {
				_updatePlay();
				_play.Click += new RoutedEventHandler(_play_Click);
			}
			catch {
			}

			MouseEnter += delegate {
				Opacity = 1f;
			};

			MouseLeave += delegate {
				Opacity = 0.7f;
			};
		}

		public int SelectedAction { get; set; }

		public event ActIndexSelector.FrameIndexChangedDelegate ActionChanged;
		public event ActIndexSelector.FrameIndexChangedDelegate FrameChanged;
		public event ActIndexSelector.FrameIndexChangedDelegate SpecialFrameChanged;

		public void OnSpecialFrameChanged(int actionindex) {
			if (!_handlersEnabled) return;
			ActIndexSelector.FrameIndexChangedDelegate handler = SpecialFrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		public event FrameIndexChangedDelegate AnimationPlaying;

		public void OnAnimationPlaying(int actionindex) {
			FrameIndexChangedDelegate handler = AnimationPlaying;
			if (handler != null) handler(this, actionindex);
		}

		public void OnFrameChanged(int actionindex) {
			if (!_handlersEnabled) return;
			if (!_frameChangedEventEnabled) {
				OnSpecialFrameChanged(actionindex);
				return;
			}
			ActIndexSelector.FrameIndexChangedDelegate handler = FrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		public void OnActionChanged(int actionindex) {
			if (!_handlersEnabled) return;
			ActIndexSelector.FrameIndexChangedDelegate handler = ActionChanged;
			if (handler != null) handler(this, actionindex);
		}

		private void _play_Click(object sender, RoutedEventArgs e) {
			_play.Dispatch(delegate {
				_play.IsPressed = !_play.IsPressed;
				_updatePlay();
			});

			if (_play.Dispatch(() => _play.IsPressed)) {
				GrfThread.Start(_playAnimation);
			}
		}

		public bool IsPlaying() {
			return _play.Dispatch(() => _play.IsPressed);
		}

		public void Play() {
			if (IsPlaying()) return;

			_play.Dispatch(delegate {
				_play.IsPressed = true;
				_updatePlay();
			});

			if (_play.Dispatch(() => _play.IsPressed)) {
				GrfThread.Start(_playAnimation);
			}
		}

		public void Stop() {
			_play.Dispatch(delegate {
				_play.IsPressed = false;
				_updatePlay();
			});
		}

		private void _playAnimation() {
			Act act = Act;

			if (act == null) {
				_play_Click(null, null);
				return;
			}

			if (act[SelectedAction].NumberOfFrames <= 1) {
				this.Dispatch(p => p.OnFrameChanged(0));
				_play_Click(null, null);
				return;
			}

			if (act[SelectedAction].AnimationSpeed < 0.8f) {
				_play_Click(null, null);
				ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
				return;
			}

			Stopwatch watch = new Stopwatch();

			int interval = (int)(act[SelectedAction].AnimationSpeed * 25f);

			int intervalsToShow = 1;
			int intervalsToHide = 0;

			if (interval <= 50) {
				intervalsToShow = 1;
				intervalsToHide = 1;
			}

			if (interval <= 25) {
				intervalsToShow = 1;
				intervalsToHide = 2;
			}

			if (intervalsToShow + intervalsToHide == act[SelectedAction].NumberOfFrames) {
				intervalsToShow++;
			}

			int currentIntervalShown = -intervalsToHide;

			try {
				OnAnimationPlaying(2);

				while (_play.Dispatch(p => p.IsPressed)) {
					watch.Reset();
					watch.Start();

					interval = (int)(act[SelectedAction].AnimationSpeed * 25f);

					if (act[SelectedAction].AnimationSpeed < 0.8f) {
						_play_Click(null, null);
						ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
						return;
					}

					currentIntervalShown++;

					if (currentIntervalShown >= intervalsToShow) {
						currentIntervalShown = -intervalsToHide;
					}

					if (!_play.Dispatch(p => p.IsPressed))
						return;

					this.Dispatch(p => SelectedFrame++);

					watch.Stop();

					Thread.Sleep(interval);

				}
			}
			finally {
				_frameChangedEventEnabled = true;
				OnAnimationPlaying(0);
			}
		}

		private void _updatePlay() {
			if (_play.IsPressed) {
				_play.ImagePath = "stop2.png";
				_play.ImageIcon.Width = 16;
				_play.ImageIcon.Stretch = Stretch.Fill;
			}
			else {
				_play.ImagePath = "play.png";
				_play.ImageIcon.Width = 16;
				_play.ImageIcon.Stretch = Stretch.Fill;
			}
		}

		public void Init(Act act, int selectedAction) {
			SelectedAction = selectedAction;
			Act = act;
		}

		public void Update() {
			try {
				int oldFrame = SelectedFrame;
				bool differedUpdate = oldFrame != 0;

				if (differedUpdate) {
					_handlersEnabled = false;
				}

				if (Act == null) return;

				if (SelectedAction >= 0 && SelectedAction < Act.NumberOfActions) {
					if (oldFrame < Act[SelectedAction].NumberOfFrames) {
						if (differedUpdate) {
							_handlersEnabled = true;
							SelectedFrame = oldFrame;

							if (SelectedFrame == oldFrame) {
								OnFrameChanged(oldFrame);
							}
							else {
								SelectedFrame = oldFrame;
							}
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public IActIndexSelector ToActIndexSelector() {
			return new ReadonlyActIndexSelector(this);
		}
	}
}