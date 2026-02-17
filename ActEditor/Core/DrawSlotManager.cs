using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ActEditor.Core {
	public class DrawSlot {
		public Image Image = new Image();
		public Border Border = new Border();
		public bool InUse;
		public bool IsConfigured;
	}

	public class DrawSlotManager {
		private readonly Canvas _canvas;

		public DrawSlotManager(Canvas canvas) {
			_canvas = canvas;

			ActEditorConfiguration.ActEditorSpriteSelectionBorder.PropertyChanged += _onPropertyChanged;
			ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay.PropertyChanged += _onPropertyChanged;
		}

		public List<DrawSlot> DrawSlots = new List<DrawSlot>();

		public void Begin() {
			foreach (var drawSlot in DrawSlots) {
				drawSlot.InUse = false;
			}
		}

		public void Begin(int drawIndex) {
			var drawSlot = GetDrawSlot(drawIndex);
			drawSlot.InUse = false;
		}

		public void End() {
			foreach (var drawSlot in DrawSlots) {
				if (!drawSlot.InUse) {
					if (drawSlot.Image.Visibility != Visibility.Hidden)
						drawSlot.Image.Visibility = Visibility.Hidden;
					if (drawSlot.Border.Visibility != Visibility.Hidden)
						drawSlot.Border.Visibility = Visibility.Hidden;
				}
			}
		}

		public void End(int drawIndex) {
			var drawSlot = GetDrawSlot(drawIndex);
			
			if (!drawSlot.InUse) {
				if (drawSlot.Image.Visibility != Visibility.Hidden)
					drawSlot.Image.Visibility = Visibility.Hidden;
				if (drawSlot.Border.Visibility != Visibility.Hidden)
					drawSlot.Border.Visibility = Visibility.Hidden;
			}
		}

		public DrawSlot GetDrawSlot(int drawIndex) {
			if (drawIndex >= DrawSlots.Count) {
				var edgeMode = ActEditorConfiguration.UseAliasing ? EdgeMode.Aliased : EdgeMode.Unspecified;
				var borderBrush = BufferedBrushes.GetBrush(LayerDraw.SelectionBorderBrush);
				var backgroundBrush = BufferedBrushes.GetBrush(LayerDraw.SelectionOverlayBrush);

				while (drawIndex >= DrawSlots.Count) {
					DrawSlot drawSlot = new DrawSlot();
					DrawSlots.Add(drawSlot);
					drawSlot.Image.Visibility = Visibility.Hidden;
					drawSlot.Border.Visibility = Visibility.Hidden;

					drawSlot.Border.BorderBrush = borderBrush;
					drawSlot.Border.Background = backgroundBrush;
					drawSlot.Border.BorderThickness = new Thickness(1);
					//drawSlot.Border.Stroke = borderBrush;
					//drawSlot.Border.Fill = backgroundBrush;
					//drawSlot.Border.StrokeThickness = 1d;
					drawSlot.Border.SetValue(RenderOptions.EdgeModeProperty, edgeMode);
					drawSlot.Border.SnapsToDevicePixels = true;
					drawSlot.Border.IsHitTestVisible = false;

					drawSlot.Image.VerticalAlignment = VerticalAlignment.Top;
					drawSlot.Image.HorizontalAlignment = HorizontalAlignment.Left;
					drawSlot.Image.SnapsToDevicePixels = true;
					drawSlot.Image.SetValue(RenderOptions.BitmapScalingModeProperty, ActEditorConfiguration.ActEditorScalingMode);

					//var effect = new ColorMultiplyEffect();
					//drawSlot.Image.Effect = effect;

					_canvas.Children.Add(drawSlot.Image);
					_canvas.Children.Add(drawSlot.Border);
				}
			}

			{
				var drawSlot = DrawSlots[drawIndex];
				drawSlot.InUse = true;
				//if (drawSlot.Image.Visibility != Visibility.Visible)
				//	drawSlot.Image.Visibility = Visibility.Visible;
				//if (drawSlot.Border.Visibility != Visibility.Hidden)
				//	drawSlot.Border.Visibility = Visibility.Hidden;
				Panel.SetZIndex(drawSlot.Image, 4 * drawIndex + 10);
				Panel.SetZIndex(drawSlot.Border, 4 * drawIndex + 12);
				return drawSlot;
			}
		}

		//public void SetUnused(DrawSlot drawSlot) {
		//	drawSlot.InUse = false;
		//	drawSlot.Image.Visibility = Visibility.Hidden;
		//	//drawSlot.Image.Source = null;
		//	drawSlot.Border.Visibility = Visibility.Hidden;
		//}
		//
		//public void SetUnused(int index) {
		//	var drawSlot = DrawSlots[index];
		//	drawSlot.InUse = false;
		//	drawSlot.Image.Visibility = Visibility.Hidden;
		//	//drawSlot.Image.Source = null;
		//	drawSlot.Border.Visibility = Visibility.Hidden;
		//}

		public void ImagesDirty() {
			var scalingMode = ActEditorConfiguration.ActEditorScalingMode;

			foreach (var drawSlot in DrawSlots) {
				drawSlot.Image.SetValue(RenderOptions.BitmapScalingModeProperty, scalingMode);
			}
		}

		public void BordersDirty() {
			// Border
			var edgeMode = ActEditorConfiguration.UseAliasing ? EdgeMode.Aliased : EdgeMode.Unspecified;
			var borderBrush = BufferedBrushes.GetBrush(LayerDraw.SelectionBorderBrush);
			var backgroundBrush = BufferedBrushes.GetBrush(LayerDraw.SelectionOverlayBrush);

			foreach (var drawSlot in DrawSlots) {
				drawSlot.Border.BorderBrush = borderBrush;
				drawSlot.Border.Background = backgroundBrush;
				//drawSlot.Border.Stroke = borderBrush;
				//drawSlot.Border.Fill = backgroundBrush;
				drawSlot.Border.SetValue(RenderOptions.EdgeModeProperty, edgeMode);
			}
		}

		private void _onPropertyChanged() {
			BordersDirty();
		}

		public void Unload() {
			DrawSlots.Clear();
			_canvas.Children.Clear();

			ActEditorConfiguration.ActEditorSpriteSelectionBorder.PropertyChanged -= _onPropertyChanged;
			ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay.PropertyChanged -= _onPropertyChanged;
		}
	}
}
