using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActEditor.Core.DrawingComponents;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using Point = System.Windows.Point;

namespace ActEditor.Core.WPF.FrameEditor {
	public class FrameRendererTransformInput {
		public bool KeyboardTranslated { get; set; }
		public bool Moved { get; set; }
		public bool Scaled { get; set; }
		public bool Rotated { get; set; }
		public bool Translated { get; set; }
		public bool TransformEnabled { get; set; }
		public bool AnyMouseDown { get; set; }
		public Point BeforeTransformMousePosition { get; set; }
		public Point LatestTransformMousePosition { get; set; }
		public ScaleDirection? FavoriteOrientation { get; set; }
	}

	public enum ScaleDirection {
		Horizontal,
		Vertical,
		Both
	}

	public class FrameRendererEdit {
		private readonly FrameRenderer _renderer;
		private readonly IFrameRendererEditor _editor;
		private readonly FrameRendererTransformInput _frti = new FrameRendererTransformInput();
		public bool EnableEdit { get; set; }
		
		public FrameRendererEdit(FrameRenderer renderer, IFrameRendererEditor editor) {
			EnableEdit = true;
			_renderer = renderer;
			_editor = editor;

			_renderer.MouseMove += new MouseEventHandler(_renderer_MouseMove);
			_renderer.MouseDown += new MouseButtonEventHandler(_renderer_MouseDown);
			_renderer.MouseUp += new MouseButtonEventHandler(_renderer_MouseUp);

			_renderer.Loaded += delegate {
				if (_editor != null) {
					_editor.Element.KeyDown += new KeyEventHandler(Renderer_KeyDown);
					_editor.Element.KeyUp += new KeyEventHandler(_renderer_KeyUp);
				}
			};
		}

		private void _renderer_KeyUp(object sender, KeyEventArgs e) {
			if (!EnableEdit) return;
			if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.Right || e.Key == Key.Down) {
				if (_frti.KeyboardTranslated) {
					try {
						_applyTranslate();
					}
					finally {
						_frti.KeyboardTranslated = false;
					}
				}
			}
		}

		public void Renderer_KeyDown(object sender, KeyEventArgs e) {
			if (!EnableEdit) return;
			if (_editor.IndexSelector.IsPlaying) return;

			if (ApplicationShortcut.IsCommandActive())
				return;

			if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.Right || e.Key == Key.Down) {
				if (_editor == null) return;

				_renderer_MouseUp(this, null);

				if (_renderer.MainDrawingComponent != null) {
					if (!_frti.KeyboardTranslated) {
						_editor.SelectionEngine.PopSelectedLayerState();
						_frti.KeyboardTranslated = true;
					}

					foreach (LayerDraw layer in _editor.SelectionEngine.SelectedLayerDraws) {
						layer.PreviewTranslateRaw(
							(Keyboard.IsKeyDown(Key.Left) ? -1 : 0) + (Keyboard.IsKeyDown(Key.Right) ? 1 : 0),
							(Keyboard.IsKeyDown(Key.Up) ? -1 : 0) + (Keyboard.IsKeyDown(Key.Down) ? 1 : 0));
					}
				}

				e.Handled = true;
			}
		}

		private void _renderer_MouseDown(object sender, MouseButtonEventArgs e) {
			try {
				_frti.AnyMouseDown = true;
				_frti.Moved = false;

				if (Keyboard.FocusedElement != _renderer._cbZoom)
					Keyboard.Focus(_editor.GridPrimary);

				_frti.BeforeTransformMousePosition = e.GetPosition(_renderer);
				_frti.FavoriteOrientation = null;

				if (_frti.Translated || _frti.Scaled || _frti.Rotated) {
					e.Handled = true;
					return;
				}

				if (e.RightButton == MouseButtonState.Pressed) {
					_frti.TransformEnabled = true;
					_renderer.CaptureMouse();
				}

				if (e.LeftButton == MouseButtonState.Pressed && !_editor.IndexSelector.IsPlaying && _editor.SelectionEngine != null) {
					if (_editor.SelectionEngine.SelectedItems.Count > 0) {
						if (_noSelectedComponentsUnderMouse(e) &&
							((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift) &&
							((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)) {
							_frti.TransformEnabled = false;
							return;
						}

						_editor.SelectionEngine.PopSelectedLayerState();
						_frti.TransformEnabled = true;
					}
					else {
						_frti.TransformEnabled = false;
					}
				}

				_frti.LatestTransformMousePosition = _frti.BeforeTransformMousePosition;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _renderer_MouseMove(object sender, MouseEventArgs e) {
			try {
				if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
					return;

				if (_renderer.ContextMenu != null && _renderer.ContextMenu.IsOpen) return;

				Point current = e.GetPosition(_renderer);

				double deltaX = (current.X - _frti.BeforeTransformMousePosition.X);
				double deltaY = (current.Y - _frti.BeforeTransformMousePosition.Y);

				if (deltaX == 0 && deltaY == 0)
					return;

				if (_renderer.GetObjectAtPoint<ComboBox>(e.GetPosition(_renderer)) == _renderer._cbZoom && !_renderer.IsMouseCaptured)
					return;

				var distPoint = Point.Subtract(current, _frti.LatestTransformMousePosition);

				if (_frti.FavoriteOrientation == null) {
					_frti.FavoriteOrientation = distPoint.X * distPoint.X > distPoint.Y * distPoint.Y ? ScaleDirection.Horizontal : ScaleDirection.Vertical;
				}

				if (e.RightButton == MouseButtonState.Pressed && _frti.AnyMouseDown) {
					_renderer.RelativeCenter = new Point(
						_renderer.RelativeCenter.X + deltaX / _renderer.Canva.ActualWidth,
						_renderer.RelativeCenter.Y + deltaY / _renderer.Canva.ActualHeight);

					_frti.BeforeTransformMousePosition = current;
					_renderer.SizeUpdate();
					_renderer.OnViewerMoved(_renderer.RelativeCenter);
					_frti.Moved = true;
				}

				if (_editor.IndexSelector.IsPlaying) return;
				if (e.LeftButton == MouseButtonState.Pressed && EnableEdit) {
					if (!_renderer.IsMouseCaptured)
						_renderer.CaptureMouse();

					if (!_frti.TransformEnabled) {
						if (_frti.AnyMouseDown && _editor.SelectionEngine != null) {
							if (_getDistance(_frti.BeforeTransformMousePosition, e.GetPosition(_renderer)) > 5) {
								_editor.SelectionEngine.UpdateSelection(new Rect(_frti.BeforeTransformMousePosition, e.GetPosition(_renderer)), true);
							}
							else {
								_editor.SelectionEngine.UpdateSelection(default(Rect), false);
							}
						}
						return;
					}

					if (EnableEdit) {
						if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control || _frti.Scaled) {
							_frti.Scaled = true;
						}
						else if (Keyboard.Modifiers == ModifierKeys.Shift || _frti.Rotated) {
							_frti.Rotated = true;
						}
						else {
							if (!_frti.Translated && !_frti.Scaled && !_frti.Rotated) {
								if (_noSelectedComponentsUnderMouse(e)) {
									return;
								}
							}

							_frti.Translated = true;
						}

						if (_renderer.MainDrawingComponent != null && _editor.SelectionEngine != null) {
							_editor.IndexSelector.OnAnimationPlaying(1);
							List<LayerDraw> layers = _editor.SelectionEngine.SelectedLayerDraws;
							List<Layer> layersAct = _editor.SelectionEngine.SelectedLayers.ToList();

							var centerSelected = new GRF.Graphics.Point(0, 0);

							foreach (Layer layer in layersAct) {
								centerSelected.X += layer.OffsetX;
								centerSelected.Y += layer.OffsetY;
							}

							centerSelected.X = (float)(centerSelected.X / layersAct.Count * _renderer.ZoomEngine.Scale) + _renderer.CenterX;
							centerSelected.Y = (float)(centerSelected.Y / layersAct.Count * _renderer.ZoomEngine.Scale) + _renderer.CenterY;

							Vertex diffVector = new Vertex(_frti.BeforeTransformMousePosition.ToGrfPoint() - centerSelected);

							foreach (LayerDraw layer in layers.OrderBy(p => p.LayerIndex)) {
								if (_frti.Scaled) {
									if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
										if (_frti.FavoriteOrientation != null) {
											if (_frti.FavoriteOrientation.Value == ScaleDirection.Horizontal)
												deltaY = 0;
											else if (_frti.FavoriteOrientation.Value == ScaleDirection.Vertical)
												deltaX = 0;
										}

										layer.PreviewScale(diffVector, deltaX, deltaY);
									}
									else if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
										double scale = (current.ToGrfPoint() - centerSelected).Lenght / (_frti.BeforeTransformMousePosition.ToGrfPoint() - centerSelected).Lenght;
										layer.PreviewScale(Math.Pow(scale, 1.2d));
									}
									else {
										_frti.FavoriteOrientation = null;
										layer.PreviewScale(diffVector, deltaX, deltaY);
									}
								}

								if (_frti.Translated) {
									layer.PreviewTranslate(deltaX, deltaY);
								}

								if (_frti.Rotated) {
									layer.PreviewRotate(_frti.BeforeTransformMousePosition, (float)deltaX, (float)deltaY);
								}
							}
						}
					}
				}

				_frti.LatestTransformMousePosition = current;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _renderer_MouseUp(object sender, MouseButtonEventArgs e) {
			try {
				bool canMouseUp = true;

				_frti.AnyMouseDown = false;

				if (!_editor.IndexSelector.IsPlaying && EnableEdit) {
					if (_frti.Scaled) {
						_applyScale();
						_frti.Scaled = false;
						canMouseUp = false;
					}

					if (_frti.Rotated) {
						_applyRotated();
						_frti.Rotated = false;
						canMouseUp = false;
					}

					if (_frti.Translated) {
						_applyTranslate();
						_frti.Translated = false;
					}
					else {
						if (e != null && _editor.SelectionEngine != null) {
							if (_noSelectedComponentsUnderMouse(e) &&
								e.ChangedButton == MouseButton.Left &&
								_editor.SelectionEngine.IsUnderMouse(_frti.BeforeTransformMousePosition) == false &&
								_getDistance(_frti.BeforeTransformMousePosition, e.GetPosition(_renderer)) < 6) {
									_editor.SelectionEngine.DeselectAll();
							}
							else if (e.ChangedButton == MouseButton.Left && _componentsUnderMouse(e) && canMouseUp) {
								var selectionDraw = _renderer.Components.OfType<SelectionDraw>().FirstOrDefault();

								if (selectionDraw == null || !selectionDraw.Visible)
									_editor.SelectionEngine.SelectUnderMouse(_frti.BeforeTransformMousePosition, e);
							}
							else if (e.ChangedButton == MouseButton.Right && (canMouseUp && !_frti.Moved && _componentsUnderMouse(e) && _renderer.GetObjectAtPoint<ComboBox>(e.GetPosition(_renderer)) != _renderer._cbZoom)) {
								List<LayerDraw> reverse = new List<DrawingComponent>(_renderer.MainDrawingComponent.Components).OfType<LayerDraw>().ToList();
								reverse.Reverse();

								int selected = -1;

								if (_noSelectedComponentsUnderMouse(e)) {
									foreach (var sd in reverse) {
										if (sd.IsMouseUnder(e)) {
											sd.IsSelected = true;
											selected = sd.LayerIndex;
											break;
										}
									}
								}
								else {
									// There is something selected, try and get it
									foreach (var sd in reverse) {
										if (sd.IsSelected && sd.IsMouseUnder(e)) {
											selected = sd.LayerIndex;
											break;
										}
									}
								}

								if (selected < 0) {
									foreach (var sd in reverse) {
										if (sd.IsMouseUnder(e)) {
											selected = sd.LayerIndex;
											break;
										}
									}
								}

								if (selected > -1 && _editor.SelectionEngine != null) {
									_editor.SelectionEngine.LatestSelected = selected;
								}

								if (_renderer.ContextMenu != null)
									_renderer.ContextMenu.IsOpen = true;
							}

							if (_renderer.GetObjectAtPoint<ComboBox>(e.GetPosition(_renderer)) != _renderer._cbZoom)
								e.Handled = true;
						}
					}

					_editor.IndexSelector.OnAnimationPlaying(0);
				}
				else {
					if (_renderer.GetObjectAtPoint<ComboBox>(e.GetPosition(_renderer)) != _renderer._cbZoom)
						e.Handled = true;
				}

				_frti.TransformEnabled = false;

				_renderer.OnFrameMouseUp(e);

				_renderer.ReleaseMouseCapture();

				if (_editor.SelectionEngine != null)
					_editor.SelectionEngine.UpdateSelection(default(Rect), false);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private bool _noSelectedComponentsUnderMouse(MouseEventArgs e) {
			if (_renderer.MainDrawingComponent == null) return true;

			foreach (LayerDraw sd in _renderer.MainDrawingComponent.Components) {
				if (sd.IsMouseUnder(e) && sd.IsSelected) {
					return false;
				}
			}

			return true;
		}

		private bool _componentsUnderMouse(MouseEventArgs e) {
			if (_renderer.MainDrawingComponent == null) return false;

			foreach (LayerDraw sd in _renderer.MainDrawingComponent.Components) {
				if (sd.IsMouseUnder(e)) {
					return true;
				}
			}

			return false;
		}

		private double _getDistance(Point p1, Point p2) {
			return Point.Subtract(p2, p1).Length;
		}

		private void _applyTranslate() {
			_editor.Act.Commands.Begin();

			try {
				if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) {
					if (_editor.SelectionEngine.SelectedLayerDraws.Count == _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].Layers.Count) {
						// All selected, apply translate to the entire action
						foreach (var layer in _editor.SelectionEngine.SelectedLayerDraws) {
							var point = layer.GetTranslatePoint();

							foreach (var layerDraw in _editor.SelectionEngine.SelectedLayerDraws) {
								layerDraw.TranslateRestore();
							}

							_editor.Act.Commands.Translate(_editor.SelectedAction, (int)point.X, (int)point.Y);
							break;
						}
					}
					else {
						// Only apply translation to the selected indexes
						foreach (var layer in _editor.SelectionEngine.SelectedLayerDraws) {
							var point = layer.GetTranslatePoint();

							foreach (var layerDraw in _editor.SelectionEngine.SelectedLayerDraws) {
								layerDraw.TranslateRestore();
							}

							foreach (var layerIndex in _editor.SelectionEngine.SelectedLayerDraws.Select(p => p.LayerIndex)) {
								for (int fid = 0; fid < _editor.Act[_editor.SelectedAction].Frames.Count; fid++) {
									var layerT = _editor.Act.TryGetLayer(_editor.SelectedAction, fid, layerIndex);

									if (layerT != null) {
										_editor.Act.Commands.Translate(_editor.SelectedAction, fid, layerIndex, (int)point.X, (int)point.Y);
									}
								}
							}

							break;
						}
					}
				}
				else {
					_editor.SelectionEngine.SelectedLayerDraws.ForEach(p => p.Translate());
				}
			}
			finally {
				_editor.Act.Commands.EndEdit();
			}
		}

		private void _applyScale() {
			_editor.Act.Commands.Begin();

			try {
				_editor.SelectionEngine.SelectedLayerDraws.ForEach(p => p.Scale());
			}
			finally {
				_editor.Act.Commands.EndEdit();
			}
		}

		private void _applyRotated() {
			_editor.Act.Commands.Begin();

			try {
				_editor.SelectionEngine.SelectedLayerDraws.ForEach(p => p.Rotate());
			}
			finally {
				_editor.Act.Commands.EndEdit();
			}
		}
	}
}
