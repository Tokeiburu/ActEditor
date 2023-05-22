using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ActEditor.Core.WPF.GenericControls {
	public class QuickTextPreviewAdorner : Adorner {
		private readonly TextBlock _block;

		public QuickTextPreviewAdorner(TextBlock block, UIElement adornedElement) : base(adornedElement) {
			_block = block;
			AddVisualChild(_block);
		}

		protected override int VisualChildrenCount {
			get { return 1; }
		}

		protected override Visual GetVisualChild(int index) {
			if (index != 0)
				throw new ArgumentOutOfRangeException();
			return _block;
		}

		protected override Size ArrangeOverride(Size finalSize) {
			_block.Arrange(new Rect(new Point(0, 0), finalSize));
			return new Size(_block.ActualWidth, _block.ActualHeight);
		}
	}
}