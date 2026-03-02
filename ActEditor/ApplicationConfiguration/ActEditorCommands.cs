using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TokeiLibrary.Shortcuts;

namespace ActEditor.ApplicationConfiguration {
	public static class ActEditorCommands {
		// Duplicate default commands, not particular use, it's just cleaner
		public static TkCommand Save = ApplicationShortcut.Save;
		public static TkCommand New = ApplicationShortcut.New;
		public static TkCommand Open = ApplicationShortcut.Open;
		public static TkCommand Copy = ApplicationShortcut.Copy;
		public static TkCommand Paste = ApplicationShortcut.Paste;
		public static TkCommand Cut = ApplicationShortcut.Cut;
		public static TkCommand Delete = ApplicationShortcut.Delete;
		public static TkCommand Undo = ApplicationShortcut.Undo;
		public static TkCommand Redo = ApplicationShortcut.Redo;
		public static TkCommand SaveAs = ApplicationShortcut.FromString("Ctrl-Shift-S", "Application.SaveAs");
		public static TkCommand MoveAt = ApplicationShortcut.FromString("Ctrl-T", "ListData.MoveAt");

		public static TkCommand DeselectAll = ApplicationShortcut.FromString("Ctrl-D", "ActEditor.DeselectAll");
		public static TkCommand FrameEditorNextFrame = ApplicationShortcut.FromString("Ctrl-Right", "FrameEditor.NextFrame");
		public static TkCommand FrameEditorPreviousFrame = ApplicationShortcut.FromString("Ctrl-Left", "FrameEditor.PreviousFrame");
		public static TkCommand FrameEditorNextAction = ApplicationShortcut.FromString("Ctrl-Shift-Right", "FrameEditor.NextAction");
		public static TkCommand FrameEditorPreviousAction = ApplicationShortcut.FromString("Ctrl-Shift-Left", "FrameEditor.PreviousAction");
		public static TkCommand ActEditorStyleEditor = ApplicationShortcut.FromString("Ctrl-Alt-P", "ActEditor.StyleEditor");
		public static TkCommand ActEditorStopPlayAnimation = ApplicationShortcut.FromString("Space", "ActEditor.PlayStopAnimation");
		public static TkCommand ActEditorNewFile = ApplicationShortcut.FromString("Ctrl-N", "ActEditor.NewFile");
		public static TkCommand ActEditorCloseTab = ApplicationShortcut.FromString("Ctrl-Q", "ActEditor.CloseTab");
		public static TkCommand ActEditorSelectActInExplorer = ApplicationShortcut.FromString(null, "ActEditor.SelectActInExplorer");
		public static TkCommand ActEditorSaveAsGarment = ApplicationShortcut.FromString(null, "ActEditor.SaveAsGarment");
		public static TkCommand FrameEditorShowAdjascentFrames = ApplicationShortcut.FromString("Ctrl-Shift-A", "FrameEditor.ShowAdjascentFrames");
		public static TkCommand FrameEditorShowAnchor1 = ApplicationShortcut.FromString("Ctrl-1", "FrameEditor.ShowAnchor1");
		public static TkCommand FrameEditorShowAnchor2 = ApplicationShortcut.FromString("Ctrl-2", "FrameEditor.ShowAnchor2");
		public static TkCommand FrameEditorShowAnchor3 = ApplicationShortcut.FromString("Ctrl-3", "FrameEditor.ShowAnchor3");
		public static TkCommand FrameEditorShowAnchor4 = ApplicationShortcut.FromString("Ctrl-4", "FrameEditor.ShowAnchor4");
		public static TkCommand FrameEditorShowAnchor5 = ApplicationShortcut.FromString("Ctrl-5", "FrameEditor.ShowAnchor5");
		public static TkCommand LayerEditorBringOneUp = ApplicationShortcut.FromString("Alt-F", "LayerEditor.BringOneUp");
		public static TkCommand FrameEditorLayerMirrorVertical = ApplicationShortcut.FromString(null, "FrameEditor.LayerMirrorVertical");
		public static TkCommand FrameEditorLayerMirrorHorizontal = ApplicationShortcut.FromString(null, "FrameEditor.LayerMirrorHorizontal");
		public static TkCommand ActEditorTabCloseAllButThis = ApplicationShortcut.FromString(null, "ActEditor.TabCloseAllButThis");
		public static TkCommand ListDataNew = ApplicationShortcut.FromString("Ctrl-N", "ListData.New");

		// Script runner commands
		public static TkCommand ScriptRunnerRunScript = ApplicationShortcut.FromString("Ctrl-R", "ScriptRunner.RunScript");

		// Sprite editor commands
		public static TkCommand SpriteEditorSelect = ApplicationShortcut.FromString("Ctrl-Q", "SpriteEditor.Select");
		public static TkCommand SpriteEditorBucket = ApplicationShortcut.FromString("Ctrl-B", "SpriteEditor.Bucket");
		public static TkCommand SpriteEditorStamp = ApplicationShortcut.FromString("Ctrl-T", "SpriteEditor.Stamp");
		public static TkCommand SpriteEditorEraser = ApplicationShortcut.FromString("Ctrl-E", "SpriteEditor.Eraser");
		public static TkCommand SpriteEditorPen = ApplicationShortcut.FromString("Ctrl-P", "SpriteEditor.Pen");
		public static TkCommand SpriteEditorRectangle = ApplicationShortcut.FromString("Ctrl-R", "SpriteEditor.Rectangle");
		public static TkCommand SpriteEditorLine = ApplicationShortcut.FromString("Ctrl-L", "SpriteEditor.Line");
		public static TkCommand SpriteEditorCircle = ApplicationShortcut.FromString("Ctrl-I", "SpriteEditor.Circle");
		public static TkCommand SpriteEditorSwitchColors = ApplicationShortcut.FromString("Ctrl-1", "SpriteEditor.SwitchColors");
		public static TkCommand SpriteEditorSwitchColorsIndex = ApplicationShortcut.FromString("Ctrl-2", "SpriteEditor.SwitchColorsIndex");
		public static TkCommand SpriteEditorSwitchColorsKeep = ApplicationShortcut.FromString("Ctrl-3", "SpriteEditor.SwitchColorsKeep");
		public static TkCommand SpriteEditorRedirectTo = ApplicationShortcut.FromString("Ctrl-4", "SpriteEditor.RedirectTo");
		public static TkCommand SpriteEditorStampLock = ApplicationShortcut.FromString("Ctrl-L", "SpriteEditor.StampLock");
		public static TkCommand SpriteEditorBrushIncrease = ApplicationShortcut.FromString("Add", "SpriteEditor.BrushIncrease");
		public static TkCommand SpriteEditorBrushDecrease = ApplicationShortcut.FromString("Subtract", "SpriteEditor.BrushDecrease");
		public static TkCommand SpriteEditorPaletteSelector = ApplicationShortcut.FromString("Ctrl-U", "SpriteEditor.PaletteSelector");

		public static TkCommand ListDataInsertAt = ApplicationShortcut.FromString("Ctrl-Shift-V", "ListData.InsertAt");
		public static TkCommand FrameEditorSelectAll = ApplicationShortcut.FromString("Ctrl-A", "FrameEditor.SelectAll");
	}
}
