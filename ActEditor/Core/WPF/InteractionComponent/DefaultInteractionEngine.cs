using System;
using System.IO;
using System.Linq;
using System.Windows;
using ActEditor.Core.WPF.FrameEditor;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using Utilities.Extension;

namespace ActEditor.Core.WPF.InteractionComponent {
	public class DefaultInteraction : IEditorInteraction {
		private readonly FrameRenderer _renderer;
		private readonly IFrameRendererEditor _editor;

		public DefaultInteraction(FrameRenderer renderer, IFrameRendererEditor editor) {
			_renderer = renderer;
			_editor = editor;
		}

		public void Copy() {
			var layers = _renderer.Editor.SelectionEngine.SelectedLayers;

			if (layers.Length > 0) {
				Clipboard.SetDataObject(new DataObject("Layers", new LayerClipboardData(layers, _editor.Act)));
				//Clipboard.SetDataObject(new DataObject("Layers", _renderer.Editor.SelectionEngine.SelectedLayers.ToList().Select(p => new Layer(p)).ToArray()));
			}
		}

		public void Paste() {
			if (_editor.Act == null) return;

			var layersObj = Clipboard.GetDataObject()?.GetData("Layers");
			LayerClipboardData layerClipboardData = layersObj as LayerClipboardData;

			if (layerClipboardData == null || layerClipboardData.Layers.Length == 0) return;

			int start = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].NumberOfLayers;

			try {
				_editor.Act.Commands.ActEditBegin("Clipboard paste - Added layers (" + start + ")");

				// Resolve images
				if (_editor.Act.LoadedPath != layerClipboardData.SourceActPath) {
					try {
						_editor.SpriteManager.Begin();
						SpriteManager.SpriteConverterOption = -1;

						var spr = new Spr(File.ReadAllBytes(layerClipboardData.SourceSprPath));

						foreach (var layer in layerClipboardData.Layers) {
							var image = layer.GetImage(spr);

							if (image.GrfImageType == GRF.Image.GrfImageType.Indexed8)
								image.Palette[3] = 0;

							if (image != null) {
								// Check if image exists already
								var idx = _editor.Act.Sprite.Exists(image);

								if (!idx.Valid) {
									idx = _editor.SpriteManager.AddImage(image);
								}

								layer.SprSpriteIndex = idx;
							}
						}
					}
					finally {
						_editor.SpriteManager.End();
					}
				}

				var frame = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame];
				frame.Layers.AddRange(layerClipboardData.Layers);
				//_editor.Act.Commands.LayerAdd(_editor.SelectedAction, _editor.SelectedFrame, layers);
			}
			catch (OperationCanceledException) {
				_editor.Act.Commands.ActCancelEdit();
			}
			catch (Exception err) {
				_editor.Act.Commands.ActCancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_editor.Act.Commands.ActEditEnd();
				_editor.IndexSelector.OnFrameChanged(_editor.SelectedFrame);
				_editor.SelectionEngine.SetSelection(start, _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].NumberOfLayers - start);
			}
		}

		public void Cut() {
			var layers = _editor.SelectionEngine.SelectedLayers;

			if (layers.Length > 0) {
				Copy();

				try {
					_editor.Act.Commands.Begin();

					foreach (int index in _editor.SelectionEngine.CurrentlySelected.OrderByDescending(p => p)) {
						_editor.Act.Commands.LayerDelete(_editor.SelectedAction, _editor.SelectedFrame, index);
					}

					_editor.SelectionEngine.DeselectAll();
				}
				catch (Exception err) {
					_editor.Act.Commands.CancelEdit();
					ErrorHandler.HandleException(err);
				}
				finally {
					_editor.Act.Commands.End();
					_editor.Act.InvalidateVisual();
				}
			}
		}

		public void Delete() {
			try {
				_editor.Act.Commands.Begin();

				foreach (int index in _editor.SelectionEngine.CurrentlySelected.OrderByDescending(p => p)) {
					_editor.Act.Commands.LayerDelete(_editor.SelectedAction, _editor.SelectedFrame, index);
				}

				_editor.SelectionEngine.DeselectAll();
			}
			catch (Exception err) {
				_editor.Act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_editor.Act.Commands.End();
				_editor.Act.InvalidateVisual();
			}
		}
	}
}
