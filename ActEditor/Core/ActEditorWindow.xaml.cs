using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Scripts;
using ActEditor.Core.Scripts.Effects;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Tools.GrfShellExplorer;
using ErrorManager;
using GRF.Core.GroupedGrf;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.GrfSystem;
using GRF.Image;
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
		private EditorPosition _editorPosition = new EditorPosition();
		private SplashWindow _splashWindow;

		public RecentFilesManager RecentFiles {
			get { return _recentFiles; }
		}

		public ActEditorWindow()
			: base("Act Editor", "app.ico", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			Instance = this;
			_initializeSplashWindow();
			
			InitializeComponent();

			_tabEngine = new TabEngine(_tabControl, this);
			_scriptLoader = new ScriptLoader(_mainMenu, _dpUndoRedo);
			_recentFiles = new WpfRecentFiles(ActEditorConfiguration.ConfigAsker, 6, _miOpenRecent, "Act");
			_recentFiles.FileClicked += new RecentFilesManager.RFMFileClickedEventHandler(_recentFiles_FileClicked);

			_initializeMenu();
			_initializeShortcuts();
			_miReverseAnchors.Checked += (e, s) => _tabEngine.ReverseAnchorChecked();
			_miReverseAnchors.Unchecked += (e, s) => _tabEngine.ReverseAnchorUnchecked();
			_miReverseAnchors.IsChecked = ActEditorConfiguration.ReverseAnchor;
			_initializeMetaGrf();
		}

		private void _actEditorWindow_Loaded(object sender, RoutedEventArgs e) {
			// Allow modifying size on load
			SizeToContent = SizeToContent.Manual;
			_editorPosition.Load(this);

			_initializeScripts();

			try {
				if (!_parseCommandLineArguments()) {
					if (_recentFiles.Files.Count > 0 && ActEditorConfiguration.ReopenLatestFile && _recentFiles.Files[0].IsExtension(".act") && File.Exists(new TkPath(_recentFiles.Files[0]).FilePath))
						_tabEngine.Open(_recentFiles.Files[0]);
				}
			}
			catch (Exception err) {
				ShowException(_splashWindow, err);
			}

			if (_tabControl.Items.Count == 0) {
				_miNew_Click(null, null);
			}

			_splashWindow.Terminate(400);
		}

		private void _tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_tabControl.SelectedIndex < 0)
				return;

			if (_tabControl.Items[_tabControl.SelectedIndex] is TabAct tabItem) {
				_tmbUndo.SetUndo(tabItem.Act.Commands);
				_tmbRedo.SetRedo(tabItem.Act.Commands);
			}
		}

		private void _initializeMetaGrf() {
			_metaGrfViewer.SaveResourceMethod = delegate (string resources) {
				ActEditorConfiguration.Resources = Methods.StringToList(resources);
				_metaGrfViewer.LoadResourcesInfo();
				_metaGrf.Update(_metaGrfViewer.Paths);
			};

			_metaGrfViewer.LoadResourceMethod = () => ActEditorConfiguration.Resources.Select(p => new MultiGrfPath(p) { FromConfiguration = true, IsCurrentlyLoadedGrf = false }).ToList();
			_metaGrfViewer.LoadResourcesInfo();
			GrfThread.Start(() => {
				try {
					_metaGrf.Update(_metaGrfViewer.Paths);
				}
				catch {
				}
			}, "ActEditor - MetaGrf loader");
		}

		private void _initializeShortcuts() {
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Z", "ActEditor.Undo"), Undo, this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Y", "ActEditor.Redo"), Redo, this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Right", "FrameEditor.NextFrame"), () => _tabEngine.FrameMove(1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Left", "FrameEditor.PreviousFrame"), () => _tabEngine.FrameMove(-1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Shift-Right", "FrameEditor.NextAction"), () => _tabEngine.ActionMove(1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Shift-Left", "FrameEditor.PreviousAction"), () => _tabEngine.ActionMove(-1), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Delete", "LayerEditor.DeleteSelected"), () => _tabEngine.Execute(v => v._rendererPrimary.InteractionEngine.Delete()), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-Alt-P", "Dialog.StyleEditor"), () => WindowProvider.Show(new StyleEditor(), new Control()), this);
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Space", "ActEditor.PlayStopAnimation"), () => _tabEngine.Execute(v => {
				if (v._frameSelector.IsPlaying)
					v._frameSelector.Stop();
				else
					v._frameSelector.Play();
			}), this);
			//ApplicationShortcut.Link(ApplicationShortcut.FromString("R", "Debug.TestMethod"), delegate {
			//	_tabEngine.Execute(v => {
			//		//v.DummyScript();
			//	});
			//}, this);

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
		}

		private void _initializeScripts() {
			_splashWindow.Display = "Loading custom scripts...";

			try {
				ScriptLoader.VerifyExampleScriptsInstalled();
				_scriptLoader.PendingErrors.Clear();
				_scriptLoader.CombineErrors = true;
				_scriptLoader.AddScriptsToMenu(this, _mainMenu, _dpUndoRedo);
			}
			catch (Exception err) {
				ShowException(_splashWindow, err);
			}
			finally {
				_scriptLoader.CombineErrors = false;
			}

			foreach (var exception in _scriptLoader.PendingErrors) {
				ShowException(_splashWindow, exception);
			}

			_scriptLoader.PendingErrors.Clear();
		}

		private void _initializeSplashWindow() {
			_splashWindow = new SplashWindow();
			_splashWindow.Display = "Initializing components...";
			_splashWindow.Show();
		}

		public void ShowException(Window diag, Exception ex) {
			try {
				diag.Visibility = Visibility.Hidden;
				WindowProvider.WindowOpened += _windowProvider_WindowOpened;
				ErrorHandler.HandleException(ex);
			}
			finally {
				diag.Visibility = Visibility.Visible;
				WindowProvider.WindowOpened -= _windowProvider_WindowOpened;
			}
		}

		private void _windowProvider_WindowOpened(TkWindow window) {
			window.ShowInTaskbar = true;
		}

		private void Undo() => _tabEngine.Undo();
		private void Redo() => _tabEngine.Redo();
		public ScriptLoader ScriptLoader => _scriptLoader;
		public MultiGrfReader MetaGrf => _metaGrf;
		public TabEngine TabEngine => _tabEngine;

		private void _initializeMenu() {
			_splashWindow.Display = "Loading Act Editor's scripts...";
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
			_scriptLoader.AddScriptsToMenu(new ActionAdd(), this, _mainMenu, null);
			//_scriptLoader.AddScriptsToMenu(new ActionInsertAt(), this, _mainMenu, null);
			//_scriptLoader.AddScriptsToMenu(new ActionSwitchSelected(), this, _mainMenu, null);
			//_scriptLoader.AddScriptsToMenu(new ActionCopyAt(), this, _mainMenu, null);
			//((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionAdvanced(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionCopyMirror(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionMirrorVertical(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionMirrorHorizontal(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionLayerMove(ActionLayerMove.MoveDirection.Down, null), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionLayerMove(ActionLayerMove.MoveDirection.Up, null), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new FrameDelete(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameAdd(), this, _mainMenu, null);
			//_scriptLoader.AddScriptsToMenu(new FrameInsertAt(), this, _mainMenu, null);
			//_scriptLoader.AddScriptsToMenu(new FrameSwitchSelected(), this, _mainMenu, null);
			//_scriptLoader.AddScriptsToMenu(new FrameCopyAt(), this, _mainMenu, null);
			//((MenuItem)_mainMenu.Items[4]).Items.Add(new Separator());
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
			_scriptLoader.AddScriptsToMenu(new EffectBreathing(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FloatingEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new TrailAttackEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new SimpleAttackEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new HitEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new RadialErosionEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new SpikeErosionEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new VerticalStripeErosion(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new CrystalErosionEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new SmokeFadeEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new StrokeSilhouetteEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new SilhouetteDistortionEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FloorAuraEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new DelayedShadowEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new SpriteOutlineEffect(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FadeParticleEffect(), this, _mainMenu, null);


			_scriptLoader.AddScriptsToMenu(new ScriptRunnerMenu(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[7]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new OpenScriptsFolder(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ReloadScripts { ActEditor = this }, this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new BatchScriptMenu(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[7]).Items.Add(new Separator());

			Binder.Bind(_miViewSameAction, () => ActEditorConfiguration.KeepPreviewSelectionFromActionChange);
		}

		private bool _parseCommandLineArguments() {
			try {
				List<GenericCLOption> options = CommandLineParser.GetOptions(Environment.CommandLine, false);

				foreach (GenericCLOption option in options) {
					if (option.CommandName == "-REM" || option.CommandName == "REM") {
						return true;
					}
					else {
						if (option.Args.Count <= 0)
							continue;

						if (option.Args.All(p => p.IsExtension(".act"))) {
							_tabEngine.Open(option.Args[0]);
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
						_tabEngine.OpenFiles(files.Where(p => p.IsExtension(".act")).Select(p => new TkPath(p)).ToArray());
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

			_editorPosition.Save(this);
			base.OnClosing(e);
			ApplicationManager.Shutdown();
		}

		private void _recentFiles_FileClicked(string file) => _tabEngine.Open(file);

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
		}

		private void _miOpen_Click(object sender, RoutedEventArgs e) {
			try {
				string file = TkPathRequest.OpenFile<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", FileFormat.MergeFilters(Format.Act));

				if (file != null) {
					_tabEngine.Open(file);
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
				_specialLoad(act, TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}"));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		public bool Save() => _tabEngine.Save();
		public bool SaveAs() => _tabEngine.SaveAs();
		private void _miSave_Click(object sender, RoutedEventArgs e) => Save();
		private void _miSaveAs_Click(object sender, RoutedEventArgs e) => SaveAs();

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
			((TextBox)dialog.FindName("_textBlock")).Text += "\r\n\r\nCredits: Nebraskka (suggestions and feedback)";
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

						_tabEngine.Open(new TkPath(file, relativePath));
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

		private void _miCopy_Click(object sender, RoutedEventArgs e) => _tabEngine.Copy();
		private void _miPaste_Click(object sender, RoutedEventArgs e) => _tabEngine.Paste();
		private void _miCut_Click(object sender, RoutedEventArgs e) => _tabEngine.Cut();

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

			_tabEngine?.SetAnchorIndex(Int32.Parse(((FrameworkElement)sender).Tag.ToString()));
		}

		private void _miSelectAct_Click(object sender, RoutedEventArgs e) => _tabEngine.Select();

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
				_specialLoad(act, TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}"));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _specialLoad(Act act, string fileName) {
			act.LoadedPath = fileName + ".act";
			act.Sprite.Save(fileName + ".spr");
			act.Save(fileName + ".act");

			_tabEngine.Open(fileName + ".act", isNew: true);
			_recentFiles.RemoveRecentFile(fileName + ".act");
		}

		private void _new(string name) {
			try {
				var act = new Act(ApplicationManager.GetResource(name), new Spr());
				_specialLoad(act, TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}"));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miNewNpc_Click(object sender, RoutedEventArgs e) => _new("NPC.act");
		private void _miNewWeapon_Click(object sender, RoutedEventArgs e) => _new("weapon.act");
		private void _miNewMonster_Click(object sender, RoutedEventArgs e) => _new("monster.act");
		private void _miNewHomunculus_Click(object sender, RoutedEventArgs e) => _new("homunculus.act");

		private void _miNewHeadgearMale_Click(object sender, RoutedEventArgs e) {
			try {
				var act = new Act(ApplicationManager.GetResource("ref_head_m.act"), new Spr());
				act.AllFrames(p => p.Layers.Clear());
				_specialLoad(act, TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}"));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_tabEngine.RestoreFocus();
			}
		}

		private void _miViewPrevAnim_Click(object sender, RoutedEventArgs e) => _tabEngine.ShowPreviewFrames();

		public TabAct GetCurrentTab2() {
			var tab = _tabEngine.GetCurrentTab();

			if (tab == null)
				throw new Exception("No file opened. Please open an ACT file before using this command.");

			return tab;
		}
	}
}