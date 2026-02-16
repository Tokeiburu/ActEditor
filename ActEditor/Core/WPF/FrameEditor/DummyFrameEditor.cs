using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ActEditor.Core.DrawingComponents;
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
		public FrameRenderer FrameRenderer { get; set; }
		public SpriteManager SpriteManager { get; private set; }

		public Func<Act> ActFunc;
		public Func<int> SelectedActionFunc;
		public Func<int> SelectedFrameFunc;
		public Func<List<ReferenceControl>> ReferencesFunc;

		public static DummyFrameEditor CreateEditor(
			Func<Act> getterAct, 
			UIElement eventElement, 
			IActIndexSelector indexSelector, 
			FrameRenderer frameRenderer, 
			int selectedAction = 0, 
			int selectedFrame = 0, 
			bool playAnimation = false) {

			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = getterAct;
			editor.Element = eventElement;
			editor.IndexSelector = indexSelector;
			editor.SelectedActionFunc = () => indexSelector.SelectedAction;
			editor.SelectedFrameFunc = () => indexSelector.SelectedFrame;
			editor.FrameRenderer = frameRenderer;

			indexSelector.Init(editor, indexSelector.SelectedAction, indexSelector.SelectedFrame);
			indexSelector.SelectedAction = selectedAction;
			indexSelector.SelectedFrame = selectedFrame;

			if (playAnimation)
				indexSelector.Play();

			frameRenderer.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (editor.Act != null) {
					return new List<DrawingComponent> { new ActDraw(editor.Act, editor) };
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			frameRenderer.Init(editor);
			frameRenderer.Update();

			return editor;
		}
	}
}
