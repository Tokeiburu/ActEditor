using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActImaging;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Action = System.Action;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for ActionSelector.xaml
	/// </summary>
	public partial class ActionSelector : UserControl {
		public static readonly DependencyProperty ShowInsertBarProperty = DependencyProperty.Register("ShowInsertBar", typeof (bool), typeof (ActionSelector));
		private readonly ManualResetEvent _actThreadHandle = new ManualResetEvent(false);
		private readonly List<FancyButton> _fancyButtons;

		private readonly object _lockAnimation = new object();
		private readonly Stopwatch _watch = new Stopwatch();
		private int _actThreadSleepDelay = 100;
		private bool _changedAnimationIndex;
		private int _frameIndex;
		private bool _isRunning = true;
		private int _selectedAction;
		private bool _threadIsEnabled = true;

		public ActionSelector() {
			InitializeComponent();

			try {
				_fancyButtons = new FancyButton[] {_fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7}.ToList();
				BitmapSource image = ApplicationManager.PreloadResourceImage("arrow.png");
				BitmapSource image2 = ApplicationManager.PreloadResourceImage("arrowoblique.png");

				_fancyButton0.ImageIcon.Source = image;
				_fancyButton0.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton0.ImageIcon.RenderTransform = new RotateTransform {Angle = 90};

				_fancyButton1.ImageIcon.Source = image2;
				_fancyButton1.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton1.ImageIcon.RenderTransform = new RotateTransform {Angle = 90};

				_fancyButton2.ImageIcon.Source = image;
				_fancyButton2.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton2.ImageIcon.RenderTransform = new RotateTransform {Angle = 180};

				_fancyButton3.ImageIcon.Source = image2;
				_fancyButton3.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton3.ImageIcon.RenderTransform = new RotateTransform {Angle = 180};

				_fancyButton4.ImageIcon.Source = image;
				_fancyButton4.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton4.ImageIcon.RenderTransform = new RotateTransform {Angle = 270};

				_fancyButton5.ImageIcon.Source = image2;
				_fancyButton5.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton5.ImageIcon.RenderTransform = new RotateTransform {Angle = 270};

				_fancyButton6.ImageIcon.Source = image;
				_fancyButton6.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton6.ImageIcon.RenderTransform = new RotateTransform {Angle = 360};

				_fancyButton7.ImageIcon.Source = image2;
				_fancyButton7.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
				_fancyButton7.ImageIcon.RenderTransform = new RotateTransform {Angle = 360};

				_fancyButtons.ForEach(p => p.IsEnabled = false);

				new Thread(_actAnimationThread) { Name = "GrfEditor - Sprite animation update thread" }.Start();

				_scrollViewer.Visibility = Visibility.Collapsed;

				SizeChanged += delegate {
					if (ActualHeight > 0) {
						_scrollViewer.MaxHeight = ActualHeight;
						_scrollViewer.Visibility = Visibility.Visible;
					}
				};

				Loaded += delegate {
					Window window = WpfUtilities.FindParentControl<Window>(this);

					window.Closed += delegate {
						_isRunning = false;
						_enableActThread = true;
					};

					if (SelectedAction < _comboBoxActionIndex.Items.Count)
						_comboBoxActionIndex_SelectionChanged(null, null);
				};
			}
			catch {
			}
		}

		public bool ShowInsertBar {
			get { return (bool) GetValue(ShowInsertBarProperty); }
			set { SetValue(ShowInsertBarProperty, value); }
		}

		public int SelectedAction {
			get { return _selectedAction; }
			set {
				_comboBoxActionIndex.SelectedIndex = value;
				_selectedAction = _comboBoxActionIndex.SelectedIndex;
			}
		}

		private bool _enableActThread {
			set {
				if (value) {
					if (!_threadIsEnabled)
						_actThreadHandle.Set();
				}
				else {
					if (_threadIsEnabled) {
						_threadIsEnabled = false;
						_actThreadHandle.Reset();
					}
				}
			}
		}

		public Act Act { get; set; }

		public void Show(int index) {
			_line0.Visibility = Visibility.Hidden;
			_line1.Visibility = Visibility.Hidden;
			_line2.Visibility = Visibility.Hidden;
			_line3.Visibility = Visibility.Hidden;
			_line4.Visibility = Visibility.Hidden;
			_line5.Visibility = Visibility.Hidden;
			_line6.Visibility = Visibility.Hidden;
			_line7.Visibility = Visibility.Hidden;

			int baseIndex = index % 8;

			switch (baseIndex) {
				case 0:
					_line0.Visibility = Visibility.Visible;
					break;
				case 1:
					_line1.Visibility = Visibility.Visible;
					break;
				case 2:
					_line2.Visibility = Visibility.Visible;
					break;
				case 3:
					_line3.Visibility = Visibility.Visible;
					break;
				case 4:
					_line4.Visibility = Visibility.Visible;
					break;
				case 5:
					_line5.Visibility = Visibility.Visible;
					break;
				case 6:
					_line6.Visibility = Visibility.Visible;
					break;
				case 7:
					_line7.Visibility = Visibility.Visible;
					break;
			}
		}

		public event ActIndexSelector.FrameIndexChangedDelegate ActionChanged;

		public void OnActionChanged(int actionindex) {
			ActIndexSelector.FrameIndexChangedDelegate handler = ActionChanged;
			if (handler != null) handler(this, actionindex);
		}

		private void _actAnimationThread() {
			while (true) {
				if (!_isRunning)
					return;

				_watch.Reset();
				_watch.Start();

				lock (_lockAnimation) {
					_displayNextFrame();
				}

				_watch.Stop();

				int delay = (int) (_actThreadSleepDelay - _watch.ElapsedMilliseconds);
				delay = delay < 0 ? 0 : delay;

				Thread.Sleep(delay);

				if (!_threadIsEnabled) {
					_actThreadHandle.WaitOne();

					if (!_threadIsEnabled)
						_threadIsEnabled = true;
				}
			}
		}

		private void _displayNextFrame() {
			try {
				if (SelectedAction < 0) {
					_enableActThread = false;
					return;
				}

				if (Act == null) {
					_enableActThread = false;
					return;
				}

				_frameIndex++;
				_frameIndex = _frameIndex >= Act[SelectedAction].NumberOfFrames ? 0 : _frameIndex;

				if (Act[SelectedAction].Frames[_frameIndex % Act[SelectedAction].NumberOfFrames].NumberOfLayers <= 0) {
					_imagePreview.Dispatch(p => p.Source = null);
					return;
				}

				List<Layer> layers = Act[SelectedAction].Frames[_frameIndex % Act[SelectedAction].NumberOfFrames].Layers.Where(p => p.SpriteIndex >= 0).ToList();

				if (layers.Count <= 0) {
					_imagePreview.Dispatch(p => p.Source = null);
					return;
				}

				bool isValid = (bool) _imagePreview.Dispatcher.Invoke(new Func<bool>(delegate {
					try {
						Dispatcher.Invoke(new Action(delegate {
							try {
								if (_changedAnimationIndex) {
									_frameIndex = 0;
									_changedAnimationIndex = false;
								}

								foreach (GrfImage image in Act.Sprite.Images) {
									if (image.GrfImageType == GrfImageType.Indexed8) {
										image.Palette[3] = 0;
									}
								}

								ImageSource source = Imaging.GenerateImage(Act, SelectedAction, _frameIndex);
								_imagePreview.Margin = new Thickness(
									(int) (_scrollViewer.ActualWidth / 2 - (double) source.Dispatcher.Invoke(new Func<double>(() => source.Width)) / 2),
									(int) (_scrollViewer.ActualHeight / 2 - (double) source.Dispatcher.Invoke(new Func<double>(() => source.Height)) / 2),
									0, 0);
								_imagePreview.Source = source;
								//_scrollViewer.ScrollToVerticalOffset(_scrollViewer.ScrollableHeight / 2d);
								//_scrollViewer.ScrollToHorizontalOffset(_scrollViewer.ScrollableWidth / 2d);
							}
							catch {
								_enableActThread = false;
								//ErrorHandler.HandleException("Unable to load the animation.");
							}
							finally {
								foreach (GrfImage image in Act.Sprite.Images) {
									if (image.GrfImageType == GrfImageType.Indexed8) {
										image.Palette[3] = 255;
									}
								}
							}
						}));

						return true;
					}
					catch {
						return false;
					}
				}));

				if (!isValid)
					throw new Exception("Unable to load the animation.");
			}
			catch (Exception) {
				_enableActThread = false;
			}
		}

		public static RangeObservableCollection<string> GetAnimations(Act act) {
			return new RangeObservableCollection<string>(act.GetAnimationStrings());
		}

		public void SetAct(Act act) {
			Act = act;

			_fancyButtons.ForEach(p => p.IsPressed = false);

			_comboBoxActionIndex.ItemsSource = null;
			_comboBoxActionIndex.Items.Clear();

			_comboBoxAnimationIndex.ItemsSource = null;
			_comboBoxAnimationIndex.Items.Clear();

			int animations = (int) Math.Ceiling(Act.NumberOfActions / 8f);
			int actions = Act.NumberOfActions;

			_comboBoxAnimationIndex.ItemsSource = GetAnimations(Act);

			if (ShowInsertBar) {
				actions++;

				int newAnimationCount = (int) Math.Ceiling(actions / 8f);

				if (newAnimationCount > animations) {
					((RangeObservableCollection<string>) _comboBoxAnimationIndex.ItemsSource).Add("Append");
				}
			}

			for (int i = 0; i < actions; i++) {
				_comboBoxActionIndex.Items.Add(i);
			}

			if (actions != 0) {
				_comboBoxActionIndex.SelectedIndex = 0;
			}
		}

		private void _fancyButton_Click(object sender, RoutedEventArgs e) {
			int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

			_fancyButtons.ForEach(p => p.IsPressed = false);
			((FancyButton) sender).IsPressed = true;

			_comboBoxActionIndex.SelectedIndex = animationIndex * 8 + Int32.Parse(((FancyButton) sender).Tag.ToString());
		}

		private void _comboBoxAnimationIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxAnimationIndex.SelectedIndex < 0) return;

			int direction = _comboBoxActionIndex.SelectedIndex % 8;

			if (8 * _comboBoxAnimationIndex.SelectedIndex + direction >= Act.NumberOfActions) {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex;
			}
			else {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex + direction;
			}
		}

		private void _comboBoxActionIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxActionIndex.SelectedIndex < 0) return;

			int actionIndex = _comboBoxActionIndex.SelectedIndex;
			int animationIndex = actionIndex / 8;

			if (ShowInsertBar) {
				Show(actionIndex);
			}

			_disableEvents();
			_comboBoxAnimationIndex.SelectedIndex = animationIndex;
			_fancyButton_Click(_fancyButtons.First(p => p.Tag.ToString() == (actionIndex % 8).ToString(CultureInfo.InvariantCulture)), null);
			_setDisabledButtons();
			SelectedAction = _comboBoxActionIndex.SelectedIndex;

			if (ShowInsertBar && actionIndex == Act.NumberOfActions) {
				OnActionChanged(SelectedAction);
				_enableEvents();
				return;
			}

			if (actionIndex < 0 || actionIndex >= Act.NumberOfActions) {
				_enableEvents();
				return;
			}

			if ((int) Act[actionIndex].AnimationSpeed * 25 == 0 ||
			    float.IsNaN(Act[actionIndex].AnimationSpeed)) {
				if (Act[actionIndex].Frames[0].Layers[0].SpriteIndex < 0) {
					_imagePreview.Source = null;
					return;
				}

				_imagePreview.Source = Act.Sprite.Images[Act[actionIndex].Frames[0].Layers[0].SpriteIndex].Cast<BitmapSource>();
			}
			else {
				_actThreadSleepDelay = (int) (Act[actionIndex].AnimationSpeed * 25);
			}

			_changedAnimationIndex = true;
			_enableActThread = true;

			OnActionChanged(SelectedAction);
			_enableEvents();
		}

		private void _disableEvents() {
			_comboBoxAnimationIndex.SelectionChanged -= _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click -= _fancyButton_Click);
		}

		private void _enableEvents() {
			_comboBoxAnimationIndex.SelectionChanged += _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click += _fancyButton_Click);
		}

		private void _setDisabledButtons() {
			Dispatcher.Invoke(new Action(delegate {
				int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

				if ((animationIndex + 1) * 8 > Act.NumberOfActions) {
					_fancyButtons.ForEach(p => p.IsButtonEnabled = true);

					int toDisable = (animationIndex + 1) * 8 - Act.NumberOfActions;

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
	}
}