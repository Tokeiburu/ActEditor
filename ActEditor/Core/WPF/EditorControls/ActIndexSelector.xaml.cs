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
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Threading;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Commands;
using Utilities.Extension;
using Action = System.Action;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for FrameSelector.xaml
	/// </summary>
	public partial class ActIndexSelector : UserControl, IActIndexSelector {
		#region Delegates

		public delegate void FrameIndexChangedDelegate(object sender, int frameIndex);

		#endregion

		//public static float MaxAnimationSpeed = 0.8f;
		public static float MaxAnimationSpeed = 0.1f;
		private readonly List<FancyButton> _fancyButtons;
		private readonly SoundEffect _se = new SoundEffect();
		private IFrameRendererEditor _actEditor;
		private int _eventsEnabledCounter = 0;
		private bool _eventsEnabled = true;
		private bool _frameChangedEventEnabled = true;
		private bool _handlersEnabled = true;

		public ActIndexSelector() {
			InitializeComponent();

			try {
				_fancyButtons = new FancyButton[] { _fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7 }.ToList();

				ActIndexSelectorHelper.BuildDirectionalActionSelectorUI(_fancyButtons, false);

				_sbFrameIndex.PreviewMouseLeftButtonDown += delegate {
					if (_actEditor.Act == null)
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
				_cbSound.SelectionChanged += _cbSound_SelectionChanged;
				_play.Click += new RoutedEventHandler(_play_Click);
				FrameChanged += _frameChanged;
				SpecialFrameChanged += _frameChanged;
				ActionChanged += _actionChanged;

				_cbSoundEnable.IsPressed = !ActEditorConfiguration.ActEditorPlaySound;

				Action action = new Action(delegate {
					ActEditorConfiguration.ActEditorPlaySound = !_cbSoundEnable.IsPressed;
					_cbSoundEnable.ImagePath = ActEditorConfiguration.ActEditorPlaySound ? "soundOn.png" : "soundOff.png";
					_cbSoundEnable.ToolTip = ActEditorConfiguration.ActEditorPlaySound ? "Sounds are currently enabled." : "Sounds are currenty disabled.";
				});

				_cbSoundEnable.Click += delegate {
					_cbSoundEnable.IsPressed = !_cbSoundEnable.IsPressed;
					action();
				};

				action();

				((TextBlock)_buttonRenderMode.FindName("_tbIdentifier")).Margin = new Thickness(3, 0, 0, 3);
				((Grid)((Grid)((Border)_buttonRenderMode.FindName("_border")).Child).Children[2]).HorizontalAlignment = HorizontalAlignment.Left;
				((Grid)((Grid)((Border)_buttonRenderMode.FindName("_border")).Child).Children[2]).Margin = new Thickness(2, 0, 0, 0);

				ActIndexSelectorHelper.UpdatePlayButtonUI(_play);

				Action action2 = new Action(delegate {
					bool isEditor = ActEditorConfiguration.ActEditorScalingMode == BitmapScalingMode.NearestNeighbor;

					_buttonRenderMode.ImagePath = isEditor ? "editor.png" : "ingame.png";
					_buttonRenderMode.TextHeader = isEditor ? "Editor" : "Ingame";

					_buttonRenderMode.IsPressed = !isEditor;

					_buttonRenderMode.ToolTip = isEditor ? "Render mode is currently set to \"Editor\"." : "Render mode is currently set to \"Ingame\".";
				});

				_buttonRenderMode.Click += delegate {
					ActEditorConfiguration.ActEditorScalingMode = ActEditorConfiguration.ActEditorScalingMode == BitmapScalingMode.NearestNeighbor ? BitmapScalingMode.Fant : BitmapScalingMode.NearestNeighbor;
					action2();
					_actEditor.FrameRenderer.DrawSlotManager.ImagesDirty();
				};

				action2();

				WpfUtilities.AddFocus(_tbFrameIndex, _interval);
			}
			catch {
			}
		}

		private void _actionChanged(object sender, int actionIndex) {
			if (actionIndex < _comboBoxActionIndex.Items.Count && actionIndex > -1) {
				_comboBoxActionIndex.SelectedIndex = actionIndex;
			}
		}

		private void _frameChanged(object sender, int frameIndex) {
			try {
				DisableEvents();

				this.Dispatch(p => {
					_sbFrameIndex.Value = frameIndex;
					_tbFrameIndex.Text = frameIndex.ToString(CultureInfo.InvariantCulture);
					_cbSound.SelectedIndex = _actEditor.Act[_actEditor.SelectedAction, frameIndex].SoundId + 1;
				});
			}
			finally {
				EnableEvents();
			}
		}

		private int _selectedFrame;
		private int _selectedAction;

		public int SelectedAction {
			get => _selectedAction;
			set {
				if (value == _selectedAction)
					return;

				int max = _actEditor.Act.NumberOfActions;
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

				int max = _actEditor.Act[SelectedAction].NumberOfFrames;
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

		public void OnAnimationPlaying(int mode) {
			FrameIndexChangedDelegate handler = AnimationPlaying;
			if (handler != null) handler(this, mode);
		}

		public void OnFrameChanged(int frameIndex) {
			if (!_handlersEnabled) return;
			if (!_frameChangedEventEnabled) {
				OnSpecialFrameChanged(frameIndex);
				return;
			}

			FrameIndexChangedDelegate handler = FrameChanged;
			if (handler != null) handler(this, frameIndex);
		}

		public void OnActionChanged(int actionindex) {
			if (!_handlersEnabled) return;
			FrameIndexChangedDelegate handler = ActionChanged;
			if (handler != null) handler(this, actionindex);
		}

		private void _cbSound_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (!_eventsEnabled) return;

			if (_cbSound.SelectedIndex == _cbSound.Items.Count - 1) {
				InputDialog dialog = new InputDialog("New sound file name", "New sound", "atk", false, false);
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					if (dialog.Input == "") return;

					_actEditor.Act.Commands.InsertSoundId(dialog.Input, _actEditor.Act.SoundFiles.Count);

					DisableEvents();
					_reloadSound();
					EnableEvents();
					_cbSound.SelectedIndex = _actEditor.Act.SoundFiles.Count;
				}
				else {
					_cbSound.SelectedIndex = _actEditor.Act[SelectedAction, SelectedFrame].SoundId + 1;
				}
			}
			else {
				_actEditor.Act.Commands.SetSoundId(_actEditor.SelectedAction, _actEditor.SelectedFrame, _cbSound.SelectedIndex - 1);
			}
		}

		private void _play_Click(object sender, RoutedEventArgs e) {
			if (IsPlaying)
				Stop();
			else
				Play();
		}

		private void _playAnimation() {
			Act act = _actEditor.Act;

			if (act == null) {
				_play_Click(null, null);
				return;
			}

			if (act[SelectedAction].NumberOfFrames <= 1) {
				_play_Click(null, null);
				return;
			}

			if (act[SelectedAction].AnimationSpeed < ActIndexSelector.MaxAnimationSpeed) {
				_play_Click(null, null);
				ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
				return;
			}

			Stopwatch watch = new Stopwatch();
			int startFrame = SelectedFrame;
			int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;
			int oldInterval = Int32.MinValue;
			long idx = startFrame;

			try {
				OnAnimationPlaying(2);

				while (IsPlaying) {
					var interval = (int)(act[SelectedAction].AnimationSpeed * frameInterval);

					if (oldInterval != interval) {
						oldInterval = interval;
						watch.Restart();
						idx = startFrame = SelectedFrame;
					}

					if (act[SelectedAction].AnimationSpeed < ActIndexSelector.MaxAnimationSpeed) {
						_play_Click(null, null);
						ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
						return;
					}

					SelectedFrame++;
					PlaySound();

					if (!IsPlaying)
						return;

					long expectedNextFrame = (idx + 1 - startFrame) * interval - watch.ElapsedMilliseconds;
					idx++;

					Thread.Sleep((int)Math.Max(20, Math.Min(interval, expectedNextFrame)));
				}
			}
			finally {
				IsPlaying = false;
				_frameChangedEventEnabled = true;
				OnAnimationPlaying(0);

				_sbFrameIndex.Dispatch(p => p.IsEnabled = true);
			}
		}

		public void PlaySound() {
			if (ActEditorConfiguration.ActEditorPlaySound) {
				var act = _actEditor.Act;

				int soundId = act[SelectedAction, SelectedFrame].SoundId;

				if (soundId > -1 && soundId < act.SoundFiles.Count) {
					string soundFile = act.SoundFiles[soundId];

					if (soundFile.GetExtension() == null)
						soundFile = soundFile + ".wav";

					byte[] file = ActEditorWindow.Instance.MetaGrf.GetData("data\\wav\\" + soundFile);

					if (file != null) {
						try {
							_se.Play(file);
						}
						catch (Exception err) {
							_cbSoundEnable.Dispatch(p => p.OnClick(null));
							ErrorHandler.HandleException(err);
						}
					}
				}
			}
		}

		public void Init(IFrameRendererEditor actEditor, int actionIndex, int selectedAction) {
			_actEditor = actEditor;
			_actEditor.ActLoaded += new ActEditorWindow.ActEditorEventDelegate(_actEditor_ActLoaded);
			ActionChanged += _frameSelector_ActionChanged;
			_sbFrameIndex.ValueChanged += _sbFrameIndex_ValueChanged;
			_tbFrameIndex.TextChanged += _tbFrameIndex_TextChanged;

			_sbFrameIndex.SmallChange = 1;
			_sbFrameIndex.LargeChange = 1;
		}

		private void _tbFrameIndex_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			int ival;

			Int32.TryParse(_tbFrameIndex.Text, out ival);

			if (ival > _actEditor.Act[SelectedAction].NumberOfFrames || ival < 0) {
				ival = 0;
			}

			_sbFrameIndex.Value = ival;
		}

		private void _sbFrameIndex_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (!_eventsEnabled) return;
			if (_actEditor.Act == null) return;

			int value = (int) Math.Round(_sbFrameIndex.Value);

			SelectedFrame = value;
			_sbFrameIndex.Value = value;
		}

		private void _updateAction() {
			if (_actEditor.Act == null) return;
			if (SelectedAction >= _actEditor.Act.NumberOfActions) return;

			DisableEvents();

			if (_actEditor.SelectedFrame >= _actEditor.Act[_actEditor.SelectedAction].NumberOfFrames && _actEditor.SelectedFrame > 0) {
				((TabAct)_actEditor).SelectedFrame = Math.Max(0, _actEditor.Act[_actEditor.SelectedAction].NumberOfFrames - 1);
			}

			int selectedSoundId = _actEditor.Act[_actEditor.SelectedAction, _actEditor.SelectedFrame].SoundId;

			if (selectedSoundId >= _actEditor.Act.SoundFiles.Count) {
				selectedSoundId = -1;
			}

			_reloadSound();
			_cbSound.SelectedIndex = selectedSoundId + 1;
			EnableEvents();

			int max = _actEditor.Act[SelectedAction].NumberOfFrames - 1;
			max = max < 0 ? 0 : max;

			_sbFrameIndex.Minimum = 0;
			_sbFrameIndex.Maximum = max;

			_labelFrameIndex.Text = "/ " + max + " frame" + (max > 1 ? "s" : "");
		}

		private void _frameSelector_ActionChanged(object sender, int actionindex) {
			_updateAction();
			_sbFrameIndex.Value = 0;
		}

		public void DisableEvents() {
			if (_eventsEnabledCounter == 0) {
				_eventsEnabled = false;
			}

			_eventsEnabledCounter--;
		}

		public void EnableEvents() {
			_eventsEnabledCounter++;

			if (_eventsEnabledCounter == 0) {
				_eventsEnabled = true;
			}
		}

		private void _actEditor_ActLoaded(object sender) {
			_fancyButtons.ForEach(p => p.IsPressed = false);

			_comboBoxActionIndex.ItemsSource = null;
			_comboBoxActionIndex.Items.Clear();

			_comboBoxAnimationIndex.ItemsSource = null;
			_comboBoxAnimationIndex.Items.Clear();

			DisableEvents();
			_reloadSound();
			EnableEvents();

			int actions = _actEditor.Act.NumberOfActions;

			_comboBoxAnimationIndex.ItemsSource = ActHelper.GetAnimations(_actEditor.Act);
			_comboBoxActionIndex.ItemsSource = Enumerable.Range(0, actions);

			if (actions != 0) {
				_comboBoxActionIndex.SelectedIndex = 0;
			}

			_actEditor.Act.VisualInvalidated += s => Update();
			_actEditor.Act.RenderInvalidated += s => _actEditor.FrameRenderer.Update();
			_actEditor.Act.Commands.CommandIndexChanged += new AbstractCommand<IActCommand>.AbstractCommandsEventHandler(_commands_CommandUndo);
		}

		private List<string> _previousSoundFiles;

		private void _reloadSound() {
			if (_previousSoundFiles == null)
				_previousSoundFiles = new List<string>();
			else if (_previousSoundFiles.Count == _actEditor.Act.SoundFiles.Count) {
				bool sameSounds = true;

				for (int i = 0; i < _previousSoundFiles.Count; i++) {
					if (_previousSoundFiles[i] != _actEditor.Act.SoundFiles[i]) {
						sameSounds = false;
						break;
					}
				}

				if (sameSounds)
					return;
			}

			List<DummyStringView> items = new List<DummyStringView>();
			items.Add(new DummyStringView("None"));
			_actEditor.Act.SoundFiles.ForEach(p => items.Add(new DummyStringView(p)));
			items.Add(new DummyStringView("Add new..."));
			_cbSound.ItemsSource = items;
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

					if (SelectedAction >= _actEditor.Act.NumberOfActions)
						SelectedAction = _actEditor.Act.NumberOfActions - 1;

					_updateActionSelection(false, true);
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

				var backupCmd = _getCommand<BackupCommand>(command);
				var actBackupCmd = _getCommand<ActEditCommand>(command);

				if (backupCmd != null || actBackupCmd != null) {
					_updateActionSelection(true, false);
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

		private void _updateActionSelection(bool keepFrameIndex, bool update) {
			try {
				int selectedAction = SelectedAction;
				int frameIndex = SelectedFrame;

				var animations = ActHelper.GetAnimations(_actEditor.Act);
				var actions = Enumerable.Range(0, _actEditor.Act.NumberOfActions);

				bool animationsChanged = false;

				if (animations.Count != _comboBoxAnimationIndex.Items.Count) {
					animationsChanged = true;
				}
				else {
					for (int i = 0; i < animations.Count; i++) {
						if (animations[i] != (string)_comboBoxAnimationIndex.Items[i]) {
							animationsChanged = true;
							break;
						}
					}
				}

				if (animationsChanged) {
					_comboBoxAnimationIndex.ItemsSource = null;
					_comboBoxAnimationIndex.ItemsSource = animations;
				}

				if (actions.Count() != _comboBoxActionIndex.Items.Count) {
					_comboBoxActionIndex.ItemsSource = null;
					_comboBoxActionIndex.ItemsSource = actions;
				}

				if (selectedAction >= _comboBoxActionIndex.Items.Count) {
					_comboBoxActionIndex.SelectedIndex = _comboBoxActionIndex.Items.Count - 1;
				}

				if (_comboBoxActionIndex.SelectedIndex != selectedAction)
					_comboBoxActionIndex.SelectedIndex = selectedAction;

				if (_comboBoxAnimationIndex.SelectedIndex != selectedAction / 8)
					_comboBoxAnimationIndex.SelectedIndex = selectedAction / 8;

				if (keepFrameIndex) {
					_sbFrameIndex.Value = frameIndex;
				}

				if (update)
					Update();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _updateInfo() {
			Stop();
			_updateAction();
			_updateInterval();
		}

		public void Play() {
			if (IsPlaying) return;

			_play.Dispatch(delegate {
				_play.IsPressed = true;
				_sbFrameIndex.IsEnabled = false;
				IsPlaying = true;
				ActIndexSelectorHelper.UpdatePlayButtonUI(_play);
			});

			GrfThread.Start(_playAnimation);
		}

		public void Stop() {
			if (!IsPlaying) return;

			_play.Dispatch(delegate {
				_play.IsPressed = false;
				_sbFrameIndex.IsEnabled = true;
				IsPlaying = false;
				ActIndexSelectorHelper.UpdatePlayButtonUI(_play);
			});
		}

		public void RefreshIntervalDisplay() {
			_disableEvents();
			_updateInterval();
			_enableEvents();
		}

		private void _updateInterval() {
			int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;
			_interval.Text = (_actEditor.Act[SelectedAction].AnimationSpeed * frameInterval).ToString(CultureInfo.InvariantCulture);
		}

		private void _fancyButton_Click(object sender, RoutedEventArgs e) {
			int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

			var fb = (FancyButton)sender;

			_fancyButtons.ForEach(p => p.IsPressed = false);
			fb.IsPressed = true;

			_comboBoxActionIndex.SelectedIndex = animationIndex * 8 + _fancyButtons.IndexOf(fb);
		}

		private void _setDisabledButtons() {
			Dispatcher.Invoke(new Action(delegate {
				int baseAnimationIndex = _comboBoxActionIndex.SelectedIndex / 8 * 8;

				for (int i = 0; i < 8; i++) {
					_fancyButtons[i].IsButtonEnabled = baseAnimationIndex + i < _actEditor.Act.NumberOfActions;
				}
			}));
		}

		private void _comboBoxAnimationIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxAnimationIndex.SelectedIndex < 0) return;

			int direction = _comboBoxActionIndex.SelectedIndex % 8;

			if (8 * _comboBoxAnimationIndex.SelectedIndex + direction >= _actEditor.Act.NumberOfActions) {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex;
			}
			else {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex + direction;
			}
		}

		private void _comboBoxActionIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxActionIndex.SelectedIndex < 0) return;
			if (_comboBoxActionIndex.SelectedIndex >= _actEditor.Act.NumberOfActions) return;

			int actionIndex = _comboBoxActionIndex.SelectedIndex;
			int animationIndex = actionIndex / 8;
			_disableEvents();
			_comboBoxAnimationIndex.SelectedIndex = animationIndex;
			_fancyButton_Click(_fancyButtons[actionIndex % 8], null);
			_setDisabledButtons();
			SelectedAction = _comboBoxActionIndex.SelectedIndex;
			SelectedFrame = 0;
			_updateInterval();
			OnActionChanged(SelectedAction);
			_enableEvents();
		}

		public void Update() {
			try {
				int oldFrame = SelectedFrame;
				bool sameAction = SelectedAction == _comboBoxActionIndex.SelectedIndex;
				HashSet<int> selected = new HashSet<int>();

				bool differedUpdate = oldFrame != 0;

				if (differedUpdate) {
					_handlersEnabled = false;
				}

				if (sameAction) {
					selected = new HashSet<int>(_actEditor.SelectionEngine.SelectedItems);
				}

				_comboBoxActionIndex_SelectionChanged(null, null);

				if (_actEditor.Act == null) return;

				if (SelectedAction >= 0 && SelectedAction < _actEditor.Act.NumberOfActions) {
					if (oldFrame < _actEditor.Act[SelectedAction].NumberOfFrames) {
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

				if (sameAction) {
					_actEditor.SelectionEngine.SetSelection(selected);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _disableEvents() {
			_comboBoxAnimationIndex.SelectionChanged -= _comboBoxAnimationIndex_SelectionChanged;
			_interval.TextChanged -= _interval_TextChanged;
			_fancyButtons.ForEach(p => p.Click -= _fancyButton_Click);
		}

		private void _enableEvents() {
			_comboBoxAnimationIndex.SelectionChanged += _comboBoxAnimationIndex_SelectionChanged;
			_interval.TextChanged += _interval_TextChanged;
			_fancyButtons.ForEach(p => p.Click += _fancyButton_Click);
		}

		private void _interval_TextChanged(object sender, TextChangedEventArgs e) {
			if (_actEditor.Act == null) return;
			if (_actEditor.Act.Commands.IsLocked) return;

			float fval = FormatConverters.SingleConverterNoThrow(_interval.Text);

			if (fval > 0) {
				int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;
				_actEditor.Act.Commands.SetInterval(SelectedAction, fval / frameInterval);
			}
		}
	}
}