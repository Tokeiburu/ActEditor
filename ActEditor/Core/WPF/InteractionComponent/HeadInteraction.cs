using System;
using System.Linq;
using System.Windows;
using ActEditor.Core.WPF.FrameEditor;
using ErrorManager;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.WPF.InteractionComponent {
	public class HeadInteraction : IEditorInteraction {
		private readonly FrameRenderer _renderer;
		private readonly IFrameRendererEditor _editor;

		public HeadInteraction(FrameRenderer renderer, IFrameRendererEditor editor) {
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
			var layersList = layers.Take(1);

			try {
				_editor.Act.Commands.BeginNoDelay();
				_editor.Act.Commands.LayerDelete(_editor.SelectedAction, _editor.SelectedFrame, 0);
				_editor.Act.Commands.LayerAdd(_editor.SelectedAction, _editor.SelectedFrame, layersList.ToArray());
			}
			catch (Exception err) {
				_editor.Act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				_editor.Act.Commands.End();
				_editor.FrameSelector.OnFrameChanged(_editor.SelectedFrame);
				_editor.SelectionEngine.SetSelection(0);
			}
		}

		public void Cut() {
		}

		public void Delete() {
		}
	}
}
