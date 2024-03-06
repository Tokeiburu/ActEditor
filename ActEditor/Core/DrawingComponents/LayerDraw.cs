using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Drawing component for a frame's layer.
	/// </summary>
	public class LayerDraw : DrawingComponent {
		public const string SelectionBorderBrush = "SelectionBorderBrush";
		public const string SelectionOverlayBrush = "SelectionOverlayBrush";

		private static readonly Thickness _bufferedThickness = new Thickness(1);
		private readonly IFrameRendererEditor _editor;

		private readonly TransformGroup _borderTransformGroup = new TransformGroup();
		private readonly RotateTransform _rotate = new RotateTransform();
		private readonly ScaleTransform _scale = new ScaleTransform();
		private readonly TransformGroup _transformGroup = new TransformGroup();
		private readonly TranslateTransform _translateFrame = new TranslateTransform();
		private readonly TranslateTransform _translateToCenter = new TranslateTransform();

		private readonly IFrameRenderer _renderer;
		private Act _act;
		private Border _border;
		private Image _image;
		private Layer _layer;
		private Layer _layerCopy;
		private ScaleTransform _scalePreview = new ScaleTransform();
		private TranslateTransform _translatePreview = new TranslateTransform();

		static LayerDraw() {
			BufferedBrushes.Register(SelectionBorderBrush, () => ActEditorConfiguration.ActEditorSpriteSelectionBorder);
			BufferedBrushes.Register(SelectionOverlayBrush, () => ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay);
		}

		public LayerDraw() {
			_transformGroup.Children.Add(_translateToCenter);
			_transformGroup.Children.Add(_scale);
			_transformGroup.Children.Add(_rotate);
			_transformGroup.Children.Add(_translateFrame);
			_transformGroup.Children.Add(_scalePreview);
			_transformGroup.Children.Add(_translatePreview);

			_borderTransformGroup.Children.Add(_translateToCenter);
			_borderTransformGroup.Children.Add(_scale);
			_borderTransformGroup.Children.Add(_rotate);
			_borderTransformGroup.Children.Add(_translateFrame);
			_borderTransformGroup.Children.Add(_scalePreview);
			_borderTransformGroup.Children.Add(_translatePreview);
		}

		public LayerDraw(IFrameRendererEditor editor, Act act, int layerIndex) : this() {
			_editor = editor;
			_renderer = editor.FrameRenderer;
			_act = act;
			LayerIndex = layerIndex;
		}

		public LayerDraw(IFrameRenderer renderer, Act act, int layerIndex) : this() {
			_renderer = renderer;
			_act = act;
			LayerIndex = layerIndex;
		}

		public int LayerIndex { get; private set; }

		public Layer Layer {
			get { return _act.TryGetLayer(_editor.SelectedAction, _editor.SelectedFrame, LayerIndex); }
		}

		public override bool IsSelected {
			get { return base.IsSelected; }
			set {
				base.IsSelected = value;
				_border.Visibility = IsSelected ? Visibility.Visible : Visibility.Hidden;
			}
		}

		public override bool IsSelectable {
			get { return base.IsSelectable; }
			set {
				if (base.IsSelectable && base.IsSelectable == value ||
				    !base.IsSelectable && base.IsSelectable == value)
					return;

				base.IsSelectable = value;

				_initBorder();
			}
		}

		private bool _canInternalUpdate {
			get { return _editor != null && _editor.LayerEditor != null; }
		}

		private Brush _getBorderBrush() {
			return BufferedBrushes.GetBrush(SelectionBorderBrush);
		}

		private Brush _getBorderBackgroundBrush() {
			return BufferedBrushes.GetBrush(SelectionOverlayBrush);
		}

		public void Init(Act act, int layerIndex) {
			_act = act;
			LayerIndex = layerIndex;
		}

		public override void OnSelected(int index, bool isSelected) {
			base.OnSelected(LayerIndex, isSelected);
		}

		public void AsyncLayerControlUpdate() {
			_editor.LayerEditor.AsyncUpdateLayerControl(LayerIndex);
		}

		public override void Select() {
			_initBorder();

			if (!IsSelectable) {
				IsSelected = false;
				return;
			}

			IsSelected = true;
		}

		private void _initBorder() {
			if (_border == null) {
				_border = new Border();
				_border.BorderThickness = _bufferedThickness;
				_border.BorderBrush = _getBorderBrush();
				_border.Background = _getBorderBackgroundBrush();
				_border.SnapsToDevicePixels = true;
				_border.IsHitTestVisible = false;
				_border.Visibility = Visibility.Hidden;
			}

			if (!IsSelectable) {
				IsSelected = false;
				IsHitTestVisible = false;
				_border.IsHitTestVisible = false;

				if (_image != null) {
					_image.IsHitTestVisible = false;
				}
			}
		}

		private void _initImage() {
			if (_image == null) {
				_image = new Image();
				_image.SnapsToDevicePixels = true;

				if (!IsSelectable) {
					_image.IsHitTestVisible = false;
				}
				else {
					_image.PreviewMouseLeftButtonUp += _image_MouseLeftButtonUp;
				}
			}
		}

		private void _initDc(IFrameRenderer renderer) {
			if (!renderer.Canva.Children.Contains(_image))
				renderer.Canva.Children.Add(_image);

			if (!renderer.Canva.Children.Contains(_border))
				renderer.Canva.Children.Add(_border);
		}

		private bool _valideMouseOperation() {
			if (!IsSelectable) {
				IsSelected = false;
				return false;
			}

			return true;
		}

		private void _image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			if (!_valideMouseOperation()) return;

			IsSelected = !IsSelected;

			ReleaseMouseCapture();
			e.Handled = true;
		}

		public override void Render(IFrameRenderer renderer) {
			_initBorder();
			_initImage();
			_initDc(renderer);
			bool isCopied = false;

			Act act = _act ?? renderer.Act;

			int actionIndex = renderer.SelectedAction;
			int frameIndex = renderer.SelectedFrame;
			int? anchorFrameIndex = null;

			if (actionIndex >= act.NumberOfActions) return;
			if (act.Name == "Head" || act.Name == "Body") {
				bool handled = false;

				if (act[actionIndex].NumberOfFrames == 3 &&
				    (0 <= actionIndex && actionIndex < 8) ||
				    (16 <= actionIndex && actionIndex < 24)) {
					if (renderer.Act != null) {
						Act editorAct = renderer.Act;

						int group = editorAct[actionIndex].NumberOfFrames / 3;

						if (group != 0) {
							anchorFrameIndex = frameIndex;

							if (frameIndex < group) {
								frameIndex = 0;
								handled = true;
							}
							else if (frameIndex < 2 * group) {
								frameIndex = 1;
								handled = true;
							}
							else if (frameIndex < 3 * group) {
								frameIndex = 2;
								handled = true;
							}
							else {
								frameIndex = 2;
								handled = true;
							}
						}
					}
				}

				if (!handled) {
					if (frameIndex >= act[actionIndex].NumberOfFrames) {
						if (act[actionIndex].NumberOfFrames > 0)
							frameIndex = frameIndex % act[actionIndex].NumberOfFrames;
						else
							frameIndex = 0;
					}
				}
			}
			else {
				if (frameIndex >= act[actionIndex].NumberOfFrames) {
					if (act[actionIndex].NumberOfFrames > 0)
						frameIndex = frameIndex % act[actionIndex].NumberOfFrames;
					else
						frameIndex = 0;
				}
			}

			Frame frame = act[actionIndex, frameIndex];
			if (LayerIndex >= frame.NumberOfLayers) return;

			_layer = act[actionIndex, frameIndex, LayerIndex];

			if (_layer.SpriteIndex < 0) {
				_image.Source = null;
				return;
			}

			int index = _layer.IsBgra32() ? _layer.SpriteIndex + act.Sprite.NumberOfIndexed8Images : _layer.SpriteIndex;

			if (index < 0 || index >= act.Sprite.Images.Count) {
				_image.Source = null;
				return;
			}

			GrfImage img = act.Sprite.Images[index];

			if (img.GrfImageType == GrfImageType.Indexed8) {
				img = img.Copy();
				img.Palette[3] = 0;
				isCopied = true;
			}

			int diffX = 0;
			int diffY = 0;

			if (act.AnchoredTo != null && frame.Anchors.Count > 0) {
				Frame frameReference;

				if (anchorFrameIndex != null && act.Name != null && act.AnchoredTo.Name != null) {
					frameReference = act.AnchoredTo.TryGetFrame(actionIndex, frameIndex);

					if (frameReference == null) {
						frameReference = act.AnchoredTo.TryGetFrame(actionIndex, anchorFrameIndex.Value);
					}
				}
				else {
					frameReference = act.AnchoredTo.TryGetFrame(actionIndex, anchorFrameIndex ?? frameIndex);
				}

				if (frameReference != null && frameReference.Anchors.Count > 0) {
					diffX = frameReference.Anchors[0].OffsetX - frame.Anchors[0].OffsetX;
					diffY = frameReference.Anchors[0].OffsetY - frame.Anchors[0].OffsetY;

					if (act.AnchoredTo.AnchoredTo != null) {
						frameReference = act.AnchoredTo.AnchoredTo.TryGetFrame(actionIndex, anchorFrameIndex ?? frameIndex);

						if (frameReference != null && frameReference.Anchors.Count > 0) {
							diffX = frameReference.Anchors[0].OffsetX - frame.Anchors[0].OffsetX;
							diffY = frameReference.Anchors[0].OffsetY - frame.Anchors[0].OffsetY;
						}
					}
				}
			}
			
			int extraX = _layer.Mirror ? -(img.Width + 1) % 2 : 0;

			_translateToCenter.X = -((img.Width + 1) / 2) + extraX;
			_translateToCenter.Y = -((img.Height + 1) / 2);
			_translateFrame.X = _layer.OffsetX + diffX;
			_translateFrame.Y = _layer.OffsetY + diffY;

			_scale.ScaleX = _layer.ScaleX * (_layer.Mirror ? -1 : 1);
			_scale.ScaleY = _layer.ScaleY;

			_rotate.Angle = _layer.Rotation;

			_image.RenderTransform = _transformGroup;
			_image.SetValue(RenderOptions.BitmapScalingModeProperty, ActEditorConfiguration.ActEditorScalingMode);

			if (!isCopied) {
				img = img.Copy();
			}

			img.ApplyChannelColor(_layer.Color);
			_image.Source = img.Cast<BitmapSource>();
			_image.VerticalAlignment = VerticalAlignment.Top;
			_image.HorizontalAlignment = HorizontalAlignment.Left;

			_border.Width = img.Width;
			_border.Height = img.Height;
			_border.RenderTransform = _borderTransformGroup;

			QuickRender(renderer);
		}

		public override void QuickRender(IFrameRenderer renderer) {
			if (_scalePreview == null)
				_scalePreview = new ScaleTransform();
			
			_scalePreview.CenterX = renderer.CenterX;
			_scalePreview.CenterY = renderer.CenterY;

			_scalePreview.ScaleX = renderer.ZoomEngine.Scale;
			_scalePreview.ScaleY = renderer.ZoomEngine.Scale;

			if (_translatePreview == null)
				_translatePreview = new TranslateTransform();

			_translatePreview.X = renderer.CenterX * renderer.ZoomEngine.Scale;
			_translatePreview.Y = renderer.CenterY * renderer.ZoomEngine.Scale;

			if (_border != null) {
				_border.SetValue(RenderOptions.EdgeModeProperty, ActEditorConfiguration.UseAliasing ? EdgeMode.Aliased : EdgeMode.Unspecified);
				_border.BorderBrush = _getBorderBrush();
				_border.Background = _getBorderBackgroundBrush();

				if (_image.Source == null) {
					_border.BorderThickness = new Thickness(0);
					_border.Width = 0;
					_border.Height = 0;
				}
				else {
					double scaleX = Math.Abs(1d / (renderer.ZoomEngine.Scale * _scale.ScaleX));
					double scaleY = Math.Abs(1d / (renderer.ZoomEngine.Scale * _scale.ScaleY));

					if (double.IsInfinity(scaleX) || double.IsNaN(scaleX) ||
					    double.IsInfinity(scaleY) || double.IsNaN(scaleY)) {
						_border.Width = 0;
						_border.Height = 0;
					}
					else {
						_border.BorderThickness = new Thickness(scaleX, scaleY, scaleX, scaleY);
					}
				}
			}
		}

		public override void Remove(IFrameRenderer renderer) {
			if (_image != null)
				renderer.Canva.Children.Remove(_image);

			if (_border != null)
				renderer.Canva.Children.Remove(_border);
		}

		public void SaveInitialData() {
			_layerCopy = new Layer(_layer);
		}

		public bool IsMouseUnder(MouseEventArgs e) {
			_initImage();

			try {
				if (_scale.ScaleX == 0 || _scale.ScaleY == 0) return false;

				return ReferenceEquals(_image.InputHitTest(e.GetPosition(_image)), _image);
			}
			catch {
				return false;
			}
		}

		public bool IsMouseUnder(Point point) {
			_initImage();

			try {
				if (_scale.ScaleX == 0 || _scale.ScaleY == 0) return false;

				return ReferenceEquals(_image.InputHitTest(_image.PointFromScreen(point)), _image);
			}
			catch {
				return false;
			}
		}

		#region Scale

		public void PreviewScale(double scale) {
			if (_layerCopy == null) return;

			double scaleX;
			double scaleY;

			if (_layer.Width == 0) {
				scaleX = 0;
				scaleY = 0;
			}
			else {
				scaleX = _layerCopy.ScaleX * scale;
				scaleY = _layerCopy.ScaleY * scale;
			}

			_layer.ScaleX = (float) scaleX;
			_layer.ScaleY = (float) scaleY;

			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			Render(_renderer);
		}

		/// <summary>
		/// Previews the scale.
		/// </summary>
		/// <param name="diffVector">The diff vector.</param>
		/// <param name="deltaX">The delta X.</param>
		/// <param name="deltaY">The delta Y.</param>
		public void PreviewScale(TkVector2 diffVector, double deltaX, double deltaY) {
			if (_layerCopy == null) return;

			var revRad = 2 * Math.PI - _layer.RotationRadian;

			if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift) {
				bool multipleSelected = _editor != null && _editor.SelectionEngine.CurrentlySelected.Count > 1;

				if (multipleSelected) {
					double delX = deltaX * Math.Cos(_layer.RotationRadian) + deltaY * Math.Sin(_layer.RotationRadian);
					double delY = deltaX * Math.Sin(revRad) + deltaY * Math.Cos(revRad);

					deltaX = delX;
					deltaY = delY;
				}
				else {
					TkVector2 click = diffVector;
					TkVector2 dest = new TkVector2((float) (click.X + deltaX), (float) (click.Y + deltaY));

					click.RotateZ(_layer.Rotation);
					dest.RotateZ(_layer.Rotation);

					_layer.ScaleX = _layerCopy.ScaleX * (dest.X / click.X);
					_layer.ScaleY = _layerCopy.ScaleY * (dest.Y / click.Y);

					if (_canInternalUpdate)
						AsyncLayerControlUpdate();

					Render(_renderer);
					return;
				}
			}

			double diffX = deltaX * 2d / _renderer.ZoomEngine.Scale;
			double diffY = deltaY * 2d / _renderer.ZoomEngine.Scale;

			double scaleX;
			double scaleY;

			if (_layer.Width == 0 || _layer.Height == 0) {
				scaleX = 0;
				scaleY = 0;
			}
			else {
				// We have to add diffX pixels to the image, which is... a simple ratio
				if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
					double scale = Math.Max(_layer.Width, _layer.Height);
					scale = (scale + diffX) / scale;

					scaleX = _layerCopy.ScaleX * scale;
					scaleY = _layerCopy.ScaleY * scale;
				}
				else {
					scaleX = _layerCopy.ScaleX * (_layer.Width + diffX) / _layer.Width;
					scaleY = _layerCopy.ScaleY * (_layer.Height + diffY) / _layer.Height;
				}
			}

			_layer.ScaleX = (float) scaleX;
			_layer.ScaleY = (float) scaleY;

			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			Render(_renderer);
		}

		public void Scale() {
			if (_layerCopy == null) return;
			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			float scaleX = _layer.ScaleX;
			float scaleY = _layer.ScaleY;

			// Restore original settings
			_layer.ScaleX = _layerCopy.ScaleX;
			_layer.ScaleY = _layerCopy.ScaleY;

			_act.Commands.SetScale(_renderer.SelectedAction, _renderer.SelectedFrame, LayerIndex, scaleX, scaleY);
		}

		#endregion

		#region Rotate

		public void PreviewRotate(Point initialPoint, float deltaX, float deltaY) {
			if (_layerCopy == null) return;

			int[] selected = _editor == null ? new[] { LayerIndex } : _editor.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();

			if (selected.Length > 1 && selected[0] != LayerIndex) {
				var comp = _renderer.Components.OfType<ActDraw>().FirstOrDefault(p => p.Primary).Components[selected[0]];

				LayerDraw layerDraw = comp as LayerDraw;

				if (layerDraw != null) {
					int angle3 = layerDraw._layer.Rotation - layerDraw._layerCopy.Rotation;
					_layer.Rotation = _layerCopy.Rotation;
					_layer.Rotate(angle3);

					if (_canInternalUpdate)
						AsyncLayerControlUpdate();
					Render(_renderer);

					return;
				}
			}

			Point centerOfImage = new Point(_renderer.CenterX + _layer.OffsetX * _renderer.ZoomEngine.Scale, _renderer.CenterY + _layer.OffsetY * _renderer.ZoomEngine.Scale);
			TkVector2 pointReference = new TkVector2(1, 0);
			Point point1 = new Point(initialPoint.X - centerOfImage.X, initialPoint.Y - centerOfImage.Y);
			Point point2 = new Point(point1.X + deltaX, point1.Y + deltaY);

			double angle1 = TkVector2.CalculateAngle(new TkVector2(point1.X, point1.Y), pointReference);
			double angle2 = TkVector2.CalculateAngle(new TkVector2(point2.X, point2.Y), pointReference);

			if (point1.Y < 0) {
				angle1 = 2d * Math.PI - angle1;
			}

			if (point2.Y < 0) {
				angle2 = 2d * Math.PI - angle2;
			}

			int angle = (int) ((angle2 - angle1) * 360d / (2d * Math.PI));

			_layer.Rotation = _layerCopy.Rotation;
			_layer.Rotate(angle);

			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			Render(_renderer);
		}

		public void Rotate() {
			if (_layerCopy == null) return;
			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			int rotation = _layer.Rotation;
			_layer.Rotation = _layerCopy.Rotation;

			_act.Commands.SetRotation(_renderer.SelectedAction, _renderer.SelectedFrame, LayerIndex, rotation);
		}

		#endregion

		#region Translate

		public void PreviewTranslate(double deltaX, double deltaY) {
			if (_layerCopy == null) return;

			int diffX = (int) (deltaX / _renderer.ZoomEngine.Scale);
			int diffY = (int) (deltaY / _renderer.ZoomEngine.Scale);

			_layer.OffsetX = _layerCopy.OffsetX + diffX;
			_layer.OffsetY = _layerCopy.OffsetY + diffY;

			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			Render(_renderer);
		}

		public void PreviewTranslateRaw(int x, int y) {
			if (_layerCopy == null) return;

			_layer.OffsetX = _layer.OffsetX + x;
			_layer.OffsetY = _layer.OffsetY + y;

			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			Render(_renderer);
		}

		public Point GetTranslatePoint() {
			if (_layerCopy == null) return new Point(0, 0);

			return new Point(
				_layer.OffsetX - _layerCopy.OffsetX,
				_layer.OffsetY - _layerCopy.OffsetY);
		}

		public void TranslateRestore() {
			if (_layerCopy == null) return;
			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			// Restore original settings
			_layer.OffsetX = _layerCopy.OffsetX;
			_layer.OffsetY = _layerCopy.OffsetY;
		}

		public void Translate() {
			if (_layerCopy == null) return;
			if (_canInternalUpdate)
				AsyncLayerControlUpdate();

			int diffX = _layer.OffsetX - _layerCopy.OffsetX;
			int diffY = _layer.OffsetY - _layerCopy.OffsetY;

			// Restore original settings
			_layer.OffsetX = _layerCopy.OffsetX;
			_layer.OffsetY = _layerCopy.OffsetY;

			_act.Commands.Translate(_renderer.SelectedAction, _renderer.SelectedFrame, LayerIndex, diffX, diffY);
		}

		#endregion
	}
}