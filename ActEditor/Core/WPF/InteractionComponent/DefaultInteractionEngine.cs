using System;
using System.Linq;
using System.Windows;
using ActEditor.Core.WPF.FrameEditor;
using ErrorManager;
using GRF.FileFormats.ActFormat;

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
				Clipboard.SetDataObject(new DataObject("Layers", _renderer.Editor.SelectionEngine.SelectedLayers.ToList().Select(p => new Layer(p)).ToArray()));
			}
		}

		public void Paste() {
			if (_editor.Act == null) return;

			var layersDataObj = Clipboard.GetDataObject();

			if (layersDataObj == null) return;

			var layersObj = layersDataObj.GetData("Layers");

			Layer[] layers = layersObj as Layer[];

			if (layers == null || layers.Length == 0) return;

			int start = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].NumberOfLayers;

			try {
				_editor.Act.Commands.Begin();
				_editor.Act.Commands.LayerAdd(_editor.SelectedAction, _editor.SelectedFrame, layers);
			}
			catch (Exception err) {
				_editor.Act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_editor.Act.Commands.End();
				_editor.IndexSelector.OnFrameChanged(_editor.SelectedFrame);
				_editor.SelectionEngine.SetSelection(start, _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].NumberOfLayers - start);
			}
		}

		public void Cut() {
			var layers = _editor.SelectionEngine.SelectedLayers;

			if (layers.Length > 0) {
				Clipboard.SetDataObject(new DataObject("Layers", layers.ToList().Select(p => new Layer(p)).ToArray()));

				try {
					_editor.Act.Commands.Begin();

					foreach (int index in _editor.SelectionEngine.CurrentlySelected.OrderByDescending(p => p)) {
						_editor.Act.Commands.LayerDelete(_editor.SelectedAction, _editor.SelectedFrame, index);
					}

					_editor.SelectionEngine.ClearSelection();
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

				_editor.SelectionEngine.ClearSelection();
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
