using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using GrfToWpfBridge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Utilities;

namespace ActEditor.Core.DrawingComponents {
	public class LayerTransform {
		public TranslateTransform TranslateToCenter = new TranslateTransform();
		public ScaleTransform Scale = new ScaleTransform();
		public RotateTransform Rotate = new RotateTransform();
		public TranslateTransform TranslateFrame = new TranslateTransform();

		public TransformGroup Group = new TransformGroup();

		public LayerTransform() {
			Group.Children.Add(TranslateToCenter);
			Group.Children.Add(Scale);
			Group.Children.Add(Rotate);
			Group.Children.Add(TranslateFrame);
		}
	}

	public class DrawnData {
		public LayerTransform Transform = new LayerTransform();
		public bool IsSelected { get; set; }
		private FrameRenderer _frameRenderer;

		public DrawnData(FrameRenderer frameRenderer) {
			_frameRenderer = frameRenderer;
		}

	}

	public class LayerDrawingVisual : DrawingVisual {
		public LayerDrawingVisual() {
			
		}

		public void SetEdgeMode(EdgeMode edgeMode) {
			this.VisualEdgeMode = edgeMode;
		}
	}

	public class Renderer : FrameworkElement {
		private VisualCollection _visuals;
		public VisualCollection Visuals => _visuals;
		private List<LayerTransform> _transforms = new List<LayerTransform>();
		private FrameRenderer _frameRenderer;
		private IFrameRendererEditor _editor;
		private Act _act;
		public bool VisualDirty { get; set; } = true;

		private List<DrawnData> _layers = new List<DrawnData>();

		static Renderer() {
			BufferedBrushes.Register(LayerDraw.SelectionBorderBrush, ActEditorConfiguration.ActEditorSpriteSelectionBorder);
			BufferedBrushes.Register(LayerDraw.SelectionOverlayBrush, ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay);
		}

		public void SetAct(IFrameRendererEditor editor, FrameRenderer frameRenderer) {
			_frameRenderer = frameRenderer;
			_editor = editor;
			_act = _frameRenderer.Act;
			_visuals = new VisualCollection(_frameRenderer.Canvas);

			_editor.SelectionEngine.SelectionChanged += delegate {
				InvalidateVisual();
			};

			ActEditorConfiguration.ActEditorSpriteSelectionBorder.PropertyChanged += _onPropertyChanged;
			ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay.PropertyChanged += _onPropertyChanged;

			_effectMultiplyColorChannel = new ColorMultiplyEffect();

			BordersDirty();
		}

		protected override int VisualChildrenCount => _visuals.Count;
		protected override Visual GetVisualChild(int index) => _visuals[index];

		private List<(int HashCode, GrfImage SourceImage, WriteableBitmap RenderImage)> _bitmaps = new List<(int, GrfImage, WriteableBitmap)>();

		public Matrix ViewMatrix = Matrix.Identity;

		public static double SnappedRound(double value, double dpi) {
			return Math.Round(value * dpi) / dpi;
		}

		public void UpdateLayerDraw(int layerIndex, Layer layer, Frame frame) {
			var visualLayer = (DrawingVisual)_visuals[layerIndex];
			var image = layer.GetImage(_frameRenderer.Act.Sprite);

			LayerTransform transform = _transforms[layerIndex];

			CalculateLayerTransform();

			var isSelected = ((PrimaryFrameRenderer)_frameRenderer).Editor.SelectionEngine.IsSelected(layerIndex);

			if (VisualDirty || (isSelected != _layers[layerIndex].IsSelected) || DirtyLayerIndexes.Contains(layerIndex)) {
				_layers[layerIndex].IsSelected = isSelected;

				//visualLayer.Effect = _effectMultiplyColorChannel;

				using (var dc = visualLayer.RenderOpen()) {
					Matrix m = transform.Group.Value;
					m *= ViewMatrix;

					_effectMultiplyColorChannel.Color = layer.Color.ToColor();

					var scaleX = Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
					var scaleY = Math.Sqrt(m.M21 * m.M21 + m.M22 * m.M22);

					if (scaleX != 0) { m.M11 /= scaleX; m.M12 /= scaleX; }
					if (scaleY != 0) { m.M21 /= scaleY; m.M22 /= scaleY; }

					double rawOffsetX = m.OffsetX;
					double rawOffsetY = m.OffsetY;

					double snappedOffsetX = SnappedRound(rawOffsetX, _dpi.DpiScaleX);
					double snappedOffsetY = SnappedRound(rawOffsetY, _dpi.DpiScaleY);

					m.OffsetX = snappedOffsetX;
					m.OffsetY = snappedOffsetY;

					dc.PushTransform(new MatrixTransform(m));
					
					//dc.PushEffect(_effectMultiplyColorChannel, null);

					dc.DrawImage(_getBitmap(layer), 
						new Rect(0, 0,
						SnappedRound(rawOffsetX + image.Width * scaleX, _dpi.DpiScaleX) - snappedOffsetX,
						SnappedRound(rawOffsetY + image.Height * scaleY, _dpi.DpiScaleY) - snappedOffsetY
					));

					if (isSelected) {
						dc.DrawRectangle(_borderBackgroundBrush, _borderPen,
							new Rect(_halfBorderBrushThickness, _halfBorderBrushThickness,
							SnappedRound(rawOffsetX + image.Width * scaleX, _dpi.DpiScaleX) - snappedOffsetX - _borderBrushThickness,
							SnappedRound(rawOffsetY + image.Height * scaleY, _dpi.DpiScaleY) - snappedOffsetY - _borderBrushThickness
						));
					}

					dc.Pop();
				}
			}
		}

		public double Snap(double x) {
			return Math.Round(x);
		}

		public void EnsureVisualCount(Frame frame) {
			var edgeMode = ActEditorConfiguration.UseAliasing ? EdgeMode.Aliased : EdgeMode.Unspecified;
			
			for (int i = _visuals.Count; i < frame.Layers.Count; i++) {
				var visual = new LayerDrawingVisual();
				visual.SetEdgeMode(edgeMode);
				_visuals.Add(visual);
				_transforms.Add(new LayerTransform());
				_layers.Add(new DrawnData(_frameRenderer));
			}
		}

		private ActIndex _currentActIndex;

		public void DirtyVisual() {
			VisualDirty = true;
		}

		private HashSet<int> DirtyLayerIndexes = new HashSet<int>();
		private Brush _borderBrush;
		private Brush _borderBackgroundBrush;
		private DpiScale _dpi;
		private double _borderBrushThickness;
		private double _halfBorderBrushThickness;
		private Pen _borderPen;
		private ColorMultiplyEffect _effectMultiplyColorChannel;

		public void DirtyVisual(int layerIndex) {
			DirtyLayerIndexes.Add(layerIndex);
		}

		private BitmapSource _getBitmap(Layer layer) {
			int spriteIndex = layer.GetAbsoluteSpriteId(_act.Sprite);
			Spr spr = _act.Sprite;

			if (spriteIndex < 0 || spriteIndex >= spr.Images.Count)
				return null;

			if (_bitmaps.Count != spr.Images.Count) {
				// Remove extra
				for (int i = spr.Images.Count; i < _bitmaps.Count; i++) {
					_bitmaps.RemoveAt(spr.Images.Count);
				}

				// Add missing
				for (int i = _bitmaps.Count; i < spr.Images.Count; i++) {
					_bitmaps.Add((0, null, null));
				}
			}

			var bitmapEntry = _bitmaps[spriteIndex];
			var grfImage = spr.Images[spriteIndex];

			WriteableBitmap bitmap;

			if (bitmapEntry.SourceImage == null || bitmapEntry.HashCode != grfImage.GetHashCode()) {
				// Reload data
				bitmap = new WriteableBitmap(grfImage.Width, grfImage.Height, 96, 96, PixelFormats.Bgra32, null);
				bitmapEntry = (grfImage.GetHashCode(), grfImage, bitmap);
				_bitmaps[spriteIndex] = bitmapEntry;
			}

			bitmap = bitmapEntry.RenderImage;

			Z.Start(200);
			bitmap.Lock();
			
			unsafe {
				byte r = layer.Color.R;
				byte g = layer.Color.G;
				byte b = layer.Color.B;
				byte a = layer.Color.A;

				fixed (byte* pixelsSrcBase = grfImage.Pixels) {
					if (grfImage.GrfImageType == GrfImageType.Indexed8) {
						int palSize = grfImage.Palette.Length / 4;
						// Convert palette to Bgra32 (from Rgba32)
						// Apply color multiplication to the palette directly
						uint* pNewPalette = stackalloc uint[palSize];
						IntPtr pBackBuffer = bitmap.BackBuffer;

						fixed (byte* pPaletteSrcBase = grfImage.Palette)
						{
							for (int i = 0; i < palSize; i++) {
								byte* srcCol = pPaletteSrcBase + (i * 4);

								pNewPalette[i] = (uint)((byte)(srcCol[2] * b / 255) | ((byte)(srcCol[1] * g / 255) << 8) | ((byte)(srcCol[0] * r / 255) << 16) | ((byte)(srcCol[3] * a / 255) << 24));
							}

							uint* pDst = (uint*)pBackBuffer;

							int size = grfImage.Pixels.Length;

							for (int i = 0; i < size; i++) {
								pDst[i] = pNewPalette[pixelsSrcBase[i]];
							}
						}
					}
					else {
						IntPtr pBackBuffer = bitmap.BackBuffer;
						int size = grfImage.Pixels.Length / 4;
						uint* pDst = (uint*)pBackBuffer;
						uint* pSrc = (uint*)pixelsSrcBase;

						for (int i = 0; i < size; i++) {
							uint c = pSrc[i];
							// Extract, Multiply, Repack (Assuming Source is RGBA)
							byte resB = 0;// (byte)(((c >> 0) & 0xFF) * b / 255);
							byte resG = 0;// (byte)(((c >> 8) & 0xFF) * g / 255);
							byte resR = (byte)(((c >> 16) & 0xFF) * r / 255);
							byte resA = (byte)(((c >> 24) & 0xFF) * a / 255);

							//pDst[i] = (uint)(resB | (resG << 8) | (resR << 16) | (resA << 24));
							pDst[i] = (uint)(resB | (resG << 8) | (resR << 16) | (resA << 24));
						}
					}
				}
			}

			bitmap.AddDirtyRect(new Int32Rect(0, 0, grfImage.Width, grfImage.Height));
			bitmap.Unlock();
			Z.Stop(200);

			return bitmap;
		}

		protected override void OnRender(DrawingContext dc) {
			ViewMatrix = Matrix.Identity;
			ViewMatrix.ScaleAt(_frameRenderer.ZoomEngine.Scale, _frameRenderer.ZoomEngine.Scale, _frameRenderer.CenterX, _frameRenderer.CenterY);
			ViewMatrix.Translate(_frameRenderer.CenterX * _frameRenderer.ZoomEngine.Scale, _frameRenderer.CenterY * _frameRenderer.ZoomEngine.Scale);
			_dpi = VisualTreeHelper.GetDpi(this);

			Z.Start(200);
			int aid = _frameRenderer.SelectedAction;
			int fid = _frameRenderer.SelectedFrame;
			
			var frame = _frameRenderer.Act[aid, fid];

			EnsureVisualCount(frame);

			dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, _frameRenderer.Canvas.ActualWidth, _frameRenderer.Canvas.ActualHeight));

			for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
				_currentActIndex = new ActIndex() { ActionIndex = aid, FrameIndex = fid, LayerIndex = layerIndex };
				var layer = frame.Layers[layerIndex];

				UpdateLayerDraw(layerIndex, layer, frame);
			}

			for (int visualIndex = frame.Layers.Count; visualIndex < _visuals.Count; visualIndex++) {
				var dv = (DrawingVisual)_visuals[visualIndex];

				using (var dc2 = dv.RenderOpen()) {

				}
			}

			System.Windows.Controls.Canvas.SetZIndex(this, 9999);

			VisualDirty = false;
			DirtyLayerIndexes.Clear();
			Z.Stop(200);
			Z.StopAndDisplayAll();
		}

		public void CalculateLayerTransform() {
			if (!VisualDirty)
				return;

			LayerTransform transform = _transforms[_currentActIndex.LayerIndex];

			Act act = _act ?? _frameRenderer.Act;

			int actionIndex = _currentActIndex.ActionIndex;
			int frameIndex = _currentActIndex.FrameIndex;
			int layerIndex = _currentActIndex.LayerIndex;
			int? anchorFrameIndex = null;

			if (actionIndex >= act.NumberOfActions) return;
			if (act.Name == "Head" || act.Name == "Body") {
				bool handled = false;

				if (act[actionIndex].NumberOfFrames == 3 &&
					(0 <= actionIndex && actionIndex < 8) ||
					(16 <= actionIndex && actionIndex < 24)) {
					if (_frameRenderer.Act != null) {
						Act editorAct = _frameRenderer.Act;

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
			if (layerIndex >= frame.NumberOfLayers) return;

			var layer = act[actionIndex, frameIndex, layerIndex];

			if (layer.SpriteIndex < 0) {
				return;
			}

			int index = layer.IsBgra32() ? layer.SpriteIndex + act.Sprite.NumberOfIndexed8Images : layer.SpriteIndex;

			if (index < 0 || index >= act.Sprite.Images.Count) {
				return;
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

			var img = layer.GetImage(act.Sprite);

			if (img == null)
				return;

			int extraX = layer.Mirror ? -(img.Width + 1) % 2 : 0;

			transform.TranslateToCenter.X = -((img.Width + 1) / 2) + extraX;
			transform.TranslateToCenter.Y = -((img.Height + 1) / 2);
			transform.TranslateFrame.X = layer.OffsetX + diffX;
			transform.TranslateFrame.Y = layer.OffsetY + diffY;

			transform.Scale.ScaleX = layer.ScaleX * (layer.Mirror ? -1 : 1);
			transform.Scale.ScaleY = layer.ScaleY;

			transform.Rotate.Angle = layer.Rotation;
		}

		public void BordersDirty() {
			// Border
			var edgeMode = ActEditorConfiguration.UseAliasing ? EdgeMode.Aliased : EdgeMode.Unspecified;
			_borderBrush = BufferedBrushes.GetBrush(LayerDraw.SelectionBorderBrush);
			_borderBackgroundBrush = BufferedBrushes.GetBrush(LayerDraw.SelectionOverlayBrush);

			for (int i = 0; i < _visuals.Count; i++) {
				((LayerDrawingVisual)_visuals[i]).SetEdgeMode(edgeMode);
			}

			_dpi = VisualTreeHelper.GetDpi(this);
			_borderBrushThickness = 1d / _dpi.DpiScaleX;
			_halfBorderBrushThickness = _borderBrushThickness * 0.5d;
			_borderPen = new Pen(_borderBrush, _borderBrushThickness);

			if (_borderPen.CanFreeze)
				_borderPen.Freeze();
		}

		public void Unload() {
			ActEditorConfiguration.ActEditorSpriteSelectionBorder.PropertyChanged -= _onPropertyChanged;
			ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay.PropertyChanged -= _onPropertyChanged;
		}

		private void _onPropertyChanged() {
			BordersDirty();
		}
	}
}
