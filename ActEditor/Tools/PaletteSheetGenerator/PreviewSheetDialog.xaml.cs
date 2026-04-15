using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core;
using ActEditor.Core.WPF.EditorControls;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Utilities.Services;
using static ActEditor.Tools.PaletteSheetGenerator.BodySpritesLoader;
using static ActEditor.Tools.PaletteSheetGenerator.HeadSpriteLoader;

namespace ActEditor.Tools.PaletteSheetGenerator {
	/// <summary>
	/// Interaction logic for PaletteGenerator.xaml
	/// </summary>
	public partial class PreviewSheetDialog : TkWindow {
		private HeadSpriteResource _lastHeadResource;
		private BodySpriteResource _lastBodyResource;
		private Genders _lastBodyGender;
		private Genders _lastHeadGender;
		private GrfHolder _grfJobs;
		private GrfHolder _grfPalettes;
		private TextBox[] _searchBoxes;
		private readonly ListView[] _lists;
		private readonly List<SpriteResource>[] _resources = { new List<SpriteResource>(), new List<SpriteResource>(), new List<SpriteResource>(), new List<SpriteResource>() };
		private readonly bool _isLoaded = false;
		private QuickPreviewPaletteSheet _quickPreview = null;
		private BodySpritesLoader _bodySpritesLoader = new BodySpritesLoader();
		private HeadSpriteLoader _headSpritesLoader = new HeadSpriteLoader();

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

				_initializeBinders();

				try {
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
							_grfJobs?.Dispose();
							_grfJobs = new GrfHolder(_pbJob.Text);
							_reloadSpriteLists();
						}
					};

					_pbPalettes.TextChanged += delegate {
						if (File.Exists(_pbPalettes.Text)) {
							_pbPalettes.RecentFiles.AddRecentFile(_pbPalettes.Text);
							_grfPalettes?.Dispose();
							_grfPalettes = new GrfHolder(_pbPalettes.Text);
							_reloadSpriteLists();
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

			_loadSpriteLists();
		}

		private void _initializeBinders() {
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
			Binder.Bind(_cbPaletteOld, () => ActEditorConfiguration.PreviewSheetUseOldConfig, v => ActEditorConfiguration.PreviewSheetUseOldConfig = v, _reloadSpriteLists);
			Binder.Bind(_cbPalette, () => ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath, v => ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath = v, delegate {
				_tbPalette.IsEnabled = !ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath;
				_tbPaletteOverlay.Visibility = ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath ? Visibility.Visible : Visibility.Collapsed;
				_cbPaletteOld.IsEnabled = ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath;
				_previewReload();
			}, true);
			Binder.Bind((TextBox)_tbPalette, () => ActEditorConfiguration.PreviewSheetPredefinedPalettePath, v => ActEditorConfiguration.PreviewSheetPredefinedPalettePath = v);
			Binder.Bind(_cbFontType, () => ActEditorConfiguration.PreviewFontType, v => ActEditorConfiguration.PreviewFontType = v, _previewReload);
		}

		private void _reloadSpriteLists() {
			if (!_isLoaded)
				return;

			_loadSpriteLists();
		}

		private void _loadSpriteLists() {
			try {
				var bodyResult = _bodySpritesLoader.Load();

				_resources[0] = bodyResult.MaleResources.OrderBy(p => p.DisplayName).Cast<SpriteResource>().ToList();
				_resources[1] = bodyResult.FemaleResources.OrderBy(p => p.DisplayName).Cast<SpriteResource>().ToList();
				_lvCM.ItemsSource = _resources[0];
				_lvCF.ItemsSource = _resources[1];

				if (ActEditorConfiguration.PreviewSheetLastSelectedJob != "") {
					var listIdx = ActEditorConfiguration.PreviewSheetLastSelectedJobList % 2;
					_lists[listIdx].SelectedItem = _resources[listIdx].FirstOrDefault(p => p.DisplayName == ActEditorConfiguration.PreviewSheetLastSelectedJob);
					_lists[listIdx].ScrollToCenterOfView(_lists[listIdx].SelectedItem);
					_mainTabControlBody.SelectedIndex = listIdx;
				}

				var headResult = _headSpritesLoader.Load(_grfJobs);
				var comprarer = new AlphanumComparer(StringComparison.OrdinalIgnoreCase);
				_resources[2] = headResult.MaleResources.OrderBy(p => p.DisplayName, comprarer).Cast<SpriteResource>().ToList();
				_resources[3] = headResult.FemaleResources.OrderBy(p => p.DisplayName, comprarer).Cast<SpriteResource>().ToList();
				_lvHM.ItemsSource = _resources[2];
				_lvHF.ItemsSource = _resources[3];

				if (ActEditorConfiguration.PreviewSheetLastSelectedHead != "") {
					var listIdx = (ActEditorConfiguration.PreviewSheetLastSelectedHeadList % 2) + 2;
					_lists[listIdx].SelectedItem = _resources[listIdx].FirstOrDefault(p => p.DisplayName == ActEditorConfiguration.PreviewSheetLastSelectedHead);
					_lists[listIdx].ScrollToCenterOfView(_lists[listIdx].SelectedItem);
					_tabControlHead.SelectedIndex = listIdx;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_previewReload();
			}
		}

		private void _lv_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				if (sender == _lists[0] || sender == _lists[1]) {
					_lastBodyResource = ((ListView)sender).SelectedItem as BodySpriteResource;

					if (_lastBodyResource != null) {
						_lastBodyGender = sender == _lists[0] ? Genders.Male : Genders.Female;
						_tbPaletteOverlay.Text = _lastBodyResource.Palette;
						ActEditorConfiguration.PreviewSheetLastSelectedJob = _lastBodyResource.DisplayName;
						ActEditorConfiguration.PreviewSheetLastSelectedJobList = _lists.ToList().IndexOf((ListView)sender);
					}
				}
				else {
					_lastHeadResource = ((ListView)sender).SelectedItem as HeadSpriteResource;

					if (_lastHeadResource != null) {
						_lastHeadGender = sender == _lists[2] ? Genders.Male : Genders.Female;
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

				if (image == null) {
					_imagePreview.Source = null;
					return;
				}

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

			var lastBodyResource = _lastBodyResource;
			var lastHeadResource = _lastHeadResource;

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
				_grfPalettes?.Dispose();
				_grfPalettes = new GrfHolder(pbPalettes);
			}

			try {
				if (lastBodyResource == null)
					lastBodyResource = (BodySpriteResource)_resources[0].First();
			}
			catch {
				DisplayError("Failed to load default body sprite. There are no body sprites loaded in this GRF (job sprites): " + pbJob);
				throw;
			}

			try {
				if (lastHeadResource == null && _resources[2].Count > 0)
					lastHeadResource = (HeadSpriteResource)_resources[2].First();
			}
			catch {
				// Do nothing, it will be handled later
			}

			Act bodyAct = lastBodyResource.GetAct(_lastBodyGender, _grfJobs);
			Act headAct = lastHeadResource?.GetAct(_grfJobs);

			if (bodyAct == null) {
				DisplayError("Failed to load body sprite. The following files are missing from your GRF: \r\n" + pbJob + "\r\n" +
					lastBodyResource.GetLoadPath(_lastBodyGender) + "\r\n" +
					lastBodyResource.GetLoadPath(_lastBodyGender).ReplaceExtension(".spr"));
				throw new Exception("No body has been selected.");
			}

			var settings = _generatePreviewSettings();

			if (headAct == null && settings.ShowHead) {
				if (lastHeadResource == null) {
					DisplayError("Failed to load head sprite. Are you missing the head sprites in your job sprite GRF? \r\n" + pbJob  + "\r\n" +
					EncodingService.FromAnyToDisplayEncoding(@"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\³²\"));
				}
				else {
					DisplayError("Failed to load head sprite. The following files are missing from your GRF: \r\n" + pbJob + "\r\n" +
					lastHeadResource.GetLoadPath() + "\r\n" +
					lastHeadResource.GetLoadPath().ReplaceExtension(".spr"));
				}
				throw new Exception("No head has been selected.");
			}

			var refBodyPal = lastBodyResource.GetPalette(188, Genders.Male, _grfPalettes);

			Func<int, byte[]> getPaletteMethod = index => {
				byte[] palData;

				if (settings.ShowBody) {
					palData = lastBodyResource.GetPalette(index, _lastBodyGender, _grfPalettes);
				}
				else {
					palData = lastHeadResource.GetPalette(index, _lastHeadGender, _grfPalettes);
				}

				if (palData != null)
					return palData;

				var path = settings.ShowBody ? lastBodyResource.GetPalettePath(index, _lastBodyGender) : _lastHeadResource.GetPalettePath(index, _lastHeadGender);
				DisplayError("Failed to loaded palette file:\r\n" + pbPalettes + "\r\n" + path);
				throw new Exception("Palette path is misssing: " + path);
			};

			if (!settings.ShowBody && !settings.ShowHead)
				return null;

			return SpriteSheetGenerator.GeneratePreviewSheet(bodyAct, headAct, settings, pattern, getPaletteMethod);
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
					name = Path.GetFileNameWithoutExtension(_lastHeadResource.GetLoadPath());
					return "head_" + (_lastHeadGender == Genders.Female ? "f" : "m") + "_" + String.Format("{0:0000}", name.Split('_')[0]) + ".png";
				}

				name = _lastBodyResource.DisplayName.Replace("[", "").Replace("]", "").Replace(" ", "_").ToLowerInvariant();

				int idx = name.IndexOf("(");
				
				if (idx > -1) {
					name = name.Remove(idx, name.IndexOf(")", idx) - idx + 1);
					name = name.Insert(idx, "riding");
				}

				name += "_" + (_lastBodyGender == Genders.Female ? "f" : "m");
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

		protected override void OnClosed(EventArgs e) {
			_grfJobs?.Dispose();
			_grfPalettes?.Dispose();
			_debouncer?.Dispose();
		}
	}
}
