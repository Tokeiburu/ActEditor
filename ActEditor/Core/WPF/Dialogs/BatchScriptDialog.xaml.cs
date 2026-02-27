using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Scripting;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities.Extension;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for TestSheetDialog.xaml
	/// </summary>
	public partial class BatchScriptDialog : TkWindow {
		private readonly ActEditorWindow _editor;
		private string _group;
		private string _displayName;

		public BatchScriptDialog()
			: base("add.png", "add.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
		}

		public BatchScriptDialog(ActEditorWindow editor)
			: base("add.png", "add.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_editor = editor;
			InitializeComponent();

			_buttonScript.ContextMenu = new ContextMenu();
			_buttonScript.ContextMenu.Placement = PlacementMode.Bottom;
			_buttonScript.ContextMenu.PlacementTarget = _buttonScript;
			_buttonScript.PreviewMouseRightButtonUp += _disableButton;
			_buttonScript.Click += delegate {
				_buttonScript.ContextMenu.IsOpen = true;
			};

			Binder.Bind(_cbCurrentFolder, () => ActEditorConfiguration.ActEditorExportCurrentFolder, v => ActEditorConfiguration.ActEditorExportCurrentFolder = v, delegate {
				if (ActEditorConfiguration.ActEditorExportCurrentFolder == true) {
					ActEditorConfiguration.ActEditorExportCurrentSprite = false;
				}

				_pathBrowserSource.IsEnabled = false;

				if (ActEditorConfiguration.ActEditorExportCurrentSprite == false && ActEditorConfiguration.ActEditorExportCurrentFolder == false) {
					_pathBrowserSource.IsEnabled = true;
				}
			}, true);

			WpfUtilities.AddMouseInOutUnderline(_cbCurrentFolder);

			_pathBrowserSource.Loaded += delegate {
				if (_pathBrowserSource.RecentFiles.Files.Count > 0) {
					_pathBrowserSource.Text = _pathBrowserSource.RecentFiles.Files[0];
				}
			};

			foreach (var group in ScriptLoader.CompiledScripts) {
				MenuItem menuItem = new MenuItem();

				menuItem.Header = group.Key;

				foreach (var actScript in group.Value.Values) {
					MenuItem scriptMenu = new MenuItem();
					var script = actScript;

					scriptMenu.Header = ActScriptLoader.GenerateHeader(actScript);

					if (actScript.Image != null) scriptMenu.Icon = new Image { Source = ActScriptLoader.GetScriptImage(actScript.Image) };

					scriptMenu.Click += delegate {
						_group = script.Group;
						_displayName = script.DisplayName.ToString();
						_buttonScript.Content = script.Group + " > " + script.DisplayName;
					};

					menuItem.Items.Add(scriptMenu);
				}

				_buttonScript.ContextMenu.Items.Add(menuItem);
			}
		}

		private void _disableButton(object sender, MouseButtonEventArgs e) {
			e.Handled = true;
		}

		private void _buttonOK_Click(object sender, RoutedEventArgs e) {
			try {
				_pathBrowserSource.RecentFiles.AddRecentFile(_pathBrowserSource.Text);

				var previousWarning = Configuration.WarningLevel;

				try {
					// Hide errors
					Configuration.WarningLevel = (ErrorLevel)4;
					InputDialog.SkipAndRememberInput = true;
					EffectConfiguration.SkipAndRememberInput = 1;

					List<Act> acts = new List<Act>();

					if (ActEditorConfiguration.ActEditorExportCurrentFolder) {
						var tab = _editor.TabEngine.GetCurrentTab();

						if (tab == null || tab.Act == null) {
							throw new Exception("No Act file is currently in use, open an Act first if you are using the 'Current sprite' option.");
						}

						foreach (var actFile in Directory.GetFiles(GrfPath.GetDirectoryName(tab.Act.LoadedPath), "*.act")) {
							try {
								var act = new Act(actFile, actFile.ReplaceExtension(".spr"));

								acts.Add(act);
							}
							catch {
							}
						}
					}
					else {
						foreach (var actFile in Directory.GetFiles(_pathBrowserSource.Text, "*.act")) {
							try {
								var act = new Act(actFile, actFile.ReplaceExtension(".spr"));

								acts.Add(act);
							}
							catch {
							}
						}
					}

					_execute(acts);
				}
				finally {
					InputDialog.SkipAndRememberInput = false;
					EffectConfiguration.SkipAndRememberInput = 0;
					Configuration.WarningLevel = previousWarning;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _execute(List<Act> acts) {
			int i = 0;

			TaskManager.DisplayTaskC("Execute", "Executing batch script...", () => i, acts.Count, isCancelling => {
				try {
					IActScript actScript = _fetchActScript();

					for (; i < acts.Count; i++) {
						var act = acts[i];

						this.Dispatch(delegate {
							actScript.Execute(act, 0, 0, new int[] { 0 });
							act.SaveWithSprite(act.LoadedPath, act.LoadedPath.ReplaceExtension(".spr"));
						});
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
				finally {
					i = acts.Count;
				}
			});
		}

		private IActScript _fetchActScript() {
			foreach (var actScript in ScriptLoader.CompiledScripts[_group].Values) {
				if (actScript.DisplayName.ToString() == _displayName)
					return actScript;
			}

			throw new Exception("Unable to find the specified script: " + _group + " > " + _displayName + ". Perhaps the scripts have been reloaded? Re-open this window to reload the listing.");
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}
