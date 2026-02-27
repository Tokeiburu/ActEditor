using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Tools.PaletteEditorTool;
using ErrorManager;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.GrfSystem;
using PaletteEditor;
using TokeiLibrary;
using TokeiLibrary.Paths;
using Utilities;
using Utilities.Extension;

namespace ActEditor.Core.Scripting.Scripts {
	public class EditSelectAll : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			ActEditorWindow.Instance.GetCurrentTab2().SelectionEngine.SelectAll();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex, selectedFrameIndex].NumberOfLayers > 0;
		}

		public object DisplayName => "Select all";
		public string Group => "Edit";
		public string InputGesture => "{FrameEditor.SelectAll|Ctrl-A}";
		public string Image => null;

		#endregion
	}

	public class EditDeselectAll : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			ActEditorWindow.Instance.GetCurrentTab2().SelectionEngine.DeselectAll();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex, selectedFrameIndex].NumberOfLayers > 0;
		}

		public object DisplayName => "Deselect all";
		public string Group => "Edit";
		public string InputGesture => "{FrameEditor.DeselectAll|Ctrl-D}";
		public string Image => null;

		#endregion
	}

	public class InvertSelection : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			ActEditorWindow.Instance.GetCurrentTab2().SelectionEngine.SelectReverse();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex, selectedFrameIndex].NumberOfLayers > 0;
		}

		public object DisplayName => "Invert selection";
		public string Group => "Edit";
		public string InputGesture => "{LayerEditor.InvertSelection|Ctrl-Shift-I}";
		public string Image => null;

		#endregion
	}

	public class BringToFront : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			ActEditorWindow.Instance.GetCurrentTab2()._layerEditor.BringToFront();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && selectedLayerIndexes.Length > 0;
		}

		public object DisplayName => "Bring to front";
		public string Group => "Edit";
		public string InputGesture => "{LayerEditor.BringToFront|Ctrl-Shift-F}";
		public string Image => "front.png";

		#endregion
	}

	public class BringToBack : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			ActEditorWindow.Instance.GetCurrentTab2()._layerEditor.BringToBack();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && selectedLayerIndexes.Length > 0;
		}

		public object DisplayName => "Bring to back";
		public string Group => "Edit";
		public string InputGesture => "{LayerEditor.BringToBack|Ctrl-Shift-B}";
		public string Image => "back.png";

		#endregion
	}

	public class EditSound : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			SoundEditDialog dialog = new SoundEditDialog(act);
			dialog.Owner = WpfUtilities.TopWindow;
			dialog.ShowDialog();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName => "Edit sound list...";
		public string Group => "Edit";
		public string InputGesture => "{FrameEditor.EditSoundList}";
		public string Image => "soundOn.png";

		#endregion
	}

	public class EditPalette : IActScript {
		public static bool CanOpen = true;

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act.Sprite.Palette == null) return;
			CanOpen = false;

			var dialog = new SingleColorEditDialog();
			dialog.PreviewKeyDown += delegate {
				if (Keyboard.IsKeyDown(Key.Escape))
					dialog.Close();
			};
			byte[] paletteBefore = Methods.Copy(act.Sprite.Palette.BytePalette);
			Pal pal = new Pal(paletteBefore);
			dialog.SingleColorEditControl.SetPalette(pal);

			bool pendingUpdate = false;

			pal.PaletteChanged += delegate {
				if (pendingUpdate)
					return;

				pendingUpdate = true;

				dialog.Dispatcher.BeginInvoke(new System.Action(() => {
					var newPalette = pal.BytePalette;
					newPalette[3] = 0;

					act.Sprite.Palette.SetPalette(pal.BytePalette);
					act.InvalidateVisual();
					act.InvalidatePaletteVisual();

					pendingUpdate = false;
				}), System.Windows.Threading.DispatcherPriority.Background);
			};

			dialog.Owner = WpfUtilities.TopWindow;

			dialog.Closing += delegate {
				CanOpen = true;
				dialog.Owner.Focus();

				act.Sprite.Palette.SetPalette(paletteBefore);

				if (!Methods.ByteArrayCompare(paletteBefore, 0, 1024, dialog.SingleColorEditControl.PaletteSelector.Palette.BytePalette, 0)) {
					dialog.SingleColorEditControl.PaletteSelector.Palette.BytePalette[3] = 0;
					act.Commands.SpriteSetPalette(dialog.SingleColorEditControl.PaletteSelector.Palette.BytePalette);
					act.InvalidateVisual();
				}
			};

			dialog.Show();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && CanOpen && act.Sprite.NumberOfIndexed8Images > 0;
		}

		public object DisplayName => "Quick palette edit...";
		public string Group => "Edit";
		public string InputGesture => "{SpriteEditor.QuickPaletteEdit|Ctrl-U}";
		public string Image => "pal.png";

		#endregion
	}

	public class EditPaletteAdvanced : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			EditPalette.CanOpen = false;
			var dialog = new PaletteEditorWindow();
			string tempPath = TemporaryFilesManager.GetTemporaryFilePath("tmp_sprite_{0:0000}.spr");
			act.Sprite.Save(tempPath);
			if (dialog.PaletteEditor.Load(tempPath)) {
				dialog.Owner = WpfUtilities.TopWindow;
				dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
				dialog.Closing += delegate {
					EditPalette.CanOpen = true;
					tempPath = TemporaryFilesManager.GetTemporaryFilePath("tmp_sprite_{0:0000}.spr");

					if (act.Sprite.Palette != null && dialog.PaletteEditor.Sprite.Palette != null) {
						dialog.PaletteEditor.Sprite.Palette.EnableRaiseEvents = false;
						dialog.PaletteEditor.Sprite.Palette.BytePalette[3] = 0;
						dialog.PaletteEditor.Sprite.Palette.EnableRaiseEvents = true;
					}

					dialog.PaletteEditor.SaveAs(tempPath);
				};
				dialog.ShowDialog();

				Spr spr = new Spr(tempPath);

				bool sameFile = false;

				if (act.Sprite.Palette != null && spr.Palette != null && act.Sprite.NumberOfImagesLoaded == spr.NumberOfImagesLoaded && act.Sprite.NumberOfIndexed8Images == spr.NumberOfIndexed8Images) {
					if (Methods.ByteArrayCompare(spr.Palette.BytePalette, 0, 1024, act.Sprite.Palette.BytePalette, 0)) {
						sameFile = true;

						for (int i = 0; i < spr.NumberOfIndexed8Images; i++) {
							if (!Methods.ByteArrayCompare(spr.Images[i].Pixels, act.Sprite.Images[i].Pixels)) {
								sameFile = false;
							}
						}
					}
				}

				if (!sameFile) {
					act.Commands.SetSprite(spr);
					act.InvalidateVisual();
				}
			}
			else {
				EditPalette.CanOpen = true;
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act.Sprite.NumberOfIndexed8Images > 0 && EditPalette.CanOpen;
		}

		public object DisplayName => "Palette editor...";
		public string Group => "Edit";
		public string InputGesture => "{SpriteEditor.PaletteEditor|Ctrl-P}";
		public string Image => "pal.png";

		#endregion
	}

	public class ImportPaletteFrom : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				EditPalette.CanOpen = false;
				string file = TkPathRequest.OpenFile<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.PaletteContainers, Format.Pal, Format.Spr));

				if (file != null) {
					if (file.IsExtension(".pal")) {
						act.Commands.SpriteSetPalette(new Pal(File.ReadAllBytes(file)).BytePalette);
						act.InvalidateVisual();
					}
					else if (file.IsExtension(".spr")) {
						Spr spr = new Spr(file);

						if (spr.Palette == null)
							throw new Exception("This sprite doesn't have a palette.");

						act.Commands.SpriteSetPalette(spr.Palette.BytePalette);
						act.InvalidateVisual();
					}
					else if (file.IsExtension(".bmp")) {
						GrfImage image = file;

						if (image.GrfImageType == GrfImageType.Indexed8) {
							act.Commands.SpriteSetPalette(image.Palette);
							act.InvalidateVisual();
						}
						else {
							throw new Exception("Invalid image format. Only bitmap files with palettes are allowed.");
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				EditPalette.CanOpen = true;
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && EditPalette.CanOpen && act.Sprite.NumberOfIndexed8Images > 0;
		}

		public object DisplayName => "Import palette...";
		public string Group => "Edit";
		public string InputGesture => null;
		public string Image => null;

		#endregion
	}

	public class EditBackground : IActScript {
		public object DisplayName {
			get {
				Grid grid = new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition());
				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(-1, GridUnitType.Auto) });
				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(-1, GridUnitType.Auto) });

				Label label = new Label { Content = "Select background", Padding = new Thickness(0), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
				grid.Children.Add(label);
				label.SetValue(Grid.ColumnProperty, 0);

				Image img = new Image { Source = ApplicationManager.PreloadResourceImage("reset.png"), Width = 16, Height = 16, Stretch = Stretch.None, ToolTip = "Resets the background to its original value." };
				img.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
				grid.Children.Add(img);
				img.SetValue(Grid.ColumnProperty, 2);

				img.MouseEnter += (s, e) => Mouse.OverrideCursor = Cursors.Hand;
				img.MouseLeave += (s, e) => Mouse.OverrideCursor = null;
				img.PreviewMouseDown += (s, e) => { e.Handled = true; _resetBackground(); };
				img.PreviewMouseUp += (s, e) => { e.Handled = true; _resetBackground(); };

				return grid;
			}
		}

		public string Group => "Edit";
		public string InputGesture => "{FrameEditor.EditBackground}";
		public string Image => "background.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			string path = TkPathRequest.OpenFile(new Setting(null, typeof (ActEditorConfiguration).GetProperty("BackgroundPath")), "filter", FileFormat.MergeFilters(Format.Image));

			var tabs = ActEditorWindow.Instance._tabControl.Items.OfType<TabAct>();

			foreach (var tab in tabs) {
				tab.LoadBackground(path);
			}
		}

		private void _resetBackground() {
			var tabs = ActEditorWindow.Instance._tabControl.Items.OfType<TabAct>();

			foreach (var tab in tabs) {
				tab.ResetBackground();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return true;
		}
	}

	public class EditClearPalette : IActScript {
		public object DisplayName => "Clear palette";
		public string Group => "Edit";
		public string InputGesture => "{Sprite.ClearPalette|Ctrl-Shift-L}";
		public string Image => "delete.png";

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			try {
				act.Commands.Begin();
				act.Commands.Backup(_ => {
					byte[] palette = new byte[1024];

					for (int i = 0; i < 1024; i += 4) {
						palette[i + 3] = 255;
					}

					palette[0] = 255;
					palette[2] = 255;

					act.Sprite.Palette.SetPalette(palette);
				}, (string)DisplayName);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act.Sprite.Palette != null;
		}
	}
}