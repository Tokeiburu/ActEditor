using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Image;
using GRF.Threading;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities.Commands;
using Utilities.Extension;
using Action = System.Action;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for FrameSelector.xaml
	/// </summary>
	public partial class ActIndexSelector : UserControl, IActIndexSelector {
		#region Delegates

		public delegate void FrameIndexChangedDelegate(object sender, int actionIndex);

		#endregion

		private readonly List<FancyButton> _fancyButtons;
		private readonly object _lock = new object();
		private readonly SoundEffect _se = new SoundEffect();
		private TabAct _actEditor;
		private bool _eventsEnabled = true;
		private bool _frameChangedEventEnabled = true;
		private bool _handlersEnabled = true;
		private int _pending;

		public ActIndexSelector() {
			InitializeComponent();

			try {
				_fancyButtons = new FancyButton[] { _fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7 }.ToList();

				BitmapSource image = ApplicationManager.PreloadResourceImage("arrow.png");
				BitmapSource image2 = ApplicationManager.PreloadResourceImage("arrowoblique.png");

				_fancyButton0.ImageIcon.Source = image;
				_fancyButton0.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton0.ImageIcon.RenderTransform = new RotateTransform { Angle = 90 };

				_fancyButton1.ImageIcon.Source = image2;
				_fancyButton1.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton1.ImageIcon.RenderTransform = new RotateTransform { Angle = 90 };

				_fancyButton2.ImageIcon.Source = image;
				_fancyButton2.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton2.ImageIcon.RenderTransform = new RotateTransform { Angle = 180 };

				_fancyButton3.ImageIcon.Source = image2;
				_fancyButton3.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton3.ImageIcon.RenderTransform = new RotateTransform { Angle = 180 };

				_fancyButton4.ImageIcon.Source = image;
				_fancyButton4.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton4.ImageIcon.RenderTransform = new RotateTransform { Angle = 270 };

				_fancyButton5.ImageIcon.Source = image2;
				_fancyButton5.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton5.ImageIcon.RenderTransform = new RotateTransform { Angle = 270 };

				_fancyButton6.ImageIcon.Source = image;
				_fancyButton6.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton6.ImageIcon.RenderTransform = new RotateTransform { Angle = 360 };

				_fancyButton7.ImageIcon.Source = image2;
				_fancyButton7.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton7.ImageIcon.RenderTransform = new RotateTransform { Angle = 360 };

				_fancyButtons.ForEach(p => p.IsButtonEnabled = false);

				_sbFrameIndex.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(_sbFrameIndex_MouseLeftButtonDown);
				_sbFrameIndex.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(_sbFrameIndex_MouseLeftButtonUp);
			}
			catch {
			}

			try {
				_updatePlay();
				_cbSound.SelectionChanged += _cbSound_SelectionChanged;
				_play.Click += new RoutedEventHandler(_play_Click);

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

				((TextBlock) _buttonRenderMode.FindName("_tbIdentifier")).Margin = new Thickness(3, 0, 0, 3);
				((Grid) ((Grid) ((Border) _buttonRenderMode.FindName("_border")).Child).Children[2]).HorizontalAlignment = HorizontalAlignment.Left;
				((Grid) ((Grid) ((Border) _buttonRenderMode.FindName("_border")).Child).Children[2]).Margin = new Thickness(2, 0, 0, 0);

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
					_actEditor._rendererPrimary.UpdateAndSelect();
				};

				action2();

				WpfUtilities.AddFocus(_tbFrameIndex, _interval);
			}
			catch {
			}
		}

		public int SelectedAction { get; set; }
		public int SelectedFrame { get; set; }

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

					_eventsEnabled = false;
					_reloadSound();
					_eventsEnabled = true;
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

		private void _sbFrameIndex_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			lock (_lock) {
				_pending++;
			}

			OnAnimationPlaying(0);

			GrfThread.Start(delegate {
				int max = 20;

				while (max-- > 0) {
					if (e.LeftButton == MouseButtonState.Pressed)
						return;

					Thread.Sleep(100);
				}

				// Resets the mouse operations to 0
				lock (_lock) {
					_pending = 0;
				}
			});
		}

		public void SetAction(int index) {
			if (index < _comboBoxActionIndex.Items.Count && index > -1) {
				_comboBoxActionIndex.SelectedIndex = index;
			}
		}

		public void SetFrame(int index) {
			_sbFrameIndex.Value = index;
		}

		private void _sbFrameIndex_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			if (_actEditor.Act == null) {
				lock (_lock) {
					_pending--;
				}
				return;
			}

			Point position = e.GetPosition(_sbFrameIndex);

			bool isLeft = position.X > 0 && position.Y > 0 && position.Y < _sbFrameIndex.ActualHeight && position.X < SystemParameters.HorizontalScrollBarButtonWidth;
			bool isRight = position.X > (_sbFrameIndex.ActualWidth - SystemParameters.HorizontalScrollBarButtonWidth) && position.Y > 0 && position.Y < _sbFrameIndex.ActualHeight && position.X < _sbFrameIndex.ActualWidth;
			bool isWithin = position.X > 0 && position.Y > 0 && position.X < _sbFrameIndex.ActualWidth && position.Y < _sbFrameIndex.ActualHeight;

			if (isWithin) {
				OnAnimationPlaying(2);
			}

			if (!isLeft && !isRight) {
				lock (_lock) {
					_pending--;
				}
				return;
			}

			GrfThread.Start(delegate {
				int count = 0;
				while (this.Dispatch(() => Mouse.LeftButton) == MouseButtonState.Pressed) {
					_sbFrameIndex.Dispatch(delegate {
						position = e.GetPosition(_sbFrameIndex);

						isLeft = position.X > 0 && position.Y > 0 && position.Y < _sbFrameIndex.ActualHeight && position.X < SystemParameters.HorizontalScrollBarButtonWidth;
						isRight = position.X > (_sbFrameIndex.ActualWidth - SystemParameters.HorizontalScrollBarButtonWidth) && position.Y > 0 && position.Y < _sbFrameIndex.ActualHeight && position.X < _sbFrameIndex.ActualWidth;
					});

					if (isLeft) {
						SelectedFrame--;
						if (SelectedFrame < 0)
							SelectedFrame = _actEditor.Act[SelectedAction].NumberOfFrames - 1;
					}

					if (isRight) {
						SelectedFrame++;
						if (SelectedFrame >= _actEditor.Act[SelectedAction].NumberOfFrames)
							SelectedFrame = 0;
					}

					_sbFrameIndex.Dispatch(p => p.Value = SelectedFrame);

					Thread.Sleep(count == 0 ? 400 : 50);

					lock (_lock) {
						if (_pending > 0) {
							_pending--;
							return;
						}
					}

					count++;
				}
			});

			e.Handled = true;
		}

		private void _play_Click(object sender, RoutedEventArgs e) {
			_play.Dispatch(delegate {
				_play.IsPressed = !_play.IsPressed;
				_sbFrameIndex.IsEnabled = !_play.IsPressed;
				_updatePlay();
			});

			if (_play.Dispatch(() => _play.IsPressed)) {
				GrfThread.Start(_playAnimation);
			}
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

			if (act[SelectedAction].AnimationSpeed < 0.8f) {
				_play_Click(null, null);
				ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
				return;
			}

			Stopwatch watch = new Stopwatch();
			SelectedFrame--;

			int interval = (int) (act[SelectedAction].AnimationSpeed * 25f);

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
				this.Dispatch(p => p._actEditor._layerEditor.IsHitTestVisible = false);
				OnAnimationPlaying(2);
				IsPlaying = true;

				while (_play.Dispatch(p => p.IsPressed)) {
					watch.Reset();
					watch.Start();

					interval = (int) (act[SelectedAction].AnimationSpeed * 25f);

					if (act[SelectedAction].AnimationSpeed < 0.8f) {
						_play_Click(null, null);
						ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
						return;
					}

					SelectedFrame++;

					if (SelectedFrame >= act[SelectedAction].NumberOfFrames) {
						SelectedFrame = 0;
					}

					if (!_cbSoundEnable.Dispatch(p => p.IsPressed)) {
						int soundId = act[SelectedAction, SelectedFrame].SoundId;

						if (soundId > -1 && soundId < act.SoundFiles.Count) {
							string soundFile = act.SoundFiles[soundId];

							if (soundFile.GetExtension() == null)
								soundFile = soundFile + ".wav";

							byte[] file = _actEditor.ActEditor.MetaGrf.GetData("data\\wav\\" + soundFile);

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

					Thread.Sleep(interval);
				}
			}
			finally {
				IsPlaying = false;
				_frameChangedEventEnabled = true;
				this.Dispatch(p => p._actEditor._layerEditor.IsHitTestVisible = true);
				OnAnimationPlaying(0);

				_sbFrameIndex.Dispatch(p => p.IsEnabled = true);
			}
		}

		private void _updatePlay() {
			((TextBlock) _play.FindName("_tbIdentifier")).Margin = new Thickness(3, 0, 0, 3);
			((Grid) ((Grid) ((Border) _play.FindName("_border")).Child).Children[2]).HorizontalAlignment = HorizontalAlignment.Left;
			((Grid) ((Grid) ((Border) _play.FindName("_border")).Child).Children[2]).Margin = new Thickness(2, 0, 0, 0);

			if (_play.IsPressed) {
				_play.ImagePath = "stop2.png";
				_play.TextHeader = "Stop";
			}
			else {
				_play.ImagePath = "play.png";
				_play.TextHeader = "Play";
			}
		}

		public void Init(TabAct actEditor) {
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

			_eventsEnabled = false;
			_sbFrameIndex.Value = value;
			_tbFrameIndex.Text = value.ToString(CultureInfo.InvariantCulture);
			_cbSound.SelectedIndex = _actEditor.Act[_actEditor.SelectedAction, value].SoundId + 1;
			_eventsEnabled = true;
			SelectedFrame = value;
			OnFrameChanged(value);
		}

		private void _updateAction() {
			if (_actEditor.Act == null) return;
			if (SelectedAction >= _actEditor.Act.NumberOfActions) return;

			_eventsEnabled = false;

			while (_actEditor.SelectedFrame >= _actEditor.Act[_actEditor.SelectedAction].NumberOfFrames && _actEditor.SelectedFrame > 0) {
				_actEditor.SelectedFrame--;
			}

			int selectedSoundId = _actEditor.Act[_actEditor.SelectedAction, _actEditor.SelectedFrame].SoundId;

			if (selectedSoundId >= _actEditor.Act.SoundFiles.Count) {
				selectedSoundId = -1;
			}

			_reloadSound();
			_cbSound.SelectedIndex = selectedSoundId + 1;
			_eventsEnabled = true;

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

		private void _actEditor_ActLoaded(object sender) {
			_fancyButtons.ForEach(p => p.IsPressed = false);

			_comboBoxActionIndex.ItemsSource = null;
			_comboBoxActionIndex.Items.Clear();

			_comboBoxAnimationIndex.ItemsSource = null;
			_comboBoxAnimationIndex.Items.Clear();

			_eventsEnabled = false;
			_reloadSound();
			_eventsEnabled = true;

			int actions = _actEditor.Act.NumberOfActions;

			_comboBoxAnimationIndex.ItemsSource = ActionSelector.GetAnimations(_actEditor.Act);
			_comboBoxActionIndex.ItemsSource = Enumerable.Range(0, actions);

			if (actions != 0) {
				_comboBoxActionIndex.SelectedIndex = 0;
			}

			_actEditor.Act.VisualInvalidated += s => Update();
			_actEditor.Act.Commands.CommandIndexChanged += new AbstractCommand<IActCommand>.AbstractCommandsEventHandler(_commands_CommandUndo);
		}

		private void _reloadSound() {
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

				if (backupCmd != null) {
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

				_comboBoxAnimationIndex.ItemsSource = null;
				_comboBoxAnimationIndex.ItemsSource = ActionSelector.GetAnimations(_actEditor.Act);
				_comboBoxActionIndex.ItemsSource = null;
				_comboBoxActionIndex.ItemsSource = Enumerable.Range(0, _actEditor.Act.NumberOfActions);

				if (selectedAction >= _comboBoxActionIndex.Items.Count) {
					_comboBoxActionIndex.SelectedIndex = _comboBoxActionIndex.Items.Count - 1;
				}

				_comboBoxActionIndex.SelectedIndex = selectedAction;

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
			_play.IsPressed = false;
			_updatePlay();
			_updateAction();
			_updateInterval();
		}

		private void _updateInterval() {
			_interval.Text = (_actEditor.Act[SelectedAction].AnimationSpeed * 25f).ToString(CultureInfo.InvariantCulture);
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

				if ((animationIndex + 1) * 8 > _actEditor.Act.NumberOfActions) {
					_fancyButtons.ForEach(p => p.IsButtonEnabled = true);

					int toDisable = (animationIndex + 1) * 8 - _actEditor.Act.NumberOfActions;

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
			_fancyButton_Click(_fancyButtons.First(p => p.Tag.ToString() == (actionIndex % 8).ToString(CultureInfo.InvariantCulture)), null);
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
			_fancyButtons.ForEach(p => p.Click -= _fancyButton_Click);
		}

		private void _enableEvents() {
			_comboBoxAnimationIndex.SelectionChanged += _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click += _fancyButton_Click);
		}

		private void _interval_TextChanged(object sender, TextChangedEventArgs e) {
			if (_actEditor.Act == null) return;
			if (_actEditor.Act.Commands.IsLocked) return;

			float fval;

			if (float.TryParse(_interval.Text, out fval)) {
				if (fval > 0) {
					_actEditor.Act.Commands.SetInterval(SelectedAction, fval / 25f);
				}
			}
		}

		public void Reset() {
			_eventsEnabled = false;

			_fancyButtons.ForEach(p => p.IsPressed = false);
			_fancyButtons.ForEach(p => p.IsButtonEnabled = false);

			_comboBoxActionIndex.ItemsSource = null;
			_comboBoxActionIndex.Items.Clear();

			_comboBoxAnimationIndex.ItemsSource = null;
			_comboBoxAnimationIndex.Items.Clear();

			_cbSound.ItemsSource = null;
			_cbSound.Items.Clear();

			_sbFrameIndex.Value = 0;
			_tbFrameIndex.Text = "0";
			_interval.Text = "";
			_labelFrameIndex.Text = "/ 0 frame";
			_sbFrameIndex.Maximum = 0;

			_play.IsPressed = false;
			_updatePlay();

			_eventsEnabled = true;
		}
	}
}