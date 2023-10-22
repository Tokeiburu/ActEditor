using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ActImaging;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for FrameSelector2.xaml
	/// </summary>
	public partial class FrameSelector : UserControl {
		public static readonly DependencyProperty ShowPreviewProperty = DependencyProperty.Register("ShowPreview", typeof (bool), typeof (FrameSelector));
		public static readonly DependencyProperty AllowLastIndexProperty = DependencyProperty.Register("AllowLastIndex", typeof (bool), typeof (FrameSelector));

		private readonly Line _linePreview = new Line();

		private Act _act;
		private int _actionIndex;

		public FrameSelector() {
			InitializeComponent();

			_sbFrameIndex.SmallChange = 1;
			_sbFrameIndex.LargeChange = 1;

			_sbFrameIndex.ValueChanged += new RoutedPropertyChangedEventHandler<double>(_sbFrameIndex_ValueChanged);

			Loaded += delegate {
				if (SelectedFrame < _sbFrameIndex.Maximum)
					_sbFrameIndex_ValueChanged(null, null);
			};
		}

		public int SelectedFrame {
			get { return (int) _sbFrameIndex.Value; }
			set {
				_sbFrameIndex.Value = value;

				if (value == Action.NumberOfFrames)
					_linePreview.SetValue(Grid.ColumnProperty, value + 1);
			}
		}

		public Action Action { get; set; }

		public bool ShowPreview {
			get { return (bool) GetValue(ShowPreviewProperty); }
			set { SetValue(ShowPreviewProperty, value); }
		}

		public bool AllowLastIndex {
			get { return (bool) GetValue(AllowLastIndexProperty); }
			set { SetValue(AllowLastIndexProperty, value); }
		}

		public event ActIndexSelector.FrameIndexChangedDelegate FrameChanged;

		public void OnFrameChanged(int actionindex) {
			ActIndexSelector.FrameIndexChangedDelegate handler = FrameChanged;
			if (handler != null) handler(this, actionindex);
		}

		private void _sbFrameIndex_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (Action == null) return;

			int value = (int) Math.Round(_sbFrameIndex.Value);
			SelectedFrame = value;

			if (SelectedFrame < Action.NumberOfFrames) {
				try {
					foreach (GrfImage image in _act.Sprite.Images) {
						if (image.GrfImageType == GrfImageType.Indexed8) {
							image.Palette[3] = 0;
						}
					}
					var source = Imaging.GenerateImage(_act, _actionIndex, SelectedFrame);
					_imagePreview.Margin = new Thickness(
						(int) (_scrollViewer.ActualWidth / 2 - (double) source.Dispatcher.Invoke(new Func<double>(() => source.Width)) / 2),
						(int) (_scrollViewer.ActualHeight / 2 - (double) source.Dispatcher.Invoke(new Func<double>(() => source.Height)) / 2),
						0, 0);

					_imagePreview.Source = source;
				}
				finally {
					foreach (GrfImage image in _act.Sprite.Images) {
						if (image.GrfImageType == GrfImageType.Indexed8) {
							image.Palette[3] = 255;
						}
					}
				}
			}
			else {
				_imagePreview.Source = null;
			}

			_linePreview.SetValue(Grid.ColumnProperty, value + 1);

			OnFrameChanged(value);
		}

		public void Set(Act act, int actionIndex) {
			_act = act;
			_actionIndex = actionIndex;
			Action = act[actionIndex];

			_sbFrameIndex.Maximum = Action.NumberOfFrames - 1 + (AllowLastIndex ? 1 : 0);

			_gridBlocks.ColumnDefinitions.Clear();
			_gridBlocks.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(SystemParameters.HorizontalScrollBarButtonWidth)});

			for (int i = 0; i < Action.NumberOfFrames; i++) {
				Rectangle rect = new Rectangle();
				double factor = ((i % 5)) / 10d + 0.5d;

				_gridBlocks.ColumnDefinitions.Add(new ColumnDefinition());

				rect.Fill = new SolidColorBrush(Color.FromArgb(255, 255, (byte) (factor * 255), 0));
				rect.VerticalAlignment = VerticalAlignment.Bottom;
				rect.Height = 16;
				rect.SetValue(Grid.ColumnProperty, i + 1);

				if (Action.NumberOfFrames > 1) {
					if (i == 0 || i == Action.NumberOfFrames - 1) {
						var last = _gridBlocks.ColumnDefinitions.Last();

						last.Width = new GridLength((166 - 2 * SystemParameters.HorizontalScrollBarButtonWidth) / Action.NumberOfFrames);
					}
				}

				_gridBlocks.Children.Add(rect);
			}

			_linePreview.Stretch = Stretch.Fill;
			_linePreview.Y2 = 1;
			_linePreview.HorizontalAlignment = HorizontalAlignment.Left;
			_linePreview.Stroke = Brushes.Blue;
			_linePreview.StrokeStartLineCap = PenLineCap.Square;
			_linePreview.StrokeEndLineCap = PenLineCap.Square;

			if (_gridBlocks.Children.Contains(_linePreview))
				_gridBlocks.Children.Remove(_linePreview);

			_gridBlocks.Children.Add(_linePreview);

			_linePreview.Visibility = ShowPreview ? Visibility.Visible : Visibility.Hidden;

			_gridBlocks.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(SystemParameters.HorizontalScrollBarButtonWidth)});
			_sbFrameIndex_ValueChanged(null, null);
		}
	}
}