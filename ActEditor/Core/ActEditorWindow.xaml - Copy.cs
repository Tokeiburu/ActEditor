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
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Scripts;
using ActEditor.Core.WPF;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.KeyFrameEditor;
using ActEditor.Tools.GrfShellExplorer;
using ActEditor.WPF;
using ActImaging;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.IO;
using GRF.Image;
using GRF.Image.Decoders;
using GRF.System;
using GRF.Threading;
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
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core {
	/// <summary>
	/// Interaction logic for ActEditorWindow.xaml
	/// </summary>
	public partial class ActEditorWindow : TkWindow {
		#region Delegates

		public delegate void ActEditorEventDelegate(object sender);

		#endregion

		private static Brush _uiGridBackground;
		private static Brush _uiGridBackgroundLight;

		private readonly MultiGrfReader _metaGrf = new MultiGrfReader();
		private readonly MetaGrfResourcesViewer _metaGrfViewer = new MetaGrfResourcesViewer();
		private readonly WpfRecentFiles _recentFiles;
		private readonly ScriptLoader _scriptLoader;
		private readonly SelectionEngine _selectionEngine = new SelectionEngine();
		private readonly SpriteManager _spriteManager = new SpriteManager();
		private bool _isNew;
		private List<ReferenceControl> _references = new List<ReferenceControl>();

		//private IEnumerable<GrfColor> _getUsedColors(GrfImage image) {
		//    HashSet<byte> usedPixels = new HashSet<byte>();

		//    for (int i = 0; i < image.Pixels.Length; i++) {
		//        usedPixels.Add(image.Pixels[i]);
		//    }

		//    usedPixels.Remove(0);

		//    HashSet<GrfColor> colors = new HashSet<GrfColor>();

		//    foreach (byte b in usedPixels) {
		//        colors.Add(new GrfColor(image.Palette, b * 4));
		//    }

		//    return colors;
		//}
		//private HashSet<GrfColor> _getPaletteColors(Spr _sprite) {
		//    byte[] palette = _sprite.Palette.BytePalette;

		//    HashSet<GrfColor> colors = new HashSet<GrfColor>();

		//    for (int i = 0; i < 1024; i += 4) {
		//        colors.Add(new GrfColor(palette, i));
		//    }

		//    return colors;
		//}

		//private GrfImage _getConvertedImage(Spr _sprite, GrfImage image) {
		//    byte[] palette = _sprite.Palette == null ? null : _sprite.Palette.BytePalette;

		//    if (true) {
		//        // Check if the image has all the same colors found in the palette
		//        IEnumerable<GrfColor> usedColors = _getUsedColors(image);
		//        HashSet<GrfColor> paletteColors = _getPaletteColors(_sprite);

		//        bool hasAllBeenFound = true;

		//        foreach (GrfColor color in usedColors) {
		//            if (!paletteColors.Contains(color)) {
		//                hasAllBeenFound = false;
		//                break;
		//            }
		//        }

		//        if (hasAllBeenFound) {
		//            image.Palette[0] = palette[0];
		//            image.Palette[1] = palette[1];
		//            image.Palette[2] = palette[2];
		//            image.Palette[3] = palette[3];
		//            image.Convert(new Indexed8FormatConverter { ExistingPalette = _sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette });
		//            //image.SetPalette(ref palette);
		//            return image;
		//        }
		//    }

		//    if (image.GrfImageType != GrfImageType.Indexed8) {
		//        image.Convert(new Bgra32FormatConverter());
		//    }

		//    SpriteConverterFormatDialog dialog = new SpriteConverterFormatDialog(_sprite.Palette.BytePalette, image, _sprite, 2);
		//    dialog.Owner = WpfUtilities.TopWindow;

		//    if (dialog.ShowDialog() == true) {
		//        image = dialog.Result;

		//        if (image.GrfImageType == GrfImageType.Indexed8) {
		//            _sprite.Palette.SetBytes(0, image.Palette);
		//        }
		//    }

		//    return image;
		//}

		public ActEditorWindow() : base("Act Editor", "app.ico", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			//string path = @"C:\Users\Sylvain\Downloads\Pokemon-Sprites2\Pokemon Sprites\Back";
			//string path = @"C:\Users\Sylvain\Downloads\Pokemon-Sprites2\Pokemon Sprites\Front";

			//foreach (string file in Directory.GetFiles(path, "*.gif")) {
			//    string folder = file.ReplaceExtension("");
			//    Directory.CreateDirectory(folder);

			//    string id = Path.GetFileNameWithoutExtension(file);

			//    foreach (string fileToMove in Directory.GetFiles(path, "*.bmp")) {
			//        if (Path.GetFileNameWithoutExtension(fileToMove).StartsWith(id)) {
			//            File.Move(fileToMove, GrfPath.Combine(folder, id + "_" + Path.GetFileName(fileToMove).Substring(3)));
			//        }
			//    }
			//}
			//return;
			//foreach (string file in Directory.GetFiles(path, "*.gif")) {
			//    string folder = file.ReplaceExtension("");
			//    Directory.CreateDirectory(folder);

			//    string id = Path.GetFileNameWithoutExtension(file);
			//    bool first = true;
			//    GrfColor transparent = null;
			//    HashSet<GrfColor> colors = new HashSet<GrfColor>();

			//    foreach (string fileToFix in Directory.GetFiles(folder, "*.*")) {
			//        GrfImage image = fileToFix;

			//        if (first) {
			//            transparent = image.GetColor(0);
			//            first = false;
			//        }

			//        foreach (var color in image.Colors) {
			//            colors.Add(color);
			//        }
			//    }

			//    colors.Remove(transparent);

			//    Pal pal = new Pal(new byte[1024]);
			//    pal.SetBytes(0, transparent.ToRgbaBytes());
			//    int index = 4;

			//    foreach (var color in colors) {
			//        pal.SetBytes(index, color.ToRgbaBytes());
			//        index += 4;
			//    }


			//    foreach (string fileToFix in Directory.GetFiles(folder, "*.*")) {
			//        GrfImage image = fileToFix;

			//        image.Convert(GrfImageType.Indexed8, pal.BytePalette);
			//        image.Trim();
			//        image.Margin(1);
			//        GrfToWpfBridge.Imaging.Save(image, fileToFix);
			//    }
			//}

			//return;
			//foreach (string file in Directory.GetFiles(path, "*.gif")) {
			//    string folder = file.ReplaceExtension("");
			//    Directory.CreateDirectory(folder);

			//    try {
			//        string folderFront = folder;
			//        string folderBack = folder.Replace("Front", "Back");

			//        Spr spr = new Spr();

			//        GrfImage imageFirst = null;

			//        foreach (string fileSub in Directory.GetFiles(folderFront, "*.*")) {
			//            if (imageFirst == null) {
			//                imageFirst = fileSub;

			//                spr.InsertAny(imageFirst);
			//                spr.Palette = new Pal(imageFirst.Palette);
			//                continue;
			//            }

			//            GrfImage image = fileSub;

			//            if (!Methods.ByteArrayCompare(image.Palette, spr.Palette.BytePalette)) {
			//                image = _getConvertedImage(spr, image.Copy());
			//            }

			//            spr.InsertAny(image);
			//        }

			//        foreach (string fileSub in Directory.GetFiles(folderBack, "*.*")) {
			//            if (imageFirst == null) {
			//                imageFirst = fileSub;

			//                spr.InsertAny(imageFirst);
			//                spr.Palette = new Pal(imageFirst.Palette);
			//                continue;
			//            }

			//            GrfImage image = fileSub;

			//            if (!Methods.ByteArrayCompare(image.Palette, spr.Palette.BytePalette)) {
			//                image = _getConvertedImage(spr, image.Copy());
			//            }

			//            spr.InsertAny(image);
			//        }

			//        spr.Save(@"C:\Users\Sylvain\Downloads\Pokemon-Sprites2\Pokemon Sprites\Sprites\" + Path.GetFileNameWithoutExtension(folder) + ".spr");
			//    }
			//    catch (Exception err) {
			//        ErrorHandler.HandleException(err);
			//    }
			//}


			//_test();
			//ApplicationManager.Shutdown();

			DataContext = this;
			var diag = new SplashWindow();
			diag.Display = "Initializing components...";
			diag.Show();

			Loaded += delegate { diag.Terminate(1000); };

			Title = "Act Editor";
			SizeToContent = SizeToContent.WidthAndHeight;

			InitializeComponent();
			diag.Display = "Loading scripting engine...";

			_scriptLoader = new ScriptLoader();

			// Set min size on loaded
			Loaded += delegate {
				MinWidth = ActualWidth + SystemParameters.VerticalScrollBarWidth;
				MinHeight = ActualHeight;
				SizeToContent = SizeToContent.Manual;
				Top = (SystemParameters.FullPrimaryScreenHeight - ActualHeight) / 2;
				Left = (SystemParameters.FullPrimaryScreenWidth - ActualWidth) / 2;
				MinHeight = MinHeight + 50;
			};

			ShowInTaskbar = true;

			diag.Display = "Setting components...";

			_frameSelector.Init(this);
			_framePreview.Init(this);
			_layerEditor.Init(this);
			_selectionEngine.Init(this);
			_spriteSelector.Init(this);
			_spriteManager.Init(this);
			_initEvents();

			_recentFiles = new WpfRecentFiles(GrfEditorConfiguration.ConfigAsker, 6, _miOpenRecent, "Act");
			_recentFiles.FileClicked += new RecentFilesManager.RFMFileClickedEventHandler(_recentFiles_FileClicked);

			diag.Display = "Loading Act Editor's scripts...";

			_loadMenu();

			DragEnter += new DragEventHandler(_actEditorWindow_DragEnter);
			Drop += new DragEventHandler(_actEditorWindow_Drop);

			Loaded += delegate {
				diag.Display = "Loading custom scripts...";

				GrfThread.Start(delegate {
					ScriptLoader.VerifyExampleScriptsInstalled();
					_scriptLoader.AddScriptsToMenu(this, _mainMenu, _dpUndoRedo);
				});

				try {
					if (!_parseCommandLineArguments()) {
						if (_recentFiles.Files.Count > 0 && GrfEditorConfiguration.ReopenLatestFile && _recentFiles.Files[0].IsExtension(".act") && File.Exists(new TkPath(_recentFiles.Files[0]).FilePath))
							_open(_recentFiles.Files[0]);
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
			};

			ApplicationShortcut.Link(ApplicationShortcut.Undo, () => { if (Act != null) Act.Commands.Undo(); }, this);
			ApplicationShortcut.Link(ApplicationShortcut.Redo, () => { if (Act != null) Act.Commands.Redo(); }, this);
			//ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-U"), _test, this);

			_metaGrfViewer.SaveResourceMethod = delegate(string resources) {
				GrfEditorConfiguration.Resources = Methods.StringToList(resources);
				_metaGrfViewer.LoadResourcesInfo();
				_metaGrf.Update(_metaGrfViewer.Paths);
			};

			_metaGrfViewer.LoadResourceMethod = () => GrfEditorConfiguration.Resources;
			_metaGrfViewer.LoadResourcesInfo();
			GrfThread.Start(() => {
				try {
					_metaGrf.Update(_metaGrfViewer.Paths);
				}
				catch {
				}
			}, "ActEditor - MetaGrf loader");

			TemporaryFilesManager.UniquePattern("new_{0:0000}");

			EncodingService.SetDisplayEncoding(GrfEditorConfiguration.EncodingCodepage);
		}

		private void _test() {
			string file = "C:\\mm_brinaranea.act";
			Act = new Act(File.ReadAllBytes(file), new Spr(File.ReadAllBytes(file.ReplaceExtension(".spr")), true));
			new KeyFrameEdit(this.Act, 0).ShowDialog();
		}

		public SelectionEngine SelectionEngine {
			get { return _selectionEngine; }
		}

		public SpriteManager SpriteManager {
			get { return _spriteManager; }
		}

		public ScriptLoader ScriptLoader {
			get { return _scriptLoader; }
		}

		public MultiGrfReader MetaGrf {
			get { return _metaGrf; }
		}

		public int SelectedAction {
			get { return _frameSelector.SelectedAction; }
		}

		public int SelectedFrame {
			get { return _frameSelector.SelectedFrame; }
			set {
				value = value < 0 ? 0 : value;
				_frameSelector.SelectedFrame = value;
			}
		}

		public Frame Frame {
			get { return Act[_frameSelector.SelectedAction, _frameSelector.SelectedFrame]; }
		}

		public List<ReferenceControl> References {
			get { return _references; }
			set { _references = value; }
		}

		public Act Act { get; set; }

		public Spr Sprite {
			get { return Act == null ? null : Act.Sprite; }
		}

		public static Brush UIGridBackground {
			get {
				if (_uiGridBackground == null) {
					var brush = new SolidColorBrush(Color.FromArgb(255, 189, 189, 189));
					brush.Freeze();
					_uiGridBackground = brush;
				}

				return _uiGridBackground;
			}
		}

		public static Brush UIGridBackgroundLight {
			get {
				if (_uiGridBackgroundLight == null) {
					var brush = new SolidColorBrush(Color.FromArgb(255, 235, 235, 235));
					brush.Freeze();
					_uiGridBackgroundLight = brush;
				}

				return _uiGridBackgroundLight;
			}
		}

		public Func<bool> IsActOpened {
			get { return _isActOpened; }
		}

		private void _loadMenu() {
			_scriptLoader.AddScriptsToMenu(new SpriteExport(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new EditSelectAll {ActEditor = this}, this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EditDeselectAll {ActEditor = this}, this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new InvertSelection {ActEditor = this}, this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new BringToFront {ActEditor = this}, this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new BringToBack {ActEditor = this}, this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new EditSound(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[1]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new EditPalette(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new EditPaletteAdvanced(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ImportPaletteFrom(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new EditAnchor {ActEditor = this}, this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[2]).Items.Add(new Separator());
			((MenuItem) _mainMenu.Items[2]).Items.Add(new TkMenuItem {Header = "Set anchors", IconPath = "forward.png"});
			_scriptLoader.AddScriptsToMenu(new ImportAnchor(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ImportDefaultMaleAnchor(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ImportDefaultFemaleAnchor(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[2]).Items.Add(new Separator());
			((MenuItem) _mainMenu.Items[2]).Items.Add(new TkMenuItem {Header = "Adjust anchors", IconPath = "adjust.png"});
			_scriptLoader.AddScriptsToMenu(new AdjustAnchor(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new AdjustAnchorMale(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new AdjustAnchorFemale(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new ActionCopy(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionPaste(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionDelete(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionInsertAt(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionSwitchSelected(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ActionCopyAt(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionAdvanced(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[3]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new ActionCopyMirror(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new FrameDelete(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameInsertAt(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameSwitchSelected(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyAt(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[4]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new FrameAdvanced(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[4]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new FrameDuplicate(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new FrameCopyBrB(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyBBl(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyBBr(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new FrameCopyBlB(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ReverseAnimation(), this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[5]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new FadeAnimation(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new AnimationReceivingHit(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[5]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new InterpolationAnimation(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new LayerInterpolationAnimation(), this, _mainMenu, null);
			((MenuItem)_mainMenu.Items[5]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new InterpolationAnimationAdv(), this, _mainMenu, null);

			_scriptLoader.AddScriptsToMenu(new ScriptRunnerMenu {ActEditor = this}, this, _mainMenu, null);
			((MenuItem) _mainMenu.Items[6]).Items.Add(new Separator());
			_scriptLoader.AddScriptsToMenu(new OpenScriptsFolder(), this, _mainMenu, null);
			_scriptLoader.AddScriptsToMenu(new ReloadScripts {ActEditor = this}, this, _mainMenu, _dpUndoRedo);
			((MenuItem) _mainMenu.Items[6]).Items.Add(new Separator());
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

						if (option.Args.All(p => p.GetExtension() == ".act")) {
							_open(option.Args[0]);
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

		public event ActEditorEventDelegate ReferencesChanged;
		public event ActEditorEventDelegate ActLoaded;

		public void OnActLoaded() {
			ActEditorEventDelegate handler = ActLoaded;
			if (handler != null) handler(this);
		}

		public void OnReferencesChanged() {
			ActEditorEventDelegate handler = ReferencesChanged;
			if (handler != null) handler(this);
		}

		private void _actEditorWindow_Drop(object sender, DragEventArgs e) {
			try {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".act"))) {
						_isNew = false;
						_open(files.First(p => p.IsExtension(".act")));
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
				if (!_closeAct()) {
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
			_isNew = false;
			_open(file);
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
		}

		private void _open(string file) {
			_open(new TkPath(file));
		}

		private void _openFromFile(string file) {
			try {
				if (!file.IsExtension(".act")) {
					_recentFiles.RemoveRecentFile(file);
					ErrorHandler.HandleException("Invalid file extension; only .act files are allowed.");
					return;
				}

				if (!File.Exists(file)) {
					_recentFiles.RemoveRecentFile(file);
					ErrorHandler.HandleException("File not found while trying to open the Act.\r\n\r\n" + file);
					return;
				}

				_recentFiles.AddRecentFile(file);

				if (!File.Exists(file.ReplaceExtension(".spr"))) {
					_recentFiles.RemoveRecentFile(file);
					ErrorHandler.HandleException("File not found : " + file.ReplaceExtension(".spr"));
					return;
				}

				Act = new Act(File.ReadAllBytes(file), new Spr(File.ReadAllBytes(file.ReplaceExtension(".spr")), true));
				Act.LoadedPath = file;

				_addEvents();

				OnActLoaded();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _open(TkPath file) {
			try {
				if (_closeAct()) {
					if (file.FilePath.IsExtension(".act") || String.IsNullOrEmpty(file.RelativePath)) {
						_openFromFile(file.FilePath);
						return;
					}

					if (!File.Exists(file.FilePath)) {
						_recentFiles.RemoveRecentFile(file.GetFullPath());
						return;
					}

					_recentFiles.AddRecentFile(file.GetFullPath());

					TkPath sprPath = new TkPath(file);
					sprPath.RelativePath = sprPath.RelativePath.ReplaceExtension(".spr");

					byte[] dataAct = null;
					byte[] dataSpr = null;

					using (GrfHolder grf = new GrfHolder(file.FilePath)) {
						if (grf.FileTable.ContainsFile(file.RelativePath))
							dataAct = grf.FileTable[file.RelativePath].GetDecompressedData();

						if (grf.FileTable.ContainsFile(file.RelativePath.ReplaceExtension(".spr")))
							dataSpr = grf.FileTable[file.RelativePath.ReplaceExtension(".spr")].GetDecompressedData();
					}

					if (dataAct == null) {
						ErrorHandler.HandleException("File not found : " + file);
						return;
					}

					if (dataSpr == null) {
						ErrorHandler.HandleException("File not found : " + sprPath);
						return;
					}

					Act = new Act(dataAct, new Spr(dataSpr, true));
					Act.LoadedPath = file.GetFullPath();

					_addEvents();

					OnActLoaded();
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _addEvents() {
			this.Dispatch(p => p.Title = "Act Editor - " + Methods.CutFileName(Act.LoadedPath) + (_isNew ? " *" : ""));

			Act.Commands.CommandIndexChanged += delegate {
				if (!Act.Commands.IsModified && !_isNew) {
					this.Dispatch(p => p.Title = "Act Editor - " + Methods.CutFileName(Act.LoadedPath));
				}
				else if (Act.Commands.IsModified || _isNew) {
					this.Dispatch(p => p.Title = "Act Editor - " + Methods.CutFileName(Act.LoadedPath) + " *");
				}
			};

			_tmbUndo.SetUndo(Act.Commands);
			_tmbRedo.SetRedo(Act.Commands);
		}

		private void _initEvents() {
			_references.Add(new ReferenceControl(this, "ref_body", "Body", false));
			_references.Add(new ReferenceControl(this, "ref_head", "Head", false));
			_references.Add(new ReferenceControl(this, "ref_body", "Other", false));
			_references.Add(new ReferenceControl(this, "ref_body", "Neighbor", true));

			_stackPanelReferences.Children.Add(_references[0]);
			_stackPanelReferences.Children.Add(_references[1]);
			_stackPanelReferences.Children.Add(_references[2]);
			_stackPanelReferences.Children.Add(_references[3]);

			_references.ForEach(p => p.Init());
			//_references.ForEach(p => _stackPanelReferences.Children.Add(p));
		}

		private void _miOpen_Click(object sender, RoutedEventArgs e) {
			try {
				//if (_closeAct()) {
				string file = PathRequest.OpenFileExtract("filter", FileFormat.MergeFilters(Format.Act));

				if (file != null) {
					_isNew = false;
					_open(file);
				}
				//}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}
		}

		private void _restoreFocus() {
			Focus();
		}

		private void _miSettings_Click(object sender, RoutedEventArgs e) {
			WindowProvider.Show(new ActEditorSettings(this, _metaGrfViewer), _miSettings, this);
		}

		//protected override void GRFEditorWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
		//}

		private void _miClose_Click(object sender, RoutedEventArgs e) {
			try {
				if (_closeAct()) {
					Close();
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private bool _closeAct() {
			if (Act != null && Act.Commands.IsModified) {
				var res = WindowProvider.ShowDialog("The project has been modified, would you like to save it first?", "Modified Act", MessageBoxButton.YesNoCancel);

				if (res == MessageBoxResult.Yes) {
					if (!SaveAs()) {
						return false;
					}
				}

				if (res == MessageBoxResult.Cancel) {
					return false;
				}
			}

			if (Act != null) {
				Act.Commands.ClearCommands();
				Act = null;

				this.Dispatch(p => p.Title = "Act Editor");
				_references.ForEach(p => p.Reset());
				_frameSelector.Reset();
				_spriteSelector.Reset();
				_layerEditor.Reset();
				_framePreview.Reset();
			}

			return true;
		}

		private void _miNew_Click(object sender, RoutedEventArgs e) {
			try {
				if (_closeAct()) {
					Act = new Act(new Spr());
					Act.AddAction();

					string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

					Act.LoadedPath = fileName + ".act";
					Act.Sprite.Converter.Save(Act.Sprite, fileName + ".spr");
					Act.ActConverter.Save(Act, fileName + ".act", Act.Sprite);

					_isNew = true;
					_open(fileName + ".act");
					_recentFiles.RemoveRecentFile(fileName + ".act");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}
		}

		public bool Save() {
			try {
				if (Act != null) {
					if (_isNew) {
						return SaveAs();
					}

					//if (Act.Commands.CommandIndex != _commandIndex) {
					var converter = ActConverterProvider.GetConverter();

					TkPath path = new TkPath(Act.LoadedPath);

					if (!String.IsNullOrEmpty((path.RelativePath))) {
						if (Methods.IsFileLocked(path.FilePath)) {
							ErrorHandler.HandleException("The file " + path.FilePath + " is locked by another process. Try closing other GRF applicactions or use the 'Save as...' option.");
							return false;
						}

						using (GrfHolder grf = new GrfHolder(path.FilePath)) {
							string temp = TemporaryFilesManager.GetTemporaryFilePath("to_grf_{0:0000}");

							Act.Sprite.Converter.Save(Act.Sprite, temp + ".spr");
							converter.Save(Act, temp + ".act", Act.Sprite);

							grf.Commands.AddFileAbsolute(path.RelativePath.ReplaceExtension(".act"), File.ReadAllBytes(temp + ".act"));
							grf.Commands.AddFileAbsolute(path.RelativePath.ReplaceExtension(".spr"), File.ReadAllBytes(temp + ".spr"));

							grf.QuickSave();

							if (!grf.CancelReload) {
								Act.Commands.SaveCommandIndex();
								//Act.Commands.ClearCommands();
							}
						}
					}
					else {
						Act.Sprite.Converter.Save(Act.Sprite, Act.LoadedPath.ReplaceExtension(".spr"));
						converter.Save(Act, Act.LoadedPath, Act.Sprite);
						Act.Commands.SaveCommandIndex();
						//Act.Commands.ClearCommands();
					}

					this.Dispatch(p => p.Title = "Act Editor - " + Methods.CutFileName(Act.LoadedPath));
					_isNew = false;
					return true;
				}
				//}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}

			return false;
		}

		private void _miSave_Click(object sender, RoutedEventArgs e) {
			Save();
		}

		public bool SaveAs() {
			try {
				if (Act != null) {
					var fileName = GrfEditorConfiguration.AppLastPath;

					if (Path.GetFileNameWithoutExtension(fileName) != Path.GetFileNameWithoutExtension(Act.LoadedPath)) {
						fileName = Act.LoadedPath;
					}

					string file = PathRequest.SaveFileEditor("fileName", fileName, "filter", "Act and Spr files|*.act;*spr|" + FileFormat.MergeFilters(Format.Act | Format.Spr | Format.Gif | Format.Pal | Format.Image));

					if (file != null) {
						var sfd = TkPathRequest.LatestSaveFileDialog;

						if (sfd.FilterIndex == 1 && file.IsExtension(".act", ".spr")) {
							Act.SaveWithSprite(file.ReplaceExtension(".act"));
							_recentFiles.AddRecentFile(file);
							Act.LoadedPath = file.ReplaceExtension(".act");
							this.Dispatch(p => p.Title = "Act Editor - " + Methods.CutFileName(Act.LoadedPath));
							Act.Commands.SaveCommandIndex();
							_isNew = false;
							return true;
						}
						else if ((sfd.FilterIndex == 2 && file.IsExtension(".act")) || file.IsExtension(".act")) {
							var converter = ActConverterProvider.GetConverter();
							converter.Save(Act, file.ReplaceExtension(".act"), Act.Sprite);
							_recentFiles.AddRecentFile(file);
						}
						else if ((sfd.FilterIndex == 3 && file.IsExtension(".pal")) || file.IsExtension(".pal")) {
							File.WriteAllBytes(file, Act.Sprite.Palette.BytePalette);
						}
						else if ((sfd.FilterIndex == 4 && file.IsExtension(".spr")) || file.IsExtension(".spr")) {
							Act.Sprite.Converter.Save(Act.Sprite, file.ReplaceExtension(".spr"));
						}
						else if ((sfd.FilterIndex == 5 && file.IsExtension(".gif")) || file.IsExtension(".gif")) {
							try {
								for (int i = 0; i < Act.Sprite.NumberOfIndexed8Images; i++) {
									Act.Sprite.Images[i].Palette[3] = 0;
								}

								GifSavingDialog dialog = new GifSavingDialog(Act, SelectedAction);
								dialog.Owner = WpfUtilities.TopWindow;

								if (GrfEditorConfiguration.ActEditorGifHideDialog || dialog.ShowDialog() == true) {
									var prog = new ProgressWindow("Saving as gif...", "app.ico");
									prog.EnableClosing = true;
									prog.Loaded += delegate {
										prog.Start(new GrfThread(() => {
											try {
												Imaging.SaveAsGif(file, Act, SelectedAction, prog, dialog.Dispatch(() => dialog.Extra));
											}
											catch (Exception err) {
												ErrorHandler.HandleException(err);
											}
										}, prog, 200, null, true, true));
									};
									prog.ShowDialog();
								}
							}
							finally {
								for (int i = 0; i < Act.Sprite.NumberOfIndexed8Images; i++) {
									Act.Sprite.Images[i].Palette[3] = 255;
								}
							}
						}
						else {
							if (!file.IsExtension(".bmp", ".png", ".jpg", ".tga")) {
								ErrorHandler.HandleException("Invalid file extension.");
								return false;
							}

							var imgSource = Imaging.GenerateImage(Act, SelectedAction, SelectedFrame);
							PngBitmapEncoder encoder = new PngBitmapEncoder();
							encoder.Frames.Add(BitmapFrame.Create(Imaging.ForceRender(imgSource, BitmapScalingMode.NearestNeighbor)));

							using (MemoryStream stream = new MemoryStream()) {
								encoder.Save(stream);

								byte[] data = new byte[stream.Length];
								stream.Seek(0, SeekOrigin.Begin);
								stream.Read(data, 0, data.Length);

								GrfImage grfImage = new GrfImage(ref data);
								GrfToWpfBridge.Imaging.Save(grfImage, file);
							}
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}

			return false;
		}

		private void _miSaveAs_Click(object sender, RoutedEventArgs e) {
			SaveAs();
		}

		private void _miCloseCurrent_Click(object sender, RoutedEventArgs e) {
			try {
				_closeAct();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}
		}

		private void _miAbout_Click(object sender, RoutedEventArgs e) {
			var dialog = new AboutDialog(GrfEditorConfiguration.PublicVersion, GrfEditorConfiguration.RealVersion, GrfEditorConfiguration.Author, GrfEditorConfiguration.ProgramName);
			dialog.Owner = WpfUtilities.TopWindow;
			((TextBox) dialog.FindName("_textBlock")).Text += "\r\n\r\nCredits : Nebraskka (suggestions and feedback)";
			dialog.ShowDialog();

			_restoreFocus();
		}

		private void _gridSpriteSelected_SizeChanged(object sender, SizeChangedEventArgs e) {
			_spriteSelector.Height = _gridSpriteSelected.ActualHeight;
		}

		private void _miOpenFromGrf_Click(object sender, RoutedEventArgs e) {
			try {
				//if (_closeAct()) {
				string file = PathRequest.OpenGrfFile("filter", FileFormat.MergeFilters(Format.AllContainers, Format.Grf, Format.Gpf, Format.Thor));

				if (file != null) {
					GrfExplorer dialog = new GrfExplorer(file);
					dialog.Owner = WpfUtilities.TopWindow;

					if (dialog.ShowDialog() == true) {
						string relativePath = dialog.SelectedItem;

						if (relativePath == null) return;

						if (!relativePath.IsExtension(".act")) {
							throw new Exception("Only ACT files can be selected.");
						}

						_isNew = false;
						_open(new TkPath(file, relativePath));
					}
				}
				//f}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}
		}

		private void _miCopy_Click(object sender, RoutedEventArgs e) {
			_framePreview.Copy();
		}

		private void _miPaste_Click(object sender, RoutedEventArgs e) {
			_framePreview.Paste();
		}

		private void _miCut_Click(object sender, RoutedEventArgs e) {
			_framePreview.Cut();
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

			if (_framePreview != null) {
				_framePreview.AnchorIndex = Int32.Parse(((MenuItem) sender).Tag.ToString());
				_framePreview.Update();
			}
		}

		private void _miSelectAct_Click(object sender, RoutedEventArgs e) {
			try {
				if (Act != null) {
					OpeningService.FilesOrFolders(Act.LoadedPath);
				}
				else {
					ErrorHandler.HandleException("No act loaded.");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private bool _isActOpened() {
			return Act != null;
		}

		private void _miShowAnchors_Loaded(object sender, RoutedEventArgs e) {
			_miShowAnchors.IsChecked = GrfEditorConfiguration.ShowAnchors;
		}

		private void _miShowAnchors_Checked(object sender, RoutedEventArgs e) {
			GrfEditorConfiguration.ShowAnchors = true;
			_framePreview.Update();
		}

		private void _miShowAnchors_Unchecked(object sender, RoutedEventArgs e) {
			GrfEditorConfiguration.ShowAnchors = false;
			_framePreview.Update();
		}

		private void _miSaveAsAdv_Click(object sender, RoutedEventArgs e) {
			ClientIntegrationDialog dialog = new ClientIntegrationDialog(Act);
			dialog.Owner = WpfUtilities.TopWindow;
			dialog.ShowDialog();
		}

		private void _miNewHeadgear_Click(object sender, RoutedEventArgs e) {
			try {
				if (_closeAct()) {
					Act = new Act(ApplicationManager.GetResource("ref_head.act"), new Spr());
					Act.AllFrames(p => p.Layers.Clear());

					string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

					Act.LoadedPath = fileName + ".act";
					Act.Sprite.Converter.Save(Act.Sprite, fileName + ".spr");
					Act.ActConverter.Save(Act, fileName + ".act", Act.Sprite);

					_isNew = true;
					_open(fileName + ".act");
					_recentFiles.RemoveRecentFile(fileName + ".act");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}
		}

		private void _new(string name) {
			try {
				if (_closeAct()) {
					Act = new Act(ApplicationManager.GetResource(name), new Spr());

					string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

					Act.LoadedPath = fileName + ".act";
					Act.Sprite.Converter.Save(Act.Sprite, fileName + ".spr");
					Act.ActConverter.Save(Act, fileName + ".act", Act.Sprite);

					_isNew = true;
					_open(fileName + ".act");
					_recentFiles.RemoveRecentFile(fileName + ".act");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
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
				if (_closeAct()) {
					Act = new Act(ApplicationManager.GetResource("ref_head_male.act"), new Spr());
					Act.AllFrames(p => p.Layers.Clear());

					string fileName = TemporaryFilesManager.GetTemporaryFilePath("new_{0:0000}");

					Act.LoadedPath = fileName + ".act";
					Act.Sprite.Converter.Save(Act.Sprite, fileName + ".spr");
					Act.ActConverter.Save(Act, fileName + ".act", Act.Sprite);

					_isNew = true;
					_open(fileName + ".act");
					_recentFiles.RemoveRecentFile(fileName + ".act");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_restoreFocus();
			}
		}
	}
}