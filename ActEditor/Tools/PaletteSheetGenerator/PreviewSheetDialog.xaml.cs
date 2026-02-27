using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core;
using ActEditor.Core.Scripting;
using ActEditor.Core.WPF.EditorControls;
using ErrorManager;
using GRF;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Utilities.Parsers;
using Utilities.Parsers.Libconfig;
using Utilities.Services;

namespace ActEditor.Tools.PaletteSheetGenerator {
	/// <summary>
	/// Interaction logic for PaletteGenerator.xaml
	/// </summary>
	public partial class PreviewSheetDialog : TkWindow {
		private SpriteResource _lastHeadResource;
		private SpriteResource _lastBodyResource;
		private GrfHolder _grfJobs;
		private GrfHolder _grfPalettes;
		private TextBox[] _searchBoxes;
		private readonly ListView[] _lists;
		private readonly List<SpriteResource>[] _resources = { new List<SpriteResource>(), new List<SpriteResource>(), new List<SpriteResource>(), new List<SpriteResource>() };
		private readonly bool _isLoaded = false;
		private QuickPreviewPaletteSheet _quickPreview = null;

		public PreviewSheetDialog()
			: base("Palette sheet generator", "busy.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
			this.ShowInTaskbar = true;

			Unloaded += delegate {
				if (_quickPreview != null) {
					_quickPreview.Close();
					_quickPreview = null;
				}
			};

			try {
				List<int> actionIndexes = Enumerable.Range(0, 104).ToList();

				_cbDirection.ItemsSource = actionIndexes;
				_tbPaletteOverlay.Visibility = Visibility.Collapsed;

				try {
					Binder.Bind((TextBox)_tbFrom, () => ActEditorConfiguration.PreviewSheetIndexes, v => ActEditorConfiguration.PreviewSheetIndexes = v);
					Binder.Bind((TextBox)_tbMax, () => ActEditorConfiguration.PreviewSheetMaxPerLine, v => ActEditorConfiguration.PreviewSheetMaxPerLine = v);
					Binder.Bind(_cbDirection, () => ActEditorConfiguration.PreviewSheetActionIndex, v => ActEditorConfiguration.PreviewSheetActionIndex = v, _previewReload);
					Binder.Bind(_cbBodyAffectedPalette, () => ActEditorConfiguration.PreviewSheetBodyAffected, v => ActEditorConfiguration.PreviewSheetBodyAffected = v, _previewReload);
					Binder.Bind(_cbHeadAffectedPalette, () => ActEditorConfiguration.PreviewSheetHeadAffected, v => ActEditorConfiguration.PreviewSheetHeadAffected = v, _previewReload);
					Binder.Bind(_cbHeadAffectedPalette, () => ActEditorConfiguration.PreviewSheetHeadAffected, v => ActEditorConfiguration.PreviewSheetHeadAffected = v, _previewReload);
					Binder.Bind(_cbShowBodySprite, () => ActEditorConfiguration.PreviewSheetShowBody, v => ActEditorConfiguration.PreviewSheetShowBody = v, delegate {
						_cbBodyAffectedPalette.IsEnabled = ActEditorConfiguration.PreviewSheetShowBody;
						_previewReload();
					}, true);
					Binder.Bind(_cTransparentBackground, () => ActEditorConfiguration.PreviewSheetTransparentBackground, v => ActEditorConfiguration.PreviewSheetTransparentBackground = v, _previewReload);
					Binder.Bind(_cShowShadow, () => ActEditorConfiguration.PreviewSheetShowShadow, v => ActEditorConfiguration.PreviewSheetShowShadow = v, _previewReload);
					Binder.Bind(_cbShowHeadSprite, () => ActEditorConfiguration.PreviewSheetShowHead, v => ActEditorConfiguration.PreviewSheetShowHead = v, delegate {
						_cbHeadAffectedPalette.IsEnabled = ActEditorConfiguration.PreviewSheetShowHead;
						_previewReload();
					}, true);
					Binder.Bind(_cbShowPalId, () => ActEditorConfiguration.PreviewSheetShowPalIndex, v => ActEditorConfiguration.PreviewSheetShowPalIndex = v, _previewReload);
					Binder.Bind(_cbPaletteOld, () => ActEditorConfiguration.PreviewSheetUseOldConfig, v => ActEditorConfiguration.PreviewSheetUseOldConfig = v, _reloadJobs);
					Binder.Bind(_cbPalette, () => ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath, v => ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath = v, delegate {
						_tbPalette.IsEnabled = !ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath;
						_tbPaletteOverlay.Visibility = ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath ? Visibility.Visible : Visibility.Collapsed;
						_cbPaletteOld.IsEnabled = ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath;
						_previewReload();
					}, true);
					Binder.Bind((TextBox)_tbPalette, () => ActEditorConfiguration.PreviewSheetPredefinedPalettePath, v => ActEditorConfiguration.PreviewSheetPredefinedPalettePath = v);
					Binder.Bind(_cbFontType, () => ActEditorConfiguration.PreviewFontType, v => ActEditorConfiguration.PreviewFontType = v, _previewReload);

					WpfUtilities.AddFocus(_tbFrom, _tbMax, _tbPalette);
					WpfUtilities.AddMouseInOutUnderline(_cbBodyAffectedPalette);
					WpfUtilities.AddMouseInOutUnderline(_cbHeadAffectedPalette);
					WpfUtilities.AddMouseInOutUnderline(_cbShowBodySprite);
					WpfUtilities.AddMouseInOutUnderline(_cbShowHeadSprite);
					WpfUtilities.AddMouseInOutUnderline(_cbShowPalId);
					WpfUtilities.AddMouseInOutUnderline(_cTransparentBackground);
					WpfUtilities.AddMouseInOutUnderline(_cShowShadow);
					WpfUtilities.AddMouseInOutUnderline(_cbPalette);
					WpfUtilities.AddMouseInOutUnderline(_cbPaletteOld);

					_pbJob.TextChanged += delegate {
						if (File.Exists(_pbJob.Text)) {
							_pbJob.RecentFiles.AddRecentFile(_pbJob.Text);
							if (_grfJobs != null) {
								_grfJobs.Close();
							}

							_grfJobs = new GrfHolder(_pbJob.Text);
							_jobGrfUpdate();
						}
					};

					_pbPalettes.TextChanged += delegate {
						if (File.Exists(_pbPalettes.Text)) {
							_pbPalettes.RecentFiles.AddRecentFile(_pbPalettes.Text);
						}
					};

					_pbJob.SetToLatestRecentFile();
					_pbPalettes.SetToLatestRecentFile();

					_lists = new ListView[] { _lvCM, _lvCF, _lvHM, _lvHF };
					_searchBoxes = new TextBox[] { _textBoxSearchCM, _textBoxSearchCF, _textBoxSearchHM, _textBoxSearchHF };
					SpsManager.InitLists(_searchBoxes, _lists, _resources, _lv_SelectionChanged);
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
			}
			finally {
				_isLoaded = true;
			}

			_loadJobs();
		}

		private void _reloadJobs() {
			if (!_isLoaded)
				return;

			_loadJobs();
		}

		private void _loadJobs() {
			try {
				_resources[0].Clear();
				_resources[1].Clear();

				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath, "sprites.conf");
				string pathOld = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath, "sprites_old.conf");

				if (!File.Exists(path)) {
					File.WriteAllBytes(path, ApplicationManager.GetResource("sprites.conf"));
				}

				if (!File.Exists(pathOld)) {
					File.WriteAllBytes(pathOld, ApplicationManager.GetResource("sprites_old.conf"));
				}

				LibconfigParser parser = new LibconfigParser(path, EncodingService.DisplayEncoding);
				var paletteConf = parser.Output["palette_conf"];

				foreach (var entry in paletteConf) {
					_loadJob(entry, false);
				}

				if (ActEditorConfiguration.PreviewSheetUseOldConfig) {
					parser = new LibconfigParser(pathOld, EncodingService.DisplayEncoding);
					paletteConf = parser.Output["palette_conf"];

					foreach (var entry in paletteConf) {
						_loadJob(entry, true);
					}
				}

				_jobGrfUpdate();

				_resources[0] = _resources[0].OrderBy(p => p.DisplayName).ToList();
				_resources[1] = _resources[1].OrderBy(p => p.DisplayName).ToList();
				_lvCM.ItemsSource = _resources[0];
				_lvCF.ItemsSource = _resources[1];

				if (ActEditorConfiguration.PreviewSheetLastSelectedJob != "") {
					_lists[ActEditorConfiguration.PreviewSheetLastSelectedJobList].SelectedItem = _resources[ActEditorConfiguration.PreviewSheetLastSelectedJobList].FirstOrDefault(p => p.DisplayName == ActEditorConfiguration.PreviewSheetLastSelectedJob);
					_lists[ActEditorConfiguration.PreviewSheetLastSelectedJobList].ScrollToCenterOfView(_lists[ActEditorConfiguration.PreviewSheetLastSelectedJobList].SelectedItem);
					_mainTabControlBody.SelectedIndex = ActEditorConfiguration.PreviewSheetLastSelectedJobList;
				}

				if (ActEditorConfiguration.PreviewSheetLastSelectedHead != "") {
					_lists[ActEditorConfiguration.PreviewSheetLastSelectedHeadList].SelectedItem = _resources[ActEditorConfiguration.PreviewSheetLastSelectedHeadList].FirstOrDefault(p => p.DisplayName == ActEditorConfiguration.PreviewSheetLastSelectedHead);
					_lists[ActEditorConfiguration.PreviewSheetLastSelectedHeadList].ScrollToCenterOfView(_lists[ActEditorConfiguration.PreviewSheetLastSelectedHeadList].SelectedItem);
					_tabControlHead.SelectedIndex = ActEditorConfiguration.PreviewSheetLastSelectedHeadList - 2;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_previewReload();
			}
		}

		private void _loadJob(ParserObject entry, bool isImport) {
			bool ismount = Boolean.Parse(entry["ismount"] ?? "false");
			string displayName = entry["class"];
			string sprite = EncodingService.FromAnyToDisplayEncoding(entry["sprite"]);
			string palette = EncodingService.FromAnyToDisplayEncoding(entry["palette"]);
			bool male = Boolean.Parse(entry["male"] ?? "false");
			bool female = Boolean.Parse(entry["female"] ?? "false");
			bool isCostume = Boolean.Parse(entry["isCostume"] ?? "false");
			var dirs = GrfPath.SplitDirectories(displayName).ToList();
			string displayName2 = (ismount ? dirs[dirs.Count - 2] + " (" + dirs[dirs.Count - 1] + ")" : Path.GetFileName(displayName)).Replace("[4th] ", "");

			if (isImport) {
				for (int i = 0; i < 2; i++) {
					var spriteResource = _resources[i].FirstOrDefault(p => p.SpriteName == sprite);

					if (spriteResource != null) {
						spriteResource.PalettePath = palette;
					}
				}

				return;
			}

			SpriteResource bodyMale = new SpriteResource(
				grf => {
					var path = String.Format(EncodingService.FromAnyToDisplayEncoding(isCostume ? @"data\sprite\ÀÎ°£Á·\¸öÅë\³²\costume_1\{0}_³²_1.act" : @"data\sprite\ÀÎ°£Á·\¸öÅë\³²\{0}_³².act"), sprite);
					var entryAct = grf.FileTable.TryGet(path);
					var entrySpr = grf.FileTable.TryGet(path.ReplaceExtension(".spr"));

					if (entryAct == null || entrySpr == null)
						return null;

					return new Act(entryAct, entrySpr);
				},
				EncodingService.FromAnyToDisplayEncoding(GrfStrings.GenderMale),
				sprite,
				displayName2,
				palette,
				isCostume
			);

			bodyMale.LoadPath = String.Format(EncodingService.FromAnyToDisplayEncoding(isCostume ? @"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\costume_1\{0}_¿©_1.act" : @"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\{0}_¿©.act"), sprite);

			SpriteResource bodyFemale = new SpriteResource(
				grf => {
					var path = String.Format(EncodingService.FromAnyToDisplayEncoding(isCostume ? @"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\costume_1\{0}_¿©_1.act" : @"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\{0}_¿©.act"), sprite);
					var entryAct = grf.FileTable.TryGet(path);
					var entrySpr = grf.FileTable.TryGet(path.ReplaceExtension(".spr"));

					if (entryAct == null || entrySpr == null)
						return null;

					return new Act(entryAct, entrySpr);
				},
				EncodingService.FromAnyToDisplayEncoding(GrfStrings.GenderFemale),
				sprite,
				displayName2,
				palette,
				isCostume
			);

			bodyFemale.LoadPath = String.Format(EncodingService.FromAnyToDisplayEncoding(isCostume ? @"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\costume_1\{0}_³²_1.act" : @"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\{0}_¿©.act"), sprite);

			if (!female && !male) {
				_resources[0].Add(bodyMale);
				_resources[1].Add(bodyFemale);
			}
			else if (female) {
				_resources[1].Add(bodyFemale);
			}
			else {
				_resources[0].Add(bodyMale);
			}
		}

		private void _jobGrfUpdate() {
			try {
				if (_grfJobs == null || !_grfJobs.IsOpened) {
					return;
				}

				_resources[2].Clear();
				_resources[3].Clear();

				foreach (var entry_s in _grfJobs.FileTable.EntriesInDirectory(EncodingService.FromAnyToDisplayEncoding(@"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\³²\"), SearchOption.TopDirectoryOnly)) {
					if (!entry_s.RelativePath.IsExtension(".act"))
						continue;

					var entry = entry_s;

					SpriteResource male = new SpriteResource(
						grf => new Act(
							entry,
							_grfJobs.FileTable.TryGet(entry.RelativePath.ReplaceExtension(".spr"))),
						EncodingService.FromAnyToDisplayEncoding(GrfStrings.GenderMale),
						entry.RelativePath,
						String.Format("Head Sprite #{0:000}", Path.GetFileName(entry.RelativePath).Replace(EncodingService.FromAnyToDisplayEncoding("_³².act"), "")),
						"",
						false
						);
					male.LoadPath = entry_s.RelativePath;

					_resources[2].Add(male);
				}

				foreach (var entry_s in _grfJobs.FileTable.EntriesInDirectory(EncodingService.FromAnyToDisplayEncoding(@"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\¿©\"), SearchOption.TopDirectoryOnly)) {
					if (!entry_s.RelativePath.IsExtension(".act"))
						continue;

					var entry = entry_s;

					SpriteResource female = new SpriteResource(
						grf => new Act(
							entry,
							_grfJobs.FileTable.TryGet(entry.RelativePath.ReplaceExtension(".spr"))),
						EncodingService.FromAnyToDisplayEncoding(GrfStrings.GenderFemale),
						entry.RelativePath,
						String.Format("Head Sprite #{0:000}", Path.GetFileName(entry.RelativePath).Replace(EncodingService.FromAnyToDisplayEncoding("_¿©.act"), "")),
						"",
						false
						);
					female.LoadPath = entry_s.RelativePath;

					_resources[3].Add(female);
				}

				var comprarer = new AlphanumComparer(StringComparison.OrdinalIgnoreCase);
				_resources[2] = _resources[2].OrderBy(p => p.DisplayName, comprarer).ToList();
				_resources[3] = _resources[3].OrderBy(p => p.DisplayName, comprarer).ToList();
				_lvHM.ItemsSource = _resources[2];
				_lvHF.ItemsSource = _resources[3];
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _lv_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				if (sender == _lists[0] || sender == _lists[1]) {
					_lastBodyResource = ((ListView)sender).SelectedItem as SpriteResource;

					if (_lastBodyResource != null) {
						_tbPaletteOverlay.Text = _lastBodyResource.PalettePath;
						ActEditorConfiguration.PreviewSheetLastSelectedJob = _lastBodyResource.DisplayName;
						ActEditorConfiguration.PreviewSheetLastSelectedJobList = _lists.ToList().IndexOf((ListView)sender);
					}
				}
				else {
					_lastHeadResource = ((ListView)sender).SelectedItem as SpriteResource;

					if (_lastHeadResource != null) {
						ActEditorConfiguration.PreviewSheetLastSelectedHead = _lastHeadResource.DisplayName;
						ActEditorConfiguration.PreviewSheetLastSelectedHeadList = _lists.ToList().IndexOf((ListView)sender);
					}
				}

				_previewReload();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _previewReload() {
			if (!_isLoaded)
				return;

			try {
				var image = _previewReloadSub("1-2");

				_imagePreview.Source = image.Cast<ImageSource>();
				_imagePreview.Width = image.Width;
				_imagePreview.Height = image.Height;
				_showQuickPreview();
			}
			catch {
				_imagePreview.Source = null;
			}
		}

		private GrfImage _previewReloadSub(string pattern) {
			DisplayError("");

			int actionIndex = ActEditorConfiguration.PreviewSheetActionIndex;

			if (actionIndex < 0)
				actionIndex = 0;

			var pbJob = _pbJob.Dispatch(p => p.Text);
			var pbPalettes = _pbPalettes.Dispatch(p => p.Text);

			if (pbJob == "") {
				DisplayError("The GRF path for job sprites has not been set yet. Use data.grf if you're not sure.");
				return null;
			}

			if (pbPalettes == "") {
				DisplayError("The GRF path for palettes has not been set yet. Use data.grf if you're not sure (you can use the same as the job sprites one).");
				return null;
			}

			if (_grfPalettes == null || _grfPalettes.FileName != pbPalettes) {
				_grfPalettes = new GrfHolder(pbPalettes);
			}

			try {
				if (_lastBodyResource == null)
					_lastBodyResource = _resources[0].First();
			}
			catch {
				DisplayError("Failed to load default body sprite. There are no body sprites loaded in this GRF (job sprites): " + pbJob);
				throw;
			}

			try {
				if (_lastHeadResource == null && _resources[2].Count > 0)
					_lastHeadResource = _resources[2].First();
			}
			catch {
				// Do nothing, it will be handled later
			}

			Act bodyAct = _lastBodyResource.GetAct(_grfJobs);
			Act headAct = _lastHeadResource?.GetAct(_grfJobs);

			if (bodyAct == null) {
				DisplayError("Failed to load body sprite. The following files are missing from your GRF: \r\n" + pbJob + "\r\n" +
					_lastBodyResource.LoadPath + "\r\n" +
					_lastBodyResource.LoadPath.ReplaceExtension(".spr"));
				throw new Exception("No body has been selected.");
			}

			var settings = _generatePreviewSettings();

			if (headAct == null && settings.ShowHead) {
				if (_lastHeadResource == null) {
					DisplayError("Failed to load head sprite. Are you missing the head sprites in your job sprite GRF? \r\n" + pbJob  + "\r\n" +
					EncodingService.FromAnyToDisplayEncoding(@"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\³²\"));
				}
				else {
					DisplayError("Failed to load head sprite. The following files are missing from your GRF: \r\n" + pbJob + "\r\n" +
					_lastHeadResource.LoadPath + "\r\n" +
					_lastHeadResource.LoadPath.ReplaceExtension(".spr"));
				}
				throw new Exception("No head has been selected.");
			}

			string headId = "0";

			if (settings.ShowHead) {
				try {
					headId = Path.GetFileNameWithoutExtension(_lastHeadResource.LoadPath);
					headId = headId.Split('_')[0];
				}
				catch {

				}
			}

			string path0 = settings.ShowBody ?
				EncodingService.FromAnyToDisplayEncoding(@"data\palette\¸ö\{0}_" + _lastBodyResource.Gender + "_{1}.pal") :
				EncodingService.FromAnyToDisplayEncoding(@"data\palette\¸Ó¸®\¸Ó¸®{0}_" + _lastHeadResource.Gender + "_{2}.pal");

			string path1 = settings.ShowBody ?
				EncodingService.FromAnyToDisplayEncoding(@"data\palette\¸ö\{0}_{1}.pal") :
				path0;

			Func<int, byte[]> getPaletteMethod = index => {
				string path;
				string path2;

				if (settings.ShowBody) {
					path = String.Format(path0, ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath ? _lastBodyResource.PalettePath : ActEditorConfiguration.PreviewSheetPredefinedPalettePath, index);
					path2 = String.Format(path1, ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath ? _lastBodyResource.PalettePath : ActEditorConfiguration.PreviewSheetPredefinedPalettePath, index);
				}
				else {
					path = String.Format(path0, headId, _lastHeadResource.Gender, index);
					path2 = path;
				}

				var res = _grfPalettes.FileTable.TryGet(path) ?? _grfPalettes.FileTable.TryGet(path2);

				if (res != null)
					return res.GetDecompressedData();

				DisplayError("Failed to loaded palette file:\r\n" + pbPalettes + "\r\n" + path);
				throw new Exception("Palette path is misssing: " + path);
			};

			var im = SpriteSheetGenerator.GeneratePreviewSheet(bodyAct, headAct, settings, pattern, getPaletteMethod);
			return im;
		}

		public void DisplayError(string text) {
			_tbError.Dispatch(p => p.Text = text);
		}

		private GeneratorSettings _generatePreviewSettings() {
			GeneratorSettings settings = new GeneratorSettings();
			settings.ActionIndex = ActEditorConfiguration.PreviewSheetActionIndex;
			settings.BodyAffected = ActEditorConfiguration.PreviewSheetBodyAffected;

			switch (ActEditorConfiguration.PreviewFontType) {
				case 0:
					settings.Font = new GrfImage(ApplicationManager.GetResource("font.png"));
					break;
				case 1:
					settings.Font = new GrfImage(ApplicationManager.GetResource("font2.png"));
					break;
				case 2:
					settings.Font = new GrfImage(ApplicationManager.GetResource("font4.png"));
					break;
				default:
					settings.Font = new GrfImage(ApplicationManager.GetResource("font.png"));
					break;
			}

			settings.HeadAffected = ActEditorConfiguration.PreviewSheetHeadAffected;
			settings.MaxPerLine = ActEditorConfiguration.PreviewSheetMaxPerLine;
			settings.Shadow = new GrfImage(ApplicationManager.GetResource("shadow.bmp"));
			settings.ShowBody = ActEditorConfiguration.PreviewSheetShowBody;
			settings.ShowHead = ActEditorConfiguration.PreviewSheetShowHead;
			settings.ShowPalIndex = ActEditorConfiguration.PreviewSheetShowPalIndex;
			settings.ShowShadow = ActEditorConfiguration.PreviewSheetShowShadow;
			settings.TransparentBackground = ActEditorConfiguration.PreviewSheetTransparentBackground;
			return settings;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			this.Close();
		}

		private void _buttonGenerate_Click(object sender, RoutedEventArgs e) {
			try {
				string suggestedName = _getSuggestedName();

				string path = TkPathRequest.SaveFile<ActEditorConfiguration>("ExtractingServiceLastPath", "filter", "PNG Files|*.png", "fileName", suggestedName);

				if (path != null) {
					var image = _previewReloadSub(ActEditorConfiguration.PreviewSheetIndexes);
					GC.Collect();

					if (image == null)
						return;

					image.Save(path);

					try {
						OpeningService.FileOrFolder(path);
					}
					catch { }
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private string _getSuggestedName() {
			try {
				string name = "";

				var settings = _generatePreviewSettings();

				if (settings.ShowHead && !settings.ShowBody) {
					name = Path.GetFileNameWithoutExtension(_lastHeadResource.LoadPath);
					return "head_" + ((_lastHeadResource.Gender == EncodingService.FromAnyToDisplayEncoding(GrfStrings.GenderFemale)) ? "f" : "m") + "_" + String.Format("{0:0000}", name.Split('_')[0]) + ".png";
				}

				name = _lastBodyResource.DisplayName.Replace("[", "").Replace("]", "").Replace(" ", "_").ToLowerInvariant();

				int idx = name.IndexOf("(");
				
				if (idx > -1) {
					name = name.Remove(idx, name.IndexOf(")", idx) - idx + 1);
					name = name.Insert(idx, "riding");
				}

				name += "_" + (_lastBodyResource.Gender == EncodingService.FromAnyToDisplayEncoding(GrfStrings.GenderFemale) ? "f" : "m");
				name += ".png";
				return name;
			}
			catch {
				return "sheet.png";
			}
		}

		private void _buttonQuickPreview_Click(object sender, RoutedEventArgs e) {
			if (_quickPreview == null) {
				_quickPreview = new QuickPreviewPaletteSheet();
				_quickPreview.Show();
				_quickPreview.Owner = this;

				_showQuickPreview();

				_quickPreview.Closed += delegate {
					_quickPreview = null;
				};
			}
		}

		private Debouncer _debouncer = new Debouncer();

		private void _showQuickPreview() {
			try {
				if (_quickPreview == null)
					return;

				_quickPreview.SetImage(null);

				_debouncer.Execute(delegate {
					try {
						var image = _previewReloadSub(ActEditorConfiguration.PreviewSheetIndexes);

						if (_quickPreview != null)
							_quickPreview.Dispatch(p => p.SetImage(image));
					}
					catch {
					}
				}, 100);
			}
			catch {
			}
		}
	}
}
