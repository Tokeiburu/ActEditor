using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using GRF.FileFormats.PalFormat;
using GRF.IO;
using PaletteEditor;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Tools.PaletteEditorTool {
	/// <summary>
	/// Interaction logic for PalettePreset.xaml
	/// </summary>
	public partial class PalettePreset : TkWindow {
		public PalettePreset()
			: base("Palette selector", "pal.png", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			InitializeComponent();
			WindowStyle = System.Windows.WindowStyle.ToolWindow;

			List<PaletteSelector> selectors = new List<PaletteSelector> {
				_paletteSelector0,
				_paletteSelector1,
				_paletteSelector2,
				_paletteSelector3,
			};

			for (int i = 0; i < selectors.Count; i++) {
				string path = GrfPath.Combine(Configuration.ApplicationDataPath, "preset_" + i + ".pal");

				if (!File.Exists(path)) {
					var paletteData = ApplicationManager.GetResource("preset_" + i + ".pal");
					File.WriteAllBytes(path, paletteData);
				}

				var pal = new Pal(path);
				pal.PaletteChanged += delegate {
					File.WriteAllBytes(path, pal.BytePalette);
					
					// Do not save it, it will remove the undo/redo stack
					//pal.Save(path);
				};

				selectors[i].IsMultipleColorsSelectable = true;

				int current = i;

				selectors[i].PreviewMouseMove += delegate {
					if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) {
						selectors[current].UseLargeSelector = false;
					}
					else {
						selectors[current].UseLargeSelector = true;
					}
				};
				ApplicationShortcut.Link("Ctrl-Z", "Undo Palette Edit " + i, () => selectors[current].Palette.Commands.Undo(), "Palette", selectors[i]);
				ApplicationShortcut.Link("Ctrl-Y", "Redo Palette Edit " + i, () => selectors[current].Palette.Commands.Redo(), "Palette", selectors[i]);
				selectors[i].UseLargeSelector = true;
				selectors[i].SetPalette(pal);

				this.Loaded += delegate {
					this.MaxHeight = this.ActualHeight;
					this.MaxWidth = this.ActualWidth;
				};
			}
		}
	}
}

