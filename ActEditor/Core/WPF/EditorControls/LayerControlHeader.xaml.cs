using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for SubFrameControl.xaml
	/// </summary>
	public partial class LayerControlHeader : UserControl {
		public LayerControlHeader() {
			InitializeComponent();
		}

		/// <summary>
		/// Hides the ID and Sprite fields.
		/// </summary>
		public void HideIdAndSprite() {
			_grid.Columns -= 2;
			_grid.Children.Remove(_tbLayerId);
			_grid.Children.Remove(_tbSpriteId);
		}
	}
}