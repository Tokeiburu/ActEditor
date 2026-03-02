using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
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
		private List<PaletteSelector> _selectors;

		public PalettePreset()
			: base("Palette selector", "pal.png", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			InitializeComponent();
			WindowStyle = System.Windows.WindowStyle.ToolWindow;

			_selectors = new List<PaletteSelector> {
				_paletteSelector0,
				_paletteSelector1,
				_paletteSelector2,
				_paletteSelector3,
			};

			for (int i = 0; i < _selectors.Count; i++) {
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

				_selectors[i].IsMultipleColorsSelectable = true;

				int current = i;

				_selectors[i].PreviewMouseMove += delegate {
					if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) {
						_selectors[current].UseLargeSelector = false;
					}
					else {
						_selectors[current].UseLargeSelector = true;
					}
				};
				
				_selectors[i].UseLargeSelector = true;
				_selectors[i].SetPalette(pal);

				this.Loaded += delegate {
					this.MaxHeight = this.ActualHeight;
					this.MaxWidth = this.ActualWidth;
				};
			}

			ApplicationShortcut.Link(ActEditorCommands.Undo, Undo, this);
			ApplicationShortcut.Link(ActEditorCommands.Redo, Redo, this);
		}

		public void Undo() {
			foreach (var selector in _selectors) {
				selector.Palette.Commands.Undo();
			}
		}

		public void Redo() {
			foreach (var selector in _selectors) {
				selector.Palette.Commands.Redo();
			}
		}
	}
}

