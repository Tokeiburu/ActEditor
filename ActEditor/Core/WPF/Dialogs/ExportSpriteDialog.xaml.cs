﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities.Extension;
using Imaging = ActImaging.Imaging;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for TestSheetDialog.xaml
	/// </summary>
	public partial class ExportSpriteDialog : TkWindow {
		private readonly ActEditorWindow _editor;

		public ExportSpriteDialog()
			: base("add.png", "add.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
		}

		public ExportSpriteDialog(ActEditorWindow editor)
			: base("add.png", "add.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_editor = editor;
			InitializeComponent();
			
			_buttonPreset.ContextMenu.Placement = PlacementMode.Bottom;
			_buttonPreset.ContextMenu.PlacementTarget = _buttonPreset;
			_buttonPreset.PreviewMouseRightButtonUp += _disableButton;
			_buttonPreset.Click += delegate {
				_buttonPreset.ContextMenu.IsOpen = true;
			};

			Binder.Bind(_cbCurrentSprite, () => ActEditorConfiguration.ActEditorExportCurrentSprite, v => ActEditorConfiguration.ActEditorExportCurrentSprite = v, delegate {
				if (ActEditorConfiguration.ActEditorExportCurrentSprite == true) {
					ActEditorConfiguration.ActEditorExportCurrentFolder = false;

					if (_cbCurrentFolder.IsChecked == true) {
						_cbCurrentFolder.IsChecked = false;
					}
				}

				_pathBrowserSource.IsEnabled = false;

				if (ActEditorConfiguration.ActEditorExportCurrentSprite == false && ActEditorConfiguration.ActEditorExportCurrentFolder == false) {
					_pathBrowserSource.IsEnabled = true;
				}
			}, true);

			Binder.Bind(_cbCurrentFolder, () => ActEditorConfiguration.ActEditorExportCurrentFolder, v => ActEditorConfiguration.ActEditorExportCurrentFolder = v, delegate {
				if (ActEditorConfiguration.ActEditorExportCurrentFolder == true) {
					ActEditorConfiguration.ActEditorExportCurrentSprite = false;

					if (_cbCurrentSprite.IsChecked == true) {
						_cbCurrentSprite.IsChecked = false;
					}
				}

				_pathBrowserSource.IsEnabled = false;

				if (ActEditorConfiguration.ActEditorExportCurrentSprite == false && ActEditorConfiguration.ActEditorExportCurrentFolder == false) {
					_pathBrowserSource.IsEnabled = true;
				}
			}, true);

			WpfUtils.AddMouseInOutEffectsBox(_cbCurrentSprite, _cbCurrentFolder);

			_pathBrowserOutput.Loaded += delegate {
				if (_pathBrowserOutput.RecentFiles.Files.Count > 0) {
					_pathBrowserOutput.Text = _pathBrowserOutput.RecentFiles.Files[0];
				}

				if (_pathBrowserOutput.Text == "") {
					_pathBrowserOutput.Text = @"{CURRENT_FOLDER}\{NAME}_{ID}.png";
				}
			};

			_pathBrowserSource.Loaded += delegate {
				if (_pathBrowserSource.RecentFiles.Files.Count > 0) {
					_pathBrowserSource.Text = _pathBrowserSource.RecentFiles.Files[0];
				}
			};
		}

		private void _disableButton(object sender, MouseButtonEventArgs e) {
			e.Handled = true;
		}

		private void _pathBrowserSource_TextChanged(object sender, EventArgs e) {

		}

		private void _pathBrowserOutput_TextChanged(object sender, EventArgs e) {
			if (!_pathBrowserOutput.Text.Contains("{ID}") && !_pathBrowserOutput.Text.Contains(".gif")) {
				_pathBrowserOutput.Text = GrfPath.Combine(_pathBrowserOutput.Text, @"{NAME}_{ID}.png");
			}
		}

		public class ActProvider : IEnumerable<Act> {
			private readonly List<string> _actPaths;
			private Act _directAct;

			public ActProvider(Act act) {
				_directAct = act;
			}

			public ActProvider(List<string> actPaths) {
				_actPaths = actPaths;
			}

			public int GetTotalImagesCount() {
				if (_directAct != null)
					return _directAct.Sprite.NumberOfImagesLoaded;

				int total = 0;

				foreach (var path in _actPaths) {
					var sprPath = path.ReplaceExtension(".spr");

					if (!File.Exists(sprPath))
						continue;

					var data = File.ReadAllBytes(sprPath);
					total += BitConverter.ToUInt16(data, 4) + BitConverter.ToUInt16(data, 6);
				}

				return total;
			}

			public IEnumerator<Act> GetEnumerator() {
				if (_directAct != null) {
					yield return _directAct;
				}
				else {
					foreach (var path in _actPaths) {
						var sprPath = path.ReplaceExtension(".spr");

						if (!File.Exists(sprPath))
							continue;

						yield return new Act(path, sprPath);
					}
				}
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return GetEnumerator();
			}
		}

		private void _buttonOK_Click(object sender, RoutedEventArgs e) {
			try {
				_pathBrowserSource.RecentFiles.AddRecentFile(_pathBrowserSource.Text);
				_pathBrowserOutput.RecentFiles.AddRecentFile(_pathBrowserOutput.Text);

				string formatSprite = "{1:0000}";
				string formatName = "{0}";
				string formatExt = "{2}";
				string formatCurrentFolder = "{3}";
				string output = _pathBrowserOutput.Text.Replace("{NAME}", formatName).Replace("{ID}", formatSprite).Replace("{EXT}", formatExt).Replace("{CURRENT_FOLDER}", formatCurrentFolder);
				List<Act> acts = new List<Act>();
				ActProvider provider = null;

				if (ActEditorConfiguration.ActEditorExportCurrentSprite) {
					var tab = _editor.TabEngine.GetCurrentTab();

					if (tab == null || tab.Act == null) {
						throw new Exception("No Act file is currently in use, open an Act first if you are using the 'Current sprite' option.");
					}

					provider = new ActProvider(tab.Act);

					_export(provider, output, false);
				}
				else if (ActEditorConfiguration.ActEditorExportCurrentFolder) {
					var tab = _editor.TabEngine.GetCurrentTab();

					if (tab == null || tab.Act == null) {
						throw new Exception("No Act file is currently in use, open an Act first if you are using the 'Current sprite' option.");
					}

					provider = new ActProvider(Directory.GetFiles(GrfPath.GetDirectoryName(tab.Act.LoadedPath), "*.act").ToList());
					_export(provider, output);
				}
				else {
					provider = new ActProvider(Directory.GetFiles(_pathBrowserSource.Text, "*.act").ToList());
					_export(provider, output);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _export(ActProvider provider, string outputPath, bool async = true) {
			int i = 0;
			int numberOfImages = provider.GetTotalImagesCount();

			if (async) {
				TaskManager.DisplayTaskC("Export", "Exporting sprites...", () => i, numberOfImages, isCancelling => {
					try {
						foreach (var act in provider) {
							if (isCancelling())
								return;

							if (outputPath.IsExtension(".gif")) {
								_exportSub(null, 0, outputPath, act);
								i += act.Sprite.NumberOfImagesLoaded;
							}
							else {
								for (int k = 0; k < act.Sprite.NumberOfImagesLoaded; k++) {
									_exportSub(act.Sprite.Images[k], k, outputPath, act);
									i++;
								}
							}
						}
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
					finally {
						i = numberOfImages;
					}
				});
			}
			else {
				foreach (var act in provider) {
					if (outputPath.IsExtension(".gif")) {
						_exportSub(null, 0, outputPath, act);
						i += act.Sprite.NumberOfImagesLoaded;
					}
					else {
						for (int k = 0; k < act.Sprite.NumberOfImagesLoaded; k++) {
							_exportSub(act.Sprite.Images[k], k, outputPath, act);
							i++;
						}
					}
				}
			}
		}

		private void _exportSub(GrfImage im, int k, string outputPath, Act act) {
			if (im != null)
				im = im.Copy();

			string path = GrfPath.Combine(String.Format(outputPath, Path.GetFileNameWithoutExtension(act.LoadedPath), k, im == null ? "gif" : (im.GrfImageType == GrfImageType.Indexed8 ? "bmp" : "png"), Path.GetDirectoryName(act.LoadedPath)));

			if (path.EndsWith(".gif")) {
				List<string> extra = new List<string>();

				extra.Add("indexFrom");
				extra.Add("0");

				extra.Add("indexTo");
				extra.Add("" + act[0].Frames.Count);

				extra.Add("uniform");
				extra.Add(ActEditorConfiguration.ActEditorGifUniform.ToString());

				extra.Add("background");
				extra.Add(ActEditorConfiguration.ActEditorGifBackgroundColor.ToGrfColor().ToHexString().Replace("0x", "#"));

				extra.Add("guideLinesColor");
				extra.Add(ActEditorConfiguration.ActEditorGifGuidelinesColor.ToGrfColor().ToHexString().Replace("0x", "#"));

				extra.Add("scaling");
				extra.Add(ActEditorConfiguration.ActEditorScalingMode.ToString());

				extra.Add("delay");
				extra.Add("" + act[0].Interval);

				extra.Add("delayFactor");
				extra.Add("1");

				extra.Add("margin");
				extra.Add(ActEditorConfiguration.ActEditorGifMargin.ToString(CultureInfo.InvariantCulture));

				Imaging.SaveAsGif(path, act, 0, null, extra.ToArray());

				return;
			}

			if (path.EndsWith(".png")) {
				im.Convert(GrfImageType.Bgra32);
			}

			im.Save(path);
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _miPreset_Click(object sender, RoutedEventArgs e) {
			_pathBrowserOutput.Text = ((MenuItem)sender).Header.ToString().ReplaceAll("__", "_");
		}
	}
}
