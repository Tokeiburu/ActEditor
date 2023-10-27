using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Scripts;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Tools.GrfShellExplorer;
using ErrorManager;
using GRF.Core.GroupedGrf;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.System;
using GRF.Threading;
using GrfToWpfBridge;
using GrfToWpfBridge.Application;
using GrfToWpfBridge.MultiGrf;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.CommandLine;
using Utilities.Extension;
using Utilities.Services;

namespace ActEditor.Core {
	/// <summary>
	/// Interaction logic for ActEditorWindow.xaml
	/// </summary>
	public partial class ActEditorWindow : TkWindow {
		#region Delegates

		public delegate void ActEditorEventDelegate(object sender);

		#endregion

		public static ActEditorWindow Instance { get; private set; }

		private readonly MultiGrfReader _metaGrf = new MultiGrfReader();
		private readonly MetaGrfResourcesViewer _metaGrfViewer = new MetaGrfResourcesViewer();
		private readonly WpfRecentFiles _recentFiles;
		private readonly ScriptLoader _scriptLoader;
		private readonly TabEngine _tabEngine;

		public RecentFilesManager RecentFiles {
			get { return _recentFiles; }
		}

		public ActEditorWindow()
			: base("Act Editor", "app.ico", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			Instance = this;
			Spr.EnableImageSizeCheck = false;
			_parseCommandLineArguments(false);

			DataContext = this;
			var diag = new SplashWindow();
			diag.Display = "Initializing components...";
			diag.Show();

			Title = "Act Editor";
			SizeToContent = SizeToContent.WidthAndHeight;

			InitializeComponent();
			_tabEngine = new TabEngine(_tabControl, this);
			((TkMenuItem)_miAnchor.Items[0]).IsChecked = true;
			diag.Display = "Loading scripting engine...";

			_scriptLoader = new ScriptLoader();

			// Set min size on loaded
			Loaded += delegate {
				SizeToContent = SizeToContent.Manual;
				Top = (SystemParameters.FullPrimaryScreenHeight - ActualHeight) / 2;
				Left = (SystemParameters.FullPrimaryScreenWidth - ActualWidth) / 2;
				MinHeight = MinHeight + 50;
			};

			diag.Display = "Setting components...";

			_recentFiles = new WpfRecentFiles(ActEditorConfiguration.ConfigAsker, 6, _miOpenRecent, "Act");
			_recentFiles.FileClicked += new RecentFilesManager.RFMFileClickedEventHandler(_recentFiles_FileClicked);

			diag.Display = "Loading Act Editor's scripts...";

			_loadMenu();

			DragEnter += new DragEventHandler(_actEditorWindow_DragEnter);
			Drop += new DragEventHandler(_actEditorWindow_Drop);

			Loaded += delegate {
				diag.Display = "Loading custom scripts...";

				try {
					ScriptLoader.VerifyExampleScriptsInstalled();
					_scriptLoader.AddScriptsToMenu(this, _mainMenu, _dpUndoRedo);
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}

				try {
					if (!_parseCommandLineArguments()) {
						if (_recentFiles.Files.Count > 0 && ActEditorConfiguration.ReopenLatestFile && _recentFiles.Files[0].IsExtension(".act") && File.Exists(new TkPath(_recentFiles.Files[0]).FilePath))
							_open(_recentFiles.Files[0], false, true);
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}

				diag.Terminate(500);
			};

			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Z", "ActEditor.Undo"), Undo, this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Y", "ActEditor.Redo"), Redo, this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Right", "FrameEditor.NextFrame"), () => _tabEngine.FrameMove(1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Left", "FrameEditor.PreviousFrame"), () => _tabEngine.FrameMove(-1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Alt-Right", "FrameEditor.NextAction"), () => _tabEngine.ActionMove(1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Alt-Left", "FrameEditor.PreviousAction"), () => _tabEngine.ActionMove(-1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Delete", "LayerEditor.DeleteSelected"), () => _tabEngine.Execute(v => v._rendererPrimary.InteractionEngine.Delete()), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Alt-P", "Dialog.StyleEditor"), () => WindowProvider.Show(new StyleEditor(), new Control()), this);

			try {
				ApplicationShortcut.OverrideBindings(ActEditorConfiguration.Remapper);
			}
			catch (Exception err) {
				try {
					ActEditorConfiguration.Remapper.Clear();
					ApplicationShortcut.OverrideBindings(ActEditorConfiguration.Remapper);
				}
				catch {
				}

				ErrorHandler.HandleException("Failed to load the custom key bindings. The bindings will be reset to their default values.", err);
			}

			_miReverseAnchors.Checked += (e, s) => _tabEngine.ReverseAnchorChecked();
			_miReverseAnchors.Unchecked += (e, s) => _tabEngine.ReverseAnchorUnchecked();
			_miReverseAnchors.IsChecked = ActEditorConfiguration.ReverseAnchor;

			_metaGrfViewer.SaveResourceMethod = delegate(string resources) {
				ActEditorConfiguration.Resources = Methods.StringToList(resources);
				_metaGrfViewer.LoadResourcesInfo();
				_metaGrf.Update(_metaGrfViewer.Paths);
			};

			_metaGrfViewer.LoadResourceMethod = () => ActEditorConfiguration.Resources;
			_metaGrfViewer.LoadResourcesInfo();
			GrfThread.Start(() => {
				try {
					_metaGrf.Update(_metaGrfViewer.Paths);
				}
				catch {
				}
			}, "ActEditor - MetaGrf loader");

			TemporaryFilesManager.UniquePattern("new_{0:0000}");

			EncodingService.SetDisplayEncoding(ActEditorConfiguration.EncodingCodepage);

			_tabControl.SelectionChanged += delegate {
				if (_tabControl.SelectedIndex < 0)
					return;

				var tabItem = _tabControl.Items[_tabControl.SelectedIndex] as TabAct;

				if (tabItem != null) {
					_tmbUndo.SetUndo(tabItem.Act.Commands);
					_tmbRedo.SetRedo(tabItem.Act.Commands);
				}
			};

			Loaded += delegate {
				if (_tabControl.Items.Count == 0) {
					_miNew_Click(null, null);
				}
			};
		}

		private void Undo() {
			_tabEngine.Undo();
		}

		private void Redo() {
			_tabEngine.Redo();
		}

		public ScriptLoader ScriptLoader {
			get { return _scriptLoader; }
		}

		public MultiGrfReader MetaGrf {
			get { return _metaGrf; }
		}

		public TabEngine TabEngine { get { return _tabEngine; } }

		private void _loadMenu() {
			_scriptLoader.AddScriptsToMenu(new SpriteExportNormal(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new SpriteExport(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new EditSelectAll(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EditDeselectAll(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new InvertSelection(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new BringToFront(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new BringToBack(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new EditSound(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new EditBackground(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new EditClearPalette(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EditPalette(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EditPaletteAdvanced(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ImportPaletteFrom(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new EditAnchor(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[2]).Items.Add(new Separator());
			((MenuItem)_mainMenu.Items[2]).Items.Add(new TkMenuItem { Header = "Set anchors", IconPath = "forward.png" });
			_scriptLoader.AddScriptsToMenu(new ImportAnchor(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ImportDefaultMaleAnchor(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ImportDefaultFemaleAnchor(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new ActionCopy(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionPaste(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionDelete(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionInsertAt(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionSwitchSelected(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionCopyAt(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionAdvanced(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionCopyMirror(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionMirrorVertical(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionMirrorHorizontal(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionLayerMove(ActionLayerMove.MoveDirection.Down, null), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionLayerMove(ActionLayerMove.MoveDirection.Up, null), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new FrameDelete(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameInsertAt(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameSwitchSelected(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyAt(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[4]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new FrameAdvanced(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[4]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new FrameDuplicate(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameAddLayerToAllFrames(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[4]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new FrameMirrorVertical(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameMirrorHorizontal(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new FrameCopyBrB(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyBBl(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyBBr(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyBlB(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ReverseAnimation(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyHead(this), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyHead2(this), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyGarment(this), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[5]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new InterpolationAnimation(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new LayerInterpolationAnimation(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[5]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new InterpolationAnimationAdv(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new EffectFadeAnimation(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EffectReceivingHit(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EffectStrokeSilouhette(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EffectBreathing(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new ScriptRunnerMenu(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[7]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new OpenScriptsFolder(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ReloadScripts { ActEditor = this }, this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new BatchScriptMenu(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[7]).Items.Add(new Separator());

			Binder.Bind(_miViewSameAction, () => ActEditorConfiguration.KeepPreviewSelectionFromActionChange);
		}

		private bool _parseCommandLineArguments(bool init = true) {
			try {
				List<GenericCLOption> options = CommandLineParser.GetOptions(Environment.CommandLine, false);

				foreach (GenericCLOption option in options) {
					if (init) {
						if (option.CommandName == "-REM" || option.CommandName == "REM") {
							return true;
						}
						else {
							if (option.Args.Count <= 0)
								continue;

							if (option.Args.All(p => p.GetExtension() == ".act")) {
								_open(option.Args[0], false, true);
								return true;
							}
						}
					}
					else {
						if (option.Args.Count <= 0)
							continue;

						if (option.Args.All(p => p.GetExtension() == ".spr")) {

							return true;
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			return false;
		}

		private void _actEditorWindow_Drop(object sender, DragEventArgs e) {
			try {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];
					_tabEngine.LastOpened = "";

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".act"))) {
						foreach (var file in files.Where(p => p.IsExtension(".act"))) {
							_open(file, false, false);
						}

						_tabEngine.Focus(_tabEngine.LastOpened);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _actEditorWindow_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
				string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

				if (files != null && files.Length > 0 && files.Any(p => p.IsExtension("*.act"))) {
					e.Effects = DragDropEffects.All;
				}
			}
		}

		protected override void OnClosing(CancelEventArgs e) {
			try {
				var tabs = _tabControl.Items.OfType<TabAct>().ToList();

				foreach (var tab in tabs) {
					if (tab.Act.Commands.IsModified) {
						tab.IsSelected = true;

						if (!_tabEngine.CloseAct(tab)) {
							e.Cancel = true;
							return;
						}
					}
				}

				if (tabs.Any(tab => !_tabEngine.CloseAct(tab))) {
					e.Cancel = true;
					return;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			base.OnClosing(e);
			ApplicationManager.Shutdown();
		}

		private void _recentFiles_FileClicked(string file) {
			_open(file, false, true);
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
		}

		private void _open(string file, bool isNew, bool focus) {
			_open(new TkPath(file), isNew, focus);
		}

		private void _open(TkPath file, bool isNew, bool focus) {
			if (focus) {
				_tabEngine.LastOpened = "";
			}

			_tabEngine.Open(file, isNew);

			if (focus) {
				_tabEngine.Focus(_tabEngine.LastOpened);
			}
		}

		private void _miOpen_Click(object sender, RoutedEventArgs e) {
			try {
				string file = TkPathRequest.OpenFile<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.Act));

				if (file != null) {
					_open(file, false, true);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miSettings_Click(object sender, RoutedEventArgs e) {
			WindowProvider.Show(new ActEditorSettings(_metaGrfViewer), _miSettings, this);
		}

		private void _miClose_Click(object sender, RoutedEventArgs e) {
			try {
				var tabs = _tabControl.Items.OfType<TabAct>().ToList();

				foreach (var tab in tabs) {
					if (tab.Act.Commands.IsModified) {
						tab.IsSelected = true;

						if (!_tabEngine.CloseAct(tab)) {
							return;
						}
					}
				}

				if (tabs.Any(tab => !_tabEngine.CloseAct(tab))) {
					return;
				}

				Close();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miNew_Click(object sender, RoutedEventArgs e) {
			try {
				var act = new Act(new Spr());
				act.AddAction();

				string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

				act.LoadedPath = fileName + ".act";
				act.Sprite.Converter.Save(act.Sprite, fileName + ".spr");
				act.Save(fileName + ".act");

				_open(fileName + ".act", true, true);
				_recentFiles.RemoveRecentFile(fileName + ".act");
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		public bool Save() {
			return _tabEngine.Save();
		}

		private void _miSave_Click(object sender, RoutedEventArgs e) {
			Save();
		}

		public bool SaveAs() {
			return _tabEngine.SaveAs();
		}

		private void _miSaveAs_Click(object sender, RoutedEventArgs e) {
			SaveAs();
		}

		private void _miSaveAsGarment_Click(object sender, RoutedEventArgs e) {
			try {
				var dialog = new SaveGarmentDialog(this);

				dialog.Owner = WpfUtilities.TopWindow;

				dialog.Closing += delegate {
					dialog.Owner.Focus();
					_miSaveAsGarment.IsEnabled = true;
				};

				_miSaveAsGarment.IsEnabled = false;
				dialog.Show();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miCloseCurrent_Click(object sender, RoutedEventArgs e) {
			try {
				_tabEngine.CloseAct();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miAbout_Click(object sender, RoutedEventArgs e) {
			var dialog = new AboutDialog(ActEditorConfiguration.PublicVersion, ActEditorConfiguration.RealVersion, ActEditorConfiguration.Author, ActEditorConfiguration.ProgramName);
			dialog.Owner = WpfUtilities.TopWindow;
			((TextBox)dialog.FindName("_textBlock")).Text += "\r\n\r\nCredits : Nebraskka (suggestions and feedback)";
			dialog.AboutTextBox.Background = this.FindResource("UIThemeAboutDialogBrush") as Brush;
			dialog.ShowDialog();
			_tabEngine.RestoreFocus();
		}

		private void _miOpenFromGrf_Click(object sender, RoutedEventArgs e) {
			try {
				string file = TkPathRequest.OpenFile<ActEditorConfiguration>("AppLastGrfPath", "filter", FileFormat.MergeFilters(Format.AllContainers, Format.Grf, Format.Gpf, Format.Thor));

				if (file != null) {
					GrfExplorer dialog = new GrfExplorer(file, SelectMode.Act);
					dialog.Owner = WpfUtilities.TopWindow;

					if (dialog.ShowDialog() == true) {
						string relativePath = dialog.SelectedItem;

						if (relativePath == null) return;

						if (!relativePath.IsExtension(".act")) {
							throw new Exception("Only ACT files can be selected.");
						}

						_open(new TkPath(file, relativePath), false, true);
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miCopy_Click(object sender, RoutedEventArgs e) {
			_tabEngine.Copy();
		}

		private void _miPaste_Click(object sender, RoutedEventArgs e) {
			_tabEngine.Paste();
		}

		private void _miCut_Click(object sender, RoutedEventArgs e) {
			_tabEngine.Cut();
		}

		private void _miAnchor_Click(object sender, RoutedEventArgs e) {
			foreach (MenuItem item in _miAnchor.Items) {
				if (item != sender)
					item.IsChecked = false;
				else {
					item.Checked -= _miAnchor_Click;
					item.IsChecked = true;
					item.Checked += _miAnchor_Click;
				}
			}

			_tabEngine.AnchorUpdate((MenuItem)sender);
		}

		private void _miSelectAct_Click(object sender, RoutedEventArgs e) {
			_tabEngine.Select();
		}

		private void _miShowAnchors_Loaded(object sender, RoutedEventArgs e) {
			_miShowAnchors.IsChecked = ActEditorConfiguration.ShowAnchors;
		}

		private void _miShowAnchors_Checked(object sender, RoutedEventArgs e) {
			ActEditorConfiguration.ShowAnchors = true;
			_tabEngine.RendererUpdate();
		}

		private void _miShowAnchors_Unchecked(object sender, RoutedEventArgs e) {
			ActEditorConfiguration.ShowAnchors = false;
			_tabEngine.RendererUpdate();
		}

		private void _miNewHeadgear_Click(object sender, RoutedEventArgs e) {
			try {
				var act = new Act(ApplicationManager.GetResource("ref_head_f.act"), new Spr());
				act.AllFrames(p => p.Layers.Clear());

				string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

				act.LoadedPath = fileName + ".act";
				act.Sprite.Save(fileName + ".spr");
				act.Save(fileName + ".act");

				_open(fileName + ".act", true, true);
				_recentFiles.RemoveRecentFile(fileName + ".act");
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _new(string name) {
			try {
				var act = new Act(ApplicationManager.GetResource(name), new Spr());

				string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

				act.LoadedPath = fileName + ".act";
				act.Sprite.Save(fileName + ".spr");
				act.Save(fileName + ".act");

				_open(fileName + ".act", true, true);
				_recentFiles.RemoveRecentFile(fileName + ".act");
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miNewNpc_Click(object sender, RoutedEventArgs e) {
			_new("NPC.act");
		}

		private void _miNewWeapon_Click(object sender, RoutedEventArgs e) {
			_new("weapon.act");
		}

		private void _miNewMonster_Click(object sender, RoutedEventArgs e) {
			_new("monster.act");
		}

		private void _miNewHomunculus_Click(object sender, RoutedEventArgs e) {
			_new("homunculus.act");
		}

		private void _miNewHeadgearMale_Click(object sender, RoutedEventArgs e) {
			try {
				var act = new Act(ApplicationManager.GetResource("ref_head_m.act"), new Spr());
				act.AllFrames(p => p.Layers.Clear());

				string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

				act.LoadedPath = fileName + ".act";
				act.Sprite.Save(fileName + ".spr");
				act.Save(fileName + ".act");

				_open(fileName + ".act", true, true);
				_recentFiles.RemoveRecentFile(fileName + ".act");
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miViewPrevAnim_Click(object sender, RoutedEventArgs e) {
			_tabEngine.ShowPreviewFrames();
		}

		public TabAct GetSelectedTab() {
			return _tabEngine.GetCurrentTab();
		}

		public TabAct GetCurrentTab2() {
			var tab = GetSelectedTab();

			if (tab == null)
				throw new Exception("No file opened. Please open an ACT file before using this command.");

			return tab;
		}
	}
}