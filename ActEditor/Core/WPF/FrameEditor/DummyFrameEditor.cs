using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ActEditor.Core.WPF.EditorControls;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.WPF.FrameEditor {
	public class DummyFrameEditor : IFrameRendererEditor {
		public UIElement Element { get; set; }
		public Act Act { get { return ActFunc(); } }
		public int SelectedAction { get { return SelectedActionFunc(); } }
		public int SelectedFrame { get { return SelectedFrameFunc(); } }
		public SelectionEngine SelectionEngine { get; set; }
		public List<ReferenceControl> References { get { return ReferencesFunc(); } }
		public IActIndexSelector IndexSelector { get; set; }
		public event ActEditorWindow.ActEditorEventDelegate ReferencesChanged;

		protected virtual void OnReferencesChanged() {
			ActEditorWindow.ActEditorEventDelegate handler = ReferencesChanged;
			if (handler != null) handler(this);
		}

		public event ActEditorWindow.ActEditorEventDelegate ActLoaded;

		protected virtual void OnActLoaded() {
			ActEditorWindow.ActEditorEventDelegate handler = ActLoaded;
			if (handler != null) handler(this);
		}

		public Grid GridPrimary { get; private set; }
		public LayerEditor LayerEditor { get; private set; }
		public SpriteSelector SpriteSelector { get; private set; }
		public IFrameRenderer FrameRenderer { get; set; }
		public SpriteManager SpriteManager { get; private set; }

		public Func<Act> ActFunc;
		public Func<int> SelectedActionFunc;
		public Func<int> SelectedFrameFunc;
		public Func<List<ReferenceControl>> ReferencesFunc;
	}
}
