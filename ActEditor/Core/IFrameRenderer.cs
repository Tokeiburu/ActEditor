using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.EditorControls;
using GRF.FileFormats.ActFormat;
using Utilities.Tools;

namespace ActEditor.Core {
	/// <summary>
	/// Interface for a frame renderer.
	/// </summary>
	public interface IFrameRenderer {
		Canvas Canva { get; }
		int CenterX { get; }
		int CenterY { get; }
		ZoomEngine ZoomEngine { get; }
		Act Act { get; }
		int SelectedAction { get; }
		int SelectedFrame { get; }
		List<DrawingComponent> Components { get; }
		Point PointToScreen(Point point);
	}

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
		IFrameRenderer FrameRenderer { get; }
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
		void SetAction(int index);
		void SetFrame(int index);
		int SelectedAction { get; set; }
		int SelectedFrame { get; set; }
		void Play();
		void Stop();
	}
}