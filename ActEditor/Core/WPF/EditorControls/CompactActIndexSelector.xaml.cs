using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Threading;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities.Commands;
using static ActEditor.Core.WPF.EditorControls.ActIndexSelector;
using Action = System.Action;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for FrameSelector.xaml
	/// </summary>
	public partial class CompactActIndexSelector : UserControl, IActIndexSelector {
		private readonly List<FancyButton> _fancyButtons;
		private bool _eventsEnabled = true;
		private bool _frameChangedEventEnabled = true;
		private bool _handlersEnabled = true;
		private IFrameRendererEditor _renderer;

		public CompactActIndexSelector() {
			InitializeComponent();

			try {
				_fancyButtons = new FancyButton[] {_fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7}.ToList();

				ActIndexSelectorHelper.BuildDirectionalActionSelectorUI(_fancyButtons, false);

				_sbFrameIndex.PreviewMouseLeftButtonDown += delegate {
					if (_renderer.Act == null)
						return;

					OnAnimationPlaying(2);
				};
				_sbFrameIndex.PreviewMouseLeftButtonUp += delegate {
					OnAnimationPlaying(0);
				};
				ScrollBarHelper.OverrideMouseIncrement(_sbFrameIndex, () => SelectedFrame++, () => SelectedFrame--);
			}
			catch {
			}

			try {
				_updatePlay();
				FrameChanged += _frameChanged;
				SpecialFrameChanged += _frameChanged;
				ActionChanged += _actionChanged;
				_play.Click += new RoutedEventHandler(_play_Click);

				WpfUtilities.AddFocus(_tbFrameIndex);
			}
			catch {
			}

			MouseEnter += delegate {
				Opacity = 1f;
			};

			MouseLeave += delegate {
				Opacity = 0.7f;
			};

			ActionChanged += _frameSelector_ActionChanged;
			_sbFrameIndex.ValueChanged += _sbFrameIndex_ValueChanged;
			_tbFrameIndex.TextChanged += _tbFrameIndex_TextChanged;

			_sbFrameIndex.SmallChange = 1;
			_sbFrameIndex.LargeChange = 1;

			Unloaded += delegate {
				Stop();
			};
		}

		private void _actionChanged(object sender, int actionIndex) {
			if (actionIndex < _comboBoxActionIndex.Items.Count && actionIndex > -1) {
				_comboBoxActionIndex.SelectedIndex = actionIndex;
			}
		}

		private void _frameChanged(object sender, int frameIndex) {
			try {
				_eventsEnabled = false;

				this.Dispatch(p => {
					_sbFrameIndex.Value = frameIndex;
					_tbFrameIndex.Text = frameIndex.ToString(CultureInfo.InvariantCulture);
				});
			}
			finally {
				_eventsEnabled = true;
			}
		}

		private int _selectedFrame;
		private int _selectedAction;

		public int SelectedAction {
			get => _selectedAction;
			set {
				if (value == _selectedAction)
					return;

				int max = _renderer.Act.NumberOfActions;
				_selectedAction = (value % max + max) % max;

				// This should always be done on the main UI thread
				this.Dispatch(_ => {
					OnActionChanged(_selectedAction);
				});
			}
		}

		public int SelectedFrame {
			get => _selectedFrame;
			set {
				if (value == _selectedFrame)
					return;

				int max = _renderer.Act[SelectedAction].NumberOfFrames;
				_selectedFrame = (value % max + max) % max;

				// This should always be done on the main UI thread
				this.Dispatch(_ => {
					OnFrameChanged(_selectedFrame);
				});
			}
		}

		public event FrameIndexChangedDelegate ActionChanged;
		public event FrameIndexChangedDelegate FrameChanged;
		public event FrameIndexChangedDelegate SpecialFrameChanged;

		public bool IsPlaying { get; private set; }

		public void OnSpecialFrameChanged(int actionindex) {
			if (!_handlersEnabled) return;
			FrameIndexChangedDelegate handler = SpecialFrameChanged;
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
			FrameIndexChangedDelegate handler = FrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		public void OnActionChanged(int actionindex) {
			_updateAction();
			if (!_handlersEnabled) return;
			FrameIndexChangedDelegate handler = ActionChanged;
			if (handler != null) handler(this, actionindex);
		}

		private void _play_Click(object sender, RoutedEventArgs e) {
			if (IsPlaying)
				Stop();
			else
				Play();
		}

		private void _playAnimation() {
			if (_renderer.Act == null) {
				_play_Click(null, null);
				return;
			}

			if (_renderer.Act[SelectedAction].NumberOfFrames <= 1) {
				_play_Click(null, null);
				return;
			}

			if (_renderer.Act[SelectedAction].AnimationSpeed < ActIndexSelector.MaxAnimationSpeed) {
				_play_Click(null, null);
				ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
				return;
			}

			Stopwatch watch = new Stopwatch();
			SelectedFrame--;

			int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;
			int interval = (int)(_renderer.Act[SelectedAction].AnimationSpeed * frameInterval);

			int intervalsToShow = 1;
			int intervalsToHide = 0;

			if (interval <= 50) {
				intervalsToShow = 1;
				intervalsToHide = 1;
			}

			if (interval <= frameInterval) {
				intervalsToShow = 1;
				intervalsToHide = 2;
			}

			if (intervalsToShow + intervalsToHide == _renderer.Act[SelectedAction].NumberOfFrames) {
				intervalsToShow++;
			}

			int currentIntervalShown = -intervalsToHide;

			try {
				OnAnimationPlaying(2);

				while (IsPlaying) {
					watch.Reset();
					watch.Start();

					interval = (int)(_renderer.Act[SelectedAction].AnimationSpeed * frameInterval);

					if (_renderer.Act[SelectedAction].AnimationSpeed < ActIndexSelector.MaxAnimationSpeed) {
						_play_Click(null, null);
						ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
						return;
					}

					SelectedFrame++;

					if (SelectedFrame >= _renderer.Act[SelectedAction].NumberOfFrames) {
						SelectedFrame = 0;
					}

					if (currentIntervalShown < 0) {
						_frameChangedEventEnabled = false;
						this.Dispatch(() => _sbFrameIndex.Value = SelectedFrame);
						_frameChangedEventEnabled = true;
					}
					else {
						this.Dispatch(() => _sbFrameIndex.Value = SelectedFrame);
					}

					currentIntervalShown++;

					if (currentIntervalShown >= intervalsToShow) {
						currentIntervalShown = -intervalsToHide;
					}

					if (!_play.Dispatch(p => p.IsPressed))
						return;

					watch.Stop();

					//Thread.Sleep((int)Math.Max(20, Math.Min(interval, expectedNextFrame)));
					Thread.Sleep(interval);
				}
			}
			catch {
				_play_Click(null, null);
			}
			finally {
				_frameChangedEventEnabled = true;
				OnAnimationPlaying(0);
			}
		}

		public void Play() {
			if (IsPlaying) return;

			_play.Dispatch(delegate {
				_play.IsPressed = true;
				_sbFrameIndex.IsEnabled = false;
				IsPlaying = true;
				_updatePlay();
			});

			GrfThread.Start(_playAnimation);
		}

		public void Stop() {
			if (!IsPlaying) return;

			_play.Dispatch(delegate {
				_play.IsPressed = false;
				_sbFrameIndex.IsEnabled = true;
				IsPlaying = false;
				_updatePlay();
			});
		}

		private void _updatePlay() {
			//((TextBlock) _play.FindName("_tbIdentifier")).Margin = new Thickness(3, 0, 0, 3);
			//((Grid) ((Grid) ((Border) _play.FindName("_border")).Child).Children[2]).HorizontalAlignment = HorizontalAlignment.Left;
			//((Grid) ((Grid) ((Border) _play.FindName("_border")).Child).Children[2]).Margin = new Thickness(0);

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

		private void _tbFrameIndex_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;
			if (_renderer.Act == null) return;

			int ival;

			Int32.TryParse(_tbFrameIndex.Text, out ival);

			if (ival > _renderer.Act[SelectedAction].NumberOfFrames || ival < 0) {
				ival = 0;
			}

			_sbFrameIndex.Value = ival;
		}

		private void _sbFrameIndex_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (!_eventsEnabled) return;
			if (_renderer.Act == null) return;

			int value = (int) Math.Round(_sbFrameIndex.Value);

			SelectedFrame = value;
			_sbFrameIndex.Value = value;
		}

		private void _updateAction() {
			if (_renderer.Act == null) return;
			if (SelectedAction >= _renderer.Act.NumberOfActions) return;

			_eventsEnabled = false;

			if (SelectedFrame >= _renderer.Act[_renderer.SelectedAction].NumberOfFrames && SelectedFrame > 0) {
				SelectedFrame = Math.Max(0, _renderer.Act[SelectedAction].NumberOfFrames - 1);
			}

			_eventsEnabled = true;

			int max = _renderer.Act[SelectedAction].NumberOfFrames - 1;
			max = max < 0 ? 0 : max;

			_sbFrameIndex.Minimum = 0;
			_sbFrameIndex.Maximum = max;

			_labelFrameIndex.Text = "/ " + max + " frame" + (max > 1 ? "s" : "");
		}

		private void _frameSelector_ActionChanged(object sender, int actionindex) {
			_updateAction();
			_sbFrameIndex.Value = 0;
		}

		public void Init(IFrameRendererEditor renderer, int selectedAction, int selectedFrame) {
			_renderer = renderer;
			_fancyButtons.ForEach(p => p.IsPressed = false);

			int oldAction = _comboBoxActionIndex.SelectedIndex;

			_comboBoxActionIndex.ItemsSource = null;
			_comboBoxActionIndex.Items.Clear();

			_comboBoxAnimationIndex.ItemsSource = null;
			_comboBoxAnimationIndex.Items.Clear();

			_eventsEnabled = true;

			int actions = renderer.Act.NumberOfActions;

			_comboBoxAnimationIndex.ItemsSource = ActHelper.GetAnimations(renderer.Act);
			_comboBoxActionIndex.ItemsSource = Enumerable.Range(0, actions);

			if (actions != 0) {
				_comboBoxActionIndex.SelectedIndex = 0;
			}

			renderer.Act.VisualInvalidated += s => Update();
			renderer.Act.RenderInvalidated += s => _renderer.FrameRenderer.Update();
			renderer.Act.Commands.CommandIndexChanged += new AbstractCommand<IActCommand>.AbstractCommandsEventHandler(_commands_CommandUndo);

			if (selectedAction > -1 && selectedAction < renderer.Act.NumberOfActions) {
				_comboBoxActionIndex.SelectedIndex = selectedAction;
			}
			else if (oldAction < renderer.Act.NumberOfActions && oldAction >= 0) {
				_comboBoxActionIndex.SelectedIndex = oldAction;
			}
		}

		private void _commands_CommandUndo(object sender, IActCommand command) {
			try {
				var actionCmd = _getCommand<ActionCommand>(command);

				if (actionCmd != null) {
					if (actionCmd.Executed &&
					    (actionCmd.Edit == ActionCommand.ActionEdit.CopyAt ||
					     actionCmd.Edit == ActionCommand.ActionEdit.InsertAt ||
					     actionCmd.Edit == ActionCommand.ActionEdit.ReplaceTo ||
					     actionCmd.Edit == ActionCommand.ActionEdit.InsertAt)) {
						SelectedAction = actionCmd.ActionIndexTo;
					}

					if (SelectedAction < 0)
						SelectedAction = 0;

					if (SelectedAction >= _renderer.Act.NumberOfActions)
						SelectedAction = _renderer.Act.NumberOfActions - 1;

					_updateActionSelection();
				}

				var frameCmd = _getCommand<FrameCommand>(command);

				if (frameCmd != null) {
					if (frameCmd.Executed) {
						if ((frameCmd.ActionIndexTo == SelectedAction && frameCmd.Edit == FrameCommand.FrameEdit.ReplaceTo) ||
						    (frameCmd.ActionIndexTo == SelectedAction && frameCmd.Edit == FrameCommand.FrameEdit.Switch) ||
						    (frameCmd.ActionIndexTo == SelectedAction && frameCmd.Edit == FrameCommand.FrameEdit.CopyTo)
							) {
							SelectedFrame = frameCmd.FrameIndexTo;
						}
						else if (frameCmd.ActionIndex == SelectedAction && frameCmd.Edit == FrameCommand.FrameEdit.InsertTo) {
							SelectedFrame = frameCmd.FrameIndex;
						}

						if (SelectedFrame != (int) _sbFrameIndex.Value) {
							_sbFrameIndex.Value = SelectedFrame;
						}
					}
				}

				_updateInfo();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private T _getCommand<T>(IActCommand command) where T : class, IActCommand {
			var cmd = command as ActGroupCommand;

			if (cmd != null) {
				return cmd.Commands.FirstOrDefault(p => p.GetType() == typeof (T)) as T;
			}

			if (command is T) {
				return command as T;
			}

			return null;
		}

		private void _updateActionSelection() {
			try {
				int selectedAction = SelectedAction;

				_comboBoxAnimationIndex.ItemsSource = null;
				_comboBoxAnimationIndex.ItemsSource = ActHelper.GetAnimations(_renderer.Act);
				_comboBoxActionIndex.ItemsSource = null;
				_comboBoxActionIndex.ItemsSource = Enumerable.Range(0, _renderer.Act.NumberOfActions);

				if (selectedAction >= _comboBoxActionIndex.Items.Count) {
					_comboBoxActionIndex.SelectedIndex = _comboBoxActionIndex.Items.Count - 1;
				}

				_comboBoxActionIndex.SelectedIndex = selectedAction;

				//Call update?
				Update();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _updateInfo() {
			Stop();
			_updateAction();
		}

		private void _fancyButton_Click(object sender, RoutedEventArgs e) {
			int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

			_fancyButtons.ForEach(p => p.IsPressed = false);
			((FancyButton) sender).IsPressed = true;

			_comboBoxActionIndex.SelectedIndex = animationIndex * 8 + Int32.Parse(((FancyButton) sender).Tag.ToString());
		}

		private void _setDisabledButtons() {
			Dispatcher.Invoke(new Action(delegate {
				int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

				if ((animationIndex + 1) * 8 > _renderer.Act.NumberOfActions) {
					_fancyButtons.ForEach(p => p.IsButtonEnabled = true);

					int toDisable = (animationIndex + 1) * 8 - _renderer.Act.NumberOfActions;

					for (int i = 0; i < toDisable; i++) {
						int disabledIndex = 7 - i;
						_fancyButtons.First(p => Int32.Parse(p.Tag.ToString()) == disabledIndex).IsButtonEnabled = false;
					}
				}
				else {
					_fancyButtons.ForEach(p => p.IsButtonEnabled = true);
				}
			}));
		}

		private void _comboBoxAnimationIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxAnimationIndex.SelectedIndex < 0) return;

			int direction = _comboBoxActionIndex.SelectedIndex % 8;

			if (8 * _comboBoxAnimationIndex.SelectedIndex + direction >= _renderer.Act.NumberOfActions) {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex;
			}
			else {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex + direction;
			}
		}

		private void _comboBoxActionIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxActionIndex.SelectedIndex < 0) return;
			if (_comboBoxActionIndex.SelectedIndex >= _renderer.Act.NumberOfActions) return;

			int actionIndex = _comboBoxActionIndex.SelectedIndex;
			int animationIndex = actionIndex / 8;
			_disableEvents();
			_comboBoxAnimationIndex.SelectedIndex = animationIndex;
			_fancyButton_Click(_fancyButtons.First(p => p.Tag.ToString() == (actionIndex % 8).ToString(CultureInfo.InvariantCulture)), null);
			_setDisabledButtons();
			SelectedAction = _comboBoxActionIndex.SelectedIndex;
			SelectedFrame = 0;
			OnActionChanged(SelectedAction);
			_enableEvents();
		}

		public void Update() {
			try {
				int oldFrame = SelectedFrame;
				bool differedUpdate = oldFrame != 0;

				if (differedUpdate) {
					_handlersEnabled = false;
				}

				_comboBoxActionIndex_SelectionChanged(null, null);

				if (_renderer.Act == null) return;

				if (SelectedAction >= 0 && SelectedAction < _renderer.Act.NumberOfActions) {
					if (oldFrame < _renderer.Act[SelectedAction].NumberOfFrames) {
						if (differedUpdate) {
							_handlersEnabled = true;
							SelectedFrame = oldFrame;

							if (_sbFrameIndex.Value == oldFrame) {
								OnFrameChanged(oldFrame);
							}
							else {
								_sbFrameIndex.Value = oldFrame;
							}
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _disableEvents() {
			_comboBoxAnimationIndex.SelectionChanged -= _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click -= _fancyButton_Click);
		}

		private void _enableEvents() {
			_comboBoxAnimationIndex.SelectionChanged += _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click += _fancyButton_Click);
		}

		public void Reset() {
			_eventsEnabled = false;

			_fancyButtons.ForEach(p => p.IsPressed = false);
			_fancyButtons.ForEach(p => p.IsButtonEnabled = false);

			_comboBoxActionIndex.ItemsSource = null;
			_comboBoxActionIndex.Items.Clear();

			_comboBoxAnimationIndex.ItemsSource = null;
			_comboBoxAnimationIndex.Items.Clear();

			_sbFrameIndex.Value = 0;
			_tbFrameIndex.Text = "0";
			_labelFrameIndex.Text = "/ 0 frame";
			_sbFrameIndex.Maximum = 0;

			_play.IsPressed = false;
			_updatePlay();

			_eventsEnabled = true;
		}

		public void DisableActionChange() {
			_fancyButtons.ForEach(p => p.IsPressed = false);
			_fancyButtons.ForEach(p => p.IsButtonEnabled = false);
			_comboBoxActionIndex.IsEnabled = false;
			_comboBoxAnimationIndex.IsEnabled = false;
		}
	}
}