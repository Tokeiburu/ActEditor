using System.Windows;
using System.Windows.Controls;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for SubFrameControl.xaml
	/// </summary>
	public partial class LayerControlHeader : UserControl {
		public LayerControlHeader() {
			InitializeComponent();

			SizeChanged += delegate {
				for (int i = 0; i < _grid.ColumnDefinitions.Count; i++) {
					double width = _grid.ColumnDefinitions[i].ActualWidth;
					_grid.ColumnDefinitions[i].MaxWidth = width;
					_grid.ColumnDefinitions[i].MinWidth = width;
				}
			};
		}

		public Grid Grid {
			get { return _grid; }
		}

		/// <summary>
		/// Hides the ID and Sprite fields.
		/// </summary>
		public void HideIdAndSprite() {
			_grid.ColumnDefinitions[0].MinWidth = 0;
			_grid.ColumnDefinitions[0].Width = new GridLength(0);

			_grid.ColumnDefinitions[1].MinWidth = 0;
			_grid.ColumnDefinitions[1].Width = new GridLength(0);

			_lab0.Visibility = Visibility.Hidden;
			_lab1.Visibility = Visibility.Hidden;
		}
	}
}