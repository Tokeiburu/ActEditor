using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Avalon;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.FrameEditor;
using ColorPicker.Sliders;
using ErrorManager;
using GRF;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.IO;
using GRF.System;
using GRF.Threading;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;
using Utilities.Extension;
using Utilities.Services;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for TestSheetDialog.xaml
	/// </summary>
	public partial class SaveGarmentDialog : TkWindow {
		private readonly ActEditorWindow _editor;

		public SaveGarmentDialog()
			: base("add.png", "add.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
		}

		public SaveGarmentDialog(ActEditorWindow actEditor)
			: base("add.png", "add.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_editor = actEditor;
			InitializeComponent();

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listViewMatches, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "SpriteName", DisplayExpression = "SpriteName", ToolTipBinding = "SpriteName", TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap, IsFill = true},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Ratio", DisplayExpression = "RatioDisplay", FixedWidth = 50, ToolTipBinding = "RatioDisplay", TextAlignment = TextAlignment.Right},
			}, new DefaultListViewComparer<ActMatch>(), new string[] { "Default", "{DynamicResource TextForeground}" });

			ActEditorConfiguration.ActEditorGarmentPaths = "";

			if (ActEditorConfiguration.ActEditorGarmentPaths == "") {
				ActEditorConfiguration.ActEditorGarmentPaths = EncodingService.DisplayEncoding.GetString(ApplicationManager.GetResource("def_garment_paths.txt"));
			}

			AvalonLoader.Load(_textEditor);

			Loaded += delegate {
				SizeToContent = SizeToContent.Manual;
				MinWidth = 700;
				Width = MinWidth;
				MinHeight = _textEditor.MinHeight + 500;
				Height = MinHeight;

				Left = (SystemParameters.FullPrimaryScreenWidth - MinWidth) / 2d;
				Top = (SystemParameters.FullPrimaryScreenHeight - MinHeight) / 2d;

				_textEditor.Text = ActEditorConfiguration.ActEditorGarmentPaths;
			};

			_textEditor.TextChanged += delegate {
				ActEditorConfiguration.ActEditorGarmentPaths = _textEditor.Text;
			};

			WpfUtils.AddMouseInOutEffectsBox(_cbCopySpr, _cbGuessAnchors);

			Binder.Bind(_cbCopySpr, () => ActEditorConfiguration.ActEditorGarmentCopySpr, v => ActEditorConfiguration.ActEditorGarmentCopySpr = v);
			Binder.Bind(_cbGuessAnchors, () => ActEditorConfiguration.ActEditorGarmentGuessAnchor, v => ActEditorConfiguration.ActEditorGarmentGuessAnchor = v, delegate {
				_pathBrowserDataGrf.IsEnabled = ActEditorConfiguration.ActEditorGarmentGuessAnchor;
				_gridBestMatches.Visibility = ActEditorConfiguration.ActEditorGarmentGuessAnchor ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
			}, true);
			Binder.Bind(_tbSpriteName, () => ActEditorConfiguration.ActEditorGarmentSpriteName, v => ActEditorConfiguration.ActEditorGarmentSpriteName = v);

			_procId = Process.GetCurrentProcess().Id;

			_pathBrowserDataGrf.SetToLatestRecentFile();
			_pathBrowserOutput.SetToLatestRecentFile();

			_listViewMatches.SelectionChanged += new System.Windows.Controls.SelectionChangedEventHandler(_listViewMatches_SelectionChanged);
			_actRefBody = new Act(ApplicationManager.GetResource("ref_body_m.act"), ApplicationManager.GetResource("ref_body_m.spr"));

			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = () => _actRefBody;
			editor.Element = this;
			editor.IndexSelector = _rps;
			editor.SelectedActionFunc = () => _rps.SelectedAction;
			editor.SelectedFrameFunc = () => _rps.SelectedFrame;

			//if (ActEditorConfiguration.ReverseAnchor && _name == "Body" && _actEditor.Act != null && Act != null) {
			_actRefBody.Name = "Body";
			_actRefBody.AnchoredTo = _actGarment;

			_rfp.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (editor.Act != null && _actGarment != null) {
					switch(_rfp.SelectedAction % 8) {
						case 0:
						case 1:
						case 7:
							return new List<DrawingComponent> {
								new ActDraw(_actGarment, editor),
								new ActDraw(_actRefBody, editor)
							};
						default:
							return new List<DrawingComponent> {
								new ActDraw(_actRefBody, editor),
								new ActDraw(_actGarment, editor)
							};
					}
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			_rps.Load(editor);
			_rfp.Init(editor);

			_pathBrowserDataGrf.TextChanged += delegate {
				if (File.Exists(_pathBrowserDataGrf.Text))
					_pathBrowserDataGrf.RecentFiles.AddRecentFile(_pathBrowserDataGrf.Text);
			};

			_pathBrowserOutput.TextChanged += delegate {
				if (Directory.Exists(_pathBrowserOutput.Text))
					_pathBrowserOutput.RecentFiles.AddRecentFile(_pathBrowserOutput.Text);
			};

			_loadkROGrf();
			_resetAdjustments();
			_sliderXOffset.ValueChanged += (s, e) => _sliderOffset_ValueChanged(_tbXOffset, e);
			_sliderYOffset.ValueChanged += (s, e) => _sliderOffset_ValueChanged(_tbYOffset, e);
			_tbXOffset.TextChanged += (s, e) => _tbOffset_TextChanged(_sliderXOffset, _tbXOffset);
			_tbYOffset.TextChanged += (s, e) => _tbOffset_TextChanged(_sliderYOffset, _tbYOffset);
		}

		private void _tbOffset_TextChanged(SliderColor slider, TextBox tb) {
			if (!_enableAdjustmentEvents)
				return;

			_listViewMatches_SelectionChanged(null, null);
			slider.SetPosition((FormatConverters.IntConverter(tb.Text) + 30) / 60d, true);
		}

		private void _sliderOffset_ValueChanged(TextBox sender, double value) {
			if (!_enableAdjustmentEvents)
				return;

			int offset = (int)Math.Round(value * 60 - 30, MidpointRounding.AwayFromZero);
			sender.Text = offset + "";
		}

		private bool _enableAdjustmentEvents = true;

		private void _resetAdjustments() {
			try {
				_enableAdjustmentEvents = false;
				_sliderXOffset.SetPosition(0.5d, true);
				_sliderYOffset.SetPosition(0.5d, true);
				_tbXOffset.Text = "0";
				_tbYOffset.Text = "0";
			}
			finally {
				_enableAdjustmentEvents = true;
			}
		}

		private void _loadkROGrf() {
			var path = this.Dispatch(p => _pathBrowserDataGrf.Text);

			if (_kro_grf != null) {
				_kro_grf.Close();
				_kro_grf = null;
			}

			if (File.Exists(path)) {
				_kro_grf = new GrfHolder(path);
			}

			if (_kro_grf == null)
				return;

			var actSource = this.Dispatch(p => _editor.GetCurrentTab2().Act);

			// List all matches
			var matches = _findClosestkROSpriteSub(_kro_grf, actSource);

			_listViewMatches.ItemsSource = matches.GroupBy(p => p.SpriteName).Select(p => p.First()).OrderByDescending(p => p.Ratio);

			if (matches.Count > 0) {
				_listViewMatches.SelectedItem = matches.First();
			}
		}

		private void _listViewMatches_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			ActMatch match = _listViewMatches.SelectedItem as ActMatch;

			if (match == null)
				return;

			try {
				string actPath = String.Format(EncodingService.FromAnsiToDisplayEncoding(@"data\sprite\·Îºê\{0}\{1}\{2}_{3}.act"), match.SpriteName, GrfStrings.GenderMale, "¼Ò¿ï¸µÄ¿", GrfStrings.GenderMale);
				var actData = _kro_grf.FileTable.TryGet(actPath);

				if (actData == null)
					return;

				_selectedMatch = match;
				_actGarment = new Act(actData, match.SprCompared);

				_fixSprites(_actGarment, match.kROSprite, match.SprCompared);

				_rfp.Update();
			}
			catch {
				_selectedMatch = null;
				_actGarment = null;
				_rps.Stop();

				try {
					_rfp.Update();
				}
				catch {
				}
			}
		}

		private List<Utilities.Extension.Tuple<string, string>> _parseGarmentPaths() {
			List<string> lines = ActEditorConfiguration.ActEditorGarmentPaths.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
			var paths = new List<Utilities.Extension.Tuple<string, string>>();
			int offset = 0;

			for (int i = 0; i < lines.Count; i++) {
				// Cleanup line
				try {
					string line = lines[i];

					line = line.RemoveComment();
					line = line.TrimEnd(' ', '\t');

					if (line.Length <= 1)
						continue;

					string male = line;
					string female = line;

					if (line.Contains("#")) {
						male = line.Split('#')[0];
						female = line.Split('#')[1];
					}

					paths.Add(new Utilities.Extension.Tuple<string, string>(male, female));
					offset += lines[i].Length + 2;
				}
				catch (Exception err) {
					ErrorHandler.HandleException("Unable to parse the Garment Paths data, error at line: " + (i + 1), err);

					_textEditor.SelectionLength = 0;
					_textEditor.TextArea.Caret.Offset = offset;
					_textEditor.TextArea.Caret.BringCaretToView();
					_textEditor.TextArea.Caret.Show();
				}
			}

			return paths;
		}

		public class ActMatch {
			public string SpriteName { get; set; }
			public double Ratio { get; set; }
			public Spr SprCompared { get; set; }
			public Spr kROSprite { get; set; }
			public bool Default {
				get { return true; }
			}

			public string RatioDisplay {
				get { return String.Format("{0:0.00}%", Ratio * 100); }
			}

			public static ActMatch Match(string spriteName, Spr spr1, Spr spr2) {
				ActMatch match = new ActMatch();

				match.SprCompared = spr1;
				match.kROSprite = spr2;
				match.SpriteName = spriteName;

				if (Math.Min(9, spr1.NumberOfImagesLoaded) != Math.Min(9, spr2.NumberOfImagesLoaded))
					return match;

				List<double> rates = new List<double>();

				for (int i = 0; i < Math.Min(9, spr1.NumberOfImagesLoaded); i++) {
					rates.Add(1 - (double)Math.Abs(spr1.Images[i].Width - spr2.Images[i].Width) / Math.Max(spr1.Images[i].Width, spr2.Images[i].Width));
					rates.Add(1 - (double)Math.Abs(spr1.Images[i].Height - spr2.Images[i].Height) / Math.Max(spr1.Images[i].Height, spr2.Images[i].Height));
				}

				match.Ratio = rates.Average();
				return match;
			}
		}

		private void _fixSprites(Act act, Spr original, Spr spr) {
			act.AllLayers(p => {
				if (p.SpriteIndex < 0)
					return;

				var image = p.GetImage(spr);

				if (image != null) {	
					p.Width = image.Width;
					p.Height = image.Height;
				}	
			});

			act = _adjustAct(act, false);

			if (original == null)
				return;

			for (int aid = 0; aid < act.NumberOfActions; aid++) {
				for (int fid = 0; fid < act[aid].NumberOfFrames; fid++) {
					for (int lid = 0; lid < act[aid, fid].NumberOfLayers; lid++) {
						var layer = act[aid, fid, lid];

						if (layer.SpriteIndex < 0)
							continue;
						
						GrfImage oriImg = layer.GetImage(original);
						GrfImage newImg = layer.GetImage(spr);

						if (oriImg == null || newImg == null)
							continue;

						int diffWidth = (newImg.Width - oriImg.Width) / 2;
						int diffHeight = (newImg.Height - oriImg.Height) / 2;
						int mod = aid % 8;

						switch (mod) {
							case 7:
							case 0:
								layer.OffsetY += diffHeight;
								break;
							case 2:
								layer.OffsetX += diffWidth;
								break;
							case 6:
								layer.OffsetX -= diffWidth;
								break;
						}
					}
				}
			}
		}

		private string[] _genders = { GrfStrings.GenderMale, GrfStrings.GenderFemale };
		private int _procId;
		private GrfHolder _kro_grf;
		private Act _actRefBody;
		private Act _actGarment;
		private ActMatch _selectedMatch;

		private void _buttonOK_Click(object sender, RoutedEventArgs e) {
			GrfThread.Start(delegate {
				try {
					var garmentPaths = _parseGarmentPaths();
					var folder = this.Dispatch(p => _pathBrowserOutput.Text);
					var baseSprite = this.Dispatch(p => _tbSpriteName.Text);
					var kro_grf_path = this.Dispatch(p => _pathBrowserDataGrf.Text);
					var actSource = this.Dispatch(p => _editor.GetCurrentTab2().Act);
					string message = "Generated garment '" + baseSprite + "'.";
					message += "\r\nOutput path: " + GrfPath.Combine(folder, baseSprite);
					message += "\r\nSprites created: " + garmentPaths.Count;
					this.Dispatch(p => _buttonOK.IsEnabled = false);

					if (folder == "") {
						throw new Exception("You must set the 'Garment output folder' to a folder that will be used to export all future garments. For example:\r\n" + @"C:\Ragnarok Online\data\sprite\·Îºê\");
					}

					if (baseSprite == "") {
						throw new Exception("You must set the 'Garment path' to the name used for your garment/wing. For example:\r\nwings_of_raguel.");
					}

					if (ActEditorConfiguration.ActEditorGarmentGuessAnchor) {
						if (_kro_grf == null || _kro_grf.IsClosed) {
							throw new Exception("File not found: " + kro_grf_path + "\r\n\r\n" + "To unable the offset guessing feature, you must link to an existing GRF to use as a base to the 'kRO GRF path'.");
						}

						// Find clostest kRO sprite
						var kROSprite = _selectedMatch ?? _findClosestkROSprite(_kro_grf, actSource);

						_saveSpr(kROSprite.SprCompared, folder, baseSprite, garmentPaths);

						// Copy Act files
						foreach (var garmentPath in garmentPaths) {
							for (int i = 0; i < 2; i++) {
								string gender = EncodingService.FromAnsiToDisplayEncoding(_genders[i]);
								string jobname = i == 0 ? garmentPath.Item1 : garmentPath.Item2;
								string output = GrfPath.Combine(folder, String.Format(@"{0}\{1}\{2}_{3}.act", baseSprite, gender, jobname, gender));
								GrfPath.CreateDirectoryFromFile(output);
								string actPath = String.Format(EncodingService.FromAnsiToDisplayEncoding(@"data\sprite\·Îºê\{0}\{1}\{2}_{3}.act"), kROSprite.SpriteName, gender, jobname, gender);
								var actData = _kro_grf.FileTable.TryGet(actPath);

								if (actData == null) {
									// File not found, make a dummy one
									actSource.Save(output);
									continue;
								}

								var kro_act = new Act(actData.GetDecompressedData(), kROSprite.SprCompared);
								_fixSprites(kro_act, kROSprite.kROSprite, kROSprite.SprCompared);
								kro_act.Save(output);
							}
						}

						message += "\r\n";
						message += "\r\nMatch based on: " + kROSprite.SpriteName;
						message += "\r\nMatch ratio: " + String.Format("{0:0.00}%", kROSprite.Ratio * 100);
					}
					else {
						// Just copy the current sprite
						_saveSpr(actSource.Sprite, folder, baseSprite, garmentPaths);

						string tempPath = TemporaryFilesManager.GetTemporaryFilePath(_procId + "_garm_{0:0000}");
						var actResult = _adjustAct(actSource);
						actResult.Save(tempPath);

						// Copy Act files
						foreach (var garmentPath in garmentPaths) {
							for (int i = 0; i < 2; i++) {
								string gender = EncodingService.FromAnsiToDisplayEncoding(_genders[i]);
								string jobname = i == 0 ? garmentPath.Item1 : garmentPath.Item2;
								string output = GrfPath.Combine(folder, String.Format(@"{0}\{1}\{2}_{3}.act", baseSprite, gender, jobname, gender));
								GrfPath.CreateDirectoryFromFile(output);
								GrfPath.Delete(output);
								File.Copy(tempPath, output);
							}
						}
					}

					ErrorHandler.HandleException(message, ErrorLevel.NotSpecified);
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
				finally {
					this.Dispatch(p => _buttonOK.IsEnabled = true);
				}
			});
		}

		private Act _adjustAct(Act actSource, bool newAct = true) {
			int offsetX = FormatConverters.IntConverter(_tbXOffset.Dispatch(p => _tbXOffset.Text));
			int offsetY = FormatConverters.IntConverter(_tbYOffset.Dispatch(p => _tbXOffset.Text));

			if (offsetX == 0 && offsetY == 0)
				return actSource;

			var actResult = newAct ? new Act(actSource) : actSource;

			actResult.AllLayersAdv((actIndex, l) => {
				switch(actIndex.ActionIndex % 8) {
					case 0:
					case 1:
					case 2:
					case 3:
						l.OffsetX += offsetX;
						break;
					default:
						l.OffsetX -= offsetX;
						break;
				}

				l.OffsetY += offsetY;
			});

			return actResult;
		}

		private void _saveSpr(Spr spr, string folder, string baseSprite, IEnumerable<Utilities.Extension.Tuple<string, string>> garmentPaths) {
			if (ActEditorConfiguration.ActEditorGarmentCopySpr) {
				string tempPath = TemporaryFilesManager.GetTemporaryFilePath(_procId + "_garm_{0:0000}");
				spr.Save(tempPath);

				foreach (var garmentPath in garmentPaths) {
					for (int i = 0; i < 2; i++) {
						string gender = EncodingService.FromAnsiToDisplayEncoding(_genders[i]);
						string jobname = i == 0 ? garmentPath.Item1 : garmentPath.Item2;

						string output = GrfPath.Combine(folder, String.Format(@"{0}\{1}\{2}_{3}.spr", baseSprite, gender, jobname, gender));
						GrfPath.CreateDirectoryFromFile(output);
						GrfPath.Delete(output);
						File.Copy(tempPath, output);
					}
				}
			}
			else {
				string output = GrfPath.Combine(folder, String.Format(@"{0}\{0}.spr", baseSprite));
				GrfPath.CreateDirectoryFromFile(output);
				spr.Save(output);
			}
		}

		public class NoCostumeFoundException : Exception {

		}

		private List<ActMatch> _findClosestkROSpriteSub(GrfHolder kro_grf, Act act) {
			HashSet<string> kROsprites = new HashSet<string>();
			List<ActMatch> matches1 = new List<ActMatch>();
			List<ActMatch> matches2 = new List<ActMatch>();

			Spr spr_source1 = act.Sprite;
			Spr spr_source2 = new Spr(act.Sprite);
			spr_source2.Images.ForEach(p => p.Trim());

			foreach (var folder in kro_grf.FileTable.EntriesInDirectory(EncodingService.FromAnsiToDisplayEncoding(@"data\sprite\·Îºê\"), SearchOption.AllDirectories)) {
				var sprite = folder.RelativePath.Replace(EncodingService.FromAnsiToDisplayEncoding(@"data\sprite\·Îºê\"), "");
				var dirs = GrfPath.SplitDirectories(sprite);

				if (dirs.Length == 1)
					continue;

				var baseSprite = dirs[0].Replace(".spr", "");

				if (baseSprite == EncodingService.FromAnsiToDisplayEncoding(GrfStrings.GenderMale) || baseSprite == EncodingService.FromAnsiToDisplayEncoding(GrfStrings.GenderFemale)) {
					continue;
				}

				if (!kROsprites.Add(baseSprite)) {
					continue;
				}

				string sprPath = EncodingService.FromAnsiToDisplayEncoding(String.Format(@"data\sprite\·Îºê\{0}\{0}.spr", baseSprite));
				var sprData = kro_grf.FileTable.TryGet(sprPath);

				if (sprData == null)
					continue;

				var spr_kro = new Spr(sprData.GetDecompressedData());

				matches1.Add(ActMatch.Match(baseSprite, spr_source1, spr_kro));
				matches2.Add(ActMatch.Match(baseSprite, spr_source2, spr_kro));
			}

			return matches1.Concat(matches2).OrderByDescending(p => p.Ratio).ToList();
		}

		private ActMatch _findClosestkROSprite(GrfHolder kro_grf, Act act) {
			return _findClosestkROSpriteSub(kro_grf, act).First();
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _pathBrowserDataGrf_TextChanged(object sender, EventArgs e) {
			_loadkROGrf();
		}

		private void _pathBrowserOutput_TextChanged(object sender, EventArgs e) {

		}

		private void _miSelect_Click(object sender, RoutedEventArgs e) {
			try {
				var folder = _pathBrowserOutput.Text;

				OpeningService.FileOrFolder(folder);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miSelect2_Click(object sender, RoutedEventArgs e) {
			try {
				var folder = _pathBrowserOutput.Text;
				var baseSprite = _tbSpriteName.Text;

				OpeningService.OpenFolder(GrfPath.Combine(folder, baseSprite));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miSelect3_Click(object sender, RoutedEventArgs e) {
			try {
				OpeningService.FileOrFolder(_pathBrowserDataGrf.Text);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}
	}
}
