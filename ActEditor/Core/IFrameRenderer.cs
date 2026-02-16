using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core {
	/// <summary>
	/// Interface for a frame editor, includes all related components.
	/// </summary>
	public interface IFrameRendererEditor {
		UIElement Element { get; }
		Act Act { get; }
		int SelectedAction { get; }
		int SelectedFrame { get; }
		SelectionEngine SelectionEngine { get; }
		List<ReferenceControl> References { get; }
		IActIndexSelector IndexSelector { get; }
		event ActEditorWindow.ActEditorEventDelegate ReferencesChanged;
		event ActEditorWindow.ActEditorEventDelegate ActLoaded;
		Grid GridPrimary { get; }
		LayerEditor LayerEditor { get; }
		FrameRenderer FrameRenderer { get; }
		SpriteSelector SpriteSelector { get; }
		SpriteManager SpriteManager { get; }
	}

	public interface IActIndexSelector {
		bool IsPlaying { get; }
		event ActIndexSelector.FrameIndexChangedDelegate ActionChanged;
		event ActIndexSelector.FrameIndexChangedDelegate FrameChanged;
		event ActIndexSelector.FrameIndexChangedDelegate SpecialFrameChanged;
		void OnFrameChanged(int actionindex);
		void OnAnimationPlaying(int actionindex);
		int SelectedAction { get; set; }
		int SelectedFrame { get; set; }
		void Play();
		void Stop();
		void Init(IFrameRendererEditor editor, int actionIndex, int selectedAction);
	}
}