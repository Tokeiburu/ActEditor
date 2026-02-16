using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActImaging;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Action = System.Action;

namespace ActEditor.Tools.GrfShellExplorer.PreviewTabs {
	/// <summary>
	/// Interaction logic for PreviewAct.xaml
	/// Class imported from GrfEditor
	/// </summary>
	public partial class PreviewAct : FilePreviewTab, IDisposable {
		private readonly ManualResetEvent _actThreadHandle = new ManualResetEvent(false);
		private readonly List<FancyButton> _fancyButtons;
		private readonly object _lockAnimation = new object();
		private readonly Stopwatch _watch = new Stopwatch();
		private Act _act;
		private int _actThreadSleepDelay = 100;
		private int _actionIndex = -1;
		private bool _changedAnimationIndex;
		private int _frameIndex;
		private bool _isRunning = true;
		private bool _stopAnimation;
		private bool _threadIsEnabled = true;

		public PreviewAct() {
			InitializeComponent();

			_imagePreview.Dispatch(p => p.SetValue(RenderOptions.BitmapScalingModeProperty, Configuration.BestAvailableScaleMode));
			_imagePreview.Dispatch(p => p.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased));

			try {
				_fancyButtons = new FancyButton[] {_fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7}.ToList();

				BitmapSource image = ApplicationManager.PreloadResourceImage("arrow.png");
				BitmapSource image2 = ApplicationManager.PreloadResourceImage("arrowoblique.png");

				_fancyButtons.ForEach(p => {
					p.ImageIcon.Stretch = Stretch.None;
					p.ImageIcon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
				});

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

				IsVisibleChanged += (e, a) => _enableActThread = IsVisible;

				Dispatcher.ShutdownStarted += delegate {
					_isRunning = false;
					_enableActThread = true;
				};

				new Thread(_actAnimationThread) {Name = "GrfEditor - Sprite animation update thread"}.Start();
			}
			catch {
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

		public ScrollViewer ScrollViewer {
			get { return _scrollViewer; }
		}

		private void _actAnimationThread() {
			while (true) {
				if (!_isRunning)
					return;

				_watch.Reset();
				_watch.Start();

				lock (_lockAnimation) {
					if (!_stopAnimation)
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

		protected override void _load(FileEntry entry) {
			byte[] dataDecompress = entry.GetDecompressedData();
			byte[] dataDecompressSpr;

			string actRelativePath = entry.RelativePath;

			_labelHeader.Dispatch(p => p.Content = "Animation: " + Path.GetFileName(actRelativePath));

			try {
				try {
					dataDecompressSpr = _grfData.FileTable[actRelativePath.Replace(".act", ".spr")].GetDecompressedData();
				}
				catch {
					List<string> possibleNodeNames = _grfData.FileTable.Files.Where(p => p.StartsWith(actRelativePath.Replace(Path.GetExtension(actRelativePath), ""))).ToList();
					possibleNodeNames.Remove(actRelativePath);
					if (possibleNodeNames.Count == 1) {
						dataDecompressSpr = _grfData.FileTable[possibleNodeNames[0]].GetDecompressedData();
					}
					else {
						throw;
					}
				}
			}
			catch {
				ErrorHandler.HandleException("Couldn't find the corresponding spr file: \n" + actRelativePath.Replace(".act", ".spr"), ErrorLevel.Low);
				return;
			}

			if (_isCancelRequired()) return;

			Act act = new Act(dataDecompress, dataDecompressSpr);

			if (_isCancelRequired()) return;

			List<int> actions = new List<int>();
			for (int i = 0; i < act.NumberOfActions; i++) {
				int i1 = i;
				actions.Add(i1);
			}

			lock (_lockAnimation) {
				_stopAnimation = true;
			}

			lock (_lockAnimation) {
				_act = act;
				_changedAnimationIndex = true;
				_stopAnimation = false;
			}

			_comboBoxActionIndex.Dispatcher.Invoke((Action) (() => _comboBoxActionIndex.ItemsSource = actions));
			_comboBoxAnimationIndex.Dispatcher.Invoke((Action) (() => _comboBoxAnimationIndex.ItemsSource = act.GetAnimations()));
			_setDisabledButtons();

			if (_isCancelRequired()) return;

			_imagePreview.Dispatch(p => p.VerticalAlignment = VerticalAlignment.Top);
			_imagePreview.Dispatch(p => p.HorizontalAlignment = HorizontalAlignment.Left);
			_comboBoxActionIndex.Dispatch(p => p.SelectedIndex = 0);
			_comboBoxActionIndex.Dispatch(p => p.Visibility = Visibility.Visible);
			_imagePreview.Dispatch(p => p.Visibility = Visibility.Visible);
			_scrollViewer.Dispatch(p => p.Visibility = Visibility.Visible);

			int actionIndex = (int) _comboBoxActionIndex.Dispatcher.Invoke(new Func<int>(() => _comboBoxActionIndex.SelectedIndex));

			if (actionIndex < 0)
				return;

			int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;

			if ((int)_act[actionIndex].AnimationSpeed * frameInterval == 0 ||
			    float.IsNaN(_act[actionIndex].AnimationSpeed)) {
				if (_act[actionIndex].Frames[0].Layers[0].SpriteIndex < 0) {
					_imagePreview.Dispatch(p => p.Source = null);
					return;
				}

				_imagePreview.Dispatch(p => p.Source = _act.Sprite.Images[_act[actionIndex].Frames[0].Layers[0].SpriteIndex].Cast<BitmapSource>());
			}
			else {
				_actThreadSleepDelay = (int)(_act[actionIndex].AnimationSpeed * frameInterval);
			}

			_enableActThread = true;
		}

		private void _displayNextFrame() {
			try {
				if (_actionIndex < 0) {
					_enableActThread = false;
					return;
				}

				_frameIndex++;
				_frameIndex = _frameIndex >= _act[_actionIndex].NumberOfFrames ? 0 : _frameIndex;

				if (_act[_actionIndex].Frames[_frameIndex % _act[_actionIndex].NumberOfFrames].NumberOfLayers <= 0) {
					_imagePreview.Dispatch(p => p.Source = null);
					return;
				}

				List<Layer> layers = _act[_actionIndex].Frames[_frameIndex % _act[_actionIndex].NumberOfFrames].Layers.Where(p => p.SpriteIndex >= 0).ToList();

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

								ImageSource source = Imaging.GenerateImage(_act, _actionIndex, _frameIndex);
								_imagePreview.Margin = new Thickness(
									(int) (10 + _scrollViewer.ActualWidth / 2 - (double) source.Dispatcher.Invoke(new Func<double>(() => source.Width)) / 2),
									(int) (10 + _scrollViewer.ActualHeight / 2 - (double) source.Dispatcher.Invoke(new Func<double>(() => source.Height)) / 2),
									0, 0);
								_imagePreview.Source = source;
							}
							catch {
								_enableActThread = false;
								ErrorHandler.HandleException("Unable to load the animation.");
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
			catch (Exception err) {
				_enableActThread = false;
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				if (_stopAnimation) {
					_enableActThread = false;
				}
			}
		}

		private void _comboBoxActionIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				if (_isCancelRequired()) return;
				if (_comboBoxActionIndex.SelectedIndex < 0) return;

				if (!_stopAnimation) {
					int actionIndex = _comboBoxActionIndex.SelectedIndex;
					int animationIndex = actionIndex / 8;
					_disableEvents();
					_comboBoxAnimationIndex.SelectedIndex = animationIndex;
					_fancyButton_Click(_fancyButtons.First(p => p.Tag.ToString() == (actionIndex % 8).ToString(CultureInfo.InvariantCulture)), null);
					_setDisabledButtons();
					_enableEvents();

					if (actionIndex < 0)
						return;

					int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;

					if ((int)_act[actionIndex].AnimationSpeed * frameInterval == 0 ||
					    float.IsNaN(_act[actionIndex].AnimationSpeed)) {
						if (_act[actionIndex].Frames[0].Layers[0].SpriteIndex < 0) {
							_imagePreview.Source = null;
							return;
						}

						_imagePreview.Source = _act.Sprite.Images[_act[actionIndex].Frames[0].Layers[0].SpriteIndex].Cast<BitmapSource>();
					}
					else {
						_actThreadSleepDelay = (int)(_act[actionIndex].AnimationSpeed * frameInterval);
					}

					_actionIndex = actionIndex;
					_changedAnimationIndex = true;
					_enableActThread = true;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
		}

		private void _fancyButton_Click(object sender, RoutedEventArgs e) {
			int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

			_fancyButtons.ForEach(p => p.IsPressed = false);
			((FancyButton) sender).IsPressed = true;

			_comboBoxActionIndex.SelectedIndex = animationIndex * 8 + Int32.Parse(((FancyButton) sender).Tag.ToString());
		}

		private void _disableEvents() {
			_comboBoxAnimationIndex.SelectionChanged -= _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click -= _fancyButton_Click);
		}

		private void _enableEvents() {
			_comboBoxAnimationIndex.SelectionChanged += _comboBoxAnimationIndex_SelectionChanged;
			_fancyButtons.ForEach(p => p.Click += _fancyButton_Click);
		}

		private void _comboBoxAnimationIndex_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_comboBoxAnimationIndex.SelectedIndex < 0) return;

			int direction = _comboBoxActionIndex.SelectedIndex % 8;

			if (8 * _comboBoxAnimationIndex.SelectedIndex + direction >= _act.NumberOfActions) {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex;
			}
			else {
				_comboBoxActionIndex.SelectedIndex = 8 * _comboBoxAnimationIndex.SelectedIndex + direction;
			}
		}

		private void _setDisabledButtons() {
			Dispatcher.Invoke(new Action(delegate {
				int animationIndex = _comboBoxActionIndex.SelectedIndex / 8;

				if ((animationIndex + 1) * 8 > _act.NumberOfActions) {
					int toDisable = (animationIndex + 1) * 8 - _act.NumberOfActions;

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

		#region IDisposable members

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~PreviewAct() {
			Dispose(false);
		}

		protected void Dispose(bool disposing) {
			if (disposing) {
				if (_actThreadHandle != null) {
					_actThreadHandle.Close();
				}
			}
		}

		#endregion
	}
}